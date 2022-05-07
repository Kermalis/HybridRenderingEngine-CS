using Silk.NET.OpenGL;
using System;
using System.Numerics;

namespace HybridRenderingEngine
{
	internal abstract class FrameBuffer
	{
		protected uint _width;
		protected uint _height;
		public uint Id;
		public uint Color;
		public uint Depth;

		public void DefaultInit(GL gl)
		{
			_width = DisplayManager.SCREEN_WIDTH;
			_height = DisplayManager.SCREEN_HEIGHT;
			Id = gl.GenFramebuffer();
			gl.BindFramebuffer(FramebufferTarget.Framebuffer, Id);
		}

		public void Bind(GL gl)
		{
			gl.Viewport(0, 0, _width, _height);
			gl.BindFramebuffer(FramebufferTarget.Framebuffer, Id);
		}

		public static void Clear(GL gl, ClearBufferMask clearTarget, in Vector3 clearColor)
		{
			gl.ClearColor(clearColor.X, clearColor.Y, clearColor.Z, 1f);
			gl.Clear(clearTarget);
		}

		// Currently allows only for blit to one texture, not mrt blitting
		public void BlitTo(GL gl, FrameBuffer FBO, ClearBufferMask mask)
		{
			gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, Id);
			gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, FBO.Id);
			if ((mask & ClearBufferMask.ColorBufferBit) == ClearBufferMask.ColorBufferBit)
			{
				gl.DrawBuffer(DrawBufferMode.ColorAttachment0);
			}
			gl.BlitFramebuffer(0, 0, (int)_width, (int)_height, 0, 0, (int)_width, (int)_height, mask, BlitFramebufferFilter.Nearest);
			gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, Id);
		}

		// Check if frame buffer initialized correctly
		protected static void CheckForCompleteness(GL gl)
		{
			if (gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
			{
				throw new Exception("Failed to initialize the offscreen frame buffer!");
			}
			gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
		}

		public virtual void Delete(GL gl)
		{
			gl.DeleteFramebuffer(Id);
			gl.DeleteTexture(Color);
			gl.DeleteTexture(Depth);
		}
	}

	/*
	Framebuffer Characteristics
	1. 1 Color, 1 Depth buffer
	2. Color Buffer: screen width/height, multisampled, HDR 
	3. Depth Buffer: screen width/height, multisampled, HDR 
	*/
	internal sealed class FrameBufferMultiSampled : FrameBuffer
	{
		public FrameBufferMultiSampled(GL gl)
		{
			DefaultInit(gl);

			Color = Texture.GenTextureDirectlyOnGPU(gl, _width, _height, 0, TextureType.MULT_2D_HDR_COL);
			Depth = Texture.GenTextureDirectlyOnGPU(gl, _width, _height, 0, TextureType.MULT_2D_HDR_DEP);

			CheckForCompleteness(gl);
		}
	}
	/*
	Framebuffer Characteristics
	1. 2 Color, 1 Depth buffer
	2. Color Buffer1: screen width/height, non multisampled, HDR 
	3. Color Buffer2: screen width/height, non multisampled, HDR, clamped to edges
	3. Depth Buffer: screen width/height, non multisampled, HDR 
	*/
	internal sealed class ResolveBuffer : FrameBuffer
	{
		public uint BlurHighEnd;

		public ResolveBuffer(GL gl)
		{
			DefaultInit(gl);

			Color = Texture.GenTextureDirectlyOnGPU(gl, _width, _height, 0, TextureType.SING_2D_HDR_COL);
			BlurHighEnd = Texture.GenTextureDirectlyOnGPU(gl, _width, _height, 1, TextureType.SING_2D_HDR_COL_CLAMP);
			Depth = Texture.GenTextureDirectlyOnGPU(gl, _width, _height, 0, TextureType.SING_2D_HDR_DEP);

			CheckForCompleteness(gl);
		}
	}
	/*
	Framebuffer Characteristics
	1. 1 Color
	2. Color Buffer: screen width/height, non multisampled, HDR , clamped to edges
	*/
	internal sealed class QuadHDRBuffer : FrameBuffer
	{
		public QuadHDRBuffer(GL gl)
		{
			DefaultInit(gl);

			Color = Texture.GenTextureDirectlyOnGPU(gl, _width, _height, 0, TextureType.SING_2D_HDR_COL_CLAMP);

			CheckForCompleteness(gl);
		}
	}
	/*
	Framebuffer Characteristics
	1. 1 depth
	2. depth render Buffer: user set width/height, non multisampled, HDR
	*/
	internal sealed class CaptureBuffer : FrameBuffer
	{
		public CaptureBuffer(GL gl, uint w, uint h)
		{
			_width = w;
			_height = h;
			Id = gl.GenFramebuffer();
			gl.BindFramebuffer(FramebufferTarget.Framebuffer, Id);

			Depth = gl.GenRenderbuffer();
			gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, Depth);
			gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.DepthComponent24, _width, _height);
			gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, Depth);
			gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);

			CheckForCompleteness(gl);
		}

		public void Resize(GL gl, uint size)
		{
			gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, Depth);
			gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.DepthComponent24, size, size);
		}
	}
	/*
	Framebuffer Characteristics
	1. 1 depth
	2. depth Buffer: texture, user set width/height, non multisampled, HDR, border color set to black 
	*/
	internal sealed class DirShadowBuffer : FrameBuffer
	{
		public DirShadowBuffer(GL gl, uint w, uint h)
		{
			_width = w;
			_height = h;
			Id = gl.GenFramebuffer();
			gl.BindFramebuffer(FramebufferTarget.Framebuffer, Id);

			Depth = Texture.GenTextureDirectlyOnGPU(gl, _width, _height, 0, TextureType.SING_2D_HDR_DEP_BORDER);

			gl.DrawBuffer(DrawBufferMode.None);
			gl.ReadBuffer(ReadBufferMode.None);

			CheckForCompleteness(gl);
		}
	}
	/*
	Framebuffer Characteristics
	1. 1 depth cubemap
	2. depth Buffer: texture, user set width/height, non multisampled, HDR, border color set to black
	*/
	internal sealed class PointShadowBuffer : FrameBuffer
	{
		private CubeMap _drawingTexture;

		public PointShadowBuffer(GL gl, uint w, uint h)
		{
			_width = w;
			_height = h;
			Id = gl.GenFramebuffer();
			gl.BindFramebuffer(FramebufferTarget.Framebuffer, Id);

			_drawingTexture = new CubeMap();
			_drawingTexture.GenerateCubeMap(gl, _width, _height, CubeMapType.SHADOW_MAP);
			Depth = _drawingTexture.Id;
			gl.FramebufferTexture(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, Depth, 0);

			gl.DrawBuffer(DrawBufferMode.None);
			gl.ReadBuffer(ReadBufferMode.None);

			CheckForCompleteness(gl);
		}

		public override void Delete(GL gl)
		{
			base.Delete(gl);
			_drawingTexture.Delete(gl);
		}
	}
}
