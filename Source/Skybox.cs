using HybridRenderingEngine.Utils;
using Silk.NET.OpenGL;

namespace HybridRenderingEngine
{
	internal sealed class Skybox
	{
		private const string SKYBOX_PATH = MyUtils.ASSET_PATH + @"/Skyboxes/";

		public readonly uint Res;

		// Equirectangular map is not rendered, just an intermediate state
		private readonly Texture _equirectangularMap;
		public readonly CubeMap SkyboxCubeMap;

		// Two setup paths for HDR and non HDR cubemap
		public Skybox(GL gl, string skyboxName, bool isHDR, uint res)
		{
			Res = res;

			SkyboxCubeMap = new CubeMap();
			_equirectangularMap = new Texture();

			string dir = SKYBOX_PATH + skyboxName + '/';
			if (isHDR)
			{
				// If the skybox is HDR it will come in as an equirectangular map
				// We need to load it in and generate the cubemap that will be shown

				// For now, ImageSharp cannot read .hdr files, so use temporary .png ones
				//string skyBoxFilePath = dir + skyboxName + ".hdr";
				string skyBoxFilePath = dir + skyboxName + ".png";

				_equirectangularMap.LoadHDRTexture(gl, skyBoxFilePath);
				SkyboxCubeMap.GenerateCubeMap(gl, res, res, CubeMapType.HDR_MAP);
			}
			else
			{
				SkyboxCubeMap.LoadCubeMap(gl, dir, res);
			}
		}

		public void Render(GL gl)
		{
			// We change the depth function since we set the skybox to a clip-space value of one
			gl.DepthFunc(DepthFunction.Lequal);
			gl.DepthMask(false);

			gl.ActiveTexture(TextureUnit.Texture0);
			gl.BindTexture(TextureTarget.TextureCubeMap, SkyboxCubeMap.Id);
			CubeMap.Cube.Render(gl);

			gl.DepthMask(true);
			gl.DepthFunc(DepthFunction.Less);
		}

		// Instead of passing the shader in, we could call this function when skybox is initialized?
		public void FillCubeMapWithTexture(GL gl, Shader buildCubeMapShader)
		{
			SkyboxCubeMap.EquiRectangularToCubeMap(gl, _equirectangularMap.Id, Res, buildCubeMapShader);
		}

		public void Delete(GL gl)
		{
			_equirectangularMap.Delete(gl);
			SkyboxCubeMap.Delete(gl);
		}
	}
}
