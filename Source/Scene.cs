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
		// Tired of making things private, making them public as I go and we'll fix the rest later
		public CubeMap irradianceMap, specFilteredMap;
		public Texture brdfLUTTexture;
		public Skybox mainSkyBox;
		public int pointLightCount;

		public Camera mainCamera;

		private readonly string sceneID;
		private bool slices = false;

		// TODO:: unify light model so that we can have a single array pointing to a base class (or single class)
		// so that we can iterate through it
		private DirectionalLight dirLight;
		private PointLight[] pointLights;

		// Contains the models that remain after frustrum culling which is TB done
		private readonly List<Model> visibleModels;
		private readonly List<Model> modelsInScene;

		public Scene(GL gl, string sceneName)
		{
			sceneID = sceneName;

			visibleModels = new List<Model>();
			modelsInScene = new List<Model>();

			Console.WriteLine();
			Console.WriteLine("Beginning Scene load, checking scene description file:");
			// Load all cameras, models and lights and return false if it fails
			LoadContent(gl);
		}

		public void Delete(GL gl)
		{
			irradianceMap.Delete(gl);
			specFilteredMap.Delete(gl);
			brdfLUTTexture.Delete(gl);
			mainSkyBox.Delete(gl);
			CubeMap.cubeMapCube.Delete(gl);
			foreach (Model m in modelsInScene)
			{
				m.Delete(gl);
			}
		}

		// Update Order is critical for correct culling since we want to cull the objects after moving,
		// not before. That would be very dumb, who would do that...
		public void Update(uint deltaT)
		{
			visibleModels.Clear();
			mainCamera.Update(deltaT);
			// Light update could go here too
			FrustrumCulling();
		}

		// TODO:: refactor this function too with the shadow mapping rewrite, could possibly use virtual 
		// shadow maps to switch VAO and have one draw call per mesh, but render to multiple parts of the 
		// texture.
		public void DrawPointLightShadow(GL gl, Shader pointLightShader, uint index, uint cubeMapTarget)
		{
			// Current light
			PointLight light = pointLights[index];
			light.depthMapTextureID = cubeMapTarget;
			// Shader setup
			pointLightShader.Use(gl);
			pointLightShader.SetVec3(gl, "lightPos", light.position);
			pointLightShader.SetFloat(gl, "far_plane", light.zFar);

			// Matrix setup
			for (int face = 0; face < 6; ++face)
			{
				Matrix4x4 lightMatrix = light.lookAtPerFace[face] * light.shadowProjectionMat; // KERM
				pointLightShader.SetMat4(gl, "shadowMatrices[" + face + ']', lightMatrix);
			}

			for (int i = 0; i < modelsInScene.Count; ++i)
			{
				Model currentModel = modelsInScene[i];

				// Shader setup stuff that changes every frame
				pointLightShader.SetMat4(gl, "M", currentModel.modelMatrix);

				// Draw object
				currentModel.draw(gl, pointLightShader, false);
			}
		}

		// Currently assumes there's only one directional light, also uses the simplest shadow map algorithm
		// that leaves a lot to be desired in terms of resolution, thinking about moving to cascaded shadow maps
		// or maybe variance idk yet.
		public void DrawDirLightShadows(GL gl, Shader dirLightShader, uint targetTextureID)
		{
			dirLight.depthMapTextureID = targetTextureID;

			float left = dirLight.orthoBoxSize;
			float right = -left;
			float top = left;
			float bottom = -top;
			dirLight.shadowProjectionMat = Matrix4x4.CreateOrthographicOffCenter(left, right, bottom, top, dirLight.zNear, dirLight.zFar);
			dirLight.lightView = Matrix4x4.CreateLookAt(-100f * dirLight.direction, Vector3.Zero, Vector3.UnitY);

			dirLight.lightSpaceMatrix = dirLight.lightView * dirLight.shadowProjectionMat; // KERM

			// Drawing every object into the shadow buffer
			for (int i = 0; i < modelsInScene.Count; ++i)
			{
				Model currentModel = modelsInScene[i];

				// Matrix setup
				Matrix4x4 ModelLS = currentModel.modelMatrix * dirLight.lightSpaceMatrix; // KERM

				// Shader setup stuff that changes every frame
				dirLightShader.Use(gl);
				dirLightShader.SetMat4(gl, "lightSpaceMatrix", ModelLS);

				// Draw object
				currentModel.draw(gl, dirLightShader, false);
			}
		}

		// Sets up the common uniforms for each model and loaded all texture units. A lot of driver calls here
		// Re-watch the beyond porting talk to try to reduce api calls. Specifically texture related calls.
		public void DrawFullScene(GL gl, Shader mainSceneShader, Shader skyboxShader)
		{
			// Matrix Setup
			ref Matrix4x4 vm = ref mainCamera.viewMatrix;
			Matrix4x4 VP = vm * mainCamera.projectionMatrix; // KERM
			Matrix4x4 skyBoxVM = vm;
			skyBoxVM.M41 = 0f;
			skyBoxVM.M42 = 0f;
			skyBoxVM.M43 = 0f;
			Matrix4x4 VPCubeMap = skyBoxVM * mainCamera.projectionMatrix; // KERM

			// Just to avoid magic constants
			const int numTextures = 5;

			// Setting colors in the gui
			if (ImGui.CollapsingHeader("Directional Light Settings"))
			{
				ImGui.TextColored(new Vector4(1, 1, 1, 1), "Directional light Settings");
				ImGui.ColorEdit3("Color", ref dirLight.color);
				ImGui.SliderFloat("Strength", ref dirLight.strength, 0.1f, 200.0f);
				ImGui.SliderFloat("BoxSize", ref dirLight.orthoBoxSize, 0.1f, 500.0f);
				ImGui.SliderFloat3("Direction", ref dirLight.direction, -5.0f, 5.0f);
			}

			if (ImGui.CollapsingHeader("Cluster Debugging Light Settings"))
			{
				ImGui.Checkbox("Display depth Slices", ref slices);
			}
			mainSceneShader.Use(gl);
			mainSceneShader.SetVec3(gl, "dirLight.direction", dirLight.direction);
			mainSceneShader.SetBool(gl, "slices", slices);
			mainSceneShader.SetVec3(gl, "dirLight.color", dirLight.strength * dirLight.color);
			mainSceneShader.SetMat4(gl, "lightSpaceMatrix", dirLight.lightSpaceMatrix);
			mainSceneShader.SetVec3(gl, "cameraPos_wS", mainCamera.position);
			mainSceneShader.SetFloat(gl, "zFar", mainCamera.cameraFrustum.farPlane);
			mainSceneShader.SetFloat(gl, "zNear", mainCamera.cameraFrustum.nearPlane);

			for (int i = 0; i < pointLightCount; ++i)
			{
				PointLight light = pointLights[i];

				gl.ActiveTexture(TextureUnit.Texture0 + numTextures + i);
				mainSceneShader.SetInt(gl, "depthMaps[" + i + ']', numTextures + i);
				gl.BindTexture(TextureTarget.TextureCubeMap, light.depthMapTextureID);
				mainSceneShader.SetFloat(gl, "far_plane", light.zFar);
			}

			// Setting directional shadow depth map textures
			gl.ActiveTexture(TextureUnit.Texture0 + numTextures + pointLightCount);
			mainSceneShader.SetInt(gl, "shadowMap", numTextures + pointLightCount);
			gl.BindTexture(TextureTarget.Texture2D, dirLight.depthMapTextureID);

			// TODO:: Formalize this a bit more
			// Setting environment map texture
			gl.ActiveTexture(TextureUnit.Texture0 + numTextures + pointLightCount + 1);
			mainSceneShader.SetInt(gl, "irradianceMap", numTextures + pointLightCount + 1);
			gl.BindTexture(TextureTarget.TextureCubeMap, irradianceMap.textureID);

			// Setting environment map texture for specular
			gl.ActiveTexture(TextureUnit.Texture0 + numTextures + pointLightCount + 2);
			mainSceneShader.SetInt(gl, "prefilterMap", numTextures + pointLightCount + 2);
			gl.BindTexture(TextureTarget.TextureCubeMap, specFilteredMap.textureID);

			// Setting lookup table
			gl.ActiveTexture(TextureUnit.Texture0 + numTextures + pointLightCount + 3);
			mainSceneShader.SetInt(gl, "brdfLUT", numTextures + pointLightCount + 3);
			gl.BindTexture(TextureTarget.Texture2D, brdfLUTTexture.textureID);

			for (int i = 0; i < visibleModels.Count; ++i)
			{
				Model currentModel = visibleModels[i];

				// Matrix setup
				ref Matrix4x4 M = ref currentModel.modelMatrix;
				Matrix4x4 MVP = M * VP; // KERM

				// Shader setup stuff that changes every frame
				mainSceneShader.SetMat4(gl, "MVP", MVP);
				mainSceneShader.SetMat4(gl, "M", M);

				// Draw object
				currentModel.draw(gl, mainSceneShader, true);
			}

			// Drawing skybox
			skyboxShader.Use(gl);
			skyboxShader.SetMat4(gl, "VP", VPCubeMap);
			mainSkyBox.Draw(gl);
		}

		// Very simple setup that iterates through all objects and draws their depth value to a buffer
		// Optimization is very possible here, specifically because we draw all items.
		public void DrawDepthPass(GL gl, Shader depthPassShader)
		{
			depthPassShader.Use(gl);

			// Matrix Setup
			Matrix4x4 VP = mainCamera.viewMatrix * mainCamera.projectionMatrix; // KERM

			// Drawing every object into the depth buffer
			for (int i = 0; i < modelsInScene.Count; ++i)
			{
				Model currentModel = modelsInScene[i];

				// Matrix setup
				Matrix4x4 MVP = currentModel.modelMatrix * VP; // KERM

				// Shader setup stuff that changes every frame
				depthPassShader.SetMat4(gl, "MVP", MVP);

				// Draw object
				currentModel.draw(gl, depthPassShader, false);
			}
		}

		// This is definitely getting refactored out on the model loading / mesh / material system rewrite
		// This is what I thought you had to do for all classes because I had only read about OOP but now 
		// I want to give a try the more functional/ data oriented programming philosophy I have been reading
		// about and therefore simple getters like these seem very out of place.
		// -----------------------------GETTERS----------------------------------------------
		public List<Model> GetVisiblemodels()
		{
			return visibleModels;
		}

		public uint GetShadowRes()
		{
			return dirLight.shadowRes;
		}

		public PointLight GetPointLight(int index)
		{
			return pointLights[index];
		}

		// -----------------------------SCENE LOADING-----------------------------------

		// Config file parsing, gets all the important
		private void LoadContent(GL gl)
		{
			// Parsing into Json file readable format
			string path = MyUtils.ASSET_PATH + @"/Scenes/" + sceneID + ".json";
			using (FileStream s = File.OpenRead(path))
			using (var configJson = JsonDocument.Parse(s))
			{
				JsonElement root = configJson.RootElement;
				// Checking that config file belongs to current scene and is properly formatted
				if (root.GetProperty("sceneID").GetString() != sceneID || root.GetProperty("models").GetArrayLength() == 0)
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

				Console.WriteLine("Loading skybox...");
				CubeMap.cubeMapCube = new Cube();
				CubeMap.cubeMapCube.Setup(gl);
				LoadSkyBox(root.GetProperty("skybox"));

				Console.WriteLine("Loading lights...");
				LoadLights(root);

				Console.WriteLine("Generating environment maps...");
				GenerateEnvironmentMaps(gl);

				Console.WriteLine("Reticulating splines...");

				// lastly we check if the scene is empty and return
				Console.WriteLine("Loading Complete!...");
			}
			if (modelsInScene.Count == 0)
			{
				throw new Exception("No models");
			}
		}

		private void LoadLights(in JsonElement root)
		{
			// Directional light
			Console.WriteLine("Loading directional light...");
			{
				JsonElement light = root.GetProperty("directionalLight");
				dirLight = new DirectionalLight();

				JsonElement dir = light.GetProperty("direction");
				dirLight.direction = new Vector3(dir[0].GetSingle(), dir[1].GetSingle(), dir[2].GetSingle());

				JsonElement color = light.GetProperty("color");
				dirLight.color = new Vector3(color[0].GetSingle(), color[1].GetSingle(), color[2].GetSingle());

				// Scalar values
				dirLight.distance = light.GetProperty("distance").GetSingle();
				dirLight.strength = light.GetProperty("strength").GetSingle();
				dirLight.zNear = light.GetProperty("zNear").GetSingle();
				dirLight.zFar = light.GetProperty("zFar").GetSingle();
				dirLight.orthoBoxSize = light.GetProperty("orthoSize").GetSingle();
				dirLight.shadowRes = light.GetProperty("shadowRes").GetUInt32();

				// Matrix values
				float left = dirLight.orthoBoxSize;
				float right = -left;
				float top  = left;
				float bottom = -top;
				// I'm not sure yet why we have to multiply by the distance here, I understand that if I don't much of the
				// screen won't be shown, but I am confused as this goes against my understanding of how an orthographic 
				// projection works. This will have to be reviewed at a later point.
				dirLight.shadowProjectionMat = Matrix4x4.CreateOrthographicOffCenter(left, right, bottom, top, dirLight.zNear, dirLight.zFar);
				dirLight.lightView = Matrix4x4.CreateLookAt(dirLight.distance * -dirLight.direction, Vector3.Zero, Vector3.UnitY);

				dirLight.lightSpaceMatrix = dirLight.lightView * dirLight.shadowProjectionMat; // KERM
			}
			// Point lights
			Console.WriteLine("Loading point light...");
			{
				// Get number of lights in scene and initialize array containing them
				JsonElement pl = root.GetProperty("pointLights");
				pointLightCount = pl.GetArrayLength();
				pointLights = new PointLight[pointLightCount];

				for (int i = 0; i < pointLightCount; ++i)
				{
					JsonElement light = pl[i];
					pointLights[i] = new PointLight();

					JsonElement pos = light.GetProperty("position");
					pointLights[i].position = new Vector3(pos[0].GetSingle(), pos[1].GetSingle(), pos[2].GetSingle());

					JsonElement color = light.GetProperty("color");
					pointLights[i].color = new Vector3(color[0].GetSingle(), color[1].GetSingle(), color[2].GetSingle());

					// Scalar values
					pointLights[i].strength = light.GetProperty("strength").GetSingle();
					pointLights[i].zNear = light.GetProperty("zNear").GetSingle();
					pointLights[i].zFar = light.GetProperty("zFar").GetSingle();
					pointLights[i].shadowRes = light.GetProperty("shadowRes").GetUInt32();

					// Matrix setup
					pointLights[i].shadowProjectionMat = Matrix4x4.CreatePerspectiveFieldOfView(90f * MyUtils.DEG_TO_RAD, 1f, pointLights[i].zNear, pointLights[i].zFar);

					Vector3 lightPos = pointLights[i].position;
					pointLights[i].lookAtPerFace[0] = Matrix4x4.CreateLookAt(lightPos, lightPos + new Vector3(1f, 0f, 0f), new Vector3(0f, -1f, 0f));
					pointLights[i].lookAtPerFace[1] = Matrix4x4.CreateLookAt(lightPos, lightPos + new Vector3(-1f, 0f, 0f), new Vector3(0f, -1f, 0f));
					pointLights[i].lookAtPerFace[2] = Matrix4x4.CreateLookAt(lightPos, lightPos + new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, 1f));
					pointLights[i].lookAtPerFace[3] = Matrix4x4.CreateLookAt(lightPos, lightPos + new Vector3(0f, -1f, 0f), new Vector3(0f, 0f, -1f));
					pointLights[i].lookAtPerFace[4] = Matrix4x4.CreateLookAt(lightPos, lightPos + new Vector3(0f, 0f, 1f), new Vector3(0f, -1f, 0f));
					pointLights[i].lookAtPerFace[5] = Matrix4x4.CreateLookAt(lightPos, lightPos + new Vector3(0f, 0f, -1f), new Vector3(0f, -1f, 0f));
				}
			}
		}

		private void LoadSkyBox(in JsonElement skyBox)
		{
			string skyBoxName = skyBox.GetProperty("id").GetString();
			bool isHDR = skyBox.GetProperty("hdr").GetBoolean();
			uint resolution = skyBox.GetProperty("resolution").GetUInt32();

			mainSkyBox = new Skybox(DisplayManager.Instance.OpenGL, skyBoxName, isHDR, resolution);
		}

		// TODO:: rewrite during the material system update
		private void LoadSceneModels(in JsonElement models)
		{
			// model setup
			TransformParameters initParameters;
			int modelCount = models.GetArrayLength();

			for (int i = 0; i < modelCount; ++i)
			{
				// get model mesh and material info
				JsonElement currentModel = models[i];
				string modelMesh = currentModel.GetProperty("mesh").GetString();
				bool IBL = currentModel.GetProperty("IBL").GetBoolean();

				string modelName = modelMesh.Substring(0, modelMesh.LastIndexOf('.'));

				// position
				JsonElement pos = currentModel.GetProperty("position");
				initParameters.translation = new Vector3(pos[0].GetSingle(), pos[1].GetSingle(), pos[2].GetSingle());

				// rotation
				JsonElement rot = currentModel.GetProperty("rotation");
				initParameters.angle = rot[0].GetSingle() * MyUtils.DEG_TO_RAD;
				initParameters.rotationAxis = new Vector3(rot[1].GetSingle(), rot[2].GetSingle(), rot[3].GetSingle());

				// scaling
				JsonElement scaling = currentModel.GetProperty("scaling");
				initParameters.scaling = new Vector3(scaling[0].GetSingle(), scaling[1].GetSingle(), scaling[2].GetSingle());

				// attempts to load model with the initparameters it has read
				modelMesh = MyUtils.ASSET_PATH + @"/Models/" + modelName + "/" + modelMesh;
				modelsInScene.Add(new Model(DisplayManager.Instance.OpenGL, DisplayManager.Instance.Assimp, modelMesh, initParameters, IBL));
			}
		}

		// TODO move the fixed size somewhere else
		private unsafe void GenerateEnvironmentMaps(GL gl)
		{
			// Diffuse map
			irradianceMap = new CubeMap();
			irradianceMap.width = 32;
			irradianceMap.height = 32;
			irradianceMap.GenerateCubeMap(gl, irradianceMap.width, irradianceMap.height, CubeMapType.HDR_MAP);

			// Specular map
			specFilteredMap = new CubeMap();
			specFilteredMap.width = 128;
			specFilteredMap.height = 128;
			specFilteredMap.GenerateCubeMap(gl, specFilteredMap.width, specFilteredMap.height, CubeMapType.PREFILTER_MAP);

			// Setting up texture ahead of time
			const uint res = 512;
			brdfLUTTexture = new Texture();
			brdfLUTTexture.height = res;
			brdfLUTTexture.width = res;
			brdfLUTTexture.textureID = gl.GenTexture();
			gl.BindTexture(TextureTarget.Texture2D, brdfLUTTexture.textureID);
			gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.RG16f, res, res, 0, PixelFormat.RG, PixelType.Float, null);
			gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
			gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
			gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
			gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
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

			mainCamera = new Camera(tar, pos, fov, speed, sens, nearP, farP);
		}

		// TODO TODO TODO TODO TODO TODO TODO
		private void FrustrumCulling()
		{
			foreach (Model model in modelsInScene)
			{
				visibleModels.Add(model);
				// bool visible = mainCamera.checkVisibility(model->getBounds());
				// if (visible) {
				// }
			}
		}
	}
}
