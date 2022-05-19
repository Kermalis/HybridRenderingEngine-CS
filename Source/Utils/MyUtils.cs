using Silk.NET.OpenGL;
using Silk.NET.SDL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;
using System.Numerics;

namespace HybridRenderingEngine.Utils
{
	internal sealed class MyUtils
	{
		public const string ASSET_PATH = @"../../Assets";

		public const float DEG_TO_RAD = MathF.PI / 180f;
		public const float RAD_TO_DEG = 180f / MathF.PI;

		public const int SDL_BUTTON_LMASK = 1 << (Sdl.ButtonLeft - 1);

		public static void SaveReadBufferAsImage(GL gl, uint width, uint height, string path)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			path = Path.GetFullPath(path);

			var data = new Rgb24[width * height];

			gl.ReadPixels(0, 0, width, height, GLEnum.Rgb, GLEnum.UnsignedByte, data.AsSpan());
			using (var img = Image.LoadPixelData(data, (int)width, (int)height))
			{
				img.Mutate(x => x.Flip(FlipMode.Vertical));
				img.SaveAsPng(path);
			}
		}
		public static void SaveTexture2DAsImage(GL gl, uint tex, int width, int height, string path)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			path = Path.GetFullPath(path);

			gl.BindTexture(GLEnum.Texture2D, tex);
			var data = new Rgb24[width * height];

			gl.GetTexImage(GLEnum.Texture2D, 0, GLEnum.Rgb, GLEnum.UnsignedByte, data.AsSpan());
			using (var img = Image.LoadPixelData(data, width, height))
			{
				img.Mutate(x => x.Flip(FlipMode.Vertical));
				img.SaveAsPng(path);
			}
		}
		public static unsafe void SaveCubeMapAsImages(GL gl, uint tex, uint width, uint height, string path)
		{
			Directory.CreateDirectory(path);
			path = Path.GetFullPath(path);

			gl.BindTexture(GLEnum.TextureCubeMap, tex);
			var data = new Rgb24[width * height];

			for (int i = 0; i < 6; ++i)
			{
				gl.GetTexImage(GLEnum.TextureCubeMapPositiveX + i, 0, GLEnum.Rgb, GLEnum.UnsignedByte, data.AsSpan());
				using (var img = Image.LoadPixelData(data, (int)width, (int)height))
				{
					img.SaveAsPng(Path.Combine(path, (GLEnum.TextureCubeMapPositiveX + i) + ".png"));
				}
			}
		}

		public static unsafe void UploadPixelData(GL gl, Image<Bgra32> img, TextureTarget target)
		{
			img.ProcessPixelRows(accessor =>
			{
				for (int y = 0; y < accessor.Height; y++)
				{
					fixed (void* data = accessor.GetRowSpan(y))
					{
						gl.TexSubImage2D(target, 0, 0, y, (uint)accessor.Width, 1, GLEnum.Bgra, GLEnum.UnsignedByte, data);
					}
				}
			});
		}

		// GLM-style projection matrices -- used to get expected GL-spec depth and coordinate systems
		public static Matrix4x4 GLM_LookAtRH(in Vector3 eye, in Vector3 center, in Vector3 up)
		{
			var f = Vector3.Normalize(center - eye);
			var s = Vector3.Normalize(Vector3.Cross(f, up));
			var u = Vector3.Cross(s, f);
			Matrix4x4 m;
			m.M11 = s.X;
			m.M12 = u.X;
			m.M13 = -f.X;
			m.M14 = 0f;
			m.M21 = s.Y;
			m.M22 = u.Y;
			m.M23 = -f.Y;
			m.M24 = 0f;
			m.M31 = s.Z;
			m.M32 = u.Z;
			m.M33 = -f.Z;
			m.M34 = 0f;
			m.M41 = -Vector3.Dot(s, eye);
			m.M42 = -Vector3.Dot(u, eye);
			m.M43 = Vector3.Dot(f, eye);
			m.M44 = 1f;
			return m;
		}
		public static Matrix4x4 GLM_OrthoRH_NO(float left, float right, float bottom, float top, float zNear, float zFar)
		{
			Matrix4x4 m;
			m.M11 = 2f / (right - left);
			m.M12 = 0f;
			m.M13 = 0f;
			m.M14 = 0f;
			m.M21 = 0f;
			m.M22 = 2f / (top - bottom);
			m.M23 = 0f;
			m.M24 = 0f;
			m.M31 = 0f;
			m.M32 = 0f;
			m.M33 = -2f / (zFar - zNear);
			m.M34 = 0f;
			m.M41 = -(right + left) / (right - left);
			m.M42 = -(top + bottom) / (top - bottom);
			m.M43 = -(zFar + zNear) / (zFar - zNear);
			m.M44 = 1f;
			return m;
		}
		public static Matrix4x4 GLM_PerspectiveRH_NO(float fovy, float aspect, float zNear, float zFar)
		{
			float tanHalfFovy = (float)Math.Tan(fovy * 0.5f);
			Matrix4x4 m;
			m.M11 = 1f / (aspect * tanHalfFovy);
			m.M12 = 0f;
			m.M13 = 0f;
			m.M14 = 0f;
			m.M21 = 0f;
			m.M22 = 1f / tanHalfFovy;
			m.M23 = 0f;
			m.M24 = 0f;
			m.M31 = 0f;
			m.M32 = 0f;
			m.M33 = -(zFar + zNear) / (zFar - zNear);
			m.M34 = -1f; // Negative Z is forward
			m.M41 = 0f;
			m.M42 = 0f;
			m.M43 = -2f * zFar * zNear / (zFar - zNear);
			m.M44 = 0f;
			return m;
		}
	}
}
