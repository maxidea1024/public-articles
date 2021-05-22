using System;
using System.IO;

namespace G.Util
{
	public class FileEx
	{
		public static string SearchParentDirectory(string filePath, int retryParentDirectory = 5)
		{
			if (File.Exists(filePath))
				return filePath;

			string dir = Path.GetDirectoryName(filePath);
			string fileName = Path.GetFileName(filePath);

			for (int i = 1; i <= retryParentDirectory; i++)
			{
				dir = Path.Combine(dir, "..");

				//warning 윈도우즈에서는 전체경로로 expand해야만 동작함.
				dir = Path.GetFullPath(dir);

				filePath = Path.Combine(dir, fileName);

				if (File.Exists(filePath))
					return filePath;
			}

			return null;
		}

		public static string GetDirectory(string filePath)
		{
			if (string.IsNullOrEmpty(filePath)) return null;

			int index = filePath.LastIndexOfAny(new char[] { '/', '\\' });
			if (index < 0) return string.Empty;

			return filePath.Substring(0, index);
		}
	}
}
