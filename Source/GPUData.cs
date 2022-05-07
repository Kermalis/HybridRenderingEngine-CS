using System.Numerics;

namespace HybridRenderingEngine
{
	internal unsafe struct ScreenToView
	{
		public Matrix4x4 InverseProjectionMat;
		public fixed uint TileSizes[4];
		public uint ScreenWidth;
		public uint ScreenHeight;
		public float SliceScalingFactor;
		public float SliceBiasFactor;
	}
}
