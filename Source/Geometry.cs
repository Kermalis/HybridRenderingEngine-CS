using HybridRenderingEngine.Utils;
using System;

namespace HybridRenderingEngine
{
	internal struct Frustum
	{
		public float fov, nearPlane, farPlane, AR, nearH, nearW;

		public void setCamInternals()
		{
			float tanHalfFOV = MathF.Tan(fov / 2f * MyUtils.DEG_TO_RAD);
			nearH = nearPlane * tanHalfFOV; // Half of the frustrum near plane height
			nearW = nearH * AR;
		}
	}
}
