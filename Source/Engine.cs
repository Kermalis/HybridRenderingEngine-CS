using Silk.NET.OpenGL;
using Silk.NET.SDL;
using System;

namespace HybridRenderingEngine
{
	internal static class Engine
	{
		public static void Init()
		{
			int start = Environment.TickCount;

			// Start up of all SDL and opengl Display related content
			_ = new DisplayManager();
			GL gl = DisplayManager.Instance.OpenGL;

			// Load default scene
			_ = new Scene(gl, "sponza");

			// Initializes rendererer manager, which is in charge of high level
			// rendering tasks (render queue, locating render scene etc)
			// It gets passed references to the other major subsystems for use later
			_ = new RenderManager(gl, Scene.Instance);

			// Want to keep track of how much time the whole loading process took
			int deltaT = Environment.TickCount - start;
			Console.WriteLine("(Load time: {0}ms)", deltaT);
		}

		// Runs main application loop
		public static void Run()
		{
			// Early exit or other shared data flags
			bool done = false;

			// Iteration and time keeping counters
			Sdl sdl = DisplayManager.Instance.SDL;
			int count = 0;
			uint deltaT = sdl.GetTicks();
			uint total = 0;
			Console.WriteLine();
			Console.WriteLine("Entered Main Loop!");

			while (!done)
			{
				++count;
				uint start = sdl.GetTicks();

				// Handle all user input
				// Any changes to the scene are directly sent to the respective objects in
				// the scene class. Also sets exit flag based on user input.
				Scene s = Scene.Instance;
				InputManager.ProcessInput(s.Cam, ref done, deltaT);

				// Update all models, camera and lighting in the current scene
				s.Update(deltaT);

				RenderManager.Instance.Render(DisplayManager.Instance.OpenGL, s, start);

				// Obtaining deltaT for any
				deltaT = sdl.GetTicks() - start;
				total += deltaT;
			}

			// Average performance over the whole loop (excludes load time costs)
			Console.WriteLine();
			Console.WriteLine("Performance Stats:");
			Console.WriteLine("------------------");
			Console.WriteLine("Average frame time over {0} frames: {1}ms.", count, total / (float)count);
			Console.WriteLine();
			Console.WriteLine();
			Console.WriteLine("Closing down engine...");
		}

		public static void Quit()
		{
			GL gl = DisplayManager.Instance.OpenGL;
			RenderManager.Instance.Quit(gl);

			Scene.Instance.Delete(gl);

			DisplayManager.Instance.Quit();
		}
	}
}
