using HybridRenderingEngine.Utils;
using ImGuiNET;
using Silk.NET.Assimp;
using Silk.NET.OpenGL;
using Silk.NET.SDL;
using System;

namespace HybridRenderingEngine
{
	internal sealed unsafe class DisplayManager
	{
		public const int SCREEN_WIDTH = 1920;
		public const int SCREEN_HEIGHT = 1080;
		public const float SCREEN_ASPECT_RATIO = SCREEN_WIDTH / (float)SCREEN_HEIGHT;

		public static DisplayManager Instance;

		public Sdl SDL;
		public GL OpenGL;
		public Assimp Assimp;

		private Window* mWindow;
		private void* mContext;

		// Initialization sequence of all rendering related libraries such as SDL, Silk.NET, and ImGUI.
		public DisplayManager()
		{
			Instance = this;

			Assimp = Assimp.GetApi();
			StartSDL();
			StartOpenGL();
			CreateWindow();
			CreateGLContext();
			CreateImGuiContext();
		}

		// Closes down all contexts and subsystems in the reverse initialization order
		public void Quit()
		{
			ImGui.EndFrame();
			ImGui_ImplOpenGL3.Shutdown();
			ImGui_ImplSDL2.Shutdown();
			ImGui.DestroyContext();

			SDL.GLDeleteContext(mContext);

			SDL.DestroyWindow(mWindow);

			SDL.Quit();

			Assimp.Dispose();
		}

		// Swaps the finished drawn buffer with the window bufffer.
		// Also initializes a new frame for the gui renderer.
		public void SwapDisplayBuffer()
		{
			// Now that all data has been added to the frame we overlay the GUI onto it
			// Just before frame swap
			ImGui.EndFrame();
			ImGui.Render();
			ImGui_ImplOpenGL3.RenderDrawData(ImGui.GetDrawData());

			// Actual buffer swap
			SDL.GLSwapWindow(mWindow);

			// Signaling beginning of frame to gui
			ImGui_ImplOpenGL3.NewFrame();
			ImGui_ImplSDL2.NewFrame();
			ImGui.NewFrame();
		}

		// Binding the display framebuffer for drawing and clearing it before rendering
		public void Bind()
		{
			OpenGL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
			OpenGL.ClearColor(0f, 0f, 0f, 1f);
			OpenGL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
		}

		// Entry point to SDL
		private void StartSDL()
		{
			SDL = Sdl.GetApi();
			if (SDL.Init(Sdl.InitVideo) != 0)
			{
				throw new Exception("Failed to initialize SDL. Error: " + SDL.GetErrorS());
			}
		}

		// Loads the openGL library and sets the attributes of the future OpenGL context 
		private void StartOpenGL()
		{
			if (SDL.GLLoadLibrary((byte*)null) != 0)
			{
				throw new Exception("Failed to initialize OpenGL. Error: " + SDL.GetErrorS());
			}

			// Request an OpenGL 4.3 context (should be core)
			SDL.GLSetAttribute(GLattr.GLAcceleratedVisual, 1);
			SDL.GLSetAttribute(GLattr.GLContextMajorVersion, 4);
			SDL.GLSetAttribute(GLattr.GLContextMinorVersion, 3);

			// No point in having a deplth buffer if you're using the default 
			// buffer only for post processing
			// SDL.GLSetAttribute(SDL_GL_DEPTH_SIZE, 24);

			// Also set the default buffer to be sRGB 
			SDL.GLSetAttribute(GLattr.GLAlphaSize, 8);
			SDL.GLSetAttribute(GLattr.GLFramebufferSrgbCapable, 1);
		}

		// Initializes SDL2 window object with the given width and height and marks it as an openGL enabled window;
		private void CreateWindow()
		{
			mWindow = SDL.CreateWindow("Hybrid Renderering Engine", Sdl.WindowposUndefined, Sdl.WindowposUndefined,
				SCREEN_WIDTH, SCREEN_HEIGHT, (uint)WindowFlags.WindowOpengl);
			if (mWindow is null)
			{
				throw new Exception("Could not create window. Error: " + SDL.GetErrorS());
			}
		}

		/*
		1. Creates the OpenGL context using SDL
		2. Inits Silk.NET functionality
		3. Prints some vendor information
		4. Sets some more GL context stuff
		5. Sets the glViewport to be the same size as the SDL window
		*/
		private void CreateGLContext()
		{
			mContext = SDL.GLCreateContext(mWindow);
			if (mContext is null)
			{
				throw new Exception("Could not create OpenGL context. Error: " + SDL.GetErrorS());
			}

			OpenGL = GL.GetApi((proc) => (nint)SDL.GLGetProcAddress(proc));

			// Printing some vendor Information
			Console.WriteLine("Vendor:   " + OpenGL.GetStringS(StringName.Vendor));
			Console.WriteLine("Renderer: " + OpenGL.GetStringS(StringName.Renderer));
			Console.WriteLine("Version:  " + OpenGL.GetStringS(StringName.Version));

			// Init GL context settings
			SDL.GLSetSwapInterval(0); // No VSync
			OpenGL.Enable(EnableCap.CullFace);
			OpenGL.Enable(EnableCap.Multisample);
			OpenGL.Enable(EnableCap.FramebufferSrgb);
			OpenGL.Enable(EnableCap.TextureCubeMapSeamless);

			// Setting the glViewport to be the size of the SDL window
			int w, h;
			SDL.GetWindowSize(mWindow, &w, &h);
			OpenGL.Viewport(0, 0, (uint)w, (uint)h);
		}

		// Inits our GUI library and calls all init functions related to configuring it for use
		// in OpenGL3+ and SDL2
		private void CreateImGuiContext()
		{
			IntPtr mGuiContext = ImGui.CreateContext();
			if (mGuiContext == IntPtr.Zero)
			{
				throw new Exception("Could not load IMGUI context!");
			}

			// Init and configure for OpenGL and SDL
			ImGui_ImplSDL2.Init(mWindow, null);
			ImGui_ImplOpenGL3.Init();

			// Imgui first frame setup
			ImGui.StyleColorsDark();
			ImGui_ImplOpenGL3.NewFrame();
			ImGui_ImplSDL2.NewFrame();
			ImGui.NewFrame();
		}
	}
}
