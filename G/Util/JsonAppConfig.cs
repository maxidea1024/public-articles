using System.IO;
using System.Text;
using System.Reflection;
using Newtonsoft.Json;

namespace G.Util
{
	public class JsonAppConfig
	{
		public static T Load<T>(string filePath, int retryParentDirectory = 5)
		{
            //fix: single file application issue
			//var appDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var appDirectory = Path.GetDirectoryName(System.AppContext.BaseDirectory);
			string path = Path.Combine(appDirectory, filePath);

			path = FileEx.SearchParentDirectory(path, retryParentDirectory);
			if (path == null) return default(T);

			var sb = new StringBuilder();

			string[] lines = File.ReadAllLines(path);
			foreach (var line in lines)
			{
				var text = line.RemoveComment();
				sb.AppendLine(text);
			}

			string json = sb.ToString();
			if (json == null)
				throw new FileNotFoundException(filePath);

			return JsonConvert.DeserializeObject<T>(json);
		}

		/// <summary>
		/// 주어진 json 스트링에서 configuration 객체 생성하기
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="json"></param>
		/// <returns></returns>
		public static T LoadFromJsonString<T>(string json)
        {
			return JsonConvert.DeserializeObject<T>(json);
		}
	}
}
