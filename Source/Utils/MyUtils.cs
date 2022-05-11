using Silk.NET.OpenGL;
using Silk.NET.SDL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Numerics;

namespace HybridRenderingEngine.Utils
{
	internal sealed class MyUtils
	{
		public const string ASSET_PATH = @"../../Assets";

		public const float DEG_TO_RAD = MathF.PI / 180f;
		public const float RAD_TO_DEG = 180f / MathF.PI;

		public const int SDL_BUTTON_LMASK = 1 << (Sdl.ButtonLeft - 1);

		public static unsafe void UploadPixelData(GL gl, Image<Rgba32> img, TextureTarget target, InternalFormat internalformat)
		{
			gl.TexImage2D(target, 0, internalformat, (uint)img.Width, (uint)img.Height, 0, GLEnum.Rgba, GLEnum.UnsignedByte, null);

			img.ProcessPixelRows(accessor =>
			{
				for (int y = 0; y < accessor.Height; y++)
				{
					fixed (void* data = accessor.GetRowSpan(y))
					{
						gl.TexSubImage2D(target, 0, 0, y, (uint)accessor.Width, 1, GLEnum.Rgba, GLEnum.UnsignedByte, data);
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
