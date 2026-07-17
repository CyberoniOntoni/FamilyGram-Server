using MyTelegram.EventBus;
using MyTelegram.SessionServer.Services;

namespace MyTelegram.SessionServer.EventHandlers;

public sealed class AuthKeyCreatedEventHandler(
    IAuthKeyStore authKeyStore,
    IAuthKeyIdHelper authKeyIdHelper,
    IEventBus eventBus,
    ILogger<AuthKeyCreatedEventHandler> logger)
    : IEventHandler<AuthKeyCreatedIntegrationEvent>, ITransientDependency
{
    public async Task HandleEventAsync(AuthKeyCreatedIntegrationEvent eventData)
    {
        var authKey = eventData.Data.ToArray();
        if (authKey.Length == 0)
        {
            return;
        }

        var authKeyId = authKeyIdHelper.GetAuthKeyId(authKey);
        var state = new SessionState
        {
            AuthKeyId = authKeyId,
            AuthKey = authKey,
            ServerSalt = eventData.ServerSalt,
            IsPermanent = eventData.IsPermanent,
            ConnectionId = eventData.ConnectionId,
            ConnectionType = eventData.ConnectionType,
            Layer = Layers.LayerLatest,
            PermAuthKeyId = eventData.IsPermanent ? authKeyId : 0
        };

        await authKeyStore.UpsertAsync(state);
        logger.LogInformation(
            "Auth key stored {AuthKeyId:x} permanent={Permanent} connection={ConnectionId}",
            authKeyId,
            eventData.IsPermanent,
            eventData.ConnectionId);

        // Permanent auth keys: auth-server returns null; session must deliver dh_gen_ok to the client.
        if (eventData.IsPermanent && !eventData.SetClientDhParamsAnswer.IsEmpty)
        {
            await eventBus.PublishAsync(new UnencryptedMessageResponse(
                0,
                eventData.SetClientDhParamsAnswer,
                eventData.ConnectionId,
                eventData.ReqMsgId));
        }
    }
}
