using System.Numerics;

namespace HybridRenderingEngine
{
	internal unsafe struct ScreenToView
	{
		public Matrix4x4 inverseProjectionMat;
		public fixed uint tileSizes[4];
		public uint screenWidth;
		public uint screenHeight;
		public float sliceScalingFactor;
		public float sliceBiasFactor;
	}
}
