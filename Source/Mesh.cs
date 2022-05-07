using Silk.NET.OpenGL;
using System.Numerics;

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

		public Vector3 position;
		public Vector3 normal;
		public Vector3 tangent;
		public Vector3 biTangent;
		public Vector2 texCoords;
	}

	internal sealed class Mesh
	{
		// OpenGL drawing variables
		public uint VAO, VBO, EBO;

		public Vertex[] vertices;
		public uint[] indices;
		public uint[] textures;

		public unsafe Mesh(GL gl, Vertex[] v, uint[] i, uint[] t)
		{
			vertices = v;
			indices = i;
			textures = t;

			// Generate Buffers
			VAO = gl.GenVertexArray();
			VBO = gl.GenBuffer();
			EBO = gl.GenBuffer();

			gl.BindVertexArray(VAO);

			// VBO stuff
			gl.BindBuffer(BufferTargetARB.ArrayBuffer, VBO);
			fixed (void* data = v)
			{
				gl.BufferData(BufferTargetARB.ArrayBuffer, (uint)v.Length * Vertex.SIZE, data, BufferUsageARB.StaticDraw);
			}

			// EBO stuff
			gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, EBO);
			fixed (void* data = i)
			{
				gl.BufferData(BufferTargetARB.ElementArrayBuffer, (uint)i.Length * sizeof(uint), data, BufferUsageARB.StaticDraw);
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
		public void Render(GL gl, Shader shader, bool textured)
		{
			// Diffuse
			gl.ActiveTexture(TextureUnit.Texture0);
			shader.SetInt(gl, "albedoMap", 0);
			gl.BindTexture(TextureTarget.Texture2D, textures[0]);
			if (textured)
			{
				// Emissive
				gl.ActiveTexture(TextureUnit.Texture1);
				shader.SetInt(gl, "emissiveMap", 1);
				gl.BindTexture(TextureTarget.Texture2D, textures[1]);

				// Normals
				if (textures[2] == 0)
				{
					shader.SetBool(gl, "normalMapped", false);
				}
				else
				{
					shader.SetBool(gl, "normalMapped", true);
				}
				gl.ActiveTexture(TextureUnit.Texture2);
				shader.SetInt(gl, "normalsMap", 2);
				gl.BindTexture(TextureTarget.Texture2D, textures[2]);

				// Ambient Oclussion
				if (textures[3] == 0)
				{
					shader.SetBool(gl, "aoMapped", false);
				}
				else
				{
					shader.SetBool(gl, "aoMapped", true);
				}
				gl.ActiveTexture(TextureUnit.Texture3);
				shader.SetInt(gl, "lightMap", 3);
				gl.BindTexture(TextureTarget.Texture2D, textures[3]);

				// Metal / Roughness
				gl.ActiveTexture(TextureUnit.Texture4);
				shader.SetInt(gl, "metalRoughMap", 4);
				gl.BindTexture(TextureTarget.Texture2D, textures[4]);

			}

			// Mesh Drawing
			gl.BindVertexArray(VAO);
			gl.DrawElements(PrimitiveType.Triangles, (uint)indices.Length, DrawElementsType.UnsignedInt, 0);
		}

		public void Delete(GL gl)
		{
			gl.DeleteVertexArray(VAO);
			gl.DeleteBuffer(VBO);
			gl.DeleteBuffer(EBO);
		}
	}
}
