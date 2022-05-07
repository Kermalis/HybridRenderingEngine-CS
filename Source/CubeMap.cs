using HybridRenderingEngine.Utils;
using Silk.NET.OpenGL;
using StbiSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

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
		public const int numSidesInCube = 6;

		// Initializing all static variables at compile time
		private static readonly Matrix4x4 captureProjection = Matrix4x4.CreatePerspectiveFieldOfView(90f * MyUtils.DEG_TO_RAD, 1f, 0.1f, 10f);
		private static readonly Matrix4x4[] captureViews = new Matrix4x4[numSidesInCube]
		{
			Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3(1f, 0f, 0f), new Vector3(0f, -1f, 0f)),
			Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3(-1f, 0f, 0f), new Vector3(0f, -1f, 0f)),
			Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, 1f)),
			Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3(0f, -1f, 0f), new Vector3(0f, 0f, -1f)),
			Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, 1f), new Vector3(0f, -1f, 0f)),
			Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, -1f), new Vector3(0f, -1f, 0f))
		};
		private static readonly string[] fileHandleForFaces = new string[numSidesInCube]
		{
			"right.jpg", "left.jpg", "top.jpg", "bottom.jpg", "front.jpg", "back.jpg"
		};

		// Useful in the Specular IBL component
		public uint maxMipLevels;

		// Static constants used in all cubemaps
		public static Cube cubeMapCube;

		// Traditional cubemap generation from 6 regular image files named according to the
		// string array fileHandlesForFaces. Order comes frfom Opengl cubemap specification
		public void LoadCubeMap(GL gl, string folderPath)
		{
			path = folderPath + "/";
			type = "cubemap";

			textureID = gl.GenTexture();
			gl.BindTexture(TextureTarget.TextureCubeMap, textureID);

			for (int i = 0; i < numSidesInCube; ++i)
			{
				MemoryStream ms;
				using (FileStream stream = File.OpenRead(path + fileHandleForFaces[i]))
				{
					ms = new MemoryStream();
					stream.CopyTo(ms);
				}
				StbiImage img = Stbi.LoadFromMemory(ms, 0);
				uint width = (uint)img.Width;
				uint height = (uint)img.Height;

				gl.TexImage2D(TextureTarget.TextureCubeMapPositiveX + i, 0, InternalFormat.Rgb, width, height, 0, PixelFormat.Rgb, PixelType.UnsignedByte, img.Data);

				img.Dispose();
				ms.Dispose();
			}

			// Texture parameters
			gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
			gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
			gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
			gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
			gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);
		}

		// If the cubemap is not loaded in the traditional manner, it is probably generated.
		// It will be filled in later, either through an image that is transformed to the right format
		// or generated in some shader.
		public unsafe void GenerateCubeMap(GL gl, uint width, uint height, CubeMapType cubeType)
		{
			uint ID = gl.GenTexture();
			gl.BindTexture(TextureTarget.TextureCubeMap, ID);

			switch (cubeType)
			{
				case CubeMapType.SHADOW_MAP:
					for (int i = 0; i < numSidesInCube; ++i)
					{
						gl.TexImage2D(TextureTarget.TextureCubeMapPositiveX + i, 0, InternalFormat.DepthComponent, width, height, 0, PixelFormat.DepthComponent, PixelType.Float, null);
					}
					gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
					gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
					gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);
					gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureCompareMode, (int)GLEnum.CompareRefToTexture);
					gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
					gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
					break;

				case CubeMapType.HDR_MAP:
					for (int i = 0; i < numSidesInCube; ++i)
					{
						gl.TexImage2D(TextureTarget.TextureCubeMapPositiveX + i, 0, InternalFormat.Rgb32f, width, height, 0, PixelFormat.Rgb, PixelType.Float, null);
					}
					gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
					gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
					gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
					gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
					gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);
					break;

				case CubeMapType.PREFILTER_MAP:
					for (int i = 0; i < numSidesInCube; ++i)
					{
						gl.TexImage2D(TextureTarget.TextureCubeMapPositiveX + i, 0, InternalFormat.Rgb16f, width, height, 0, PixelFormat.Rgb, PixelType.Float, null);
					}
					gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
					gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
					gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);
					gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
					gl.TexParameterI(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

					// For the specular IBL component we use the mipmap levels to store increasingly
					// rougher representations of the environment. And then interpolater between those
					gl.GenerateMipmap(TextureTarget.TextureCubeMap);
					maxMipLevels = 5;
					break;
			}

			textureID = ID;
			path = ""; // Generated so no file path
			type = "cubemap";
		}

		// For use in the diffuse IBL setup for now
		public void ConvolveCubeMap(GL gl, uint environmentMap, Shader convolveShader)
		{
			convolveShader.Use(gl);
			convolveShader.SetInt(gl, "environmentMap", 0);
			convolveShader.SetMat4(gl, "projection", captureProjection);

			gl.Viewport(0, 0, width, height);
			gl.ActiveTexture(TextureUnit.Texture0);
			gl.BindTexture(TextureTarget.TextureCubeMap, environmentMap);

			for (int i = 0; i < numSidesInCube; ++i)
			{
				convolveShader.SetMat4(gl, "view", captureViews[i]);
				gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.TextureCubeMapPositiveX + i, textureID, 0);

				gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
				DrawCube(gl);
			}

		}

		// Specular IBL cubemap component of hte integral
		public void PreFilterCubeMap(GL gl, uint environmentMap, uint captureRBO, Shader filterShader)
		{
			filterShader.Use(gl);
			filterShader.SetInt(gl, "environmentMap", 0);
			filterShader.SetMat4(gl, "projection", captureProjection);

			gl.ActiveTexture(TextureUnit.Texture0);
			gl.BindTexture(TextureTarget.TextureCubeMap, environmentMap);

			// For each Mip level we have to pre-filter the cubemap at each cube face
			for (int mip = 0; mip < maxMipLevels; ++mip)
			{
				// Mip levels are decreasing powers of two of the original resolution of the cubemap
				uint mipWidth = (uint)(width * MathF.Pow(0.5f, mip));
				uint mipHeight = (uint)(height * MathF.Pow(0.5f, mip));

				// The depth component needs to be resized for each mip level too
				gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, captureRBO);
				gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.DepthComponent24, mipWidth, mipHeight);
				gl.Viewport(0, 0, mipWidth, mipHeight);

				for (int i = 0; i < numSidesInCube; ++i)
				{
					filterShader.SetMat4(gl, "view", captureViews[i]);

					gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.TextureCubeMapPositiveX + i, textureID, mip);

					gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
					DrawCube(gl);
				}
			}
		}

		// Transform an equirectangular map to a six sided cubemap
		public void EquiRectangularToCubeMap(GL gl, uint equirectangularMap, uint resolution, Shader transformShader)
		{
			transformShader.Use(gl);
			transformShader.SetInt(gl, "equirectangularMap", 0);
			transformShader.SetMat4(gl, "projection", captureProjection);

			gl.ActiveTexture(TextureUnit.Texture0);
			gl.BindTexture(TextureTarget.Texture2D, equirectangularMap);
			gl.Viewport(0, 0, resolution, resolution);

			for (int i = 0; i < numSidesInCube; i++)
			{
				transformShader.SetMat4(gl, "view", captureViews[i]);
				gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.TextureCubeMapPositiveX + i, textureID, 0);

				gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
				DrawCube(gl);
			}
		}

		// This function seems kind of useless, I should check to see if it is being inlined away
		// by the compiler and think of better ways to do this
		public void DrawCube(GL gl)
		{
			cubeMapCube.Draw(gl);
		}
	}
}
