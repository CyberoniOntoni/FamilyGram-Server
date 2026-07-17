using Microsoft.Extensions.Options;
using Minio;
using MongoDB.Driver;
using MyTelegram.EventBus.RabbitMQ;
using MyTelegram.EventBus.RabbitMQ.Extensions;
using MyTelegram.FileServer.BackgroundServices;
using MyTelegram.FileServer.Options;
using MyTelegram.FileServer.Services;
using MyTelegram.Services.Extensions;
using MyTelegram.Services.NativeAot;

namespace MyTelegram.FileServer.Extensions;

public static class FileServerServiceCollectionExtensions
{
    public static IServiceCollection AddMyTelegramFileServer(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MinioOptions>(configuration.GetSection("Minio"));
        services.Configure<FileServerAppOptions>(configuration.GetSection("App"));
        services.Configure<EventBusRabbitMqOptions>(configuration.GetSection("RabbitMQ:EventBus"));
        services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMQ:Connections:Default"));

        services.AddMyTelegramHandlerServices();
        services.AddMyTelegramRabbitMqEventBus();

        services.AddSingleton<IMongoClient>(sp =>
        {
            var cs = configuration.GetConnectionString("Default")
                     ?? configuration["ConnectionStrings:Default"]
                     ?? "mongodb://localhost:27017";
            return new MongoClient(cs);
        });
        services.AddSingleton(sp =>
        {
            var client = sp.GetRequiredService<IMongoClient>();
            var dbName = configuration["App:DatabaseName"] ?? "tg";
            return client.GetDatabase(dbName);
        });

        services.AddSingleton<IMinioClient>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<MinioOptions>>().Value;
            var endpoint = opts.Endpoint;
            // MinIO client expects host without scheme
            endpoint = endpoint.Replace("http://", "", StringComparison.OrdinalIgnoreCase)
                .Replace("https://", "", StringComparison.OrdinalIgnoreCase);

            return new MinioClient()
                .WithEndpoint(endpoint)
                .WithCredentials(opts.AccessKey, opts.SecretKey)
                .WithSSL(opts.UseSsl)
                .Build();
        });

        services.AddSingleton<IObjectStorage, MinioObjectStorage>();
        services.AddSingleton<IFilePartStore, MongoFilePartStore>();
        services.AddSingleton<IMediaFactory, MediaFactory>();
        services.AddSingleton<IMtpFileRequestHandler, MtpFileRequestHandler>();
        services.AddHostedService<FileServerLaneConsumer>();

        services.AddSystemTextJson(options =>
        {
            options.TypeInfoResolverChain.Add(MyJsonSerializeContext.Default);
        });

        return services;
    }
}
