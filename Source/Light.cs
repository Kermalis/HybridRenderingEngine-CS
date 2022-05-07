using System.Numerics;

namespace HybridRenderingEngine
{
	internal abstract class BaseLight
	{
		public Vector3 color = Vector3.One;
		public Matrix4x4 shadowProjectionMat;

		public bool changed;

		public float strength = 1f;
		public float zNear = 1f;
		public float zFar = 2000f;

		public uint shadowRes = 1024;
		public uint depthMapTextureID;
	}
	internal sealed class DirectionalLight : BaseLight
	{
		public Vector3 direction = new(-1f);

		public Matrix4x4 lightView;
		public Matrix4x4 lightSpaceMatrix;

		public float distance;
		public float orthoBoxSize;
	}
	internal sealed class PointLight : BaseLight
	{
		public Vector3 position;
		public Matrix4x4[] lookAtPerFace = new Matrix4x4[6];
	}

	// Currently only used in the generation of SSBO's for light culling and rendering
	// I think it potentially would be a good idea to just have one overall light struct for all light types
	// and move all light related calculations to the gpu via compute or frag shaders. This should reduce the
	// number of Api calls we're currently making and also unify the current lighting path that is split between 
	// compute shaders and application based calculations for the matrices.
	internal struct GPULight
	{
		public Vector4 position;
		public Vector4 color;
		public uint enabled;
		public float intensity;
		public float range;
		public float padding;
	}
}
