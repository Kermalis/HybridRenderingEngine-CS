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
		private Shader _depthPrePassShader, _pbrClusteredShader, _skyboxShader,
			_highPassFilterShader, _gaussianBlurShader, _screenSpaceShader,
			_dirShadowShader, _pointShadowShader, _fillCubeMapShader,
			_convolveCubeMap, _preFilterSpecShader, _integrateBRDFShader;

		// TODO::Compute shaders don't have a strong a case as regular shaders to be made a part of 
		// other classes, since they feel more like static functions of the renderer than methods that
		// are a part of certain objects. 
		private ComputeShader _buildAABBGridCompShader, _cullLightsCompShader;

		// The canvas is an abstraction for screen space rendering. It helped me build a mental model
		// of drawing at the time but I think it is now unecessary since I feel much more comfortable with
		// compute shaders and the inner workings of the GPU.
		private Quad _canvas;

		// The variables that determine the size of the cluster grid. They're hand picked for now, but
		// there is some space for optimization and tinkering as seen on the Olsson paper and the ID tech6
		// presentation.
		private const uint GRID_SIZE_X = 16;
		private const uint GRID_SIZE_Y = 9;
		private const uint GRID_SIZE_Z = 24;
		private const uint NUM_CLUSTERS = GRID_SIZE_X * GRID_SIZE_Y * GRID_SIZE_Z;

		private const uint MAX_LIGHTS = 1000; // Pretty overkill for sponza, but ok for testing
		private const uint MAX_LIGHTS_PER_TILE = 50;

		// Shader buffer objects, currently completely managed by the rendermanager class for creation
		// using and uploading to the gpu, but they should be moved somwehre else to avoid bloat
		private uint _aabbVolumeGridSSBO, _screenToViewSSBO;
		private uint _lightSSBO, _lightIndexListSSBO, _lightGridSSBO, _lightIndexGlobalCountSSBO;

		// Render pipeline FrameBuffer objects. I absolutely hate that the pointlight shadows have distinct
		// FBO's instead of one big one. I think we will take the approach that is outlined on the Id tech 6 talk
		// and use a giant texture to store all textures. However, since this require a pretty substantial rewrite
		// of the illumination code I have delayed this until after the first official github release of the
		// project.
		private ResolveBuffer _simpleFBO;
		private CaptureBuffer _captureFBO;
		private QuadHDRBuffer _pingPongFBO;
		private DirShadowBuffer _dirShadowFBO;
		private FrameBufferMultiSampled _multiSampledFBO;
		private PointShadowBuffer[] _pointLightShadowFBOs;

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

			Console.WriteLine("Pre-processing...");
			PreProcess(gl, currentScene);

			Console.WriteLine("Renderer Initialization complete.");
		}

		private void InitFBOs(GL gl, Scene currentScene)
		{
			// Init variables
			uint shadowMapResolution = currentScene.DirLight.ShadowRes;
			uint skyboxRes = currentScene.Skybox.Res;

			// Shadow Framebuffers
			_pointLightShadowFBOs = new PointShadowBuffer[currentScene.PointLights.Length];

			// Directional light
			_dirShadowFBO = new DirShadowBuffer(gl, shadowMapResolution, shadowMapResolution);

			// Point light
			for (int i = 0; i < _pointLightShadowFBOs.Length; ++i)
			{
				shadowMapResolution = currentScene.PointLights[i].ShadowRes;
				_pointLightShadowFBOs[i] = new PointShadowBuffer(gl, shadowMapResolution, shadowMapResolution);
			}

			// Rendering buffers
			_multiSampledFBO = new FrameBufferMultiSampled(gl);
			_captureFBO = new CaptureBuffer(gl, skyboxRes, skyboxRes);

			// Post processing buffers
			_pingPongFBO = new QuadHDRBuffer(gl);
			_simpleFBO = new ResolveBuffer(gl);
		}

		private void LoadShaders(GL gl)
		{
			// Pre-processing
			_buildAABBGridCompShader = new ComputeShader(gl, "ClusterShader.comp.glsl");
			_cullLightsCompShader = new ComputeShader(gl, "ClusterCullLightShader.comp.glsl");
			_fillCubeMapShader = new Shader(gl, "CubeMapShader.vert.glsl", "BuildCubeMapShader.frag.glsl");
			_convolveCubeMap = new Shader(gl, "CubeMapShader.vert.glsl", "ConvolveCubemapShader.frag.glsl");
			_preFilterSpecShader = new Shader(gl, "CubeMapShader.vert.glsl", "PreFilteringShader.frag.glsl");
			_integrateBRDFShader = new Shader(gl, "ScreenShader.vert.glsl", "BRDFIntegralShader.frag.glsl");

			// Rendering
			_depthPrePassShader = new Shader(gl, "DepthPassShader.vert.glsl", "DepthPassShader.frag.glsl");
			_pbrClusteredShader = new Shader(gl, "PBRClusteredShader.vert.glsl", "PBRClusteredShader.frag.glsl");
			_skyboxShader = new Shader(gl, "SkyboxShader.vert.glsl", "SkyboxShader.frag.glsl");
			_screenSpaceShader = new Shader(gl, "ScreenShader.vert.glsl", "ScreenShader.frag.glsl");

			// Shadow mapping
			_dirShadowShader = new Shader(gl, "ShadowShader.vert.glsl", "ShadowShader.frag.glsl");
			_pointShadowShader = new Shader(gl, "PointShadowShader.vert.glsl", "PointShadowShader.frag.glsl", "PointShadowShader.geom.glsl");

			// Post-processing
			_highPassFilterShader = new Shader(gl, "SplitHighShader.vert.glsl", "SplitHighShader.frag.glsl");
			_gaussianBlurShader = new Shader(gl, "BlurShader.vert.glsl", "BlurShader.frag.glsl");
		}

		// TODO:: some of the buffer generation and binding should be abstracted into a function
		private unsafe void InitSSBOs(GL gl, Scene currentScene)
		{
			float zNear = currentScene.Cam.NearPlane;
			float zFar = currentScene.Cam.FarPlane;

			// Buffer containing all the clusters
			// 2 Vec4s per cluster
			{
				_aabbVolumeGridSSBO = gl.GenBuffer();
				gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _aabbVolumeGridSSBO);

				// We generate the buffer but don't populate it yet.
				gl.BufferData(BufferTargetARB.ShaderStorageBuffer, NUM_CLUSTERS * 8 * sizeof(float), null, BufferUsageARB.StaticCopy);
				gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 1, _aabbVolumeGridSSBO);
				gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, 0);
			}

			// Setting up screen2View ssbo
			{
				_screenToViewSSBO = gl.GenBuffer();
				gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _screenToViewSSBO);

				// Setting up contents of buffer
				ScreenToView screen2View;
				Matrix4x4.Invert(currentScene.Cam.ProjectionMatrix, out screen2View.InverseProjectionMat);
				screen2View.TileSizeX = GRID_SIZE_X;
				screen2View.TileSizeY = GRID_SIZE_Y;
				screen2View.TileSizeZ = GRID_SIZE_Z;
				screen2View.TileSizePixels.X = 1f / MathF.Ceiling(DisplayManager.SCREEN_WIDTH / (float)GRID_SIZE_X);
				screen2View.TileSizePixels.Y = 1f / MathF.Ceiling(DisplayManager.SCREEN_HEIGHT / (float)GRID_SIZE_Y);
				screen2View.ViewPixelSize = new Vector2(1f / DisplayManager.SCREEN_WIDTH, 1f / DisplayManager.SCREEN_HEIGHT);
				// Basically reduced a log function into a simple multiplication an addition by pre-calculating these
				screen2View.SliceScalingFactor = GRID_SIZE_Z / MathF.Log2(zFar / zNear);
				screen2View.SliceBiasFactor = -(GRID_SIZE_Z * MathF.Log2(zNear) / MathF.Log2(zFar / zNear));

				// Generating and copying data to memory in GPU
				gl.BufferData(BufferTargetARB.ShaderStorageBuffer, (uint)sizeof(ScreenToView), &screen2View, BufferUsageARB.StaticCopy);
				gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 2, _screenToViewSSBO);
				gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, 0);
			}

			// Setting up lights buffer that contains all the lights in the scene
			{
				_lightSSBO = gl.GenBuffer();
				gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _lightSSBO);
				gl.BufferData(BufferTargetARB.ShaderStorageBuffer, MAX_LIGHTS * (uint)sizeof(GPULight), null, BufferUsageARB.DynamicDraw);

				var lights = (GPULight*)gl.MapBuffer(BufferTargetARB.ShaderStorageBuffer, BufferAccessARB.ReadWrite);
				for (int i = 0; i < _pointLightShadowFBOs.Length; ++i)
				{
					// Fetching the light from the current scene
					PointLight light = currentScene.PointLights[i];
					lights[i].Position = new Vector4(light.Position, 1f);
					lights[i].Color = new Vector4(light.Color, 1f);
					lights[i].IsEnabled = 1;
					lights[i].Intensity = 1f;
					lights[i].Range = 65f;
				}
				gl.UnmapBuffer(BufferTargetARB.ShaderStorageBuffer);
				gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 3, _lightSSBO);
				gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, 0);
			}

			// A list of indices to the lights that are active and intersect with a cluster
			{
				const uint TOTAL_NUM_LIGHTS = NUM_CLUSTERS * MAX_LIGHTS_PER_TILE; // 50 lights per tile max
				_lightIndexListSSBO = gl.GenBuffer();
				gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _lightIndexListSSBO);

				// We generate the buffer but don't populate it yet
				gl.BufferData(BufferTargetARB.ShaderStorageBuffer, TOTAL_NUM_LIGHTS * sizeof(uint), null, BufferUsageARB.StaticCopy);
				gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 4, _lightIndexListSSBO);
				gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, 0);
			}

			// Every tile takes two unsigned ints one to represent the number of lights in that grid
			// Another to represent the offset to the light index list from where to begin reading light indexes from
			// This implementation is straight up from Olsson paper
			{
				_lightGridSSBO = gl.GenBuffer();
				gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _lightGridSSBO);

				gl.BufferData(BufferTargetARB.ShaderStorageBuffer, NUM_CLUSTERS * 2 * sizeof(uint), null, BufferUsageARB.StaticCopy);
				gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 5, _lightGridSSBO);
				gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, 0);
			}

			// Setting up simplest ssbo in the world
			{
				_lightIndexGlobalCountSSBO = gl.GenBuffer();
				gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _lightIndexGlobalCountSSBO);

				gl.BufferData(BufferTargetARB.ShaderStorageBuffer, sizeof(uint), null, BufferUsageARB.StaticCopy);
				gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 6, _lightIndexGlobalCountSSBO);
				gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, 0);
			}
		}

		private void PreProcess(GL gl, Scene currentScene)
		{
			gl.Disable(EnableCap.Blend);

			// Initializing the surface that we use to draw screen-space effects
			_canvas = new Quad(gl);

			// Building the grid of AABB enclosing the view frustum clusters
			_buildAABBGridCompShader.Use(gl);
			_buildAABBGridCompShader.SetFloat(gl, "zNear", currentScene.Cam.NearPlane);
			_buildAABBGridCompShader.SetFloat(gl, "zFar", currentScene.Cam.FarPlane);
			ComputeShader.Dispatch(gl, GRID_SIZE_X, GRID_SIZE_Y, GRID_SIZE_Z);

			// Environment Mapping
			// Passing equirectangular map to cubemap
			_captureFBO.Bind(gl);
			currentScene.Skybox.FillCubeMapWithTexture(gl, _fillCubeMapShader);

			// Cubemap convolution TODO:: This could probably be moved to a function of the scene or environment maps
			// themselves as a class / static function
			uint res = currentScene.IrradianceMap.Width;
			_captureFBO.Resize(gl, res);
			uint environmentID = currentScene.Skybox.SkyboxCubeMap.Id;
			currentScene.IrradianceMap.ConvolveCubeMap(gl, environmentID, _convolveCubeMap);

			// Cubemap prefiltering TODO:: Same as above
			uint captureRBO = _captureFBO.Depth;
			currentScene.SpecFilteredMap.PreFilterCubeMap(gl, environmentID, captureRBO, _preFilterSpecShader);

			// BRDF lookup texture
			_integrateBRDFShader.Use(gl);
			res = currentScene.BRDF_LUT_Texture.Height;
			_captureFBO.Resize(gl, res);
			uint id = currentScene.BRDF_LUT_Texture.Id;
			gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, id, 0);
			gl.Viewport(0, 0, res, res);
			gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
			_canvas.Render(gl);

			// Making sure that the viewport is the correct size after rendering
			gl.Viewport(0, 0, DisplayManager.SCREEN_WIDTH, DisplayManager.SCREEN_HEIGHT);

			gl.Enable(EnableCap.DepthTest);
			gl.DepthMask(true);

			// Populating depth cube maps for the point light shadows
			for (int i = 0; i < _pointLightShadowFBOs.Length; ++i)
			{
				_pointLightShadowFBOs[i].Bind(gl);
				FrameBuffer.Clear(gl, ClearBufferMask.DepthBufferBit, Vector3.One);
				currentScene.DrawPointLightShadow(gl, _pointShadowShader, i, _pointLightShadowFBOs[i].Depth);
			}

			// Directional shadows
			_dirShadowFBO.Bind(gl);
			FrameBuffer.Clear(gl, ClearBufferMask.DepthBufferBit, Vector3.One);
			currentScene.DrawDirLightShadows(gl, _dirShadowShader, _dirShadowFBO.Depth);
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
		public void Render(GL gl, Scene currentScene)
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
				ImGui.InputFloat3("Camera Pos", ref currentScene.Cam.Position); // Camera controls
				ImGui.SliderFloat("Movement speed", ref currentScene.Cam.CamSpeed, 0.005f, 1f);
			}

			gl.Disable(EnableCap.Blend);
			gl.Enable(EnableCap.DepthTest);
			gl.DepthMask(true);

			// Directional shadows
			_dirShadowFBO.Bind(gl);
			FrameBuffer.Clear(gl, ClearBufferMask.DepthBufferBit, Vector3.One);
			currentScene.DrawDirLightShadows(gl, _dirShadowShader, _dirShadowFBO.Depth);

			// 1.1- Multisampled Depth pre-pass
			_multiSampledFBO.Bind(gl);
			FrameBuffer.Clear(gl, ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit, Vector3.Zero);
			currentScene.DrawDepthPass(gl, _depthPrePassShader);

			// 4-Light assignment
			_cullLightsCompShader.Use(gl);
			_cullLightsCompShader.SetMat4(gl, "viewMatrix", currentScene.Cam.ViewMatrix);
			ComputeShader.Dispatch(gl, 1, 1, 6);

			// 5 - Actual shading;
			// 5.1 - Forward render the scene in the multisampled FBO using the z buffer to discard early
			gl.DepthFunc(DepthFunction.Lequal);
			gl.DepthMask(false);
			currentScene.DrawFullScene(gl, _pbrClusteredShader, _skyboxShader);

			// 5.2 - resolve the from multisampled to normal resolution for postProcessing
			_multiSampledFBO.BlitTo(gl, _simpleFBO, ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

			// 6 - postprocessing, includes bloom, exposure mapping
			PostProcess(gl, currentScene.Cam);

			// Rendering gui scope ends here cannot be done later because the whole frame
			// is reset in the display buffer swap
			ImGui.End();

			// Drawing to the screen by swapping the window's surface with the
			// final buffer containing all rendering information
			DisplayManager.Instance.SwapDisplayBuffer();
		}

		private void PostProcess(GL gl, Camera sceneCamera)
		{
			if (ImGui.CollapsingHeader("Post-processing"))
			{
				ImGui.SliderInt("Blur", ref sceneCamera.BlurAmount, 0, 10);
				ImGui.SliderFloat("Exposure", ref sceneCamera.Exposure, 0.1f, 5f);
			}

			// TODO:: should be a compute shader
			_pingPongFBO.Bind(gl);
			FrameBuffer.Clear(gl, ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit, Vector3.Zero);
			if (sceneCamera.BlurAmount > 0)
			{
				// Filtering pixel rgb values > 1.0
				_highPassFilterShader.Use(gl);
				_canvas.Render(gl, _simpleFBO.Color);
			}

			// Applying Gaussian blur in ping pong fashion
			// TODO:: Also make it a compute shader
			_gaussianBlurShader.Use(gl);
			for (int i = 0; i < sceneCamera.BlurAmount; ++i)
			{
				// Horizontal pass
				gl.BindFramebuffer(FramebufferTarget.Framebuffer, _simpleFBO.Id);
				gl.DrawBuffer(DrawBufferMode.ColorAttachment1);
				_gaussianBlurShader.SetBool(gl, "horizontal", true);
				_canvas.Render(gl, _pingPongFBO.Color);

				// Vertical pass
				gl.BindFramebuffer(FramebufferTarget.Framebuffer, _pingPongFBO.Id);
				gl.DrawBuffer(DrawBufferMode.ColorAttachment0);
				_gaussianBlurShader.SetBool(gl, "horizontal", false);
				_canvas.Render(gl, _simpleFBO.BlurHighEnd);
			}
			// Setting back to default framebuffer (screen) and clearing
			// No need for depth testing cause we're drawing to a flat quad
			gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
			gl.ClearColor(0f, 0f, 0f, 1f);
			gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

			// Shader setup for postprocessing
			_screenSpaceShader.Use(gl);

			_screenSpaceShader.SetFloat(gl, "exposure", sceneCamera.Exposure);
			_screenSpaceShader.SetInt(gl, "screenTexture", 0);
			_screenSpaceShader.SetInt(gl, "bloomBlur", 1);
			_screenSpaceShader.SetInt(gl, "computeTexture", 2);

			// Merging the blurred high pass image with the low pass values
			// Also tonemapping and doing other post processing
			_canvas.Render(gl, _simpleFBO.Color, _pingPongFBO.Color);
		}

		public void Quit(GL gl)
		{
			_canvas.Delete(gl);

			gl.DeleteBuffer(_aabbVolumeGridSSBO);
			gl.DeleteBuffer(_screenToViewSSBO);
			gl.DeleteBuffer(_lightSSBO);
			gl.DeleteBuffer(_lightIndexListSSBO);
			gl.DeleteBuffer(_lightGridSSBO);
			gl.DeleteBuffer(_lightIndexGlobalCountSSBO);

			_simpleFBO.Delete(gl);
			_captureFBO.Delete(gl);
			_pingPongFBO.Delete(gl);
			_dirShadowFBO.Delete(gl);
			_multiSampledFBO.Delete(gl);
			foreach (PointShadowBuffer fbo in _pointLightShadowFBOs)
			{
				fbo.Delete(gl);
			}

			_buildAABBGridCompShader.Delete(gl);
			_cullLightsCompShader.Delete(gl);
			_fillCubeMapShader.Delete(gl);
			_convolveCubeMap.Delete(gl);
			_preFilterSpecShader.Delete(gl);
			_integrateBRDFShader.Delete(gl);
			_depthPrePassShader.Delete(gl);
			_pbrClusteredShader.Delete(gl);
			_skyboxShader.Delete(gl);
			_screenSpaceShader.Delete(gl);
			_dirShadowShader.Delete(gl);
			_pointShadowShader.Delete(gl);
			_highPassFilterShader.Delete(gl);
			_gaussianBlurShader.Delete(gl);
		}
	}
}
