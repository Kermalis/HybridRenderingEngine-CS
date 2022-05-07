using Silk.NET.Assimp;
using Silk.NET.OpenGL;
using System.Collections.Generic;
using System.Numerics;

namespace HybridRenderingEngine
{
	internal struct TransformParameters
	{
		public Vector3 Translation;
		public float Angle;
		public Vector3 RotationAxis;
		public Vector3 Scale;
	}

	internal sealed unsafe class Model
	{
		// Object to world space matrix
		private readonly bool _useIBL;
		public Matrix4x4 Matrix;
		private readonly List<Mesh> _meshes;

		// To avoid textures being loaded from disk more than once they are indexed into a dictionary
		private readonly Dictionary<string, Texture> _textureAtlas;
		private string _dir;

		public Model(GL gl, Assimp ass, string meshPath, in TransformParameters initParameters, bool ibl)
		{
			_textureAtlas = new Dictionary<string, Texture>();
			_meshes = new List<Mesh>();

			_useIBL = ibl;
			LoadModel(gl, ass, meshPath);
			Matrix = Matrix4x4.CreateScale(initParameters.Scale)
				* Matrix4x4.CreateFromAxisAngle(initParameters.RotationAxis, initParameters.Angle)
				* Matrix4x4.CreateTranslation(initParameters.Translation); // KERM
		}

		// We use assimp to load all our mesh files this
		private void LoadModel(GL gl, Assimp ass, string path)
		{
			Silk.NET.Assimp.Scene *scene = ass.ImportFile(path,
				(uint)(PostProcessSteps.Triangulate | PostProcessSteps.OptimizeMeshes | PostProcessSteps.CalculateTangentSpace | PostProcessSteps.FlipUVs));

			// useful for texture indexing later
			_dir = path.Substring(0, path.LastIndexOf('/'));
			_dir += "/";

			// begin recursive processing of loaded model
			ProcessNode(gl, ass, scene->MRootNode, scene);

			ass.FreeScene(scene);
		}

		// Basic ASSIMP scene tree traversal, taken from the docs
		private void ProcessNode(GL gl, Assimp ass, Node* node, Silk.NET.Assimp.Scene* scene)
		{
			// Process all the node meshes
			for (uint i = 0; i < node->MNumMeshes; i++)
			{
				Silk.NET.Assimp.Mesh * mesh = scene->MMeshes[node->MMeshes[i]];
				_meshes.Add(ProcessMesh(gl, ass, mesh, scene));
			}

			// process all the node children recursively
			for (uint i = 0; i < node->MNumChildren; i++)
			{
				ProcessNode(gl, ass, node->MChildren[i], scene);
			}
		}

		/*
		 * 1. Process vertices
		 * 2. Process indices
		 * 3. Process materials
		 * 
		 * TODO::Refactoring target?
		*/
		private Mesh ProcessMesh(GL gl, Assimp ass, Silk.NET.Assimp.Mesh* mesh, Silk.NET.Assimp.Scene* scene)
		{
			var vertices = new List<Vertex>();
			var indices = new List<uint>();
			var textures = new List<uint>();

			// Process vertices
			for (uint i = 0; i < mesh->MNumVertices; ++i)
			{
				// Process vertex positions, normals, tangents, bitangents, and texture coordinates
				Vertex vertex;
				Vector3 vector;

				// Process position
				vector.X = mesh->MVertices[i].X;
				vector.Y = mesh->MVertices[i].Y;
				vector.Z = mesh->MVertices[i].Z;
				vertex.Position = vector;

				// Process tangent
				vector.X = mesh->MTangents[i].X;
				vector.Y = mesh->MTangents[i].Y;
				vector.Z = mesh->MTangents[i].Z;
				vertex.Tangent = vector;

				// Process biTangent
				vector.X = mesh->MBitangents[i].X;
				vector.Y = mesh->MBitangents[i].Y;
				vector.Z = mesh->MBitangents[i].Z;
				vertex.BiTangent = vector;

				// Process normals
				vector.X = mesh->MNormals[i].X;
				vector.Y = mesh->MNormals[i].Y;
				vector.Z = mesh->MNormals[i].Z;
				vertex.Normal = vector;

				// Process texture coords
				if (mesh->MTextureCoords[0] is not null)
				{
					Vector2 vec;
					vec.X = mesh->MTextureCoords[0][i].X;
					vec.Y = mesh->MTextureCoords[0][i].Y;
					vertex.TexCoords = vec;
				}
				else
				{
					vertex.TexCoords = Vector2.Zero;
				}

				vertices.Add(vertex);
			}

			// Process indices
			for (uint i = 0; i < mesh->MNumFaces; ++i)
			{
				Face face = mesh->MFaces[i];
				for (uint j = 0; j < face.MNumIndices; ++j)
				{
					indices.Add(face.MIndices[j]);
				}
			}

			// Process material and texture info
			Material* material = scene->MMaterials[mesh->MMaterialIndex];
			textures = ProcessTextures(gl, ass, material);

			return new Mesh(gl, vertices.ToArray(), indices.ToArray(), textures.ToArray());
		}

		/*
		 * FIXES::
		 * 1. Have more than one texture per type
		 * 2. Make this its own material class that takes care of it properly
		*/
		private List<uint> ProcessTextures(GL gl, Assimp ass, Material* material)
		{
			var textures = new List<uint>();

			// Checking all texture stacks for each texture type
			// Checkout assimp docs on texture types
			for (Silk.NET.Assimp.TextureType type = Silk.NET.Assimp.TextureType.TextureTypeNone; type <= Silk.NET.Assimp.TextureType.TextureTypeUnknown; type++)
			{
				string fullTexturePath = _dir;

				// If there are any textures of the given type in the material
				if (ass.GetMaterialTextureCount(material, type) > 0)
				{
					// We only care about the first texture assigned we don't expect multiple to be assigned
					AssimpString texturePath;
					ass.GetMaterialTexture(material, type, 0, &texturePath, null, null, null, null, null, null);
					fullTexturePath += texturePath;

					// If this texture has not been added to the atlas yet we load it
					if (!_textureAtlas.TryGetValue(fullTexturePath, out Texture texture))
					{
						texture = new Texture();
						texture.LoadTexture(gl, fullTexturePath, false);
						_textureAtlas.Add(fullTexturePath, texture);
					}

					// We add it to the texture index array of loaded texture for a given mesh
					textures.Add(texture.Id);
				}
				else
				{
					// For now we always assume that these textures will exist in the current
					// material. If they do not, we assign 0 to their value.
					// This will be fixed when the new material model is implemented.
					switch (type)
					{
						case Silk.NET.Assimp.TextureType.TextureTypeLightmap:
						case Silk.NET.Assimp.TextureType.TextureTypeEmissive:
						case Silk.NET.Assimp.TextureType.TextureTypeNormals:
						case Silk.NET.Assimp.TextureType.TextureTypeUnknown:
							textures.Add(0);
							break;
					}
				}
			}
			return textures;
		}

		// The model currently is just a vessel for the meshes of the scene,
		// In a future revision this will probably change
		public void Render(GL gl, Shader shader, bool textured)
		{
			shader.SetBool(gl, "IBL", _useIBL);
			for (int i = 0; i < _meshes.Count; ++i)
			{
				_meshes[i].Render(gl, shader, textured);
			}
		}

		public void Delete(GL gl)
		{
			foreach (Mesh m in _meshes)
			{
				m.Delete(gl);
			}
		}
	}
}
