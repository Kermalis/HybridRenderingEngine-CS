using System;

namespace HybridRenderingEngine
{
	internal static class Program
	{
		public static void Main()
		{
			Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

			Engine.Init();
			Engine.Run();
			Engine.Quit();
		}
	}
}