using ImGuiNET;
using Silk.NET.SDL;
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace HybridRenderingEngine.Utils
{
	// https://github.com/ocornut/imgui/blob/master/backends/imgui_impl_sdl.cpp
	// Last copied on 4/26/2022, Did not copy non-GL stuff or anything non-win/linux
	internal static unsafe class ImGui_ImplSDL2
	{
		private delegate IntPtr GetClipboardTextFn(void* user_data);
		private delegate void SetClipboardTextFn(void* user_data, string text);

		private sealed class Data
		{
			public ulong Frequency;
			public ulong Time;
			public Window* Window;
			public Renderer* Renderer;
			public int MouseButtonsDown;
			public Cursor*[] MouseCursors = new Cursor*[(int)ImGuiMouseCursor.COUNT];
			public int PendingMouseLeaveFrame;
			public byte* ClipboardTextData;
			public bool MouseCanUseGlobalState;
			public GetClipboardTextFn GetClipboardTextFn;
			public SetClipboardTextFn SetClipboardTextFn;
		};

		private static Data _bd;

		private static IntPtr GetClipboardText(void* user_data)
		{
			Sdl sdl = DisplayManager.Instance.SDL;
			if (_bd.ClipboardTextData is not null)
			{
				sdl.Free(_bd.ClipboardTextData);
			}
			_bd.ClipboardTextData = sdl.GetClipboardText();
			return new IntPtr(_bd.ClipboardTextData);
		}

		private static void SetClipboardText(void* user_data, string text)
		{
			DisplayManager.Instance.SDL.SetClipboardText(text);
		}

		private static ImGuiKey KeycodeToImGuiKey(KeyCode keycode)
		{
			switch (keycode)
			{
				case KeyCode.KTab: return ImGuiKey.Tab;
				case KeyCode.KLeft: return ImGuiKey.LeftArrow;
				case KeyCode.KRight: return ImGuiKey.RightArrow;
				case KeyCode.KUp: return ImGuiKey.UpArrow;
				case KeyCode.KDown: return ImGuiKey.DownArrow;
				case KeyCode.KPageup: return ImGuiKey.PageUp;
				case KeyCode.KPagedown: return ImGuiKey.PageDown;
				case KeyCode.KHome: return ImGuiKey.Home;
				case KeyCode.KEnd: return ImGuiKey.End;
				case KeyCode.KInsert: return ImGuiKey.Insert;
				case KeyCode.KDelete: return ImGuiKey.Delete;
				case KeyCode.KBackspace: return ImGuiKey.Backspace;
				case KeyCode.KSpace: return ImGuiKey.Space;
				case KeyCode.KReturn: return ImGuiKey.Enter;
				case KeyCode.KEscape: return ImGuiKey.Escape;
				case KeyCode.KQuote: return ImGuiKey.Apostrophe;
				case KeyCode.KComma: return ImGuiKey.Comma;
				case KeyCode.KMinus: return ImGuiKey.Minus;
				case KeyCode.KPeriod: return ImGuiKey.Period;
				case KeyCode.KSlash: return ImGuiKey.Slash;
				case KeyCode.KSemicolon: return ImGuiKey.Semicolon;
				case KeyCode.KEquals: return ImGuiKey.Equal;
				case KeyCode.KLeftbracket: return ImGuiKey.LeftBracket;
				case KeyCode.KBackslash: return ImGuiKey.Backslash;
				case KeyCode.KRightbracket: return ImGuiKey.RightBracket;
				case KeyCode.KBackquote: return ImGuiKey.GraveAccent;
				case KeyCode.KCapslock: return ImGuiKey.CapsLock;
				case KeyCode.KScrolllock: return ImGuiKey.ScrollLock;
				case KeyCode.KNumlockclear: return ImGuiKey.NumLock;
				case KeyCode.KPrintscreen: return ImGuiKey.PrintScreen;
				case KeyCode.KPause: return ImGuiKey.Pause;
				case KeyCode.KKP0: return ImGuiKey.Keypad0;
				case KeyCode.KKP1: return ImGuiKey.Keypad1;
				case KeyCode.KKP2: return ImGuiKey.Keypad2;
				case KeyCode.KKP3: return ImGuiKey.Keypad3;
				case KeyCode.KKP4: return ImGuiKey.Keypad4;
				case KeyCode.KKP5: return ImGuiKey.Keypad5;
				case KeyCode.KKP6: return ImGuiKey.Keypad6;
				case KeyCode.KKP7: return ImGuiKey.Keypad7;
				case KeyCode.KKP8: return ImGuiKey.Keypad8;
				case KeyCode.KKP9: return ImGuiKey.Keypad9;
				case KeyCode.KKPPeriod: return ImGuiKey.KeypadDecimal;
				case KeyCode.KKPDivide: return ImGuiKey.KeypadDivide;
				case KeyCode.KKPMultiply: return ImGuiKey.KeypadMultiply;
				case KeyCode.KKPMinus: return ImGuiKey.KeypadSubtract;
				case KeyCode.KKPPlus: return ImGuiKey.KeypadAdd;
				case KeyCode.KKPEnter: return ImGuiKey.KeypadEnter;
				case KeyCode.KKPEquals: return ImGuiKey.KeypadEqual;
				case KeyCode.KLctrl: return ImGuiKey.LeftCtrl;
				case KeyCode.KLshift: return ImGuiKey.LeftShift;
				case KeyCode.KLalt: return ImGuiKey.LeftAlt;
				case KeyCode.KLgui: return ImGuiKey.LeftSuper;
				case KeyCode.KRctrl: return ImGuiKey.RightCtrl;
				case KeyCode.KRshift: return ImGuiKey.RightShift;
				case KeyCode.KRalt: return ImGuiKey.RightAlt;
				case KeyCode.KRgui: return ImGuiKey.RightSuper;
				case KeyCode.KApplication: return ImGuiKey.Menu;
				case KeyCode.K0: return ImGuiKey._0;
				case KeyCode.K1: return ImGuiKey._1;
				case KeyCode.K2: return ImGuiKey._2;
				case KeyCode.K3: return ImGuiKey._3;
				case KeyCode.K4: return ImGuiKey._4;
				case KeyCode.K5: return ImGuiKey._5;
				case KeyCode.K6: return ImGuiKey._6;
				case KeyCode.K7: return ImGuiKey._7;
				case KeyCode.K8: return ImGuiKey._8;
				case KeyCode.K9: return ImGuiKey._9;
				case KeyCode.KA: return ImGuiKey.A;
				case KeyCode.KB: return ImGuiKey.B;
				case KeyCode.KC: return ImGuiKey.C;
				case KeyCode.KD: return ImGuiKey.D;
				case KeyCode.KE: return ImGuiKey.E;
				case KeyCode.KF: return ImGuiKey.F;
				case KeyCode.KG: return ImGuiKey.G;
				case KeyCode.KH: return ImGuiKey.H;
				case KeyCode.KI: return ImGuiKey.I;
				case KeyCode.KJ: return ImGuiKey.J;
				case KeyCode.KK: return ImGuiKey.K;
				case KeyCode.KL: return ImGuiKey.L;
				case KeyCode.KM: return ImGuiKey.M;
				case KeyCode.KN: return ImGuiKey.N;
				case KeyCode.KO: return ImGuiKey.O;
				case KeyCode.KP: return ImGuiKey.P;
				case KeyCode.KQ: return ImGuiKey.Q;
				case KeyCode.KR: return ImGuiKey.R;
				case KeyCode.KS: return ImGuiKey.S;
				case KeyCode.KT: return ImGuiKey.T;
				case KeyCode.KU: return ImGuiKey.U;
				case KeyCode.KV: return ImGuiKey.V;
				case KeyCode.KW: return ImGuiKey.W;
				case KeyCode.KX: return ImGuiKey.X;
				case KeyCode.KY: return ImGuiKey.Y;
				case KeyCode.KZ: return ImGuiKey.Z;
				case KeyCode.KF1: return ImGuiKey.F1;
				case KeyCode.KF2: return ImGuiKey.F2;
				case KeyCode.KF3: return ImGuiKey.F3;
				case KeyCode.KF4: return ImGuiKey.F4;
				case KeyCode.KF5: return ImGuiKey.F5;
				case KeyCode.KF6: return ImGuiKey.F6;
				case KeyCode.KF7: return ImGuiKey.F7;
				case KeyCode.KF8: return ImGuiKey.F8;
				case KeyCode.KF9: return ImGuiKey.F9;
				case KeyCode.KF10: return ImGuiKey.F10;
				case KeyCode.KF11: return ImGuiKey.F11;
				case KeyCode.KF12: return ImGuiKey.F12;
			}
			return ImGuiKey.None;
		}

		private static void UpdateKeyModifiers(Keymod sdl_key_mods)
		{
			ImGuiIOPtr io = ImGui.GetIO();
			io.AddKeyEvent(ImGuiKey.ModCtrl, (sdl_key_mods & Keymod.KmodCtrl) != 0);
			io.AddKeyEvent(ImGuiKey.ModShift, (sdl_key_mods & Keymod.KmodShift) != 0);
			io.AddKeyEvent(ImGuiKey.ModAlt, (sdl_key_mods & Keymod.KmodAlt) != 0);
			io.AddKeyEvent(ImGuiKey.ModSuper, (sdl_key_mods & Keymod.KmodGui) != 0);
		}

		// You can read the io.WantCaptureMouse, io.WantCaptureKeyboard flags to tell if dear imgui wants to use your inputs.
		// - When io.WantCaptureMouse is true, do not dispatch mouse input data to your main application, or clear/overwrite your copy of the mouse data.
		// - When io.WantCaptureKeyboard is true, do not dispatch keyboard input data to your main application, or clear/overwrite your copy of the keyboard data.
		// Generally you may always pass all inputs to dear imgui, and hide them from your application based on those two flags.
		// If you have multiple SDL events and some of them are not meant to be used by dear imgui, you may need to filter events based on their windowID field.
		public static bool ProcessEvent(ref Event e)
		{
			ImGuiIOPtr io = ImGui.GetIO();

			switch ((EventType)e.Type)
			{
				case EventType.Mousemotion:
				{
					io.AddMousePosEvent(e.Motion.X, e.Motion.Y);
					return true;
				}
				case EventType.Mousewheel:
				{
					float wheel_x = (e.Wheel.X > 0) ? 1f : (e.Wheel.X < 0) ? -1f : 0f;
					float wheel_y = (e.Wheel.Y > 0) ? 1f : (e.Wheel.Y < 0) ? -1f : 0f;
					io.AddMouseWheelEvent(wheel_x, wheel_y);
					return true;
				}
				case EventType.Mousebuttondown:
				case EventType.Mousebuttonup:
				{
					int mouse_button;
					switch (e.Button.Button)
					{
						case Sdl.ButtonLeft: mouse_button = 0; break;
						case Sdl.ButtonRight: mouse_button = 1; break;
						case Sdl.ButtonMiddle: mouse_button = 2; break;
						case Sdl.ButtonX1: mouse_button = 3; break;
						case Sdl.ButtonX2: mouse_button = 4; break;
						default: goto bottom;
					}

					io.AddMouseButtonEvent(mouse_button, (EventType)e.Type == EventType.Mousebuttondown);
					_bd.MouseButtonsDown = ((EventType)e.Type == EventType.Mousebuttondown)
						? (_bd.MouseButtonsDown | (1 << mouse_button))
						: (_bd.MouseButtonsDown & ~(1 << mouse_button));

					return true;
				bottom:
					break;
				}
				case EventType.Textinput:
				{
					fixed (byte* b = e.Text.Text)
					{
						ImGuiNative.ImGuiIO_AddInputCharactersUTF8(io.NativePtr, b);
					}
					return true;
				}
				case EventType.Keydown:
				case EventType.Keyup:
				{
					UpdateKeyModifiers((Keymod)e.Key.Keysym.Mod);
					ImGuiKey key = KeycodeToImGuiKey((KeyCode)e.Key.Keysym.Sym);
					io.AddKeyEvent(key, (EventType)e.Type == EventType.Keydown);
					// To support legacy indexing (<1.87 user code). Legacy backend uses SDLK_*** as indices to IsKeyXXX() functions.
					io.SetKeyEventNativeData(key, e.Key.Keysym.Sym, (int)e.Key.Keysym.Scancode, (int)e.Key.Keysym.Scancode);
					return true;
				}
				case EventType.Windowevent:
				{
					// - When capturing mouse, SDL will send a bunch of conflicting LEAVE/ENTER event on every mouse move, but the final ENTER tends to be right.
					// - However we won't get a correct LEAVE event for a captured window.
					// - In some cases, when detaching a window from main viewport SDL may send SDL_WINDOWEVENT_ENTER one frame too late,
					//   causing SDL_WINDOWEVENT_LEAVE on previous frame to interrupt drag operation by clear mouse position. This is why
					//   we delay process the SDL_WINDOWEVENT_LEAVE events by one frame. See issue #5012 for details.
					switch ((WindowEventID)e.Window.Event)
					{
						case WindowEventID.WindoweventEnter:
						{
							_bd.PendingMouseLeaveFrame = 0;
							break;
						}
						case WindowEventID.WindoweventLeave:
						{
							_bd.PendingMouseLeaveFrame = ImGui.GetFrameCount() + 1;
							break;
						}
						case WindowEventID.WindoweventFocusGained:
						{
							io.AddFocusEvent(true);
							break;
						}
						case WindowEventID.WindoweventFocusLost:
						{
							io.AddFocusEvent(false);
							break;
						}
					}
					return true;
				}
			}
			return false;
		}

		public static bool Init(Window* window, Renderer* renderer)
		{
			if (_bd is not null)
			{
				throw new InvalidOperationException("Already initialized a platform backend!");
			}

			Sdl sdl = DisplayManager.Instance.SDL;

			// Check and store if we are on a SDL backend that supports global mouse position
			// ("wayland" and "rpi" don't support it, but we chose to use a white-list instead of a black-list)
			bool mouse_can_use_global_state = false;
			string sdl_backend = sdl.GetCurrentVideoDriverS();
			string[] global_mouse_whitelist = new string[5] { "windows", "cocoa", "x11", "DIVE", "VMAN" };
			for (int n = 0; n < global_mouse_whitelist.Length; n++)
			{
				if (sdl_backend == global_mouse_whitelist[n])
				{
					mouse_can_use_global_state = true;
					break;
				}
			}

			// Setup backend capabilities flags
			_bd = new Data();
			_bd.Frequency = sdl.GetPerformanceFrequency();
			ImGuiIOPtr io = ImGui.GetIO();
			io.BackendFlags |= ImGuiBackendFlags.HasMouseCursors; // We can honor GetMouseCursor() values (optional)
			io.BackendFlags |= ImGuiBackendFlags.HasSetMousePos; // We can honor io.WantSetMousePos requests (optional, rarely used)

			_bd.Window = window;
			_bd.Renderer = renderer;
			_bd.MouseCanUseGlobalState = mouse_can_use_global_state;

			_bd.GetClipboardTextFn = new GetClipboardTextFn(GetClipboardText);
			_bd.SetClipboardTextFn = new SetClipboardTextFn(SetClipboardText);
			io.SetClipboardTextFn = Marshal.GetFunctionPointerForDelegate(_bd.SetClipboardTextFn);
			io.GetClipboardTextFn = Marshal.GetFunctionPointerForDelegate(_bd.GetClipboardTextFn);
			io.ClipboardUserData = IntPtr.Zero;

			// Load mouse cursors
			_bd.MouseCursors[(int)ImGuiMouseCursor.Arrow] = sdl.CreateSystemCursor(SystemCursor.SystemCursorArrow);
			_bd.MouseCursors[(int)ImGuiMouseCursor.TextInput] = sdl.CreateSystemCursor(SystemCursor.SystemCursorIbeam);
			_bd.MouseCursors[(int)ImGuiMouseCursor.ResizeAll] = sdl.CreateSystemCursor(SystemCursor.SystemCursorSizeall);
			_bd.MouseCursors[(int)ImGuiMouseCursor.ResizeNS] = sdl.CreateSystemCursor(SystemCursor.SystemCursorSizens);
			_bd.MouseCursors[(int)ImGuiMouseCursor.ResizeEW] = sdl.CreateSystemCursor(SystemCursor.SystemCursorSizewe);
			_bd.MouseCursors[(int)ImGuiMouseCursor.ResizeNESW] = sdl.CreateSystemCursor(SystemCursor.SystemCursorSizenesw);
			_bd.MouseCursors[(int)ImGuiMouseCursor.ResizeNWSE] = sdl.CreateSystemCursor(SystemCursor.SystemCursorSizenwse);
			_bd.MouseCursors[(int)ImGuiMouseCursor.Hand] = sdl.CreateSystemCursor(SystemCursor.SystemCursorHand);
			_bd.MouseCursors[(int)ImGuiMouseCursor.NotAllowed] = sdl.CreateSystemCursor(SystemCursor.SystemCursorNo);

			// Set platform dependent data in viewport
			if (Environment.OSVersion.Platform == PlatformID.Win32NT)
			{
				SysWMInfo info;
				sdl.GetVersion(&info.Version);
				if (sdl.GetWindowWMInfo(window, &info))
				{
					ImGuiNative.igGetMainViewport()->PlatformHandleRaw = (void*)info.Info.Win.Hwnd;
				}
			}

			// Set SDL hint to receive mouse click events on window focus, otherwise SDL doesn't emit the event.
			// Without this, when clicking to gain focus, our widgets wouldn't activate even though they showed as hovered.
			// (This is unfortunately a global SDL setting, so enabling it might have a side-effect on your application.
			// It is unlikely to make a difference, but if your app absolutely needs to ignore the initial on-focus click:
			// you can ignore SDL_MOUSEBUTTONDOWN events coming right after a SDL_WINDOWEVENT_FOCUS_GAINED)
			sdl.SetHint(Sdl.HintMouseFocusClickthrough, "1");

			return true;
		}

		public static void Shutdown()
		{
			if (_bd is null)
			{
				throw new InvalidOperationException("No platform backend to shutdown, or already shutdown?");
			}

			Sdl sdl = DisplayManager.Instance.SDL;

			if (_bd.ClipboardTextData is not null)
			{
				sdl.Free(_bd.ClipboardTextData);
			}

			for (ImGuiMouseCursor c = 0; c < ImGuiMouseCursor.COUNT; c++)
			{
				sdl.FreeCursor(_bd.MouseCursors[(int)c]);
			}

			_bd = null;
		}

		private static void UpdateMouseData()
		{
			Sdl sdl = DisplayManager.Instance.SDL;

			// We forward mouse input when hovered or captured (via SDL_MOUSEMOTION) or when focused (below)
			// SDL_CaptureMouse() let the OS know e.g. that our imgui drag outside the SDL window boundaries shouldn't e.g. trigger other operations outside
			sdl.CaptureMouse(_bd.MouseButtonsDown != 0 ? SdlBool.True : SdlBool.False);
			if (_bd.Window != sdl.GetKeyboardFocus())
			{
				return;
			}

			ImGuiIOPtr io = ImGui.GetIO();
			// (Optional) Set OS mouse position from Dear ImGui if requested (rarely used, only when ImGuiConfigFlags_NavEnableSetMousePos is enabled by user)
			if (io.WantSetMousePos)
			{
				sdl.WarpMouseInWindow(_bd.Window, (int)io.MousePos.X, (int)io.MousePos.Y);
			}

			// (Optional) Fallback to provide mouse position when focused (SDL_MOUSEMOTION already provides this when hovered or captured)
			if (_bd.MouseCanUseGlobalState && _bd.MouseButtonsDown == 0)
			{
				int window_x, window_y, mouse_x_global, mouse_y_global;
				sdl.GetGlobalMouseState(&mouse_x_global, &mouse_y_global);
				sdl.GetWindowPosition(_bd.Window, &window_x, &window_y);
				io.AddMousePosEvent(mouse_x_global - window_x, mouse_y_global - window_y);
			}
		}

		private static void UpdateMouseCursor()
		{
			ImGuiIOPtr io = ImGui.GetIO();

			if ((io.ConfigFlags & ImGuiConfigFlags.NoMouseCursorChange) != 0)
			{
				return;
			}

			Sdl sdl = DisplayManager.Instance.SDL;

			ImGuiMouseCursor imgui_cursor = ImGui.GetMouseCursor();
			if (io.MouseDrawCursor || imgui_cursor == ImGuiMouseCursor.None)
			{
				// Hide OS mouse cursor if imgui is drawing it or if it wants no cursor
				sdl.ShowCursor(0);
			}
			else
			{
				// Show OS mouse cursor
				Cursor* c = _bd.MouseCursors[(int)imgui_cursor];
				if (c is null)
				{
					c = _bd.MouseCursors[(int)ImGuiMouseCursor.Arrow];
				}
				sdl.SetCursor(c);
				sdl.ShowCursor(1);
			}
		}

		private static void UpdateGamepads()
		{
			ImGuiIOPtr io = ImGui.GetIO();

			if ((io.ConfigFlags & ImGuiConfigFlags.NavEnableGamepad) == 0)
			{
				return;
			}

			Sdl sdl = DisplayManager.Instance.SDL;

			// Get gamepad
			io.BackendFlags &= ~ImGuiBackendFlags.HasGamepad;
			GameController* game_controller = sdl.GameControllerOpen(0);
			if (game_controller is null)
			{
				return;
			}
			io.BackendFlags |= ImGuiBackendFlags.HasGamepad;

			// Update gamepad inputs
			static float IM_SATURATE(float V)
			{
				return V < 0f ? 0f : V > 1f ? 1f : V;
			}
			void MAP_BUTTON(ImGuiKey KEY_NO, GameControllerButton BUTTON_NO)
			{
				io.AddKeyEvent(KEY_NO, sdl.GameControllerGetButton(game_controller, BUTTON_NO) != 0);
			}
			void MAP_ANALOG(ImGuiKey KEY_NO, GameControllerAxis AXIS_NO, float V0, float V1)
			{
				float vn = (float)(sdl.GameControllerGetAxis(game_controller, AXIS_NO) - V0) / (float)(V1 - V0);
				vn = IM_SATURATE(vn);
				io.AddKeyAnalogEvent(KEY_NO, vn > 0.1f, vn);
			}
			const int DEADZONE = 8000; // SDL_gamecontroller.h suggests using this value.
			MAP_BUTTON(ImGuiKey.GamepadStart, GameControllerButton.ControllerButtonStart);
			MAP_BUTTON(ImGuiKey.GamepadBack, GameControllerButton.ControllerButtonBack);
			MAP_BUTTON(ImGuiKey.GamepadFaceDown, GameControllerButton.ControllerButtonA); // Xbox A, PS Cross
			MAP_BUTTON(ImGuiKey.GamepadFaceRight, GameControllerButton.ControllerButtonB); // Xbox B, PS Circle
			MAP_BUTTON(ImGuiKey.GamepadFaceLeft, GameControllerButton.ControllerButtonX); // Xbox X, PS Square
			MAP_BUTTON(ImGuiKey.GamepadFaceUp, GameControllerButton.ControllerButtonY); // Xbox Y, PS Triangle
			MAP_BUTTON(ImGuiKey.GamepadDpadLeft, GameControllerButton.ControllerButtonDpadLeft);
			MAP_BUTTON(ImGuiKey.GamepadDpadRight, GameControllerButton.ControllerButtonDpadRight);
			MAP_BUTTON(ImGuiKey.GamepadDpadUp, GameControllerButton.ControllerButtonDpadUp);
			MAP_BUTTON(ImGuiKey.GamepadDpadDown, GameControllerButton.ControllerButtonDpadDown);
			MAP_BUTTON(ImGuiKey.GamepadL1, GameControllerButton.ControllerButtonLeftshoulder);
			MAP_BUTTON(ImGuiKey.GamepadR1, GameControllerButton.ControllerButtonRightshoulder);
			MAP_ANALOG(ImGuiKey.GamepadL2, GameControllerAxis.ControllerAxisTriggerleft, 0.0f, 32767);
			MAP_ANALOG(ImGuiKey.GamepadR2, GameControllerAxis.ControllerAxisTriggerright, 0.0f, 32767);
			MAP_BUTTON(ImGuiKey.GamepadL3, GameControllerButton.ControllerButtonLeftstick);
			MAP_BUTTON(ImGuiKey.GamepadR3, GameControllerButton.ControllerButtonRightstick);
			MAP_ANALOG(ImGuiKey.GamepadLStickLeft, GameControllerAxis.ControllerAxisLeftx, -DEADZONE, -32768);
			MAP_ANALOG(ImGuiKey.GamepadLStickRight, GameControllerAxis.ControllerAxisLeftx, +DEADZONE, +32767);
			MAP_ANALOG(ImGuiKey.GamepadLStickUp, GameControllerAxis.ControllerAxisLefty, -DEADZONE, -32768);
			MAP_ANALOG(ImGuiKey.GamepadLStickDown, GameControllerAxis.ControllerAxisLefty, +DEADZONE, +32767);
			MAP_ANALOG(ImGuiKey.GamepadRStickLeft, GameControllerAxis.ControllerAxisRightx, -DEADZONE, -32768);
			MAP_ANALOG(ImGuiKey.GamepadRStickRight, GameControllerAxis.ControllerAxisRightx, +DEADZONE, +32767);
			MAP_ANALOG(ImGuiKey.GamepadRStickUp, GameControllerAxis.ControllerAxisRighty, -DEADZONE, -32768);
			MAP_ANALOG(ImGuiKey.GamepadRStickDown, GameControllerAxis.ControllerAxisRighty, +DEADZONE, +32767);
		}

		public static void NewFrame()
		{
			if (_bd is null)
			{
				throw new InvalidOperationException("Did you call Init()?");
			}

			Sdl sdl = DisplayManager.Instance.SDL;

			// Setup display size (every frame to accommodate for window resizing)
			int w, h;
			int display_w, display_h;
			sdl.GetWindowSize(_bd.Window, &w, &h);
			if ((sdl.GetWindowFlags(_bd.Window) & (uint)WindowFlags.WindowMinimized) != 0)
			{
				w = h = 0;
			}

			if (_bd.Renderer is not null)
			{
				sdl.GetRendererOutputSize(_bd.Renderer, &display_w, &display_h);
			}
			else
			{
				sdl.GLGetDrawableSize(_bd.Window, &display_w, &display_h);
			}

			ImGuiIOPtr io = ImGui.GetIO();
			io.DisplaySize = new Vector2(w, h);
			if (w > 0 && h > 0)
			{
				io.DisplayFramebufferScale = new Vector2((float)display_w / w, (float)display_h / h);
			}

			// Setup time step (we don't use SDL_GetTicks() because it is using millisecond resolution)
			ulong current_time = sdl.GetPerformanceCounter();
			io.DeltaTime = _bd.Time > 0 ? (float)((double)(current_time - _bd.Time) / _bd.Frequency) : 1f / 60f;
			_bd.Time = current_time;

			if (_bd.PendingMouseLeaveFrame != 0 && _bd.PendingMouseLeaveFrame >= ImGui.GetFrameCount() && _bd.MouseButtonsDown == 0)
			{
				io.AddMousePosEvent(float.MinValue, float.MinValue);
				_bd.PendingMouseLeaveFrame = 0;
			}

			UpdateMouseData();
			UpdateMouseCursor();

			// Update game controllers (if enabled and available)
			UpdateGamepads();
		}
	}
}