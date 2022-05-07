using System.Numerics;

namespace HybridRenderingEngine
{
	internal abstract class BaseLight
	{
		public Vector3 Color = Vector3.One;
		public Matrix4x4 ShadowProjectionMat;

		public float Strength = 1f;
		public float ZNear = 1f;
		public float ZFar = 2000f;

		public uint ShadowRes = 1024;
		public uint DepthMapTextureID;
	}
	internal sealed class DirectionalLight : BaseLight
	{
		public Vector3 Direction = new(-1f);

		public Matrix4x4 LightView;
		public Matrix4x4 LightSpaceMatrix;

		public float Distance;
		public float OrthoBoxSize;
	}
	internal sealed class PointLight : BaseLight
	{
		public Vector3 Position;
		public Matrix4x4[] LookAtPerFace = new Matrix4x4[6];
	}

	// Currently only used in the generation of SSBO's for light culling and rendering
	// I think it potentially would be a good idea to just have one overall light struct for all light types
	// and move all light related calculations to the gpu via compute or frag shaders. This should reduce the
	// number of Api calls we're currently making and also unify the current lighting path that is split between 
	// compute shaders and application based calculations for the matrices.
	internal struct GPULight
	{
		public Vector4 Position;
		public Vector4 Color;
		public uint IsEnabled;
		public float Intensity;
		public float Range;
		public float Padding;
	}
}
