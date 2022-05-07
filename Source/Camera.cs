using HybridRenderingEngine.Utils;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace HybridRenderingEngine
{
	internal sealed class Camera
	{
		// Containers for the camera transform matrices and frustum geometry
		public Matrix4x4 viewMatrix, projectionMatrix;
		public Frustum cameraFrustum;

		// Keeps track of the current relevant keys that are pressed to avoid issues with
		// the frequency of polling of the keyboard vs the frequency of updates
		public readonly HashSet<char> activeMoveStates;

		// Original values used to initialize the camera
		// We keep them in memory in case user wants to reset position
		public Vector3 originalPosition, originalTarget, originalFront, originalRight;
		public float originalPitch, originalYaw;

		// Camera basis vectors for view matrix construction
		public Vector3 position, front, target, up;
		private Vector3 right;
		public float pitch, yaw;

		// Physical/Optical properties
		public float camSpeed, mouseSens, exposure;
		public int blurAmount;

		public Camera(in Vector3 tar, in Vector3 pos, float fov, float speed, float sens, float nearP, float farP)
		{
			activeMoveStates = new HashSet<char>();

			// Position and orientation of the camera, both in cartesian and spherical
			position = pos;
			target = tar;
			front = Vector3.Normalize(target - position);
			up = Vector3.UnitY;
			right = Vector3.Normalize(Vector3.Cross(front, up));
			pitch = GetPitch(front);
			yaw = GetYaw(front, pitch);

			// Saving reset position values
			originalPosition = pos;
			originalTarget = tar;
			originalFront = front;
			originalRight = right;
			originalPitch = pitch;
			originalYaw = yaw;

			// Shaping the frustum to the scene's imported values
			cameraFrustum = new Frustum();
			cameraFrustum.fov = fov;
			cameraFrustum.AR = DisplayManager.SCREEN_ASPECT_RATIO;
			cameraFrustum.farPlane = farP;
			cameraFrustum.nearPlane = nearP;

			// Setting default values of other miscellaneous camera parameters
			camSpeed = speed;
			mouseSens = sens;
			blurAmount = 0;
			exposure = 1f;

			// Setting up perspective and view matrix for rendering
			viewMatrix = Matrix4x4.CreateLookAt(position, target, up);
			projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(cameraFrustum.fov * MyUtils.DEG_TO_RAD,
				cameraFrustum.AR, cameraFrustum.nearPlane, cameraFrustum.farPlane);
		}

		// Updates the cameras orientation and position based on the input from the user
		// Also updates view matrix and projection matrix for rendering
		public void Update(uint deltaT)
		{
			float speed = camSpeed * deltaT;

			// We apply the rotation first
			UpdateOrientation();

			// Then translate
			foreach (char x in activeMoveStates)
			{
				switch (x)
				{
					case 'w':
						position += front * speed;
						break;
					case 's':
						position -= front * speed;
						break;
					case 'a':
						position -= right * speed;
						break;
					case 'd':
						position += right * speed;
						break;
					case 'q':
						position += up * speed;
						break;
					case 'e':
						position -= up * speed;
						break;
				}
			}

			// And we recalculate the new view and projection matrices for rendering
			target = position + front;
			viewMatrix = Matrix4x4.CreateLookAt(position, target, up);
			projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(cameraFrustum.fov * MyUtils.DEG_TO_RAD, cameraFrustum.AR, cameraFrustum.nearPlane, cameraFrustum.farPlane);
		}

		// Used by input to reset camera to origin in case user loses their bearings
		public void ResetCamera()
		{
			position = originalPosition;
			target = originalTarget;
			front = originalFront;
			right = originalRight;
			pitch = originalPitch;
			yaw = originalYaw;
		}

		// Transform from cartesian to spherical, used in the first setup of yaw and pitch
		// Since the incoming target and position values are being read from an unknown scene file
		public static float GetPitch(in Vector3 front)
		{
			return MathF.Asin(front.Y) * MyUtils.RAD_TO_DEG;
		}
		public static float GetYaw(in Vector3 front, float pitch)
		{
			return MathF.Acos(front.X / MathF.Cos(pitch * MyUtils.DEG_TO_RAD)) * MyUtils.RAD_TO_DEG;
		}

		// Orient the front and side vectors based on the screen pitch and yaw values
		private void UpdateOrientation()
		{
			front.X = MathF.Cos(pitch * MyUtils.DEG_TO_RAD) * MathF.Cos(yaw * MyUtils.DEG_TO_RAD);
			front.Y = MathF.Sin(pitch * MyUtils.DEG_TO_RAD);
			front.Z = MathF.Cos(pitch * MyUtils.DEG_TO_RAD) * MathF.Sin(yaw * MyUtils.DEG_TO_RAD);
			front = Vector3.Normalize(front);
			right = Vector3.Normalize(Vector3.Cross(front, up));
		}
	}
}
