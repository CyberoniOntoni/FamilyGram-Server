using MyTelegram.FileServer.Extensions;
using MyTelegram.FileServer.Grpc;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Async(c => c.Console(theme: AnsiConsoleTheme.Code))
    .WriteTo.Async(c => c.File("Logs/startup-log.txt"))
    .CreateLogger();

try
{
    Log.Information("FamilyGram FileServer starting...");

    var builder = WebApplication.CreateBuilder(args);
    builder.Configuration.AddEnvironmentVariables();
    builder.Configuration.AddCommandLine(args);

    builder.Host.UseSerilog((context, configuration) =>
    {
        configuration.ReadFrom.Configuration(context.Configuration)
            .WriteTo.Async(c => c.Console(theme: AnsiConsoleTheme.Code))
            .WriteTo.Async(c => c.File("Logs/log-.txt", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7));
    });

    builder.WebHost.ConfigureKestrel(options =>
    {
        // gRPC over HTTP/2 without TLS (compose internal network)
        options.ConfigureEndpointDefaults(lo =>
        {
            lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
        });
    });

    builder.Services.AddGrpc(options =>
    {
        options.MaxReceiveMessageSize = 64 * 1024 * 1024;
        options.MaxSendMessageSize = 64 * 1024 * 1024;
    });
    builder.Services.AddMyTelegramFileServer(builder.Configuration);

    var app = builder.Build();
    app.MapGrpcService<MediaGrpcService>();
    app.MapGet("/", () => "FamilyGram FileServer (MediaService gRPC on this port)");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "FileServer terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
