#pragma warning disable CS0168 // The variable was declared but not used.

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using G.Util;

namespace G.P2Agent
{
    public static class P2AgentClient
    {
        #region Properties
        /// <summary>
        /// Current environment
        /// </summary>
        public static string Environment => _environment;

        /// <summary>
        /// Returns true if current environment is 'Development'
        /// </summary>
        public static bool IsDevelopmentEnvironment => Environment == "Development";

        /// <summary>
        /// Returns true if current environment is 'Live'
        /// </summary>
        public static bool IsLiveEnvironment => Environment == "Live";

        /// <summary>
        /// Base directory
        /// </summary>
        public static string BaseDirectory => AppContext.BaseDirectory;

        /// <summary>
        /// Application Revision string
        /// </summary>
        public static string AppRevision => _appRevision;

        /// <summary>
        /// Application name
        /// </summary>
        public static string AppName => _appName;

        /// <summary>
        /// Key for access to disposable files
        /// </summary>
        public static string DisposableFilesKey => _disposableFilesKey;

        /// <summary>
        /// Spawned process id
        /// </summary>
        public static string ProcessId => _processId;
        #endregion

        #region Fields
        private static string _environment;
        private static string _appRevision;
        private static string _appName;
        private static string _disposableFilesKey;
        private static string _processId;

        private static bool _isStarted;
        private static int _heartbeatInterval;
        private static CancellationTokenSource _cts;
        private static Task _heartbeatTask;

        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        #endregion

        /// <summary>
        /// Override application program name.
        /// It must be called before the Start() function is called, and if it is not called, AssemblyName becomes the application program.
        /// </summary>
        public static void SetAppName(string appName)
        {
            _appName = appName;
        }

        #region Start and Stop
        public static void Start(int heartbeatInterval = 2500)
        {
            StartAsync(heartbeatInterval).Wait();
        }

        public static async Task StartAsync(int heartbeatInterval = 2500)
        {
            if (_isStarted)
            {
                return;
            }

            // revision
            // --p2-app-name=AppName
            // --p2-disposable-files-key=Key
            // --p2-process-id=Id

            _environment = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

            _appRevision = GetAppRevision();

            // If it has already been specified externally, the external designation takes precedence.
            if (string.IsNullOrEmpty(_appName))
            {
                // 1. The name specified with SetAppName() takes precedence
                // 2. Specifying "--p2-app-name=name" as the argument is the following
                // 3. The name of the last running assembly

                string executingAssemblyName = Assembly.GetExecutingAssembly().GetName().Name;
                _appName = GetCommandlineOption("--p2-app-name", executingAssemblyName);
            }

            _disposableFilesKey = GetCommandlineOption("--p2-disposable-files-key");

            _processId = GetCommandlineOption("--p2-process-id");

            _heartbeatInterval = heartbeatInterval;

			// If _processId is empty, it is executed directly, so it can be used for classification such as not waiting for initial response.

            await SetupNLogAsync();

            if (_processId != "")
            {
                _cts = new CancellationTokenSource();
                _heartbeatTask = Task.Run(HeartbeatAsync);
            }

            _isStarted = true;
        }

        private static async Task SetupNLogAsync()
        {
            NLogEx.SetAppConfiguration("NLog.config");

            if (_environment == "Live")
            {
                NLog.LogManager.Configuration.Variables["consoleLogLevel"] = "Off";
                NLog.LogManager.Configuration.Variables["fileLogLevel"] = "Off";
                NLog.LogManager.Configuration.Variables["seqLogLevel"] = "Trace";
            }
            else
            {
                NLog.LogManager.Configuration.Variables["consoleLogLevel"] = "Trace";
                NLog.LogManager.Configuration.Variables["fileLogLevel"] = "Trace";
                NLog.LogManager.Configuration.Variables["seqLogLevel"] = "Off"; // In case of non-live environment, seq log is disabled.
            }

            NLog.GlobalDiagnosticsContext.Set("Environment", _environment);
            NLog.GlobalDiagnosticsContext.Set("UserName", System.Environment.UserName);
            NLog.GlobalDiagnosticsContext.Set("AppName", _appName);
            NLog.GlobalDiagnosticsContext.Set("ProcessId", _processId);
            NLog.GlobalDiagnosticsContext.Set("AppRevision", _appRevision);

            /*
            // Retrieve EC2Instance information
            await InstanceInfo.InitializeAsync();

            NLog.GlobalDiagnosticsContext.Set("ServiceRegion", InstanceInfo.ServiceRegion);
            NLog.GlobalDiagnosticsContext.Set("ServiceSwitch", InstanceInfo.ServiceSwitch);

            NLog.GlobalDiagnosticsContext.Set("EC2Region", InstanceInfo.Region);
            NLog.GlobalDiagnosticsContext.Set("EC2AZ", InstanceInfo.AZ);
            NLog.GlobalDiagnosticsContext.Set("EC2InstanceId", InstanceInfo.InstanceId);
            NLog.GlobalDiagnosticsContext.Set("EC2InstanceName", InstanceInfo.InstanceName);
            NLog.GlobalDiagnosticsContext.Set("EC2InstanceType", InstanceInfo.InstanceType);
            NLog.GlobalDiagnosticsContext.Set("EC2InstanceTags", JsonSerializer.Serialize(InstanceInfo.InstanceTags));
            NLog.GlobalDiagnosticsContext.Set("EC2VpcId", InstanceInfo.VpcId);
            NLog.GlobalDiagnosticsContext.Set("EC2SubnetId", InstanceInfo.SubnetId);
            NLog.GlobalDiagnosticsContext.Set("EC2PublicDnsName", InstanceInfo.PublicDnsName);
            NLog.GlobalDiagnosticsContext.Set("EC2PublicIp", InstanceInfo.PublicIP);
            NLog.GlobalDiagnosticsContext.Set("EC2PrivateDnsName", InstanceInfo.PrivateDnsName);
            NLog.GlobalDiagnosticsContext.Set("EC2LocalIpAddresses", JsonSerializer.Serialize(InstanceInfo.LocalIPAddresses));
            */
        }

        //todo Among the disposable files, deleted files older than one day?

        // __revision__.txt 파일은 패키징 과정중에서 자동으로 주입됨.
        // 해당 파일이 없다는것은 패키징을 거치지 않고, 바로 빌드된 상태.
        private static string GetAppRevision()
        {
            string filename = Path.Combine(AppContext.BaseDirectory, "__revision__.txt");
            
            return GetAppRevisionInternal(filename);
        }

        //todo 이 함수가 여기에 위치키시는것 보다는 P2Bootstrapper에 위치시키는게 좋을듯.
        public static string GetAppPackageRevision(string appPackageName)
        {
            string appPackageBasePath = Path.Combine(AppContext.BaseDirectory, $"../{appPackageName}");
            string filename = Path.Combine(appPackageBasePath, "__revision__.txt");

            return GetAppRevisionInternal(filename);
        }

        private static string GetAppRevisionInternal(string filename)
        {
            try
            {
                string revision = File.ReadAllText(filename);
                revision = revision.Trim();
                return revision;
            }
            catch (Exception)
            {
                return "";
            }
        }

        public static void Stop()
        {
            StopAsync().Wait();
        }

        public static async Task StopAsync()
        {
            if (!_isStarted)
            {
                return;
            }

            if (_heartbeatTask == null)
            {
                return;
            }

            try
            {
                _cts.Cancel();
            }
            finally
            {
                try
                {
                    await Task.WhenAll(_heartbeatTask, Task.Delay(Timeout.Infinite, _cts.Token));
                }
                catch (TaskCanceledException)
                {
                    // ignore
                }
                catch (Exception)
                {
                    throw;
                }
            }

            _isStarted = false;
        }
        #endregion

        #region Notifications
        public static void NotifyReady()
        {
            //warning: Never run in blocking mode.
            Task.Run(async() => await NotifyReadyAsync());
        }

        public static async Task NotifyReadyAsync()
        {
            if (_processId == "")
            {
                return;
            }

            try
            {
                using var httpClient = new HttpClient();
                await httpClient.PostAsync($"http://localhost:50001/api/v1/internal/ready?processId={_processId}", null, _cts.Token);
            }
            catch (TaskCanceledException)
            {
                // ignore
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
        }

        public static async Task ReportStatus(string key, object status)
        {
            //warning: Never run in blocking mode.
            Task.Run(async () => await ReportStatusAsync(key, status));
        }

        public static async Task ReportStatusAsync(string key, object status)
        {
            if (_processId == "")
            {
                return;
            }

            try
            {
                var json = JsonSerializer.Serialize(new
                {
                    Key = key,
                    Status = JsonSerializer.Serialize(status)
                });

                using var httpClient = new HttpClient();
                await httpClient.PostAsync($"http://localhost:50001/api/v1/internal/status?processId={_processId}", ToStringContent(json), _cts.Token);
            }
            catch (TaskCanceledException)
            {
                // ignore
            }
            catch (Exception ex)
            {
                //_logger.Error(ex);
            }
        }
        #endregion

        #region Heartbeats
        private static async Task HeartbeatAsync()
        {
            if (_processId == "")
            {
                return;
            }

            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    //_logger.Info("Send heartbeat...");

                    //using var httpClient = new HttpClient();
                    //await httpClient.PostAsync($"http://localhost:50001/api/v1/internal/heartbeat?processId={_processId}", null, _cts.Token);

                    await Task.Delay(_heartbeatInterval, _cts.Token);
                }
            }
            catch (TaskCanceledException)
            {
                // ignore
            }
            catch (Exception ex)
            {
                //_logger.Error(ex);
            }
        }
        #endregion

        #region Helpers
        private static StringContent ToStringContent(string value)
        {
            return new StringContent(value, Encoding.UTF8, "application/json");
        }
        #endregion

        #region Commandline
        public static bool HasCommandlineOption(string key)
        {
            var args = System.Environment.GetCommandLineArgs();
            if (args.Contains(key))
            {
                return true;
            }

            foreach (var arg in args)
            {
                if (arg.StartsWith(key))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetCommandlineOption(string key, out string value)
        {
            value = string.Empty;

            var args = System.Environment.GetCommandLineArgs();
            foreach (var arg in args)
            {
                if (arg.StartsWith(key))
                {
                    int equalIndex = arg.IndexOf("=");
                    if (equalIndex >= 0)
                    {
                        value = arg.Substring(equalIndex + 1);
                        value = value.Trim();
                    }
                    else
                    {
                        value = string.Empty;
                    }

                    return true;
                }
            }

            return false;
        }

        public static string GetCommandlineOption(string key, string defaultValue = "")
        {
            if (TryGetCommandlineOption(key, out var value))
            {
                return value;
            }

            return defaultValue;
        }
        #endregion

        #region Load Text file
        public static string LoadDisposableTextFile(string filePath)
        {
            string path = GetDisposableFilePath(filePath);
            if (path == null)
            {
                throw new FileNotFoundException(path);
            }

            return File.ReadAllText(path);
        }

        public static string GetDisposableFilePath(string filePath, int retryParentDirectory = 5)
        {
            if (!string.IsNullOrEmpty(_disposableFilesKey))
            {
                string dir = Path.GetDirectoryName(filePath);
                string fileName = Path.GetFileName(filePath);

                string disposableFileName = $"disposable-{_disposableFilesKey}-{fileName}";
                string disposableFilePath = Path.Combine(dir, disposableFileName);
                string found = SearchParentDirectory(disposableFilePath, retryParentDirectory);
                if (found != null)
                {
                    return found;
                }
            }

            return SearchParentDirectory(filePath, retryParentDirectory);
        }

        public static string SearchParentDirectory(string filePath, int retryParentDirectory = 5)
        {
            if (File.Exists(filePath))
            {
                return filePath;
            }

            string dir = Path.GetDirectoryName(filePath);
            string fileName = Path.GetFileName(filePath);

            for (int i = 1; i <= retryParentDirectory; i++)
            {
                dir = Path.Combine(dir, "..");
                filePath = Path.Combine(dir, fileName);

                if (File.Exists(filePath))
                {
                    return filePath;
                }
            }

            return null;
        }
        #endregion
    }
}

#pragma warning restore CS0168 // The variable was declared but not used.
