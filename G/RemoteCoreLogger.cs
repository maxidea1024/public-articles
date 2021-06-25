
private readonly IRtCoreLogger _innerLogger;
private readonly ReamtimeEngine.ProxyC2S _proxyC2S;

public RemoteCoreLogger(IRtCoreLogger innerLogger, RealtimeEngine.ProxyC2S c2sProxy)
{
    _innerLogger = PreValidations.CheckNotNull(innerLogger);
    _proxyC2S = PreValidations.CheckNotNull(c2sProxy);
}

public void Log(string message)
{
    _innerLogger.Log(message);

    _proxyC2S.ReportClientCoreLogsToServer(LinkId.Server, message);
}
