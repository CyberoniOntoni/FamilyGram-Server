using MyTelegram.SessionServer.Services;

namespace MyTelegram.SessionServer.EventHandlers;

public sealed class BindUserIdEventHandler(IAuthKeyStore authKeyStore)
    : IEventHandler<BindUserIdToAuthKeyIntegrationEvent>, ITransientDependency
{
    public Task HandleEventAsync(BindUserIdToAuthKeyIntegrationEvent eventData)
    {
        return authKeyStore.BindUserAsync(
            eventData.AuthKeyId,
            eventData.UserId,
            eventData.PermAuthKeyId,
            eventData.AuthKeyId,
            Layers.LayerLatest,
            DeviceType.Unknown);
    }
}
