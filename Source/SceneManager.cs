using Silk.NET.OpenGL;
using System;

namespace HybridRenderingEngine
{
	internal sealed class SceneManager
	{
		public static SceneManager Instance;

		// String could probably be an enum instead, but it's easier this way to build
		// the relative paths if it is a string.
		private string currentSceneID;
		private Scene currentScene;

		// Starts up the scene manager and loads the default scene. If for whatever reason
		// the scene could not load any model, or there are none defined it quits early
		public SceneManager(GL gl)
		{
			Instance = this;

			currentSceneID = "sponza";
			LoadScene(gl, currentSceneID);
		}

		public void Quit(GL gl)
		{
			currentScene.Delete(gl);
		}

		// Checks if the scene that you want to load is not the one that is currently loaded.
		// If it isn't, then it deletes the current one and loads the new one.
		public void SwitchScene(GL gl, string newSceneID)
		{
			if (newSceneID != currentSceneID)
			{
				currentSceneID = newSceneID;
				currentScene.Delete(gl);
				LoadScene(gl, newSceneID);
			}
			else
			{
				Console.WriteLine("Selected already loaded scene.");
			}
		}

		// Misdirection towards the current scene to avoid pointer dangling after scene switching
		public void Update(uint deltaT)
		{
			currentScene.Update(deltaT);
		}

		public Scene GetCurrentScene()
		{
			return currentScene;
		}

		private void LoadScene(GL gl, string sceneID)
		{
			currentScene = new Scene(gl, sceneID);
		}
	}
}
