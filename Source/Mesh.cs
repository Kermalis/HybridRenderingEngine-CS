using Silk.NET.OpenGL;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace HybridRenderingEngine
{
	internal struct Vertex
	{
		public const int OFFSET_POS = 0;
		public const int OFFSET_NORM = OFFSET_POS + (3 * sizeof(float));
		public const int OFFSET_TAN = OFFSET_NORM + (3 * sizeof(float));
		public const int OFFSET_BITAN = OFFSET_TAN + (3 * sizeof(float));
		public const int OFFSET_UV = OFFSET_BITAN + (3 * sizeof(float));
		public const int SIZE = OFFSET_UV + (2 * sizeof(float));

		public Vector3 Position;
		public Vector3 Normal;
		public Vector3 Tangent;
		public Vector3 BiTangent;
		public Vector2 TexCoords;
	}

	internal sealed class Mesh
	{
		// OpenGL drawing variables
		private readonly uint _vao;
		private readonly uint _vbo;
		private readonly uint _ebo;

		private readonly uint _numIndices;
		private readonly Dictionary<Silk.NET.Assimp.TextureType, uint> _textures;

		public unsafe Mesh(GL gl, List<Vertex> v, List<uint> i, Dictionary<Silk.NET.Assimp.TextureType, uint> t)
		{
			_numIndices = (uint)i.Count;
			_textures = t;

			_vao = gl.GenVertexArray();
			gl.BindVertexArray(_vao);

			// VBO stuff
			_vbo = gl.GenBuffer();
			gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
			fixed (void* data = CollectionsMarshal.AsSpan(v))
			{
				gl.BufferData(BufferTargetARB.ArrayBuffer, (uint)v.Count * Vertex.SIZE, data, BufferUsageARB.StaticDraw);
			}

			// EBO stuff
			_ebo = gl.GenBuffer();
			gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
			fixed (void* data = CollectionsMarshal.AsSpan(i))
			{
				gl.BufferData(BufferTargetARB.ElementArrayBuffer, _numIndices * sizeof(uint), data, BufferUsageARB.StaticDraw);
			}

			// Vertex position pointer init
			gl.EnableVertexAttribArray(0);
			gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, Vertex.SIZE, (void*)Vertex.OFFSET_POS);

			// Vertex normal pointer init
			gl.EnableVertexAttribArray(1);
			gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, Vertex.SIZE, (void*)Vertex.OFFSET_NORM);

			// Vertex texture coord
			gl.EnableVertexAttribArray(2);
			gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, Vertex.SIZE, (void*)Vertex.OFFSET_UV);

			// Tangent position
			gl.EnableVertexAttribArray(3);
			gl.VertexAttribPointer(3, 3, VertexAttribPointerType.Float, false, Vertex.SIZE, (void*)Vertex.OFFSET_TAN);

			// Bi-tangent position
			gl.EnableVertexAttribArray(4);
			gl.VertexAttribPointer(4, 3, VertexAttribPointerType.Float, false, Vertex.SIZE, (void*)Vertex.OFFSET_BITAN);

			// Unbinding VAO
			gl.BindVertexArray(0);
		}

		// The diffuse texture is assumed to always exist and always loaded in case you want to do alpha
		// discard. Lower overhead texture setup is something worth investigating here.
		public unsafe void Render(GL gl, Shader shader, bool textured)
		{
			// Diffuse
			gl.ActiveTexture(TextureUnit.Texture0);
			shader.SetInt(gl, "albedoMap", 0);
			gl.BindTexture(TextureTarget.Texture2D, _textures[Silk.NET.Assimp.TextureType.TextureTypeDiffuse]);

			if (textured)
			{
				// Emissive
				gl.ActiveTexture(TextureUnit.Texture1);
				shader.SetInt(gl, "emissiveMap", 1);
				_textures.TryGetValue(Silk.NET.Assimp.TextureType.TextureTypeEmissive, out uint tex);
				gl.BindTexture(TextureTarget.Texture2D, tex);

				// Normals
				bool mapped = _textures.TryGetValue(Silk.NET.Assimp.TextureType.TextureTypeNormals, out tex);
				shader.SetBool(gl, "normalMapped", mapped);
				gl.ActiveTexture(TextureUnit.Texture2);
				shader.SetInt(gl, "normalsMap", 2);
				gl.BindTexture(TextureTarget.Texture2D, tex);

				// Ambient Occlusion
				mapped = _textures.TryGetValue(Silk.NET.Assimp.TextureType.TextureTypeAmbientOcclusion, out tex);
				shader.SetBool(gl, "aoMapped", mapped);
				gl.ActiveTexture(TextureUnit.Texture3);
				shader.SetInt(gl, "lightMap", 3);
				gl.BindTexture(TextureTarget.Texture2D, tex);

				// Metal / Roughness
				gl.ActiveTexture(TextureUnit.Texture4);
				shader.SetInt(gl, "metalRoughMap", 4);
				_textures.TryGetValue(Silk.NET.Assimp.TextureType.TextureTypeUnknown, out tex); // In this demo, Unknown is only reported for MetallicRoughness
				gl.BindTexture(TextureTarget.Texture2D, tex);
			}

			// Mesh Drawing
			gl.BindVertexArray(_vao);
			gl.DrawElements(PrimitiveType.Triangles, _numIndices, DrawElementsType.UnsignedInt, null);
		}

		public void Delete(GL gl)
		{
			gl.DeleteVertexArray(_vao);
			gl.DeleteBuffer(_vbo);
			gl.DeleteBuffer(_ebo);
		}
	}
}
