using HybridRenderingEngine.Utils;
using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
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
		// TextureID is zero only if the texture has not been initialized properly by OpenGL
		public uint Id;
		public uint Width;
		public uint Height;

		public void LoadTexture(GL gl, string filePath, bool sRGB)
		{
			filePath = filePath.Replace('\\', '/');

			Id = gl.GenTexture();
			gl.BindTexture(TextureTarget.Texture2D, Id);

			// MipMapped and repeating
			gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
			gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
			gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
			gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

			FileStream s = File.OpenRead(filePath);
			IImageInfo info = Image.Identify(s);
			s.Seek(0, SeekOrigin.Begin);

			using (var img = Image.Load<Bgra32>(s))
			{
				s.Dispose();

				SizedInternalFormat format;
				switch (info.PixelType.BitsPerPixel)
				{
					case 8:
					{
						format = SizedInternalFormat.R8;
						break;
					}
					case 16:
					{
						format = SizedInternalFormat.RG8;
						break;
					}
					case 24:
					{
						if (sRGB)
						{
							format = SizedInternalFormat.Srgb8;
						}
						else
						{
							format = SizedInternalFormat.Rgb8;
						}
						break;
					}
					case 32:
					{
						if (sRGB)
						{
							format = SizedInternalFormat.Srgb8Alpha8;
						}
						else
						{
							format = SizedInternalFormat.Rgba8;
						}
						break;
					}
					default: throw new NotImplementedException();
				}

				Width = (uint)img.Width;
				Height = (uint)img.Height;
				gl.TexStorage2D(TextureTarget.Texture2D, 1, format, Width, Height);
				MyUtils.UploadPixelData(gl, img, TextureTarget.Texture2D);
			}

			gl.GenerateMipmap(TextureTarget.Texture2D);
		}

		// Currently only in use for equirectangular skybox maps
		public void LoadHDRTexture(GL gl, string filePath)
		{
			Id = gl.GenTexture();
			gl.BindTexture(TextureTarget.Texture2D, Id);

			gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
			gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
			gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
			gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

			FileStream s = File.OpenRead(filePath);
			using (var img = Image.Load<Bgra32>(s))
			{
				s.Dispose();

				img.Mutate(o => o.Flip(FlipMode.Vertical));

				Width = (uint)img.Width;
				Height = (uint)img.Height;
				gl.TexStorage2D(TextureTarget.Texture2D, 1, SizedInternalFormat.Rgb32f, Width, Height);
				MyUtils.UploadPixelData(gl, img, TextureTarget.Texture2D);
			}
		}

		public static uint GenTextureDirectlyOnGPU(GL gl, uint width, uint height, int attachmentNum, TextureType type)
		{
			uint id = gl.GenTexture();
			switch (type)
			{
				case TextureType.MULT_2D_HDR_COL:
				{
					gl.BindTexture(TextureTarget.Texture2DMultisample, id);
					gl.TexImage2DMultisample(TextureTarget.Texture2DMultisample, 4, InternalFormat.Rgba16f, width, height, true);
					gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0 + attachmentNum, TextureTarget.Texture2DMultisample, id, 0);
					return id;
				}
				case TextureType.SING_2D_HDR_COL:
				{
					gl.BindTexture(TextureTarget.Texture2D, id);
					gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
					gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
					gl.TexStorage2D(TextureTarget.Texture2D, 1, SizedInternalFormat.Rgba16f, width, height);
					gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0 + attachmentNum, TextureTarget.Texture2D, id, 0);
					return id;
				}
				case TextureType.MULT_2D_HDR_DEP:
				{
					gl.BindTexture(TextureTarget.Texture2DMultisample, id);
					gl.TexImage2DMultisample(TextureTarget.Texture2DMultisample, 4, InternalFormat.DepthComponent32f, width, height, true);
					gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2DMultisample, id, 0);
					return id;
				}
				case TextureType.SING_2D_HDR_DEP:
				{
					gl.BindTexture(TextureTarget.Texture2D, id);
					gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
					gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
					gl.TexStorage2D(TextureTarget.Texture2D, 1, SizedInternalFormat.DepthComponent32f, width, height);
					gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, id, 0);
					return id;
				}
				case TextureType.SING_2D_HDR_COL_CLAMP:
				{
					gl.BindTexture(TextureTarget.Texture2D, id);
					gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
					gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
					gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
					gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
					gl.TexStorage2D(TextureTarget.Texture2D, 1, SizedInternalFormat.Rgba16f, width, height);
					gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0 + attachmentNum, TextureTarget.Texture2D, id, 0);
					return id;
				}
				case TextureType.SING_2D_HDR_DEP_BORDER:
				{
					gl.BindTexture(TextureTarget.Texture2D, id);
					gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
					gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
					gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureCompareMode, (int)GLEnum.CompareRefToTexture);
					gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureCompareFunc, (int)GLEnum.Less);
					gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
					gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
					Span<float> borderColor = stackalloc float[4] { 0f, 0f, 0f, 1f };
					gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, borderColor);
					gl.TexStorage2D(TextureTarget.Texture2D, 1, SizedInternalFormat.DepthComponent32f, width, height);
					gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, id, 0);
					return id;
				}
			}
			throw new ArgumentOutOfRangeException(nameof(type));
		}

		public void Delete(GL gl)
		{
			gl.DeleteTexture(Id);
		}
	}
}

