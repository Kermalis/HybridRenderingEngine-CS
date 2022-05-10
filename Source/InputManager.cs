using HybridRenderingEngine.Utils;
using ImGuiNET;
using Silk.NET.SDL;

namespace HybridRenderingEngine
{
	internal static class InputManager
	{
		/*
		 * While there are any remaining events on the SDL event stack
		 * 1. Check if one of those events is a quit
		 * 2. Check if the GUI wants to use the keyboard and mouse
		 * 3. Handle keyboard events ourselves
		 * 4. Handle mouse events directly 
		*/
		public static void ProcessInput(Sdl sdl, Camera sceneCamera, ref bool done)
		{
			Event ev = default;
			ImGuiIOPtr io = ImGui.GetIO();

			while (sdl.PollEvent(ref ev) != 0)
			{
				// First check if user requests an exit
				if (ev.Type == (uint)EventType.Quit)
				{
					done = true;
					return;
				}

				// Next, check if imGUI wants to use the mouse or keyboard
				if (io.WantCaptureKeyboard || io.WantCaptureMouse)
				{
					// Stops all camera movement if you are interacting with the GUI
					sceneCamera.ActiveMoveStates.Clear();
					ImGui_ImplSDL2.ProcessEvent(ref ev);
				}
				// Handle any other relevant input data
				// Keypresses, mouse etc
				else
				{
					HandleEvent(sceneCamera, ref ev, ref done);
				}
			}
		}

		// Maybe a candidate to break apart into smaller functions
		private static void HandleEvent(Camera sceneCamera, ref Event ev, ref bool done)
		{
			// For keyboard input we want to avoid repeated movement when the key is held
			// Instead of actually updating the camera position for each key call we update a 
			// container that keeps track of which move state the camera is in. This state is only
			// changed on keydown or key up events, freeing it from the keyboard polling rate dependency.
			bool isDown = ev.Type == (uint)EventType.Keydown;
			bool wasDown = ev.Type == (uint)EventType.Keyup;

			if (isDown || wasDown)
			{
				switch ((KeyCode)ev.Key.Keysym.Sym)
				{
					case KeyCode.KEscape:
					{
						if (isDown)
						{
							done = true;
						}
						return;
					}
					case KeyCode.KR:
					{
						if (isDown)
						{
							sceneCamera.ResetCamera();
						}
						break;
					}
					case KeyCode.KW:
					{
						if (isDown)
						{
							sceneCamera.ActiveMoveStates.Add('w');
						}
						else if (wasDown)
						{
							sceneCamera.ActiveMoveStates.Remove('w');
						}
						break;
					}
					case KeyCode.KS:
					{
						if (isDown)
						{
							sceneCamera.ActiveMoveStates.Add('s');
						}
						else if (wasDown)
						{
							sceneCamera.ActiveMoveStates.Remove('s');
						}
						break;
					}
					case KeyCode.KA:
					{
						if (isDown)
						{
							sceneCamera.ActiveMoveStates.Add('a');
						}
						else if (wasDown)
						{
							sceneCamera.ActiveMoveStates.Remove('a');
						}
						break;
					}
					case KeyCode.KD:
					{
						if (isDown)
						{
							sceneCamera.ActiveMoveStates.Add('d');
						}
						else if (wasDown)
						{
							sceneCamera.ActiveMoveStates.Remove('d');
						}
						break;
					}
					case KeyCode.KQ:
					{
						if (isDown)
						{
							sceneCamera.ActiveMoveStates.Add('q');
						}
						else if (wasDown)
						{
							sceneCamera.ActiveMoveStates.Remove('q');
						}
						break;
					}
					case KeyCode.KE:
					{
						if (isDown)
						{
							sceneCamera.ActiveMoveStates.Add('e');
						}
						else if (wasDown)
						{
							sceneCamera.ActiveMoveStates.Remove('e');
						}
						break;
					}
				}
			}
			// Handling Mouse Motion
			else if (ev.Type == (uint)EventType.Mousemotion)
			{
				// Only move camera if the left button is pressed
				if ((ev.Motion.State & MyUtils.SDL_BUTTON_LMASK) != 0)
				{
					// While left button is pressed change mouse to relative mode
					DisplayManager.Instance.SDL.SetRelativeMouseMode(SdlBool.True);

					float sens = sceneCamera.MouseSens;
					float xOffset = ev.Motion.Xrel * sens;
					float yOffset = -ev.Motion.Yrel * sens;

					// To reduce precision issues we keep the yaw constrained to 360 degrees
					sceneCamera.Yaw = (sceneCamera.Yaw + xOffset) % 360f;
					sceneCamera.Pitch += yOffset;

					// Limiting the range of the pitch to avoid flips
					if (sceneCamera.Pitch > 89f)
					{
						sceneCamera.Pitch = 89f;
					}
					else if (sceneCamera.Pitch < -89f)
					{
						sceneCamera.Pitch = -89f;
					}
				}
				else
				{
					// Once the left button is not pressed set the mouse to normal mode
					DisplayManager.Instance.SDL.SetRelativeMouseMode(SdlBool.False);
				}
			}
		}
	}
}