using MongoDB.Driver;
using MyTelegram.Abstractions;
using MyTelegram.Caching.Redis;
using MyTelegram.EventBus.Extensions;
using MyTelegram.EventBus.RabbitMQ;
using MyTelegram.EventBus.RabbitMQ.Extensions;
using MyTelegram.SessionServer.EventHandlers;
using MyTelegram.SessionServer.Services;
using MyTelegram.Services.Extensions;
using MyTelegram.Services.NativeAot;

namespace MyTelegram.SessionServer.Extensions;

public static class SessionServerServiceCollectionExtensions
{
    public static IServiceCollection AddMyTelegramSessionServer(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EventBusRabbitMqOptions>(configuration.GetSection("RabbitMQ:EventBus"));
        services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMQ:Connections:Default"));

        services.AddMyTelegramHandlerServices();
        services.RegisterServices(typeof(SessionServerServiceCollectionExtensions).Assembly);
        services.AddMyTelegramRabbitMqEventBus();
        services.AddMyTelegramStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetValue<string>("Redis:Configuration");
        });

        services.AddSingleton<IMongoClient>(_ =>
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

        services.AddSingleton<IAuthKeyStore, MongoAuthKeyStore>();
        services.AddSingleton<IMtProtoSessionCrypto, MtProtoSessionCrypto>();
        services.AddSingleton<SessionRequestDispatcher>();
        services.AddSingleton<ISessionRequestDispatcher>(sp => sp.GetRequiredService<SessionRequestDispatcher>());

        services.AddSubscription<EncryptedMessage, EncryptedMessageEventHandler>();
        services.AddSubscription<AuthKeyCreatedIntegrationEvent, AuthKeyCreatedEventHandler>();
        services.AddSubscription<DataResultResponseReceivedEvent, DataResultResponseEventHandler>();
        services.AddSubscription<DataResultResponseWithUserIdReceivedEvent, DataResultWithUserIdEventHandler>();
        services.AddSubscription<FileDataResultResponseReceivedEvent, FileDataResultResponseEventHandler>();
        services.AddSubscription<LayeredPushMessageCreatedIntegrationEvent, LayeredPushMessageEventHandler>();
        services.AddSubscription<LayeredAuthKeyIdMessageCreatedIntegrationEvent, LayeredAuthKeyIdMessageEventHandler>();
        services.AddSubscription<UserSignInSuccessEvent, UserSignInSuccessEventHandler>();
        services.AddSubscription<BindUserIdToAuthKeyIntegrationEvent, BindUserIdEventHandler>();

        services.AddSystemTextJson(options =>
        {
            options.TypeInfoResolverChain.Add(MyJsonSerializeContext.Default);
        });

        return services;
    }
}
