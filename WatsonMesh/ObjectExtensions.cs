using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WatsonMesh
{
	internal static class ObjectExtensions
	{
		public static string StreamToString(this Stream val)
		{
			using (var sr = new StreamReader(val))
			{
				return sr.ReadToEnd();
			}
		}
	}
}
