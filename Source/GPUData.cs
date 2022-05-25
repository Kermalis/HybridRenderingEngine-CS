using System.Numerics;

namespace HybridRenderingEngine
{
	internal struct ScreenToView
	{
		public Matrix4x4 InverseProjectionMat;
		public uint TileSizeX;
		public uint TileSizeY;
		public uint TileSizeZ;
		public uint Padding1;
		public Vector2 TileSizePixels;
		public Vector2 ViewPixelSize;
		public float SliceScalingFactor;
		public float SliceBiasFactor;
		public uint Padding2;
		public uint Padding3;
	}
}
