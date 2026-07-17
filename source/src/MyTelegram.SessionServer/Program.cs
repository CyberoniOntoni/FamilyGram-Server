using MyTelegram.SessionServer.Extensions;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Async(c => c.Console(theme: AnsiConsoleTheme.Code))
    .WriteTo.Async(c => c.File("Logs/startup-log.txt"))
    .CreateLogger();

try
{
    Log.Information("FamilyGram SessionServer starting...");

    var builder = Host.CreateApplicationBuilder(args);
    builder.Configuration.AddEnvironmentVariables();
    builder.Configuration.AddCommandLine(args);

    builder.Services.AddSerilog((services, configuration) =>
    {
        configuration.ReadFrom.Configuration(builder.Configuration)
            .WriteTo.Async(c => c.Console(theme: AnsiConsoleTheme.Code))
            .WriteTo.Async(c => c.File("Logs/log-.txt", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7));
    });

    builder.Services.AddMyTelegramSessionServer(builder.Configuration);

    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "SessionServer terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
