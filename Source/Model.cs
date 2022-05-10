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
		private readonly bool _useIBL;
		// Object to world space matrix
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
			Matrix = Matrix4x4.CreateScale(initParameters.Scale)
				* Matrix4x4.CreateFromAxisAngle(initParameters.RotationAxis, initParameters.Angle)
				* Matrix4x4.CreateTranslation(initParameters.Translation); // KERM

			LoadModel(gl, ass, meshPath);
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
				Silk.NET.Assimp.Mesh* mesh = scene->MMeshes[node->MMeshes[i]];
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

			// Process vertices
			for (uint i = 0; i < mesh->MNumVertices; ++i)
			{
				// Process vertex positions, normals, tangents, bitangents, and texture coordinates
				Vertex vertex;

				// Process position
				vertex.Position.X = mesh->MVertices[i].X;
				vertex.Position.Y = mesh->MVertices[i].Y;
				vertex.Position.Z = mesh->MVertices[i].Z;

				// Process tangent
				vertex.Tangent.X = mesh->MTangents[i].X;
				vertex.Tangent.Y = mesh->MTangents[i].Y;
				vertex.Tangent.Z = mesh->MTangents[i].Z;

				// Process biTangent
				vertex.BiTangent.X = mesh->MBitangents[i].X;
				vertex.BiTangent.Y = mesh->MBitangents[i].Y;
				vertex.BiTangent.Z = mesh->MBitangents[i].Z;

				// Process normals
				vertex.Normal.X = mesh->MNormals[i].X;
				vertex.Normal.Y = mesh->MNormals[i].Y;
				vertex.Normal.Z = mesh->MNormals[i].Z;

				// Process texture coords
				if (mesh->MTextureCoords[0] is not null)
				{
					vertex.TexCoords.X = mesh->MTextureCoords[0][i].X;
					vertex.TexCoords.Y = mesh->MTextureCoords[0][i].Y;
				}
				else
				{
					vertex.TexCoords = Vector2.Zero;
				}

				vertices.Add(vertex);
			}

			var indices = new List<uint>();

			// Process indices
			for (uint i = 0; i < mesh->MNumFaces; ++i)
			{
				Face face = mesh->MFaces[i];
				for (uint j = 0; j < face.MNumIndices; ++j)
				{
					indices.Add(face.MIndices[j]);
				}
			}

			return new Mesh(gl, vertices, indices, ProcessTextures(gl, ass, scene->MMaterials[mesh->MMaterialIndex]));
		}

		/*
		 * FIXES::
		 * 1. Have more than one texture per type
		 * 2. Make this its own material class that takes care of it properly
		*/
		private Dictionary<Silk.NET.Assimp.TextureType, uint> ProcessTextures(GL gl, Assimp ass, Material* material)
		{
			var textures = new Dictionary<Silk.NET.Assimp.TextureType, uint>();

			// Checking all texture stacks for each texture type
			// Max value is transmission in this version of Assimp
			for (Silk.NET.Assimp.TextureType type = Silk.NET.Assimp.TextureType.TextureTypeNone; type <= Silk.NET.Assimp.TextureType.TextureTypeTransmission; type++)
			{
				if (type == Silk.NET.Assimp.TextureType.TextureTypeBaseColor)
				{
					continue; // Ignore this type since it's reporting the diffuse texture again
				}

				if (ass.GetMaterialTextureCount(material, type) > 0)
				{
					// We only care about the first texture assigned we don't expect multiple to be assigned
					AssimpString texturePath;
					ass.GetMaterialTexture(material, type, 0, &texturePath, null, null, null, null, null, null);
					string fullTexturePath = _dir + texturePath.AsString;

					// If this texture has not been added to the atlas yet we load it
					if (!_textureAtlas.TryGetValue(fullTexturePath, out Texture texture))
					{
						texture = new Texture();
						texture.LoadTexture(gl, fullTexturePath, false);
						_textureAtlas.Add(fullTexturePath, texture);
					}

					textures.Add(type, texture.Id);
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
