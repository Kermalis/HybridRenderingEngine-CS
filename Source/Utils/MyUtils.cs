using Silk.NET.SDL;
using System;

namespace HybridRenderingEngine.Utils
{
	internal sealed class MyUtils
	{
		public const string ASSET_PATH = @"../../Assets";

		public const float DEG_TO_RAD = MathF.PI / 180f;
		public const float RAD_TO_DEG = 180f / MathF.PI;

		public const int SDL_BUTTON_LMASK = 1 << (Sdl.ButtonLeft - 1);
	}
}
