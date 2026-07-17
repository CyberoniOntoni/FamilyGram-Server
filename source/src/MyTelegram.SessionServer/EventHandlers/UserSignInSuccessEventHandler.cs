using MyTelegram.SessionServer.Services;

namespace MyTelegram.SessionServer.EventHandlers;

public sealed class UserSignInSuccessEventHandler(IAuthKeyStore authKeyStore)
    : IEventHandler<UserSignInSuccessEvent>, ITransientDependency
{
    public Task HandleEventAsync(UserSignInSuccessEvent eventData)
    {
        return authKeyStore.BindUserAsync(
            eventData.TempAuthKeyId,
            eventData.UserId,
            eventData.PermAuthKeyId,
            eventData.TempAuthKeyId,
            Layers.LayerLatest,
            DeviceType.Unknown);
    }
}
