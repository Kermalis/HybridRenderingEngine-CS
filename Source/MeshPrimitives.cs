using Silk.NET.OpenGL;
using System;

namespace HybridRenderingEngine
{
	// All primitives share a very simple interface and only vary by their setup implementation
	// and their number of vertices. This class generally should never be called and a new struct should be
	// built if you want to define a new primitive type.
	internal abstract class Primitive
	{
		protected uint _vao;
		protected uint _vbo;

		public abstract void Render(GL gl, uint readTexture1 = 0, uint readTexture2 = 0, uint readTexture3 = 0);
		protected void Render(GL gl, uint numVerts, uint readTex1, uint readTex2, uint readTex3)
		{
			gl.BindVertexArray(_vao);

			// This texture read could be compacted into a for loop and an array could be passed instead
			// But for now this is sufficient 
			if (readTex1 != 0)
			{
				gl.ActiveTexture(TextureUnit.Texture0);
				gl.BindTexture(TextureTarget.Texture2D, readTex1);
			}

			// A texture id of 0 is never assigned by opengl so we can
			// be sure that it means we haven't set any texture in the second paramenter and therefore
			// we only want one texture
			if (readTex2 != 0)
			{
				gl.ActiveTexture(TextureUnit.Texture1);
				gl.BindTexture(TextureTarget.Texture2D, readTex2);
			}

			if (readTex3 != 0)
			{
				gl.ActiveTexture(TextureUnit.Texture2);
				gl.BindTexture(TextureTarget.Texture2D, readTex3);
			}

			gl.DrawArrays(PrimitiveType.Triangles, 0, numVerts);
		}

		public void Delete(GL gl)
		{
			gl.DeleteVertexArray(_vao);
			gl.DeleteBuffer(_vbo);
		}
	}

	// Mostly used for screen space or render to texture stuff
	internal sealed class Quad : Primitive
	{
		private const int NUM_VERTS = 6;
		private const int NUM_FLOATS_PER_VERT = 2 + 2;

		public unsafe Quad(GL gl)
		{
			ReadOnlySpan<float> quadVertices = new float[NUM_VERTS * NUM_FLOATS_PER_VERT]
			{
				// POS  // UV
				-1f, 1f, 0f, 1f,
				-1f, -1f, 0f, 0f,
				1f, -1f, 1f, 0f,

				-1f, 1f, 0f, 1f,
				1f, -1f, 1f, 0f,
				1f, 1f, 1f, 1f
			};

			_vao = gl.GenVertexArray();
			gl.BindVertexArray(_vao);

			_vbo = gl.GenBuffer();
			gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
			gl.BufferData(BufferTargetARB.ArrayBuffer, NUM_VERTS * NUM_FLOATS_PER_VERT * sizeof(float), quadVertices, BufferUsageARB.StaticDraw);

			// Pos
			gl.EnableVertexAttribArray(0);
			gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, NUM_FLOATS_PER_VERT * sizeof(float), (void*)0);

			// UVs
			gl.EnableVertexAttribArray(1);
			gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, NUM_FLOATS_PER_VERT * sizeof(float), (void*)(2 * sizeof(float)));

			gl.BindVertexArray(0);
		}

		// Quads never need to be depth tested when used for screen space rendering
		public override void Render(GL gl, uint readTex1 = 0, uint readTex2 = 0, uint readTex3 = 0)
		{
			gl.Disable(EnableCap.DepthTest);
			Render(gl, NUM_VERTS, readTex1, readTex2, readTex3);
		}
	}

	// Used in cubemap rendering
	internal sealed class Cube : Primitive
	{
		private const int NUM_VERTS = 36;
		private const int NUM_FLOATS_PER_VERT = 3;

		public unsafe Cube(GL gl)
		{
			ReadOnlySpan<float> boxVertices = new float[NUM_VERTS * NUM_FLOATS_PER_VERT]
			{
				-1f, 1f, -1f,
				-1f, -1f, -1f,
				1f, -1f, -1f,
				1f, -1f, -1f,
				1f, 1f, -1f,
				-1f, 1f, -1f,

				-1f, -1f, 1f,
				-1f, -1f, -1f,
				-1f, 1f, -1f,
				-1f, 1f, -1f,
				-1f, 1f, 1f,
				-1f, -1f, 1f,

				1f, -1f, -1f,
				1f, -1f, 1f,
				1f, 1f, 1f,
				1f, 1f, 1f,
				1f, 1f, -1f,
				1f, -1f, -1f,

				-1f, -1f, 1f,
				-1f, 1f, 1f,
				1f, 1f, 1f,
				1f, 1f, 1f,
				1f, -1f, 1f,
				-1f, -1f, 1f,

				-1f, 1f, -1f,
				1f, 1f, -1f,
				1f, 1f, 1f,
				1f, 1f, 1f,
				-1f, 1f, 1f,
				-1f, 1f, -1f,

				-1f, -1f, -1f,
				-1f, -1f, 1f,
				1f, -1f, -1f,
				1f, -1f, -1f,
				-1f, -1f, 1f,
				1f, -1f, 1f
			};

			_vao = gl.GenVertexArray();
			gl.BindVertexArray(_vao);

			_vbo = gl.GenBuffer();
			gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
			gl.BufferData(BufferTargetARB.ArrayBuffer, NUM_VERTS * NUM_FLOATS_PER_VERT * sizeof(float), boxVertices, BufferUsageARB.StaticDraw);

			// Vertex position pointer init
			gl.EnableVertexAttribArray(0);
			gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, NUM_FLOATS_PER_VERT * sizeof(float), (void*)0);

			// Unbinding VAO
			gl.BindVertexArray(0);
		}

		public override void Render(GL gl, uint readTex1 = 0, uint readTex2 = 0, uint readTex3 = 0)
		{
			Render(gl, NUM_VERTS, readTex1, readTex2, readTex3);
		}
	}
}