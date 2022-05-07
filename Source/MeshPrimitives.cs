using Silk.NET.OpenGL;
using System;

namespace HybridRenderingEngine
{
	// All primitives share a very simple interface and only vary by their setup implementation
	// and their number of vertices. This class generally should never be called and a new struct should be
	// built if you want to define a new primitive type.
	internal abstract class Primitive
	{
		public uint VAO, VBO;
		public readonly uint numVertices;

		protected Primitive(uint numVertex)
		{
			numVertices = numVertex;
		}

		public abstract void Setup(GL gl);

		// The drawing function that is shared between all mesh primitives
		public virtual void Draw(GL gl, uint readTexture1 = 0, uint readTexture2 = 0, uint readTexture3 = 0)
		{
			gl.BindVertexArray(VAO);

			// This texture read could be compacted into a for loop and an array could be passed instead
			// But for now this is sufficient 
			if (readTexture1 != 0)
			{
				gl.ActiveTexture(TextureUnit.Texture0);
				gl.BindTexture(TextureTarget.Texture2D, readTexture1);
			}

			// A texture id of 0 is never assigned by opengl so we can
			// be sure that it means we haven't set any texture in the second paramenter and therefore
			// we only want one texture
			if (readTexture2 != 0)
			{
				gl.ActiveTexture(TextureUnit.Texture1);
				gl.BindTexture(TextureTarget.Texture2D, readTexture2);
			}

			if (readTexture3 != 0)
			{
				gl.ActiveTexture(TextureUnit.Texture2);
				gl.BindTexture(TextureTarget.Texture2D, readTexture3);
			}

			gl.DrawArrays(PrimitiveType.Triangles, 0, numVertices);
		}

		public void Delete(GL gl)
		{
			gl.DeleteVertexArray(VAO);
			gl.DeleteBuffer(VBO);
		}
	}

	// Mostly used for screen space or render to texture stuff
	internal sealed class Quad : Primitive
	{
		public Quad()
			: base(6)
		{
			//
		}

		public override unsafe void Setup(GL gl)
		{
			Span<float> quadVertices = stackalloc float[24]
				{
				// positions // texCoordinates
				-1.0f,
				1.0f,
				0.0f,
				1.0f,
				-1.0f,
				-1.0f,
				0.0f,
				0.0f,
				1.0f,
				-1.0f,
				1.0f,
				0.0f,

				-1.0f,
				1.0f,
				0.0f,
				1.0f,
				1.0f,
				-1.0f,
				1.0f,
				0.0f,
				1.0f,
				1.0f,
				1.0f,
				1.0f
			};

			// OpenGL postprocessing quad setup
			VAO = gl.GenVertexArray();
			VBO = gl.GenBuffer();

			// Bind Vertex Array Object and VBO in correct order
			gl.BindVertexArray(VAO);
			gl.BindBuffer(BufferTargetARB.ArrayBuffer, VBO);

			// VBO initialization
			fixed (void* data = quadVertices)
				gl.BufferData(BufferTargetARB.ArrayBuffer, sizeof(float) * 24, data, BufferUsageARB.StaticDraw);

			// Quad position pointer initialization in attribute array
			gl.EnableVertexAttribArray(0);
			gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);

			// Quad texcoords pointer initialization in attribute array
			gl.EnableVertexAttribArray(1);
			gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));

			gl.BindVertexArray(0);
		}

		// Quads never need to be depth tested when used for screen space rendering
		public override void Draw(GL gl, uint readTex1 = 0, uint readTex2 = 0, uint readTex3 = 0)
		{
			gl.Disable(EnableCap.DepthTest);
			base.Draw(gl, readTex1, readTex2, readTex3);
		}
	}

	// Used in cubemap rendering
	internal sealed class Cube : Primitive
	{
		public Cube()
	: base(36)
		{
			//
		}

		public override unsafe void Setup(GL gl)
		{
			Span<float> boxVertices = stackalloc float[108] {
				-1.0f,
				1.0f,
				-1.0f,
				-1.0f,
				-1.0f,
				-1.0f,
				1.0f,
				-1.0f,
				-1.0f,
				1.0f,
				-1.0f,
				-1.0f,
				1.0f,
				1.0f,
				-1.0f,
				-1.0f,
				1.0f,
				-1.0f,

				-1.0f,
				-1.0f,
				1.0f,
				-1.0f,
				-1.0f,
				-1.0f,
				-1.0f,
				1.0f,
				-1.0f,
				-1.0f,
				1.0f,
				-1.0f,
				-1.0f,
				1.0f,
				1.0f,
				-1.0f,
				-1.0f,
				1.0f,

				1.0f,
				-1.0f,
				-1.0f,
				1.0f,
				-1.0f,
				1.0f,
				1.0f,
				1.0f,
				1.0f,
				1.0f,
				1.0f,
				1.0f,
				1.0f,
				1.0f,
				-1.0f,
				1.0f,
				-1.0f,
				-1.0f,

				-1.0f,
				-1.0f,
				1.0f,
				-1.0f,
				1.0f,
				1.0f,
				1.0f,
				1.0f,
				1.0f,
				1.0f,
				1.0f,
				1.0f,
				1.0f,
				-1.0f,
				1.0f,
				-1.0f,
				-1.0f,
				1.0f,

				-1.0f,
				1.0f,
				-1.0f,
				1.0f,
				1.0f,
				-1.0f,
				1.0f,
				1.0f,
				1.0f,
				1.0f,
				1.0f,
				1.0f,
				-1.0f,
				1.0f,
				1.0f,
				-1.0f,
				1.0f,
				-1.0f,

				-1.0f,
				-1.0f,
				-1.0f,
				-1.0f,
				-1.0f,
				1.0f,
				1.0f,
				-1.0f,
				-1.0f,
				1.0f,
				-1.0f,
				-1.0f,
				-1.0f,
				-1.0f,
				1.0f,
				1.0f,
				-1.0f,
				1.0f
			};

			// Generate Buffers
			VAO = gl.GenVertexArray();
			VBO = gl.GenBuffer();

			// Bind Vertex Array Object and VBO in correct order
			gl.BindVertexArray(VAO);
			gl.BindBuffer(BufferTargetARB.ArrayBuffer, VBO);

			// VBO initialization
			fixed (void* data = boxVertices)
				gl.BufferData(BufferTargetARB.ArrayBuffer, sizeof(float) * 108, data, BufferUsageARB.StaticDraw);

			// Vertex position pointer init
			gl.EnableVertexAttribArray(0);
			gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);

			// Unbinding VAO
			gl.BindVertexArray(0);
		}
	}
}