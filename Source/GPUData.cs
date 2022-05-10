using System.Numerics;

namespace HybridRenderingEngine
{
	internal struct ScreenToView
	{
		public Matrix4x4 InverseProjectionMat;
		public uint TileSizeX;
		public uint TileSizeY;
		public uint TileSizeZ;
		public uint TileSizePixels;
		public uint ScreenWidth;
		public uint ScreenHeight;
		public float SliceScalingFactor;
		public float SliceBiasFactor;
	}
}
