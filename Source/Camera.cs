using HybridRenderingEngine.Utils;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace HybridRenderingEngine
{
	internal sealed class Camera
	{
		// Containers for the camera transform matrices and frustum geometry
		public Matrix4x4 ViewMatrix;
		public Matrix4x4 ProjectionMatrix;
		public float NearPlane;
		public float FarPlane;
		private readonly float _fov;

		// Keeps track of the current relevant keys that are pressed to avoid issues with
		// the frequency of polling of the keyboard vs the frequency of updates
		public readonly HashSet<char> ActiveMoveStates;

		// Original values used to initialize the camera
		// We keep them in memory in case user wants to reset position
		private readonly Vector3 _ogPosition, _ogTarget, _ogFront, _ogRight;
		private readonly float _ogPitch, _ogYaw;

		// Camera basis vectors for view matrix construction
		public Vector3 Position;
		private Vector3 _front, _right, _target, _up;
		public float Pitch;
		public float Yaw;

		// Physical/Optical properties
		public float CamSpeed, MouseSens, Exposure;
		public int BlurAmount;

		public Camera(in Vector3 target, in Vector3 pos, float fov, float speed, float sens, float nearP, float farP)
		{
			ActiveMoveStates = new HashSet<char>();

			// Position and orientation of the camera, both in cartesian and spherical
			Position = pos;
			_target = target;
			_front = Vector3.Normalize(_target - Position);
			_up = Vector3.UnitY;
			_right = Vector3.Normalize(Vector3.Cross(_front, _up));
			Pitch = GetPitch(_front);
			Yaw = GetYaw(_front, Pitch);

			// Saving reset position values
			_ogPosition = pos;
			_ogTarget = target;
			_ogFront = _front;
			_ogRight = _right;
			_ogPitch = Pitch;
			_ogYaw = Yaw;

			// Shaping the frustum to the scene's imported values
			_fov = fov;
			FarPlane = farP;
			NearPlane = nearP;

			// Setting default values of other miscellaneous camera parameters
			CamSpeed = speed;
			MouseSens = sens;
			BlurAmount = 0;
			Exposure = 1f;

			// Setting up perspective and view matrix for rendering
			ViewMatrix = MyUtils.GLM_LookAtRH(Position, _target, _up);
			ProjectionMatrix = MyUtils.GLM_PerspectiveRH_NO(_fov * MyUtils.DEG_TO_RAD, DisplayManager.SCREEN_ASPECT_RATIO, NearPlane, FarPlane);
		}

		// Updates the cameras orientation and position based on the input from the user
		// Also updates view matrix and projection matrix for rendering
		public void Update(uint deltaT)
		{
			// We apply the rotation first
			UpdateOrientation();

			float speed = CamSpeed * deltaT;

			// Then translate
			foreach (char x in ActiveMoveStates)
			{
				switch (x)
				{
					case 'w':
					{
						Position += _front * speed;
						break;
					}
					case 's':
					{
						Position -= _front * speed;
						break;
					}
					case 'a':
					{
						Position -= _right * speed;
						break;
					}
					case 'd':
					{
						Position += _right * speed;
						break;
					}
					case 'q':
					{
						Position += _up * speed;
						break;
					}
					case 'e':
					{
						Position -= _up * speed;
						break;
					}
				}
			}

			// And we recalculate the new view and projection matrices for rendering
			_target = Position + _front;
			ViewMatrix = MyUtils.GLM_LookAtRH(Position, _target, _up);
			ProjectionMatrix = MyUtils.GLM_PerspectiveRH_NO(_fov * MyUtils.DEG_TO_RAD, DisplayManager.SCREEN_ASPECT_RATIO, NearPlane, FarPlane);
		}

		// Used by input to reset camera to origin in case user loses their bearings
		public void ResetCamera()
		{
			Position = _ogPosition;
			_target = _ogTarget;
			_front = _ogFront;
			_right = _ogRight;
			Pitch = _ogPitch;
			Yaw = _ogYaw;
		}

		// Transform from cartesian to spherical, used in the first setup of yaw and pitch
		// Since the incoming target and position values are being read from an unknown scene file
		private static float GetPitch(in Vector3 front)
		{
			return MathF.Asin(front.Y) * MyUtils.RAD_TO_DEG;
		}
		private static float GetYaw(in Vector3 front, float pitch)
		{
			return MathF.Acos(front.X / MathF.Cos(pitch * MyUtils.DEG_TO_RAD)) * MyUtils.RAD_TO_DEG;
		}

		// Orient the front and side vectors based on the screen pitch and yaw values
		private void UpdateOrientation()
		{
			_front.X = MathF.Cos(Pitch * MyUtils.DEG_TO_RAD) * MathF.Cos(Yaw * MyUtils.DEG_TO_RAD);
			_front.Y = MathF.Sin(Pitch * MyUtils.DEG_TO_RAD);
			_front.Z = MathF.Cos(Pitch * MyUtils.DEG_TO_RAD) * MathF.Sin(Yaw * MyUtils.DEG_TO_RAD);
			_front = Vector3.Normalize(_front);
			_right = Vector3.Normalize(Vector3.Cross(_front, _up));
		}
	}
}
