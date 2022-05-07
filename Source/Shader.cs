using HybridRenderingEngine.Utils;
using Silk.NET.OpenGL;
using System;
using System.IO;
using System.Numerics;

namespace HybridRenderingEngine
{
	internal class Shader
	{
		private const string SHADER_PATH = MyUtils.ASSET_PATH + @"/Shaders/";

		// Shader program ID for referencing
		public uint ID;

		protected Shader()
		{
			//
		}

		// Shader setup and initialization code, could be combined with the compute shader initialization and
		// could also set success/failure flags to indicate issues during load to avoid full crashes. TODO
		public Shader(GL gl, string vertexPath, string fragmentPath, string geometryPath = null)
		{
			// Getting the vertex shader code from the text file at file path
			bool gShaderOn = !string.IsNullOrEmpty(geometryPath);

			// Vertex shader stuff
			uint vertexShader = gl.CreateShader(ShaderType.VertexShader);
			gl.ShaderSource(vertexShader, File.ReadAllText(SHADER_PATH + vertexPath));
			gl.CompileShader(vertexShader);
			gl.GetShader(vertexShader, ShaderParameterName.CompileStatus, out int success);
			if (success == 0)
			{
				throw new Exception("Vertex shader compilation failed: " + gl.GetShaderInfoLog(vertexShader));
			}

			// Fragment shader stuff
			uint fragmentShader = gl.CreateShader(ShaderType.FragmentShader);
			gl.ShaderSource(fragmentShader, File.ReadAllText(SHADER_PATH + fragmentPath));
			gl.CompileShader(fragmentShader);
			gl.GetShader(fragmentShader, ShaderParameterName.CompileStatus, out success);
			if (success == 0)
			{
				throw new Exception("Fragment shader compilation failed: " + gl.GetShaderInfoLog(fragmentShader));
			}

			// Geometry shader stuff
			uint geometryShader = 0;
			if (gShaderOn)
			{
				geometryShader = gl.CreateShader(ShaderType.GeometryShader);
				gl.ShaderSource(geometryShader, File.ReadAllText(SHADER_PATH + geometryPath));
				gl.CompileShader(geometryShader);
				gl.GetShader(geometryShader, ShaderParameterName.CompileStatus, out success);
				if (success == 0)
				{
					throw new Exception("Geometry shader compilation failed: " + gl.GetShaderInfoLog(geometryShader));
				}
			}

			// Linking shaders
			ID = gl.CreateProgram();
			gl.AttachShader(ID, vertexShader);
			gl.AttachShader(ID, fragmentShader);
			if (gShaderOn)
			{
				gl.AttachShader(ID, geometryShader);
			}
			gl.LinkProgram(ID);

			gl.GetProgram(ID, ProgramPropertyARB.LinkStatus, out success);
			if (success == 0)
			{
				throw new Exception("Shader Linking failed: " + gl.GetProgramInfoLog(ID));
			}

			// Deleting shaders
			gl.DeleteShader(vertexShader);
			gl.DeleteShader(fragmentShader);
			if (gShaderOn)
			{
				gl.DeleteShader(geometryShader);
			}
		}

		// Indicate to openGL that this is the GPU program that is going to be run
		public void Use(GL gl)
		{
			gl.UseProgram(ID);
		}

		// Setting uniforms within the shader, share functionality with compute
		public void SetBool(GL gl, string name, bool value)
		{
			gl.Uniform1(gl.GetUniformLocation(ID, name), value ? 1 : 0);
		}
		public void SetInt(GL gl, string name, int value)
		{
			gl.Uniform1(gl.GetUniformLocation(ID, name), value);
		}
		public void SetFloat(GL gl, string name, float value)
		{
			gl.Uniform1(gl.GetUniformLocation(ID, name), value);
		}
		public unsafe void SetMat4(GL gl, string name, Matrix4x4 mat)
		{
			gl.UniformMatrix4(gl.GetUniformLocation(ID, name), 1, false, (float*)&mat);
		}
		public unsafe void SetVec3(GL gl, string name, Vector3 vec)
		{
			gl.Uniform3(gl.GetUniformLocation(ID, name), 1, (float*)&vec);
		}

		public void Delete(GL gl)
		{
			gl.DeleteProgram(ID);
		}
	}
	internal sealed class ComputeShader : Shader
	{
		private const string SHADER_PATH = MyUtils.ASSET_PATH + @"/Shaders/ComputeShaders/";

		public ComputeShader(GL gl, string computePath)
		{
			// OpenGL initialization
			uint computeShader = gl.CreateShader(ShaderType.ComputeShader);
			gl.ShaderSource(computeShader, File.ReadAllText(SHADER_PATH + computePath));
			gl.CompileShader(computeShader);
			gl.GetShader(computeShader, ShaderParameterName.CompileStatus, out int success);
			if (success == 0)
			{
				throw new Exception("Vertex shader compilation failed: " + gl.GetShaderInfoLog(computeShader));
			}

			// Linking shaders
			ID = gl.CreateProgram();
			gl.AttachShader(ID, computeShader);
			gl.LinkProgram(ID);

			gl.GetProgram(ID, ProgramPropertyARB.LinkStatus, out success);
			if (success == 0)
			{
				throw new Exception("Shader Linking failed: " + gl.GetProgramInfoLog(ID));
			}

			// Deleting shaders
			gl.DeleteShader(computeShader);
		}

		// Shorthand for dispatch compute with some default parameter values
		public static void Dispatch(GL gl, uint x, uint y, uint z)
		{
			gl.DispatchCompute(x, y, z);
			gl.MemoryBarrier(MemoryBarrierMask.ShaderStorageBarrierBit);
		}
	}
}
