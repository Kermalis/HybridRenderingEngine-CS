using ImGuiNET;
using Silk.NET.OpenGL;
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace HybridRenderingEngine.Utils
{
	// https://github.com/ocornut/imgui/blob/master/backends/imgui_impl_opengl3.cpp
	// Last copied on 4/26/2022, Did not copy anything non-4.3
	internal static unsafe class ImGui_ImplOpenGL3
	{
		// imgui.h
		public delegate void ImDrawCallback(ImDrawList* parent_list, ref ImDrawCmd cmd);
		public const int ImDrawCallback_ResetRenderState = -1;

		private const int ImDrawIdxSIZE = sizeof(short);
		private const DrawElementsType ImDrawIdxELEMENTS = DrawElementsType.UnsignedShort;
		private const string GLSL_VERSION = "#version 430\n";

		private sealed class Data
		{
			public uint FontTexture;
			public uint ShaderHandle;
			public int ShaderLocationTex;
			public int ShaderLocationProjMtx;
			public uint VboHandle;
			public uint ElementsHandle;
			public uint VertexBufferSize;
			public uint IndexBufferSize;
		};
		private struct VBOData_ImDrawVert // ImDrawVert
		{
			private const int OFFSET_POS = 0;
			private const int OFFSET_UV = OFFSET_POS + (2 * sizeof(float));
			private const int OFFSET_COL = OFFSET_UV + (2 * sizeof(float));
			public const int SIZE = OFFSET_COL + sizeof(uint);

			public Vector2 Pos;
			public Vector2 UV;
			public uint Col;

			public static unsafe void AddAttributes(GL gl, uint startIndex)
			{
				gl.EnableVertexAttribArray(startIndex);
				gl.VertexAttribPointer(startIndex, 2, VertexAttribPointerType.Float, false, SIZE, (void*)OFFSET_POS);
				gl.EnableVertexAttribArray(startIndex + 1);
				gl.VertexAttribPointer(startIndex + 1, 2, VertexAttribPointerType.Float, false, SIZE, (void*)OFFSET_UV);
				gl.EnableVertexAttribArray(startIndex + 2);
				gl.VertexAttribPointer(startIndex + 2, 4, VertexAttribPointerType.UnsignedByte, true, SIZE, (void*)OFFSET_COL);
			}
		}

		private static Data _bd;

		// Functions
		public static bool Init()
		{
			if (_bd is not null)
			{
				throw new InvalidOperationException("Already initialized a renderer backend!");
			}

			// Setup backend capabilities flags
			_bd = new Data();
			ImGuiIOPtr io = ImGui.GetIO();
			io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset; // We can honor the ImDrawCmd::VtxOffset field, allowing for large meshes.

			return true;
		}

		public static void Shutdown()
		{
			if (_bd is null)
			{
				throw new InvalidOperationException("No renderer backend to shutdown, or already shutdown?");
			}

			DestroyDeviceObjects();
			_bd = null;
		}

		public static void NewFrame()
		{
			if (_bd is null)
			{
				throw new InvalidOperationException("Did you call Init()?");
			}

			if (_bd.ShaderHandle == 0)
			{
				CreateDeviceObjects();
			}
		}

		private static void SetupRenderState(ImDrawData* draw_data, uint fb_width, uint fb_height, uint vertex_array_object)
		{
			GL gl = DisplayManager.Instance.OpenGL;

			// Setup render state: alpha-blending enabled, no face culling, no depth testing, scissor enabled, polygon fill
			gl.Enable(EnableCap.Blend);
			gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
			gl.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
			gl.Disable(EnableCap.CullFace);
			gl.Disable(EnableCap.DepthTest);
			gl.Disable(EnableCap.StencilTest);
			gl.Enable(EnableCap.ScissorTest);
			gl.Disable(EnableCap.PrimitiveRestart);
			gl.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);

			// Setup viewport, orthographic projection matrix
			// Our visible imgui space lies from draw_data->DisplayPos (top left) to draw_data->DisplayPos+data_data->DisplaySize (bottom right). DisplayPos is (0,0) for single viewport apps.
			gl.Viewport(0, 0, fb_width, fb_height);
			float L = draw_data->DisplayPos.X;
			float R = draw_data->DisplayPos.X + draw_data->DisplaySize.X;
			float T = draw_data->DisplayPos.Y;
			float B = draw_data->DisplayPos.Y + draw_data->DisplaySize.Y;

			Matrix4x4 ortho_projection;
			ortho_projection.M11 = 2f / (R - L);
			ortho_projection.M12 = 0f;
			ortho_projection.M13 = 0f;
			ortho_projection.M14 = 0f;
			ortho_projection.M21 = 0f;
			ortho_projection.M22 = 2f / (T - B);
			ortho_projection.M23 = 0f;
			ortho_projection.M24 = 0f;
			ortho_projection.M31 = 0f;
			ortho_projection.M32 = 0f;
			ortho_projection.M33 = -1f;
			ortho_projection.M34 = 0f;
			ortho_projection.M41 = (R + L) / (L - R);
			ortho_projection.M42 = (T + B) / (B - T);
			ortho_projection.M43 = 0f;
			ortho_projection.M44 = 1f;

			gl.UseProgram(_bd.ShaderHandle);
			gl.Uniform1(_bd.ShaderLocationTex, 0);
			gl.UniformMatrix4(_bd.ShaderLocationProjMtx, 1, false, (float*)&ortho_projection);

			gl.BindSampler(0, 0); // We use combined texture/sampler state. Applications using GL 3.3 may set that otherwise.

			gl.BindVertexArray(vertex_array_object);

			// Bind vertex/index buffers and setup attributes for ImDrawVert
			gl.BindBuffer(BufferTargetARB.ArrayBuffer, _bd.VboHandle);
			gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _bd.ElementsHandle);
			VBOData_ImDrawVert.AddAttributes(gl, 0);
		}

		public static void RenderDrawData(ImDrawData* draw_data)
		{
			// Avoid rendering when minimized, scale coordinates for retina displays (screen coordinates != framebuffer coordinates)
			int fb_width = (int)(draw_data->DisplaySize.X * draw_data->FramebufferScale.X);
			int fb_height = (int)(draw_data->DisplaySize.Y * draw_data->FramebufferScale.Y);
			if (fb_width <= 0 || fb_height <= 0)
			{
				return;
			}

			GL gl = DisplayManager.Instance.OpenGL;

			// Backup GL state
			gl.GetInteger(GetPName.ActiveTexture, out int last_active_texture);
			gl.ActiveTexture(TextureUnit.Texture0);
			gl.GetInteger(GetPName.CurrentProgram, out int last_program);
			gl.GetInteger(GetPName.TextureBinding2D, out int last_texture);
			gl.GetInteger(GetPName.SamplerBinding, out int last_sampler);
			gl.GetInteger(GetPName.ArrayBufferBinding, out int last_array_buffer);
			gl.GetInteger(GetPName.VertexArrayBinding, out int last_vertex_array_object);
			int* last_polygon_mode = stackalloc int[2];
			gl.GetInteger(GetPName.PolygonMode, last_polygon_mode);
			int* last_viewport = stackalloc int[4];
			gl.GetInteger(GetPName.Viewport, last_viewport);
			int* last_scissor_box = stackalloc int[4];
			gl.GetInteger(GetPName.ScissorBox, last_scissor_box);
			gl.GetInteger(GetPName.BlendSrcRgb, out int last_blend_src_rgb);
			gl.GetInteger(GetPName.BlendDstRgb, out int last_blend_dst_rgb);
			gl.GetInteger(GetPName.BlendSrcAlpha, out int last_blend_src_alpha);
			gl.GetInteger(GetPName.BlendDstAlpha, out int last_blend_dst_alpha);
			gl.GetInteger(GetPName.BlendEquationRgb, out int last_blend_equation_rgb);
			gl.GetInteger(GetPName.BlendEquationAlpha, out int last_blend_equation_alpha);
			bool last_enable_blend = gl.IsEnabled(EnableCap.Blend);
			bool last_enable_cull_face = gl.IsEnabled(EnableCap.CullFace);
			bool last_enable_depth_test = gl.IsEnabled(EnableCap.DepthTest);
			bool last_enable_stencil_test = gl.IsEnabled(EnableCap.StencilTest);
			bool last_enable_scissor_test = gl.IsEnabled(EnableCap.ScissorTest);
			bool last_enable_primitive_restart = gl.IsEnabled(EnableCap.PrimitiveRestart);

			// Setup desired GL state
			// Recreate the VAO every time (this is to easily allow multiple GL contexts to be rendered to. VAO are not shared among GL contexts)
			// The renderer would actually work without any VAO bound, but then our VertexAttrib calls would overwrite the default one currently bound.
			uint vertex_array_object = 0;
			gl.GenVertexArrays(1, &vertex_array_object);
			SetupRenderState(draw_data, (uint)fb_width, (uint)fb_height, vertex_array_object);

			// Will project scissor/clipping rectangles into framebuffer space
			Vector2 clip_off = draw_data->DisplayPos;         // (0,0) unless using multi-viewports
			Vector2 clip_scale = draw_data->FramebufferScale; // (1,1) unless using retina display which are often (2,2)

			// Render command lists
			for (int n = 0; n < draw_data->CmdListsCount; n++)
			{
				ImDrawList* cmd_list = draw_data->CmdLists[n];

				// Upload vertex/index buffers
				uint vtx_buffer_size = (uint)cmd_list->VtxBuffer.Size * VBOData_ImDrawVert.SIZE;
				uint idx_buffer_size = (uint)cmd_list->IdxBuffer.Size * ImDrawIdxSIZE;
				if (_bd.VertexBufferSize < vtx_buffer_size)
				{
					_bd.VertexBufferSize = vtx_buffer_size;
					gl.BufferData(BufferTargetARB.ArrayBuffer, _bd.VertexBufferSize, null, BufferUsageARB.StreamDraw);
				}
				if (_bd.IndexBufferSize < idx_buffer_size)
				{
					_bd.IndexBufferSize = idx_buffer_size;
					gl.BufferData(BufferTargetARB.ElementArrayBuffer, _bd.IndexBufferSize, null, BufferUsageARB.StreamDraw);
				}
				gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, vtx_buffer_size, (void*)cmd_list->VtxBuffer.Data);
				gl.BufferSubData(BufferTargetARB.ElementArrayBuffer, 0, idx_buffer_size, (void*)cmd_list->IdxBuffer.Data);

				for (int cmd_i = 0; cmd_i < cmd_list->CmdBuffer.Size; cmd_i++)
				{
					ref ImDrawCmd pcmd = ref cmd_list->CmdBuffer.Ref<ImDrawCmd>(cmd_i);
					if (pcmd.UserCallback != IntPtr.Zero)
					{
						// User callback, registered via ImDrawList::AddCallback()
						// (ImDrawCallback_ResetRenderState is a special callback value used by the user to request the renderer to reset render state.)
						if (pcmd.UserCallback == new IntPtr(ImDrawCallback_ResetRenderState))
						{
							SetupRenderState(draw_data, (uint)fb_width, (uint)fb_height, vertex_array_object);
						}
						else
						{
							Marshal.GetDelegateForFunctionPointer<ImDrawCallback>(pcmd.UserCallback)(cmd_list, ref pcmd);
						}
					}
					else
					{
						// Project scissor/clipping rectangles into framebuffer space
						var clip_min = new Vector2((pcmd.ClipRect.X - clip_off.X) * clip_scale.X, (pcmd.ClipRect.Y - clip_off.Y) * clip_scale.Y);
						var clip_max = new Vector2((pcmd.ClipRect.Z - clip_off.X) * clip_scale.X, (pcmd.ClipRect.W - clip_off.Y) * clip_scale.Y);
						if (clip_max.X <= clip_min.X || clip_max.Y <= clip_min.Y)
						{
							continue;
						}

						// Apply scissor/clipping rectangle (Y is inverted in OpenGL)
						gl.Scissor((int)clip_min.X, (int)(fb_height - clip_max.Y), (uint)(clip_max.X - clip_min.X), (uint)(clip_max.Y - clip_min.Y));

						// Bind texture, Draw
						gl.BindTexture(TextureTarget.Texture2D, (uint)pcmd.TextureId);
						gl.DrawElementsBaseVertex(PrimitiveType.Triangles, pcmd.ElemCount, ImDrawIdxELEMENTS, (void*)(pcmd.IdxOffset * ImDrawIdxSIZE), (int)pcmd.VtxOffset);
					}
				}
			}

			// Destroy the temporary VAO
			gl.DeleteVertexArrays(1, &vertex_array_object);

			// Restore modified GL state
			gl.UseProgram((uint)last_program);
			gl.BindTexture(TextureTarget.Texture2D, (uint)last_texture);
			gl.BindSampler(0, (uint)last_sampler);
			gl.ActiveTexture((TextureUnit)last_active_texture);
			gl.BindVertexArray((uint)last_vertex_array_object);
			gl.BindBuffer(BufferTargetARB.ArrayBuffer, (uint)last_array_buffer);
			gl.BlendEquationSeparate((BlendEquationModeEXT)last_blend_equation_rgb, (BlendEquationModeEXT)last_blend_equation_alpha);
			gl.BlendFuncSeparate((BlendingFactor)last_blend_src_rgb, (BlendingFactor)last_blend_dst_rgb, (BlendingFactor)last_blend_src_alpha, (BlendingFactor)last_blend_dst_alpha);
			if (last_enable_blend)
			{
				gl.Enable(EnableCap.Blend);
			}
			else
			{
				gl.Disable(EnableCap.Blend);
			}
			if (last_enable_cull_face)
			{
				gl.Enable(EnableCap.CullFace);
			}
			else
			{
				gl.Disable(EnableCap.CullFace);
			}
			if (last_enable_depth_test)
			{
				gl.Enable(EnableCap.DepthTest);
			}
			else
			{
				gl.Disable(EnableCap.DepthTest);
			}
			if (last_enable_stencil_test)
			{
				gl.Enable(EnableCap.StencilTest);
			}
			else
			{
				gl.Disable(EnableCap.StencilTest);
			}
			if (last_enable_scissor_test)
			{
				gl.Enable(EnableCap.ScissorTest);
			}
			else
			{
				gl.Disable(EnableCap.ScissorTest);
			}
			if (last_enable_primitive_restart)
			{
				gl.Enable(EnableCap.PrimitiveRestart);
			}
			else
			{
				gl.Disable(EnableCap.PrimitiveRestart);
			}

			gl.PolygonMode(MaterialFace.FrontAndBack, (PolygonMode)last_polygon_mode[0]);
			gl.Viewport(last_viewport[0], last_viewport[1], (uint)last_viewport[2], (uint)last_viewport[3]);
			gl.Scissor(last_scissor_box[0], last_scissor_box[1], (uint)last_scissor_box[2], (uint)last_scissor_box[3]);
		}

		private static bool CreateFontsTexture()
		{
			ImGuiIOPtr io = ImGui.GetIO();
			GL gl = DisplayManager.Instance.OpenGL;

			// Build texture atlas
			// Load as RGBA 32-bit (75% of the memory is wasted, but default font is so small) because it is more likely to be compatible with user's existing shaders.
			// If your ImTextureId represent a higher-level concept than just a GL texture id, consider calling GetTexDataAsAlpha8() instead to save on GPU memory.
			io.Fonts.GetTexDataAsRGBA32(out byte* pixels, out int width, out int height);

			// Upload texture to graphics system
			// (Bilinear sampling is required by default. Set 'io.Fonts->Flags |= ImFontAtlasFlags_NoBakedLines' or 'style.AntiAliasedLinesUseTex = false' to allow point/nearest sampling)
			gl.GetInteger(GetPName.TextureBinding2D, out int lastTexture);
			_bd.FontTexture = gl.GenTexture();
			gl.BindTexture(TextureTarget.Texture2D, _bd.FontTexture);
			gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
			gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
			gl.PixelStore(PixelStoreParameter.UnpackRowLength, 0);
			gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);

			// Store our identifier
			io.Fonts.SetTexID(new IntPtr(_bd.FontTexture));

			// Restore state
			gl.BindTexture(TextureTarget.Texture2D, (uint)lastTexture);

			return true;
		}
		private static void DestroyFontsTexture()
		{
			if (_bd.FontTexture == 0)
			{
				return;
			}

			GL gl = DisplayManager.Instance.OpenGL;
			gl.DeleteTexture(_bd.FontTexture);
			ImGuiIOPtr io = ImGui.GetIO();
			io.Fonts.SetTexID(IntPtr.Zero);
			_bd.FontTexture = 0;
		}

		private static bool CheckShader(uint handle, string desc)
		{
			GL gl = DisplayManager.Instance.OpenGL;
			gl.GetShader(handle, ShaderParameterName.CompileStatus, out int status);
			gl.GetShader(handle, ShaderParameterName.InfoLogLength, out int log_length);
			if (status == 0)
			{
				Console.WriteLine("ERROR: CreateDeviceObjects: failed to compile {0}! With GLSL: {1}", desc, GLSL_VERSION);
			}

			if (log_length > 1)
			{
				//ImVector<char> buf;
				//buf.resize((int)(log_length + 1));
				//gl.GetShaderInfoLog(handle, log_length, NULL, (GLchar*)buf.begin());
				//fprintf(stderr, "%s\n", buf.begin());
			}
			return status == 1;
		}
		private static bool CheckProgram(uint handle, string desc)
		{
			GL gl = DisplayManager.Instance.OpenGL;
			gl.GetProgram(handle, ProgramPropertyARB.LinkStatus, out int status);
			gl.GetProgram(handle, ProgramPropertyARB.InfoLogLength, out int log_length);
			if (status == 0)
			{
				Console.WriteLine("ERROR: CreateDeviceObjects: failed to link {0}! With GLSL {1}", desc, GLSL_VERSION);
			}

			if (log_length > 1)
			{
				//ImVector<char> buf;
				//buf.resize((int)(log_length + 1));
				//gl.GetProgramInfoLog(handle, log_length, NULL, (GLchar*)buf.begin());
				//fprintf(stderr, "%s\n", buf.begin());
			}
			return status == 1;
		}

		private static bool CreateDeviceObjects()
		{
			GL gl = DisplayManager.Instance.OpenGL;

			// Backup GL state
			gl.GetInteger(GetPName.TextureBinding2D, out int last_texture);
			gl.GetInteger(GetPName.ArrayBufferBinding, out int last_array_buffer);
			gl.GetInteger(GetPName.VertexArrayBinding, out int last_vertex_array);

			const string vertex_shader_glsl_410_core =
				"layout (location = 0) in vec2 Position;\n" +
				"layout (location = 1) in vec2 UV;\n" +
				"layout (location = 2) in vec4 Color;\n" +
				"uniform mat4 ProjMtx;\n" +
				"out vec2 Frag_UV;\n" +
				"out vec4 Frag_Color;\n" +
				"void main()\n" +
				"{\n" +
				"    Frag_UV = UV;\n" +
				"    Frag_Color = Color;\n" +
				"    gl_Position = ProjMtx * vec4(Position.xy,0,1);\n" +
				"}\n";

			const string fragment_shader_glsl_410_core =
				"in vec2 Frag_UV;\n" +
				"in vec4 Frag_Color;\n" +
				"uniform sampler2D Texture;\n" +
				"layout (location = 0) out vec4 Out_Color;\n" +
				"void main()\n" +
				"{\n" +
				"    Out_Color = Frag_Color * texture(Texture, Frag_UV.st);\n" +
				"}\n";

			// Select shaders matching our GLSL versions
			string vertex_shader = vertex_shader_glsl_410_core;
			string fragment_shader = fragment_shader_glsl_410_core;

			// Create shaders
			string[] vertex_shader_with_version = { GLSL_VERSION, vertex_shader };
			uint vert_handle = gl.CreateShader(ShaderType.VertexShader);
			gl.ShaderSource(vert_handle, 2, vertex_shader_with_version, null);
			gl.CompileShader(vert_handle);
			CheckShader(vert_handle, "vertex shader");

			string[] fragment_shader_with_version = { GLSL_VERSION, fragment_shader };
			uint frag_handle = gl.CreateShader(ShaderType.FragmentShader);
			gl.ShaderSource(frag_handle, 2, fragment_shader_with_version, null);
			gl.CompileShader(frag_handle);
			CheckShader(frag_handle, "fragment shader");

			// Link
			_bd.ShaderHandle = gl.CreateProgram();
			gl.AttachShader(_bd.ShaderHandle, vert_handle);
			gl.AttachShader(_bd.ShaderHandle, frag_handle);
			gl.LinkProgram(_bd.ShaderHandle);
			CheckProgram(_bd.ShaderHandle, "shader program");

			gl.DetachShader(_bd.ShaderHandle, vert_handle);
			gl.DetachShader(_bd.ShaderHandle, frag_handle);
			gl.DeleteShader(vert_handle);
			gl.DeleteShader(frag_handle);

			_bd.ShaderLocationTex = gl.GetUniformLocation(_bd.ShaderHandle, "Texture");
			_bd.ShaderLocationProjMtx = gl.GetUniformLocation(_bd.ShaderHandle, "ProjMtx");

			// Create buffers
			_bd.VboHandle = gl.GenBuffer();
			_bd.ElementsHandle = gl.GenBuffer();

			CreateFontsTexture();

			// Restore modified GL state
			gl.BindTexture(TextureTarget.Texture2D, (uint)last_texture);
			gl.BindBuffer(BufferTargetARB.ArrayBuffer, (uint)last_array_buffer);
			gl.BindVertexArray((uint)last_vertex_array);

			return true;
		}
		private static void DestroyDeviceObjects()
		{
			GL gl = DisplayManager.Instance.OpenGL;
			if (_bd.VboHandle != 0)
			{
				gl.DeleteBuffer(_bd.VboHandle);
				_bd.VboHandle = 0;
			}
			if (_bd.ElementsHandle != 0)
			{
				gl.DeleteBuffer(_bd.ElementsHandle);
				_bd.ElementsHandle = 0;
			}
			if (_bd.ShaderHandle != 0)
			{
				gl.DeleteProgram(_bd.ShaderHandle);
				_bd.ShaderHandle = 0;
			}
			DestroyFontsTexture();
		}
	}
}