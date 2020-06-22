```csharp
public class TelnetSession : AppSession<TelnetSession>
{
    protected override void OnSessionStarted()
    {

    }

    protected override void OnUnknownRequest(StringRequestInfo requestInfo)
    {

    }

    protected override void OnException(Exception e)
    {

    }

    protected override void OnSessionClosed(CloseReason reason)
    {
        base.OnSessionClosed(reason);
    }
}
```

```csharp
public class PlayerSession : AppSession<PlayerSession>
{
    public int GameHallId { get; internal set; }
    public int RoomId { get; internal set; }
}
```

```csharp
public class ECHO : CommandBase<AppSession, StringRequestInfo>
{
    public override void ExecuteCommand(AppSession session, StringRequestInfo requestInfo)
    {
        session.Send(requestInfo.Body);
    }
}
```

```csharp
public class TelnetServer : AppServer<TelnetSession>
{
    protected override bool Setup(IRootConfig rootConfig, IServerConfig config)
    {
        return base.Setup(rootConfig, config);
    }

    protected override void OnStartup()
    {
        base.OnStartup();
    }

    protected override void OnStopped()
    {
        base.OnStopped();
    }
}
```

```csharp
static void Main(string[] args)
{
    var bootstrap = BootstrapFactory.CreateBootstrap();
    if (!bootstrap.Initialize())
    {
        return;
    }

    var result = bootstrap.Start();
    if (result == StartResult.Failed)
    {
        return;
    }

    // WAIT...

    bootstrap.Stop();
}
```

```csharp
var session = appServer.GetSessionByID(sessionID);
if (session != null)
{
    session.Send(data, 0, data.Length);
}


foreach (var session in appServer.GetAllSessions())
{
    session.Send(data, 0, data.Length);
}


var sessions = appServer.GetSessions(session => session.CompanyID == companyID);
foreach (var session in sessions)
{
    session.Send(data, 0, data.Length);
}
```


### Command Filter

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public abstract class CommandFilterAttribute : Attribute
{

}
````

```csharp
public class MyPipelineFilter : FixedHeaderPipeineFilter<MyPackage>
{
    public MyPipelineFilter()
        : base(3)
    {

    }

    protected override int GetBodyLengthFromHeader(ref ReadOnlySequence<byte> buffer)
    {
        var reader = new SequenceReader<byte>(buffer);
        reader.Advance(1); // skip first byte
        reader.TryReadBigEndian(out short len);
        return len;
    }

    protected overide MyPackage DecodePackage(ref ReadOnlySequence<byte> buffer)
    {
        var package = new MyPackage();

        var reader = new SequenceReader<byte>(buffer);

        read.TryRead(out byte packageKey);
        package.Key = packageKey;
        reader.Advance(2); // skip the Length
        package.Body = reader.ReadString();

        return package;
    }
}
```

```csharp
var host = SuperSocketHostBuilder.Create<MyPackage, MyPipelineFilter>()
    .UsePackageHandler(async (session, package) =>
    {
        // ...
    } ).Build();
```

```csharp
public class MyPackageDecoder : IPackageDecoder<MyPackage>
{
    public MyPackage Decode(ref ReadOnlySequence<byte> buffer, object context)
    {
        var package = new MyPackage();

        var reader = new SequenceReader<byte>(buffer);

        read.TryRead(out byte packageKey);
        package.Key = packageKey;
        reader.Advance(2); // skip the Length
        package.Body = reader.ReadString();

        return package;
    }
}
```

```csharp
builder.UsePackageDecoder<MyPackageDecoder>();
```

```csharp
public interface ICommand<TSessionBase, TPackageInfo>
    where TSessionBase : ISessionBase
{
    void Execute(TSessionBase session, TPackageInfo package);
}
```

```csharp
public interface IAsyncCommand<TSessionBase, TPackageInfo> : ICommand
    where TSessionBase : ISessionBase
{
    ValueTask ExecuteAsync(TSessionBase session, TPackageInfo package);
}
```

```csharp
[Command(Key = 0x03)]
public class ShowVolatage : IAsyncCommand<StringPackageInfo>
{
    public async ValueTask ExecuteAsync(ISessionBase session, StringPackageInfo package)
    {
        ...
    }
}
```


## Register Command

```csharp
hostBuilder.UseCommand(commandOptions =>
{
    commandOptions.AddCommand<ADD>();
    commandOptions.AddCommand<MULT>();
    commandOptions.AddCommand<SUM>();
});
```


## Command Filter

```csharp
public class HelloCommandAttribute : CommandFilterAttribute
{
    public override void OnCommandExecute(CommandExecutingContext context)
    {

    }

    public override bool OnCommandExecuting(CommandExecutingContext context)
    {
        return true;
    }
}
```
