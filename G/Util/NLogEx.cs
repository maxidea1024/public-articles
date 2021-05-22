using NLog.Config;
using System.IO;
using System.Reflection;

namespace G.Util
{
    public class NLogEx
    {
        public static bool SetConfiguration(string filePath, int retryParentDirectory = 5)
        {
            string path = FileEx.SearchParentDirectory(filePath, retryParentDirectory);
            if (path == null)
                throw new FileNotFoundException(filePath);

            NLog.LogManager.Configuration = new NLog.Config.XmlLoggingConfiguration(path);

            return true;
        }

        public static bool SetAppConfiguration(string filePath, int retryParentDirectory = 5)
        {
            //fix: single file application issue
			//var appDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var appDirectory = Path.GetDirectoryName(System.AppContext.BaseDirectory);
            filePath = Path.Combine(appDirectory, filePath);
            return SetConfiguration(filePath, retryParentDirectory);
        }

        /// <summary>
        /// .xml 스트링으로 configuration 처리.
        /// </summary>
        /// <param name="xml">.log.config 파일의 xml string</param>
        /// <returns>Whether succeeded or not.</returns>
        public static bool SetAppConfigurationWithXmlString(string xml)
        {
            var loggingConfigration = XmlLoggingConfiguration.CreateFromXmlString(xml);
            if (loggingConfigration == null)
            {
                return false;
            }

            NLog.LogManager.Configuration = loggingConfigration;
            return true;
        }
    }
}
