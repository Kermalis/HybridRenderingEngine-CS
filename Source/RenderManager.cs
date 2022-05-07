using ImGuiNET;
using Silk.NET.OpenGL;
using System;
using System.Numerics;

namespace HybridRenderingEngine
{
	internal sealed class RenderManager
	{
		public static RenderManager Instance;

		// Todo:: shaders should belong to a material not the rendermanager
		private Shader depthPrePassShader, PBRClusteredShader, skyboxShader,
			highPassFilterShader, gaussianBlurShader, screenSpaceShader,
			dirShadowShader, pointShadowShader, fillCubeMapShader,
			convolveCubeMap, preFilterSpecShader, integrateBRDFShader;

		// TODO::Compute shaders don't have a strong a case as regular shaders to be made a part of 
		// other classes, since they feel more like static functions of the renderer than methods that
		// are a part of certain objects. 
		private ComputeShader buildAABBGridCompShader, cullLightsCompShader;

		// The canvas is an abstraction for screen space rendering. It helped me build a mental model
		// of drawing at the time but I think it is now unecessary since I feel much more comfortable with
		// compute shaders and the inner workings of the GPU.
		private Quad canvas;

		// The variables that determine the size of the cluster grid. They're hand picked for now, but
		// there is some space for optimization and tinkering as seen on the Olsson paper and the ID tech6
		// presentation.
		private const uint gridSizeX = 16;
		private const uint gridSizeY = 9;
		private const uint gridSizeZ = 24;
		private const uint numClusters = gridSizeX * gridSizeY * gridSizeZ;
		private uint sizeX;

		private int numLights;
		private const uint maxLights = 1000; // pretty overkill for sponza, but ok for testing
		private const uint maxLightsPerTile = 50;

		// Shader buffer objects, currently completely managed by the rendermanager class for creation
		// using and uploading to the gpu, but they should be moved somwehre else to avoid bloat
		private uint AABBvolumeGridSSBO, screenToViewSSBO;
		private uint lightSSBO, lightIndexListSSBO, lightGridSSBO, lightIndexGlobalCountSSBO;

		// Render pipeline FrameBuffer objects. I absolutely hate that the pointlight shadows have distinct
		// FBO's instead of one big one. I think we will take the approach that is outlined on the Id tech 6 talk
		// and use a giant texture to store all textures. However, since this require a pretty substantial rewrite
		// of the illumination code I have delayed this until after the first official github release of the
		// project.
		private ResolveBuffer simpleFBO;
		private CaptureBuffer captureFBO;
		private QuadHDRBuffer pingPongFBO;
		private DirShadowBuffer dirShadowFBO;
		private FrameBufferMultiSampled multiSampledFBO;
		private PointShadowBuffer[] pointLightShadowFBOs;

		// Sets the internal pointers to the screen and the current scene and inits the software renderer instance.
		public RenderManager(GL gl, Scene currentScene)
		{
			Instance = this;

			Console.WriteLine();
			Console.WriteLine("Initializing Renderer.");

			Console.WriteLine("Loading FBO's...");
			InitFBOs(gl, currentScene);

			Console.WriteLine("Loading Shaders...");
			LoadShaders(gl);

			Console.WriteLine("Loading SSBO's...");
			InitSSBOs(gl, currentScene);

			Console.WriteLine("Preprocessing...");
			PreProcess(gl, currentScene);

			Console.WriteLine("Renderer Initialization complete.");
		}

		public void Quit(GL gl)
		{
			canvas.Delete(gl);
			simpleFBO.Delete(gl);
			captureFBO.Delete(gl);
			pingPongFBO.Delete(gl);
			dirShadowFBO.Delete(gl);
			multiSampledFBO.Delete(gl);
			foreach (PointShadowBuffer fbo in pointLightShadowFBOs)
			{
				fbo.Delete(gl);
			}

			buildAABBGridCompShader.Delete(gl);
			cullLightsCompShader.Delete(gl);
			fillCubeMapShader.Delete(gl);
			convolveCubeMap.Delete(gl);
			preFilterSpecShader.Delete(gl);
			integrateBRDFShader.Delete(gl);
			depthPrePassShader.Delete(gl);
			PBRClusteredShader.Delete(gl);
			skyboxShader.Delete(gl);
			screenSpaceShader.Delete(gl);
			dirShadowShader.Delete(gl);
			pointShadowShader.Delete(gl);
			highPassFilterShader.Delete(gl);
			gaussianBlurShader.Delete(gl);
		}

		private void PreProcess(GL gl, Scene currentScene)
		{
			// Initializing the surface that we use to draw screen-space effects
			canvas = new Quad();
			canvas.Setup(gl);

			// Building the grid of AABB enclosing the view frustum clusters
			buildAABBGridCompShader.Use(gl);
			buildAABBGridCompShader.SetFloat(gl, "zNear", currentScene.mainCamera.cameraFrustum.nearPlane);
			buildAABBGridCompShader.SetFloat(gl, "zFar", currentScene.mainCamera.cameraFrustum.farPlane);
			ComputeShader.Dispatch(gl, gridSizeX, gridSizeY, gridSizeZ);

			// Environment Mapping
			// Passing equirectangular map to cubemap
			captureFBO.bind(gl);
			currentScene.mainSkyBox.FillCubeMapWithTexture(gl, fillCubeMapShader);

			// Cubemap convolution TODO:: This could probably be moved to a function of the scene or environment maps
			// themselves as a class / static function
			uint res = currentScene.irradianceMap.width;
			captureFBO.resizeFrameBuffer(gl, res);
			uint environmentID = currentScene.mainSkyBox.skyBoxCubeMap.textureID;
			currentScene.irradianceMap.ConvolveCubeMap(gl, environmentID, convolveCubeMap);

			// Cubemap prefiltering TODO:: Same as above
			uint captureRBO = captureFBO.depthBuffer;
			currentScene.specFilteredMap.PreFilterCubeMap(gl, environmentID, captureRBO, preFilterSpecShader);

			// BRDF lookup texture
			integrateBRDFShader.Use(gl);
			res = currentScene.brdfLUTTexture.height;
			captureFBO.resizeFrameBuffer(gl, res);
			uint id = currentScene.brdfLUTTexture.textureID;
			gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, id, 0);
			gl.Viewport(0, 0, res, res);
			gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
			canvas.Draw(gl);

			// Making sure that the viewport is the correct size after rendering
			gl.Viewport(0, 0, DisplayManager.SCREEN_WIDTH, DisplayManager.SCREEN_HEIGHT);

			gl.Enable(EnableCap.DepthTest);
			gl.DepthMask(true);

			// Populating depth cube maps for the point light shadows
			for (uint i = 0; i < currentScene.pointLightCount; ++i)
			{
				pointLightShadowFBOs[i].bind(gl);
				pointLightShadowFBOs[i].clear(gl, ClearBufferMask.DepthBufferBit, Vector3.One);
				currentScene.DrawPointLightShadow(gl, pointShadowShader, i, pointLightShadowFBOs[i].depthBuffer);
			}

			// Directional shadows
			dirShadowFBO.bind(gl);
			dirShadowFBO.clear(gl, ClearBufferMask.DepthBufferBit, Vector3.One);
			currentScene.DrawDirLightShadows(gl, dirShadowShader, dirShadowFBO.depthBuffer);
		}

		// TODO:: some of the buffer generation and binding should be abstracted into a function
		private unsafe void InitSSBOs(GL gl, Scene currentScene)
		{
			// Setting up tile size on both X and Y 
			sizeX = (uint)MathF.Ceiling(DisplayManager.SCREEN_WIDTH / (float)gridSizeX);

			float zFar = currentScene.mainCamera.cameraFrustum.farPlane;
			float zNear = currentScene.mainCamera.cameraFrustum.nearPlane;

			// Buffer containing all the clusters
			// 2 Vec4s per cluster
			{
				AABBvolumeGridSSBO = gl.GenBuffer();
				gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, AABBvolumeGridSSBO);

				// We generate the buffer but don't populate it yet.
				gl.BufferData(BufferTargetARB.ShaderStorageBuffer, numClusters * 8 * sizeof(float), null, BufferUsageARB.StaticCopy);
				gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 1, AABBvolumeGridSSBO);
				gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, 0);
			}

			// Setting up screen2View ssbo
			{
				screenToViewSSBO = gl.GenBuffer();
				gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, screenToViewSSBO);

				// Setting up contents of buffer
				ScreenToView screen2View;
				Matrix4x4.Invert(currentScene.mainCamera.projectionMatrix, out screen2View.inverseProjectionMat);
				screen2View.tileSizes[0] = gridSizeX;
				screen2View.tileSizes[1] = gridSizeY;
				screen2View.tileSizes[2] = gridSizeZ;
				screen2View.tileSizes[3] = sizeX;
				screen2View.screenWidth = DisplayManager.SCREEN_WIDTH;
				screen2View.screenHeight = DisplayManager.SCREEN_HEIGHT;
				// Basically reduced a log function into a simple multiplication an addition by pre-calculating these
				screen2View.sliceScalingFactor = gridSizeZ / MathF.Log2(zFar / zNear);
				screen2View.sliceBiasFactor = -(gridSizeZ * MathF.Log2(zNear) / MathF.Log2(zFar / zNear));

				// Generating and copying data to memory in GPU
				gl.BufferData(BufferTargetARB.ShaderStorageBuffer, (uint)sizeof(ScreenToView), &screen2View, BufferUsageARB.StaticCopy);
				gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 2, screenToViewSSBO);
				gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, 0);
			}

			// Setting up lights buffer that contains all the lights in the scene
			{
				lightSSBO = gl.GenBuffer();
				gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, lightSSBO);
				gl.BufferData(BufferTargetARB.ShaderStorageBuffer, maxLights * (uint)sizeof(GPULight), null, BufferUsageARB.DynamicDraw);

				var lights = (GPULight*)gl.MapBuffer(BufferTargetARB.ShaderStorageBuffer, BufferAccessARB.ReadWrite);
				for (int i = 0; i < numLights; ++i)
				{
					// Fetching the light from the current scene
					PointLight light = currentScene.GetPointLight(i);
					lights[i].position = new Vector4(light.position, 1f);
					lights[i].color = new Vector4(light.color, 1f);
					lights[i].enabled = 1;
					lights[i].intensity = 1f;
					lights[i].range = 65f;
				}
				gl.UnmapBuffer(BufferTargetARB.ShaderStorageBuffer);
				gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 3, lightSSBO);
				gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, 0);
			}

			// A list of indices to the lights that are active and intersect with a cluster
			{
				uint totalNumLights = numClusters * maxLightsPerTile; // 50 lights per tile max
				lightIndexListSSBO = gl.GenBuffer();
				gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, lightIndexListSSBO);

				// We generate the buffer but don't populate it yet
				gl.BufferData(BufferTargetARB.ShaderStorageBuffer, totalNumLights * sizeof(uint), null, BufferUsageARB.StaticCopy);
				gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 4, lightIndexListSSBO);
				gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, 0);
			}

			// Every tile takes two unsigned ints one to represent the number of lights in that grid
			// Another to represent the offset to the light index list from where to begin reading light indexes from
			// This implementation is straight up from Olsson paper
			{
				lightGridSSBO = gl.GenBuffer();
				gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, lightGridSSBO);

				gl.BufferData(BufferTargetARB.ShaderStorageBuffer, numClusters * 2 * sizeof(uint), null, BufferUsageARB.StaticCopy);
				gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 5, lightGridSSBO);
				gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, 0);
			}

			// Setting up simplest ssbo in the world
			{
				lightIndexGlobalCountSSBO = gl.GenBuffer();
				gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, lightIndexGlobalCountSSBO);

				// Every tile takes two unsigned ints one to represent the number of lights in that grid
				// Another to represent the offset 
				gl.BufferData(BufferTargetARB.ShaderStorageBuffer, sizeof(uint), null, BufferUsageARB.StaticCopy);
				gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 6, lightIndexGlobalCountSSBO);
				gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, 0);
			}
		}

		private void LoadShaders(GL gl)
		{
			// Pre-processing
			buildAABBGridCompShader = new ComputeShader(gl, "clusterShader.comp");
			cullLightsCompShader = new ComputeShader(gl, "clusterCullLightShader.comp");
			fillCubeMapShader = new Shader(gl, "cubeMapShader.vert", "buildCubeMapShader.frag");
			convolveCubeMap = new Shader(gl, "cubeMapShader.vert", "convolveCubemapShader.frag");
			preFilterSpecShader = new Shader(gl, "cubeMapShader.vert", "preFilteringShader.frag");
			integrateBRDFShader = new Shader(gl, "screenShader.vert", "brdfIntegralShader.frag");

			// Rendering
			depthPrePassShader = new Shader(gl, "depthPassShader.vert", "depthPassShader.frag");
			PBRClusteredShader = new Shader(gl, "PBRClusteredShader.vert", "PBRClusteredShader.frag");
			skyboxShader = new Shader(gl, "skyboxShader.vert", "skyboxShader.frag");
			screenSpaceShader = new Shader(gl, "screenShader.vert", "screenShader.frag");

			// Shadow mapping
			dirShadowShader = new Shader(gl, "shadowShader.vert", "shadowShader.frag");
			pointShadowShader = new Shader(gl, "pointShadowShader.vert", "pointShadowShader.frag", "pointShadowShader.geom");

			// Post-processing
			highPassFilterShader = new Shader(gl, "splitHighShader.vert", "splitHighShader.frag");
			gaussianBlurShader = new Shader(gl, "blurShader.vert", "blurShader.frag");
		}

		private void InitFBOs(GL gl, Scene currentScene)
		{
			// Init variables
			uint shadowMapResolution = currentScene.GetShadowRes();
			uint skyboxRes = currentScene.mainSkyBox.resolution;
			numLights = currentScene.pointLightCount;

			// Shadow Framebuffers
			pointLightShadowFBOs = new PointShadowBuffer[numLights];

			// Directional light
			dirShadowFBO = new DirShadowBuffer(gl, shadowMapResolution, shadowMapResolution);

			// Point light
			for (int i = 0; i < numLights; ++i)
			{
				pointLightShadowFBOs[i] = new PointShadowBuffer(gl, shadowMapResolution, shadowMapResolution);
			}

			// Rendering buffers
			multiSampledFBO = new FrameBufferMultiSampled(gl);
			captureFBO = new CaptureBuffer(gl, skyboxRes, skyboxRes);

			// Post processing buffers
			pingPongFBO = new QuadHDRBuffer(gl);
			simpleFBO = new ResolveBuffer(gl);
		}

		/* This time using volume tiled forward
		 * Algorithm steps:
		 * // Initialization or view frustrum change
		 * 0. Determine AABB's for each volume 
		 * // Update Every frame
		 * 1. Depth-pre pass :: DONE
		 * 2. Mark Active tiles :: POSTPONED AS OPTIMIZATION
		 * 3. Build Tile list ::  POSTPONED AS OPTIMIZATION
		 * 4. Assign lights to tiles :: DONE (BUT SHOULD BE OPTIMIZED)
		 * 5. Shading by reading from the active tiles list :: DONE
		 * 6. Post processing and screen space effects :: DONE
		*/
		public void Render(GL gl, Scene currentScene, uint start)
		{
			// Initiating rendering gui
			ImGui.Begin("Rendering Controls");
			float fps = ImGui.GetIO().Framerate;
			ImGui.Text(string.Format("Application average {0:F3} ms/frame ({1:F1} FPS)", 1000f / fps, fps));

			if (ImGui.CollapsingHeader("Controls"))
			{
				ImGui.Text("Strafe: w a s d");
				ImGui.Text("Rotate Camera: hold left click + mouse");
				ImGui.Text("Up&Down: q e");
				ImGui.Text("Reset Camera: r");
				ImGui.Text("Exit: ESC");
				ImGui.InputFloat3("Camera Pos", ref currentScene.mainCamera.position); // Camera controls
				ImGui.SliderFloat("Movement speed", ref currentScene.mainCamera.camSpeed, 0.005f, 1f);
			}
			// Making sure depth testing is enabled 
			gl.Enable(EnableCap.DepthTest);
			gl.DepthMask(true);

			// Directional shadows
			dirShadowFBO.bind(gl);
			dirShadowFBO.clear(gl, ClearBufferMask.DepthBufferBit, Vector3.One);
			currentScene.DrawDirLightShadows(gl, dirShadowShader, dirShadowFBO.depthBuffer);

			// 1.1- Multisampled Depth pre-pass
			multiSampledFBO.bind(gl);
			multiSampledFBO.clear(gl, ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit, Vector3.Zero);
			currentScene.DrawDepthPass(gl, depthPrePassShader);

			// 4-Light assignment
			cullLightsCompShader.Use(gl);
			cullLightsCompShader.SetMat4(gl, "viewMatrix", currentScene.mainCamera.viewMatrix);
			ComputeShader.Dispatch(gl, 1, 1, 6);

			// 5 - Actual shading;
			// 5.1 - Forward render the scene in the multisampled FBO using the z buffer to discard early
			gl.DepthFunc(DepthFunction.Lequal);
			gl.DepthMask(false);
			currentScene.DrawFullScene(gl, PBRClusteredShader, skyboxShader);

			// 5.2 - resolve the from multisampled to normal resolution for postProcessing
			multiSampledFBO.blitTo(gl, simpleFBO, ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

			// 6 - postprocessing, includes bloom, exposure mapping
			PostProcess(gl, currentScene.mainCamera, start);

			// Rendering gui scope ends here cannot be done later because the whole frame
			// is reset in the display buffer swap
			ImGui.End();

			// Drawing to the screen by swapping the window's surface with the
			// final buffer containing all rendering information
			DisplayManager.Instance.SwapDisplayBuffer();
		}

		private void PostProcess(GL gl, Camera sceneCamera, uint start)
		{
			if (ImGui.CollapsingHeader("Post-processing"))
			{
				ImGui.SliderInt("Blur", ref sceneCamera.blurAmount, 0, 10);
				ImGui.SliderFloat("Exposure", ref sceneCamera.exposure, 0.1f, 5.0f);
			}

			// TODO:: should be a compute shader
			pingPongFBO.bind(gl);
			pingPongFBO.clear(gl, ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit, Vector3.Zero);
			if (sceneCamera.blurAmount > 0)
			{
				// Filtering pixel rgb values > 1.0
				highPassFilterShader.Use(gl);
				canvas.Draw(gl, simpleFBO.texColorBuffer);
			}

			// Applying Gaussian blur in ping pong fashion
			// TODO:: Also make it a compute shader
			gaussianBlurShader.Use(gl);
			for (int i = 0; i < sceneCamera.blurAmount; ++i)
			{
				// Horizontal pass
				gl.BindFramebuffer(FramebufferTarget.Framebuffer, simpleFBO.frameBufferID);
				gl.DrawBuffer(DrawBufferMode.ColorAttachment1);
				gaussianBlurShader.SetBool(gl, "horizontal", true);
				canvas.Draw(gl, pingPongFBO.texColorBuffer);

				// Vertical pass
				gl.BindFramebuffer(FramebufferTarget.Framebuffer, pingPongFBO.frameBufferID);
				gaussianBlurShader.SetBool(gl, "horizontal", false);
				canvas.Draw(gl, simpleFBO.blurHighEnd);
			}
			// Setting back to default framebuffer (screen) and clearing
			// No need for depth testing cause we're drawing to a flat quad
			DisplayManager.Instance.Bind();

			// Shader setup for postprocessing
			screenSpaceShader.Use(gl);

			screenSpaceShader.SetInt(gl, "offset", (int)start);
			screenSpaceShader.SetFloat(gl, "exposure", sceneCamera.exposure);
			screenSpaceShader.SetInt(gl, "screenTexture", 0);
			screenSpaceShader.SetInt(gl, "bloomBlur", 1);
			screenSpaceShader.SetInt(gl, "computeTexture", 2);

			// Merging the blurred high pass image with the low pass values
			// Also tonemapping and doing other post processing
			canvas.Draw(gl, simpleFBO.texColorBuffer, pingPongFBO.texColorBuffer);
		}
	}
}
