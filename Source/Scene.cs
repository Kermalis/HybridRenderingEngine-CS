using HybridRenderingEngine.Utils;
using ImGuiNET;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;

namespace HybridRenderingEngine
{
	internal sealed class Scene
	{
		public static Scene Instance;

		public readonly string SceneId;

		public CubeMap IrradianceMap;
		public CubeMap SpecFilteredMap;
		public Texture BRDF_LUT_Texture;
		public Skybox Skybox;
		public Camera Cam;

		private bool _slices;

		// TODO:: unify light model so that we can have a single array pointing to a base class (or single class)
		// so that we can iterate through it
		public DirectionalLight DirLight;
		public PointLight[] PointLights;

		// Contains the models that remain after frustrum culling which is TB done
		private readonly List<Model> _visibleModels;
		private readonly List<Model> _models;

		public Scene(GL gl, string sceneId)
		{
			Instance = this;

			SceneId = sceneId;

			_visibleModels = new List<Model>();
			_models = new List<Model>();

			Console.WriteLine();
			Console.WriteLine("Beginning Scene load, checking scene description file:");
			LoadContent(gl);
		}

		private void LoadContent(GL gl)
		{
			// Parsing into Json file readable format
			string path = MyUtils.ASSET_PATH + @"/Scenes/" + SceneId + ".json";
			using (FileStream s = File.OpenRead(path))
			using (var configJson = JsonDocument.Parse(s))
			{
				JsonElement root = configJson.RootElement;
				// Checking that config file belongs to current scene and is properly formatted
				if (root.GetProperty("sceneID").GetString() != SceneId || root.GetProperty("models").GetArrayLength() == 0)
				{
					throw new Exception(string.Format("Error! Config file: {0} does not belong to current scene, check configuration.", path));
				}

				// now we parse the rest of the file, but don't do any other checks. It would be worth it to
				// have a preliminary check that looks at the content of the scene description file and only then
				// decides what to load and what to generate incase it can't find the data, because right now
				// if you can't find the data it will just crash. So a check for correct formatting might not only
				// make sense in a correctness based 
				Console.WriteLine("Loading camera...");
				LoadCamera(root.GetProperty("camera"));

				Console.WriteLine("Loading models...");
				LoadSceneModels(root.GetProperty("models"));

				if (_models.Count == 0)
				{
					throw new Exception("No models");
				}

				Console.WriteLine("Loading skybox...");
				CubeMap.Cube = new Cube(gl);
				LoadSkyBox(root.GetProperty("skybox"));

				Console.WriteLine("Loading lights...");
				LoadLights(root);

				Console.WriteLine("Generating environment maps...");
				GenerateEnvironmentMaps(gl);

				Console.WriteLine("Loading Complete!...");
			}
		}

		private void LoadCamera(in JsonElement cameraSettings)
		{
			float speed = cameraSettings.GetProperty("speed").GetSingle();
			float sens = cameraSettings.GetProperty("mouseSens").GetSingle();
			float fov = cameraSettings.GetProperty("fov").GetSingle();
			float nearP = cameraSettings.GetProperty("nearPlane").GetSingle();
			float farP = cameraSettings.GetProperty("farPlane").GetSingle();

			JsonElement position = cameraSettings.GetProperty("position");
			var pos = new Vector3(position[0].GetSingle(), position[1].GetSingle(), position[2].GetSingle());

			JsonElement target = cameraSettings.GetProperty("target");
			var tar = new Vector3(target[0].GetSingle(), target[1].GetSingle(), target[2].GetSingle());

			Cam = new Camera(tar, pos, fov, speed, sens, nearP, farP);
		}

		// TODO:: rewrite during the material system update
		private void LoadSceneModels(in JsonElement models)
		{
			int modelCount = models.GetArrayLength();

			for (int i = 0; i < modelCount; ++i)
			{
				// get model mesh and material info
				JsonElement currentModel = models[i];
				string modelMesh = currentModel.GetProperty("mesh").GetString();
				bool IBL = currentModel.GetProperty("IBL").GetBoolean();

				string modelName = modelMesh.Substring(0, modelMesh.LastIndexOf('.'));
				TransformParameters @params;

				// position
				JsonElement pos = currentModel.GetProperty("position");
				@params.Translation = new Vector3(pos[0].GetSingle(), pos[1].GetSingle(), pos[2].GetSingle());

				// rotation
				JsonElement rot = currentModel.GetProperty("rotation");
				@params.Angle = rot[0].GetSingle() * MyUtils.DEG_TO_RAD;
				@params.RotationAxis = new Vector3(rot[1].GetSingle(), rot[2].GetSingle(), rot[3].GetSingle());

				// scaling
				JsonElement scaling = currentModel.GetProperty("scaling");
				@params.Scale = new Vector3(scaling[0].GetSingle(), scaling[1].GetSingle(), scaling[2].GetSingle());

				// attempts to load model with the initparameters it has read
				modelMesh = MyUtils.ASSET_PATH + @"/Models/" + modelName + '/' + modelMesh;
				_models.Add(new Model(DisplayManager.Instance.OpenGL, DisplayManager.Instance.Assimp, modelMesh, @params, IBL));
			}
		}

		private void LoadSkyBox(in JsonElement skyBox)
		{
			string skyBoxName = skyBox.GetProperty("id").GetString();
			bool isHDR = skyBox.GetProperty("hdr").GetBoolean();
			uint resolution = skyBox.GetProperty("resolution").GetUInt32();

			Skybox = new Skybox(DisplayManager.Instance.OpenGL, skyBoxName, isHDR, resolution);
		}

		private void LoadLights(in JsonElement root)
		{
			// Directional light
			Console.WriteLine("Loading directional light...");
			{
				JsonElement light = root.GetProperty("directionalLight");
				DirLight = new DirectionalLight();

				JsonElement dir = light.GetProperty("direction");
				DirLight.Direction = new Vector3(dir[0].GetSingle(), dir[1].GetSingle(), dir[2].GetSingle());

				JsonElement color = light.GetProperty("color");
				DirLight.Color = new Vector3(color[0].GetSingle(), color[1].GetSingle(), color[2].GetSingle());

				// Scalar values
				DirLight.Distance = light.GetProperty("distance").GetSingle();
				DirLight.Strength = light.GetProperty("strength").GetSingle();
				DirLight.ZNear = light.GetProperty("zNear").GetSingle();
				DirLight.ZFar = light.GetProperty("zFar").GetSingle();
				DirLight.OrthoBoxSize = light.GetProperty("orthoSize").GetSingle();
				DirLight.ShadowRes = light.GetProperty("shadowRes").GetUInt32();

				// Matrix values
				float left = DirLight.OrthoBoxSize;
				float right = -left;
				float top = left;
				float bottom = right;
				// I'm not sure yet why we have to multiply by the distance here, I understand that if I don't much of the
				// screen won't be shown, but I am confused as this goes against my understanding of how an orthographic 
				// projection works. This will have to be reviewed at a later point.
				DirLight.ShadowProjectionMat = MyUtils.GLM_OrthoRH_NO(left, right, bottom, top, DirLight.ZNear, DirLight.ZFar);
				DirLight.LightView = MyUtils.GLM_LookAtRH(DirLight.Distance * -DirLight.Direction, Vector3.Zero, Vector3.UnitY);

				DirLight.LightSpaceMatrix = DirLight.LightView * DirLight.ShadowProjectionMat; // KERM
			}

			// Point lights
			Console.WriteLine("Loading point light...");
			{
				// Get number of lights in scene and initialize array containing them
				JsonElement pl = root.GetProperty("pointLights");
				PointLights = new PointLight[pl.GetArrayLength()];

				for (int i = 0; i < PointLights.Length; ++i)
				{
					JsonElement light = pl[i];
					PointLights[i] = new PointLight();

					JsonElement pos = light.GetProperty("position");
					PointLights[i].Position = new Vector3(pos[0].GetSingle(), pos[1].GetSingle(), pos[2].GetSingle());

					JsonElement color = light.GetProperty("color");
					PointLights[i].Color = new Vector3(color[0].GetSingle(), color[1].GetSingle(), color[2].GetSingle());

					// Scalar values
					PointLights[i].Strength = light.GetProperty("strength").GetSingle();
					PointLights[i].ZNear = light.GetProperty("zNear").GetSingle();
					PointLights[i].ZFar = light.GetProperty("zFar").GetSingle();
					PointLights[i].ShadowRes = light.GetProperty("shadowRes").GetUInt32();

					// Matrix setup
					PointLights[i].ShadowProjectionMat = MyUtils.GLM_PerspectiveRH_NO(90f * MyUtils.DEG_TO_RAD, 1f, PointLights[i].ZNear, PointLights[i].ZFar);

					ref Vector3 lightPos = ref PointLights[i].Position;
					PointLights[i].LookAtPerFace[0] = MyUtils.GLM_LookAtRH(lightPos, lightPos + new Vector3(1f, 0f, 0f), new Vector3(0f, -1f, 0f));
					PointLights[i].LookAtPerFace[1] = MyUtils.GLM_LookAtRH(lightPos, lightPos + new Vector3(-1f, 0f, 0f), new Vector3(0f, -1f, 0f));
					PointLights[i].LookAtPerFace[2] = MyUtils.GLM_LookAtRH(lightPos, lightPos + new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, 1f));
					PointLights[i].LookAtPerFace[3] = MyUtils.GLM_LookAtRH(lightPos, lightPos + new Vector3(0f, -1f, 0f), new Vector3(0f, 0f, -1f));
					PointLights[i].LookAtPerFace[4] = MyUtils.GLM_LookAtRH(lightPos, lightPos + new Vector3(0f, 0f, 1f), new Vector3(0f, -1f, 0f));
					PointLights[i].LookAtPerFace[5] = MyUtils.GLM_LookAtRH(lightPos, lightPos + new Vector3(0f, 0f, -1f), new Vector3(0f, -1f, 0f));
				}
			}
		}

		// TODO move the fixed size somewhere else
		private void GenerateEnvironmentMaps(GL gl)
		{
			// Diffuse map
			IrradianceMap = new CubeMap();
			IrradianceMap.Width = 32;
			IrradianceMap.Height = 32;
			IrradianceMap.GenerateCubeMap(gl, IrradianceMap.Width, IrradianceMap.Height, CubeMapType.HDR_MAP);

			// Specular map
			SpecFilteredMap = new CubeMap();
			SpecFilteredMap.Width = 128;
			SpecFilteredMap.Height = 128;
			SpecFilteredMap.GenerateCubeMap(gl, SpecFilteredMap.Width, SpecFilteredMap.Height, CubeMapType.PREFILTER_MAP);

			// Setting up texture ahead of time
			const uint RES = 512;
			BRDF_LUT_Texture = new Texture();
			BRDF_LUT_Texture.Height = RES;
			BRDF_LUT_Texture.Width = RES;
			BRDF_LUT_Texture.Id = gl.GenTexture();
			gl.BindTexture(TextureTarget.Texture2D, BRDF_LUT_Texture.Id);
			gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
			gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
			gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
			gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
			gl.TexStorage2D(TextureTarget.Texture2D, 1, SizedInternalFormat.RG16f, RES, RES);
		}

		// Update Order is critical for correct culling since we want to cull the objects after moving,
		// not before. That would be very dumb, who would do that...
		public void Update(uint deltaT)
		{
			_visibleModels.Clear();
			Cam.Update(deltaT);
			// Light update could go here too
			FrustrumCulling();
		}

		// Very simple setup that iterates through all objects and draws their depth value to a buffer
		// Optimization is very possible here, specifically because we draw all items.
		public void DrawDepthPass(GL gl, Shader depthPassShader)
		{
			depthPassShader.Use(gl);

			// Matrix Setup
			Matrix4x4 VP = Cam.ViewMatrix * Cam.ProjectionMatrix; // KERM

			// Drawing every object into the depth buffer
			for (int i = 0; i < _models.Count; ++i)
			{
				Model m = _models[i];

				// Matrix setup
				Matrix4x4 MVP = m.Matrix * VP; // KERM

				// Shader setup stuff that changes every frame
				depthPassShader.SetMat4(gl, "MVP", MVP);

				// Draw object
				m.Render(gl, depthPassShader, false);
			}
		}

		// TODO:: refactor this function too with the shadow mapping rewrite, could possibly use virtual 
		// shadow maps to switch VAO and have one draw call per mesh, but render to multiple parts of the 
		// texture.
		public void DrawPointLightShadow(GL gl, Shader pointLightShader, int index, uint cubeMapTarget)
		{
			// Current light
			PointLight light = PointLights[index];
			light.DepthMapTextureID = cubeMapTarget;
			// Shader setup
			pointLightShader.Use(gl);
			pointLightShader.SetVec3(gl, "lightPos", light.Position);
			pointLightShader.SetFloat(gl, "far_plane", light.ZFar);

			// Matrix setup
			for (int face = 0; face < 6; ++face)
			{
				Matrix4x4 lightMatrix = light.LookAtPerFace[face] * light.ShadowProjectionMat; // KERM
				pointLightShader.SetMat4(gl, "shadowMatrices[" + face + ']', lightMatrix);
			}

			for (int i = 0; i < _models.Count; ++i)
			{
				Model m = _models[i];

				// Shader setup stuff that changes every frame
				pointLightShader.SetMat4(gl, "M", m.Matrix);

				// Draw object
				m.Render(gl, pointLightShader, false);
			}
		}

		// Currently assumes there's only one directional light, also uses the simplest shadow map algorithm
		// that leaves a lot to be desired in terms of resolution, thinking about moving to cascaded shadow maps
		// or maybe variance idk yet.
		public void DrawDirLightShadows(GL gl, Shader dirLightShader, uint targetTextureID)
		{
			DirLight.DepthMapTextureID = targetTextureID;

			float left = DirLight.OrthoBoxSize;
			float right = -left;
			float top = left;
			float bottom = -top;
			DirLight.ShadowProjectionMat = MyUtils.GLM_OrthoRH_NO(left, right, bottom, top, DirLight.ZNear, DirLight.ZFar);
			DirLight.LightView = MyUtils.GLM_LookAtRH(-100f * DirLight.Direction, Vector3.Zero, Vector3.UnitY);

			DirLight.LightSpaceMatrix = DirLight.LightView * DirLight.ShadowProjectionMat; // KERM

			// Drawing every object into the shadow buffer
			for (int i = 0; i < _models.Count; ++i)
			{
				Model m = _models[i];

				// Matrix setup
				Matrix4x4 ModelLS = m.Matrix * DirLight.LightSpaceMatrix; // KERM

				// Shader setup stuff that changes every frame
				dirLightShader.Use(gl);
				dirLightShader.SetMat4(gl, "lightSpaceMatrix", ModelLS);

				// Draw object
				m.Render(gl, dirLightShader, false);
			}
		}

		// Sets up the common uniforms for each model and loaded all texture units. A lot of driver calls here
		// Re-watch the beyond porting talk to try to reduce api calls. Specifically texture related calls.
		public void DrawFullScene(GL gl, Shader mainSceneShader, Shader skyboxShader)
		{
			// Matrix Setup
			ref Matrix4x4 vm = ref Cam.ViewMatrix;
			Matrix4x4 skyBoxVM = vm;
			skyBoxVM.M41 = 0f;
			skyBoxVM.M42 = 0f;
			skyBoxVM.M43 = 0f;
			Matrix4x4 VPCubeMap = skyBoxVM * Cam.ProjectionMatrix; // KERM
			Matrix4x4 VP = vm * Cam.ProjectionMatrix; // KERM

			// Just to avoid magic constants
			const int NUM_TEXTURES = 5;

			// Setting colors in the gui
			if (ImGui.CollapsingHeader("Directional Light Settings"))
			{
				ImGui.TextColored(Vector4.One, "Directional light Settings");
				ImGui.ColorEdit3("Color", ref DirLight.Color);
				ImGui.SliderFloat("Strength", ref DirLight.Strength, 0.1f, 200f);
				ImGui.SliderFloat("BoxSize", ref DirLight.OrthoBoxSize, 0.1f, 500f);
				ImGui.SliderFloat3("Direction", ref DirLight.Direction, -5f, 5f);
			}

			if (ImGui.CollapsingHeader("Cluster Debugging Light Settings"))
			{
				ImGui.Checkbox("Display depth Slices", ref _slices);
			}
			mainSceneShader.Use(gl);
			mainSceneShader.SetVec3(gl, "dirLight.direction", DirLight.Direction);
			mainSceneShader.SetBool(gl, "slices", _slices);
			mainSceneShader.SetVec3(gl, "dirLight.color", DirLight.Strength * DirLight.Color);
			mainSceneShader.SetMat4(gl, "lightSpaceMatrix", DirLight.LightSpaceMatrix);
			mainSceneShader.SetVec3(gl, "cameraPos_wS", Cam.Position);
			mainSceneShader.SetFloat(gl, "zFar", Cam.FarPlane);
			mainSceneShader.SetFloat(gl, "zNear", Cam.NearPlane);

			for (int i = 0; i < PointLights.Length; ++i)
			{
				PointLight light = PointLights[i];

				gl.ActiveTexture(TextureUnit.Texture0 + NUM_TEXTURES + i);
				mainSceneShader.SetInt(gl, "depthMaps[" + i + ']', NUM_TEXTURES + i);
				gl.BindTexture(TextureTarget.TextureCubeMap, light.DepthMapTextureID);
				mainSceneShader.SetFloat(gl, "far_plane", light.ZFar);
			}

			// Setting directional shadow depth map textures
			gl.ActiveTexture(TextureUnit.Texture0 + NUM_TEXTURES + PointLights.Length);
			mainSceneShader.SetInt(gl, "shadowMap", NUM_TEXTURES + PointLights.Length);
			gl.BindTexture(TextureTarget.Texture2D, DirLight.DepthMapTextureID);

			// TODO:: Formalize this a bit more
			// Setting environment map texture
			gl.ActiveTexture(TextureUnit.Texture0 + NUM_TEXTURES + PointLights.Length + 1);
			mainSceneShader.SetInt(gl, "irradianceMap", NUM_TEXTURES + PointLights.Length + 1);
			gl.BindTexture(TextureTarget.TextureCubeMap, IrradianceMap.Id);

			// Setting environment map texture for specular
			gl.ActiveTexture(TextureUnit.Texture0 + NUM_TEXTURES + PointLights.Length + 2);
			mainSceneShader.SetInt(gl, "prefilterMap", NUM_TEXTURES + PointLights.Length + 2);
			gl.BindTexture(TextureTarget.TextureCubeMap, SpecFilteredMap.Id);

			// Setting lookup table
			gl.ActiveTexture(TextureUnit.Texture0 + NUM_TEXTURES + PointLights.Length + 3);
			mainSceneShader.SetInt(gl, "brdfLUT", NUM_TEXTURES + PointLights.Length + 3);
			gl.BindTexture(TextureTarget.Texture2D, BRDF_LUT_Texture.Id);

			for (int i = 0; i < _visibleModels.Count; ++i)
			{
				Model m = _visibleModels[i];

				// Matrix setup
				ref Matrix4x4 M = ref m.Matrix;
				Matrix4x4 MVP = M * VP; // KERM

				// Shader setup stuff that changes every frame
				mainSceneShader.SetMat4(gl, "MVP", MVP);
				mainSceneShader.SetMat4(gl, "M", M);

				// Draw object
				m.Render(gl, mainSceneShader, true);
			}

			// Drawing skybox
			skyboxShader.Use(gl);
			skyboxShader.SetMat4(gl, "VP", VPCubeMap);
			Skybox.Render(gl);
		}

		// TODO TODO TODO TODO TODO TODO TODO
		private void FrustrumCulling()
		{
			foreach (Model model in _models)
			{
				_visibleModels.Add(model);
				// bool visible = mainCamera.checkVisibility(model->getBounds());
				// if (visible) {
				// }
			}
		}

		public void Delete(GL gl)
		{
			Instance = null;

			IrradianceMap.Delete(gl);
			SpecFilteredMap.Delete(gl);
			BRDF_LUT_Texture.Delete(gl);
			Skybox.Delete(gl);
			CubeMap.Cube.Delete(gl);
			foreach (Model m in _models)
			{
				m.Delete(gl);
			}
		}
	}
}
