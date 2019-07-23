using System;

namespace WatsonMesh.Helpers
{
	public class ConsoleWriter
	{
		private readonly bool _enableConsoleMessages;

		public ConsoleWriter(bool enableConsoleMessages)
		{
			_enableConsoleMessages = enableConsoleMessages;
		}

		public void WriteLine(string message)
		{
			if (_enableConsoleMessages)
			{
				Console.WriteLine(message);
			}
		}

		public void Write(string message)
		{
			if (_enableConsoleMessages)
			{
				Console.Write(message);
			}
		}
	}
}
