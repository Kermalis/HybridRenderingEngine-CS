using HybridRenderingEngine.Utils;
using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;
using System.Numerics;

namespace HybridRenderingEngine
{
	internal enum CubeMapType
	{
		SHADOW_MAP,
		HDR_MAP,
		PREFILTER_MAP
	}

	internal sealed class CubeMap : Texture
	{
		private static readonly Matrix4x4 _captureProjection = MyUtils.GLM_PerspectiveRH_NO(90f * MyUtils.DEG_TO_RAD, 1f, 0.1f, 10f);
		private static readonly Matrix4x4[] _captureViews = new Matrix4x4[6]
		{
			MyUtils.GLM_LookAtRH(Vector3.Zero, new Vector3(1f, 0f, 0f), new Vector3(0f, -1f, 0f)),
			MyUtils.GLM_LookAtRH(Vector3.Zero, new Vector3(-1f, 0f, 0f), new Vector3(0f, -1f, 0f)),
			MyUtils.GLM_LookAtRH(Vector3.Zero, new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, 1f)),
			MyUtils.GLM_LookAtRH(Vector3.Zero, new Vector3(0f, -1f, 0f), new Vector3(0f, 0f, -1f)),
			MyUtils.GLM_LookAtRH(Vector3.Zero, new Vector3(0f, 0f, 1f), new Vector3(0f, -1f, 0f)),
			MyUtils.GLM_LookAtRH(Vector3.Zero, new Vector3(0f, 0f, -1f), new Vector3(0f, -1f, 0f))
		};
		private static readonly string[] _fileHandleForFaces = new string[6]
		{
			"Right.jpg", "Left.jpg", "Top.jpg", "Bottom.jpg", "Front.jpg", "Back.jpg"
		};

		public static Cube Cube;

		// Useful in the Specular IBL component
		private uint _maxMipLevels;

		// Traditional cubemap generation from 6 regular image files named according to the
		// string array fileHandlesForFaces. Order comes frfom Opengl cubemap specification
		public void LoadCubeMap(GL gl, string path)
		{
			Id = gl.GenTexture();
			gl.BindTexture(TextureTarget.TextureCubeMap, Id);

			gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
			gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
			gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
			gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
			gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);

			for (int i = 0; i < 6; ++i)
			{
				FileStream s = File.OpenRead(path + _fileHandleForFaces[i]);
				using (var img = Image.Load<Rgba32>(s))
				{
					s.Dispose();

					uint width = (uint)img.Width;
					uint height = (uint)img.Height;
					MyUtils.UploadPixelData(gl, img, TextureTarget.TextureCubeMapPositiveX + i, InternalFormat.Rgb);
				}
			}
		}

		// If the cubemap is not loaded in the traditional manner, it is probably generated.
		// It will be filled in later, either through an image that is transformed to the right format
		// or generated in some shader.
		public unsafe void GenerateCubeMap(GL gl, uint w, uint h, CubeMapType cubeType)
		{
			Id = gl.GenTexture();
			gl.BindTexture(TextureTarget.TextureCubeMap, Id);

			switch (cubeType)
			{
				case CubeMapType.SHADOW_MAP:
				{
					gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
					gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
					gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);
					gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureCompareMode, (int)GLEnum.CompareRefToTexture);
					gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
					gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

					for (int i = 0; i < 6; ++i)
					{
						gl.TexImage2D(TextureTarget.TextureCubeMapPositiveX + i, 0, InternalFormat.DepthComponent, w, h, 0, PixelFormat.DepthComponent, PixelType.Float, null);
					}
					break;
				}
				case CubeMapType.HDR_MAP:
				{
					gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
					gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
					gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
					gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
					gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);

					for (int i = 0; i < 6; ++i)
					{
						gl.TexImage2D(TextureTarget.TextureCubeMapPositiveX + i, 0, InternalFormat.Rgb32f, w, h, 0, PixelFormat.Rgb, PixelType.Float, null);
					}
					break;
				}
				case CubeMapType.PREFILTER_MAP:
				{
					gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
					gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
					gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);
					gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
					gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

					for (int i = 0; i < 6; ++i)
					{
						gl.TexImage2D(TextureTarget.TextureCubeMapPositiveX + i, 0, InternalFormat.Rgb16f, w, h, 0, PixelFormat.Rgb, PixelType.Float, null);
					}

					// For the specular IBL component we use the mipmap levels to store increasingly
					// rougher representations of the environment. And then interpolate between those
					gl.GenerateMipmap(TextureTarget.TextureCubeMap);
					_maxMipLevels = 5;
					break;
				}
			}
		}

		// For use in the diffuse IBL setup for now
		public void ConvolveCubeMap(GL gl, uint environmentMap, Shader convolveShader)
		{
			convolveShader.Use(gl);
			convolveShader.SetInt(gl, "environmentMap", 0);
			convolveShader.SetMat4(gl, "projection", _captureProjection);

			gl.Viewport(0, 0, Width, Height);
			gl.ActiveTexture(TextureUnit.Texture0);
			gl.BindTexture(TextureTarget.TextureCubeMap, environmentMap);

			for (int i = 0; i < 6; ++i)
			{
				convolveShader.SetMat4(gl, "view", _captureViews[i]);
				gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.TextureCubeMapPositiveX + i, Id, 0);

				gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
				Cube.Render(gl);
			}

		}

		// Specular IBL cubemap component of the integral
		public void PreFilterCubeMap(GL gl, uint environmentMap, uint captureRBO, Shader filterShader)
		{
			filterShader.Use(gl);
			filterShader.SetInt(gl, "environmentMap", 0);
			filterShader.SetMat4(gl, "projection", _captureProjection);

			gl.ActiveTexture(TextureUnit.Texture0);
			gl.BindTexture(TextureTarget.TextureCubeMap, environmentMap);

			// For each Mip level we have to pre-filter the cubemap at each cube face
			for (int mip = 0; mip < _maxMipLevels; ++mip)
			{
				// Mip levels are decreasing powers of two of the original resolution of the cubemap
				uint mipWidth = (uint)(Width * MathF.Pow(0.5f, mip));
				uint mipHeight = (uint)(Height * MathF.Pow(0.5f, mip));

				// The depth component needs to be resized for each mip level too
				gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, captureRBO);
				gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.DepthComponent24, mipWidth, mipHeight);
				gl.Viewport(0, 0, mipWidth, mipHeight);

				for (int i = 0; i < 6; ++i)
				{
					filterShader.SetMat4(gl, "view", _captureViews[i]);

					gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.TextureCubeMapPositiveX + i, Id, mip);

					gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
					Cube.Render(gl);
				}
			}
		}

		// Transform an equirectangular map to a six sided cubemap
		public void EquiRectangularToCubeMap(GL gl, uint equirectangularMap, uint size, Shader transformShader)
		{
			transformShader.Use(gl);
			transformShader.SetInt(gl, "equirectangularMap", 0);
			transformShader.SetMat4(gl, "projection", _captureProjection);

			gl.ActiveTexture(TextureUnit.Texture0);
			gl.BindTexture(TextureTarget.Texture2D, equirectangularMap);
			gl.Viewport(0, 0, size, size);

			for (int i = 0; i < 6; ++i)
			{
				transformShader.SetMat4(gl, "view", _captureViews[i]);
				gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.TextureCubeMapPositiveX + i, Id, 0);

				gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
				Cube.Render(gl);
			}
		}
	}
}
