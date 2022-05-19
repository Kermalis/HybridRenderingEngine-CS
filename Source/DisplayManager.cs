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

		private const bool VSYNC = false;

		public static DisplayManager Instance;

		public Sdl SDL;
		public GL OpenGL;
		public Assimp Assimp;

		private Window* _window;
		private void* _context;

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

		private void StartSDL()
		{
			SDL = Sdl.GetApi();
			if (SDL.Init(Sdl.InitVideo) != 0)
			{
				throw new Exception("Failed to initialize SDL. Error: " + SDL.GetErrorS());
			}
		}

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

			// No point in having a depth buffer if you're using the default buffer just for post processing
			// SDL.GLSetAttribute(GLattr.GLDepthSize, 24);

			// Also set the default buffer to be sRGB
			SDL.GLSetAttribute(GLattr.GLAlphaSize, 8);
			SDL.GLSetAttribute(GLattr.GLFramebufferSrgbCapable, 1);
		}

		private void CreateWindow()
		{
			_window = SDL.CreateWindow("Hybrid Renderering Engine", Sdl.WindowposUndefined, Sdl.WindowposUndefined,
				SCREEN_WIDTH, SCREEN_HEIGHT, (uint)WindowFlags.WindowOpengl);
			if (_window is null)
			{
				throw new Exception("Could not create window. Error: " + SDL.GetErrorS());
			}
		}

		private void CreateGLContext()
		{
			_context = SDL.GLCreateContext(_window);
			if (_context is null)
			{
				throw new Exception("Could not create OpenGL context. Error: " + SDL.GetErrorS());
			}

			OpenGL = GL.GetApi((proc) => (nint)SDL.GLGetProcAddress(proc));

			// Printing some vendor Information
			Console.WriteLine("Vendor:   " + OpenGL.GetStringS(StringName.Vendor));
			Console.WriteLine("Renderer: " + OpenGL.GetStringS(StringName.Renderer));
			Console.WriteLine("Version:  " + OpenGL.GetStringS(StringName.Version));

			// Init GL context settings
			SDL.GLSetSwapInterval(VSYNC ? 1 : 0);
			// Important for blend to remain disabled
			OpenGL.Enable(EnableCap.CullFace);
			OpenGL.Enable(EnableCap.Multisample);
			OpenGL.Enable(EnableCap.TextureCubeMapSeamless);
			// Since the only framebuffer that's created with an sRGB internalformat
			// is the default one, this will only affect the default framebuffer
			// The default one is created as sRGB by SDL with the GLSetAttribute() above
			OpenGL.Enable(EnableCap.FramebufferSrgb);

			int w, h;
			SDL.GetWindowSize(_window, &w, &h);
			OpenGL.Viewport(0, 0, (uint)w, (uint)h);
		}

		private void CreateImGuiContext()
		{
			IntPtr ctx = ImGui.CreateContext();
			if (ctx == IntPtr.Zero)
			{
				throw new Exception("Could not load IMGUI context!");
			}

			// Init and configure for OpenGL and SDL
			ImGui_ImplSDL2.Init(_window, null);
			ImGui_ImplOpenGL3.Init();

			// Imgui first frame setup
			ImGui.StyleColorsDark();
			ImGui_ImplOpenGL3.NewFrame();
			ImGui_ImplSDL2.NewFrame();
			ImGui.NewFrame();
		}

		// Swaps the finished drawn buffer with the window buffer.
		// Also initializes a new frame for the gui renderer.
		public void SwapDisplayBuffer()
		{
			// Now that all data has been added to the frame we overlay the GUI onto it
			// Just before frame swap
			ImGui.EndFrame();
			ImGui.Render();
			ImGui_ImplOpenGL3.RenderDrawData(ImGui.GetDrawData());

			// Actual buffer swap
			SDL.GLSwapWindow(_window);

			// Signaling beginning of frame to gui
			ImGui_ImplOpenGL3.NewFrame();
			ImGui_ImplSDL2.NewFrame();
			ImGui.NewFrame();
		}

		// Closes down all contexts and subsystems in the reverse initialization order
		public void Quit()
		{
			ImGui.EndFrame();
			ImGui_ImplOpenGL3.Shutdown();
			ImGui_ImplSDL2.Shutdown();
			ImGui.DestroyContext();

			SDL.GLDeleteContext(_context);
			SDL.DestroyWindow(_window);
			SDL.Quit();

			Assimp.Dispose();
		}
	}
}
