using Silk.NET.OpenGL;
using StbiSharp;
using System;
using System.IO;

namespace HybridRenderingEngine
{
	internal enum TextureType
	{
		MULT_2D_HDR_COL,
		SING_2D_HDR_COL,
		MULT_2D_HDR_DEP,
		SING_2D_HDR_DEP,
		SING_2D_HDR_COL_CLAMP,
		SING_2D_HDR_DEP_BORDER
	}

	internal class Texture
	{
		// TextureID is zero only if the texture has not been initialized properly by OpenGl
		public uint Id;
		public uint Width;
		public uint Height;

		public void LoadTexture(GL gl, string filePath, bool sRGB)
		{
			filePath = filePath.Replace('\\', '/');

			Id = gl.GenTexture();
			// Stbi.SetFlipVerticallyOnLoad(true);
			MemoryStream ms;
			using (FileStream stream = File.OpenRead(filePath))
			{
				ms = new MemoryStream();
				stream.CopyTo(ms);
			}
			StbiImage img = Stbi.LoadFromMemory(ms, 0);
			Width = (uint)img.Width;
			Height = (uint)img.Height;
			uint numChan = (uint)img.NumChannels;

			PixelFormat format;
			InternalFormat internalFormat;
			if (numChan == 1)
			{
				format = PixelFormat.Red;
				internalFormat = InternalFormat.Red;
			}
			else if (numChan == 3)
			{
				format = PixelFormat.Rgb;
				if (sRGB)
				{
					internalFormat = InternalFormat.Srgb;
				}
				else
				{
					internalFormat = InternalFormat.Rgb;
				}
			}
			else if (numChan == 4)
			{
				format = PixelFormat.Rgba;
				if (sRGB)
				{
					internalFormat = InternalFormat.SrgbAlpha;
				}
				else
				{
					internalFormat = InternalFormat.Rgba;
				}
			}
			else
			{
				throw new InvalidDataException();
			}

			gl.BindTexture(TextureTarget.Texture2D, Id);
			gl.TexImage2D(TextureTarget.Texture2D, 0, internalFormat, Width, Height, 0, format, PixelType.UnsignedByte, img.Data);
			gl.GenerateMipmap(TextureTarget.Texture2D);

			// MipMapped and repeating
			gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
			gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
			gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
			gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

			img.Dispose();
			ms.Dispose();
		}

		// Currently only in use for equirectangular skybox maps
		public void LoadHDRTexture(GL gl, string filePath)
		{
			Stbi.SetFlipVerticallyOnLoad(true);

			MemoryStream ms;
			using (FileStream stream = File.OpenRead(filePath))
			{
				ms = new MemoryStream();
				stream.CopyTo(ms);
			}
			StbiImageF img = Stbi.LoadFFromMemory(ms, 0);
			Width = (uint)img.Width;
			Height = (uint)img.Height;

			Id = gl.GenTexture();
			gl.BindTexture(TextureTarget.Texture2D, Id);
			gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgb32f, Width, Height, 0, PixelFormat.Rgb, PixelType.Float, img.Data);

			gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
			gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
			gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
			gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

			img.Dispose();
			ms.Dispose();
		}

		public static unsafe uint GenTextureDirectlyOnGPU(GL gl, uint width, uint height, int attachmentNum, TextureType type)
		{
			uint genTextureID = gl.GenTexture();
			switch (type)
			{
				case TextureType.MULT_2D_HDR_COL:
				{
					gl.BindTexture(TextureTarget.Texture2DMultisample, genTextureID);
					gl.TexImage2DMultisample(TextureTarget.Texture2DMultisample, 4, InternalFormat.Rgba16f, width, height, true);
					gl.BindTexture(TextureTarget.Texture2DMultisample, 0);
					gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0 + attachmentNum, TextureTarget.Texture2DMultisample, genTextureID, 0);
					return genTextureID;
				}
				case TextureType.SING_2D_HDR_COL:
				{
					gl.BindTexture(TextureTarget.Texture2D, genTextureID);
					gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba16f, width, height, 0, PixelFormat.Rgb, PixelType.UnsignedByte, null);
					gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
					gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
					gl.BindTexture(TextureTarget.Texture2D, 0);
					gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0 + attachmentNum, TextureTarget.Texture2D, genTextureID, 0);
					return genTextureID;
				}
				case TextureType.MULT_2D_HDR_DEP:
				{
					gl.BindTexture(TextureTarget.Texture2DMultisample, genTextureID);
					gl.TexImage2DMultisample(TextureTarget.Texture2DMultisample, 4, InternalFormat.DepthComponent32f, width, height, true);
					gl.BindTexture(TextureTarget.Texture2DMultisample, 0);
					gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2DMultisample, genTextureID, 0);
					return genTextureID;
				}
				case TextureType.SING_2D_HDR_DEP:
				{
					gl.BindTexture(TextureTarget.Texture2D, genTextureID);
					gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.DepthComponent32f, width, height, 0, PixelFormat.DepthComponent, PixelType.Float, null);
					gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
					gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
					gl.BindTexture(TextureTarget.Texture2D, 0);
					gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, genTextureID, 0);
					return genTextureID;
				}
				case TextureType.SING_2D_HDR_COL_CLAMP:
				{
					gl.BindTexture(TextureTarget.Texture2D, genTextureID);
					gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba16f, width, height, 0, PixelFormat.Rgb, PixelType.UnsignedByte, null);
					gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
					gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
					gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
					gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
					gl.BindTexture(TextureTarget.Texture2D, 0);
					gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0 + attachmentNum, TextureTarget.Texture2D, genTextureID, 0);
					return genTextureID;
				}
				case TextureType.SING_2D_HDR_DEP_BORDER:
				{
					Span<float> borderColor = stackalloc float[4] {0f, 0f, 0f, 1f};
					gl.BindTexture(TextureTarget.Texture2D, genTextureID);
					gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.DepthComponent32f, width, height, 0, PixelFormat.DepthComponent, PixelType.Float, null);
					gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
					gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
					gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureCompareMode, (int)GLEnum.CompareRefToTexture);
					gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureCompareFunc, (int)GLEnum.Less);
					gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
					gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
					gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, borderColor);
					gl.BindTexture(TextureTarget.Texture2D, 0);
					gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, genTextureID, 0);
					return genTextureID;
				}
			}
			return 0;
		}

		public void Delete(GL gl)
		{
			gl.DeleteTexture(Id);
		}
	}
}

