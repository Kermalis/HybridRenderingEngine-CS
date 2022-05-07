using HybridRenderingEngine.Utils;
using Silk.NET.OpenGL;

namespace HybridRenderingEngine
{
	internal sealed class Skybox
	{
		private const string SKYBOX_PATH = MyUtils.ASSET_PATH + @"/Skyboxes/";

		public readonly uint resolution;

		// Equirectangular map is not rendered, just an intermediate state
		public readonly Texture equirectangularMap;
		public readonly CubeMap skyBoxCubeMap;

		// Two setup paths for HDR and non HDR cubemap
		public Skybox(GL gl, string skyboxName, bool isHDR, uint res)
		{
			resolution = res;

			// If the skybox is HDR it will come in as an equirectangular map,
			// We need to load it in and generate the cubemap that will be shown
			skyBoxCubeMap = new CubeMap();
			if (isHDR)
			{
				equirectangularMap = new Texture();

				string skyBoxFilePath = SKYBOX_PATH + skyboxName + '/' + skyboxName + ".hdr";

				equirectangularMap.LoadHDRTexture(gl, skyBoxFilePath);
				skyBoxCubeMap.GenerateCubeMap(gl, res, res, CubeMapType.HDR_MAP);
			}
			else
			{
				skyBoxCubeMap.LoadCubeMap(gl, SKYBOX_PATH);
			}
		}

		public void Draw(GL gl)
		{
			// We change the depth function since we set the skybox to a clipspace value of one
			gl.DepthFunc(DepthFunction.Lequal);
			gl.DepthMask(false);

			gl.ActiveTexture(TextureUnit.Texture0);
			gl.BindTexture(TextureTarget.TextureCubeMap, skyBoxCubeMap.textureID);
			skyBoxCubeMap.DrawCube(gl);

			gl.DepthMask(true);
			gl.DepthFunc(DepthFunction.Less);
		}

		// Instead of passing the shader in, we could call this function when skybox is initialized?
		public void FillCubeMapWithTexture(GL gl, Shader buildCubeMapShader)
		{
			skyBoxCubeMap.EquiRectangularToCubeMap(gl, equirectangularMap.textureID, resolution, buildCubeMapShader);
		}

		public void Delete(GL gl)
		{
			equirectangularMap.Delete(gl);
			skyBoxCubeMap.Delete(gl);
		}
	}
}
