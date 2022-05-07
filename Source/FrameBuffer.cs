using Silk.NET.OpenGL;
using System;
using System.Numerics;

namespace HybridRenderingEngine
{
	internal abstract class FrameBuffer
	{
		public uint width, height;
		public uint frameBufferID;
		public uint texColorBuffer, depthBuffer;

		public void bind(GL gl)
		{
			gl.Viewport(0, 0, width, height);
			gl.BindFramebuffer(FramebufferTarget.Framebuffer, frameBufferID);
		}

		// TODO:: This currently clears whatever framebuffer is bound, not the framebuffer that calls this function
		public void clear(GL gl, ClearBufferMask clearTarget, in Vector3 clearColor)
		{
			gl.ClearColor(clearColor.X, clearColor.Y, clearColor.Z, 1f);
			gl.Clear(clearTarget);
		}

		// Currently allows only for blit to one texture, not mrt blitting
		public void blitTo(GL gl, FrameBuffer FBO, ClearBufferMask mask)
		{
			gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, frameBufferID);
			gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, FBO.frameBufferID);
			if ((mask & ClearBufferMask.ColorBufferBit) == ClearBufferMask.ColorBufferBit)
			{
				gl.DrawBuffer(DrawBufferMode.ColorAttachment0);
			}
			gl.BlitFramebuffer(0, 0, (int)width, (int)height, 0, 0, (int)width, (int)height, mask, BlitFramebufferFilter.Nearest);
			gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, frameBufferID);
		}

		// TODO:: include cases with width and height
		public void defaultInit(GL gl)
		{
			width = DisplayManager.SCREEN_WIDTH;
			height = DisplayManager.SCREEN_HEIGHT;
			frameBufferID = gl.GenFramebuffer();
			gl.BindFramebuffer(FramebufferTarget.Framebuffer, frameBufferID);
		}

		// Check if frame buffer initialized correctly
		protected void checkForCompleteness(GL gl)
		{
			if (gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
			{
				throw new Exception("Failed to initialize the offscreen frame buffer!");
			}
			gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
		}

		public virtual void Delete(GL gl)
		{
			gl.DeleteFramebuffer(frameBufferID);
			gl.DeleteTexture(texColorBuffer);
			gl.DeleteTexture(depthBuffer);
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
			defaultInit(gl);

			texColorBuffer = Texture.GenTextureDirectlyOnGPU(gl, width, height, 0, TextureType.MULT_2D_HDR_COL);
			depthBuffer = Texture.GenTextureDirectlyOnGPU(gl, width, height, 0, TextureType.MULT_2D_HDR_DEP);

			checkForCompleteness(gl);
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
		public uint blurHighEnd;

		public ResolveBuffer(GL gl)
		{
			defaultInit(gl);

			texColorBuffer = Texture.GenTextureDirectlyOnGPU(gl, width, height, 0, TextureType.SING_2D_HDR_COL);
			blurHighEnd = Texture.GenTextureDirectlyOnGPU(gl, width, height, 1, TextureType.SING_2D_HDR_COL_CLAMP);
			depthBuffer = Texture.GenTextureDirectlyOnGPU(gl, width, height, 0, TextureType.SING_2D_HDR_DEP);

			checkForCompleteness(gl);
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
			defaultInit(gl);

			texColorBuffer = Texture.GenTextureDirectlyOnGPU(gl, width, height, 0, TextureType.SING_2D_HDR_COL_CLAMP);

			checkForCompleteness(gl);
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
			width = w;
			height = h;
			frameBufferID = gl.GenFramebuffer();
			gl.BindFramebuffer(FramebufferTarget.Framebuffer, frameBufferID);

			depthBuffer = gl.GenRenderbuffer();
			gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, depthBuffer);
			gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.DepthComponent24, width, height);
			gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, depthBuffer);
			gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);

			checkForCompleteness(gl);
		}

		public void resizeFrameBuffer(GL gl, uint resolution)
		{
			gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, depthBuffer);
			gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.DepthComponent24, resolution, resolution);
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
			width = w;
			height = h;
			frameBufferID = gl.GenFramebuffer();
			gl.BindFramebuffer(FramebufferTarget.Framebuffer, frameBufferID);

			depthBuffer = Texture.GenTextureDirectlyOnGPU(gl, width, height, 0, TextureType.SING_2D_HDR_DEP_BORDER);

			gl.DrawBuffer(DrawBufferMode.None);
			gl.ReadBuffer(ReadBufferMode.None);

			checkForCompleteness(gl);
		}
	}
	/*
	Framebuffer Characteristics
	1. 1 depth cubemap
	2. depth Buffer: texture, user set width/height, non multisampled, HDR, border color set to black
	*/
	internal sealed class PointShadowBuffer : FrameBuffer
	{
		public CubeMap drawingTexture;

		public PointShadowBuffer(GL gl, uint w, uint h)
		{
			width = w;
			height = h;
			frameBufferID = gl.GenFramebuffer();
			gl.BindFramebuffer(FramebufferTarget.Framebuffer, frameBufferID);

			drawingTexture = new CubeMap();
			drawingTexture.GenerateCubeMap(gl, width, height, CubeMapType.SHADOW_MAP);
			depthBuffer = drawingTexture.textureID;
			gl.FramebufferTexture(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, depthBuffer, 0);

			gl.DrawBuffer(DrawBufferMode.None);
			gl.ReadBuffer(ReadBufferMode.None);

			checkForCompleteness(gl);
		}

		public override void Delete(GL gl)
		{
			base.Delete(gl);
			drawingTexture.Delete(gl);
		}
	}
}
