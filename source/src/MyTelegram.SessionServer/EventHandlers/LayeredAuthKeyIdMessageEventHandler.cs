using MyTelegram.SessionServer.Services;

namespace MyTelegram.SessionServer.EventHandlers;

public sealed class LayeredAuthKeyIdMessageEventHandler(
    IAuthKeyStore authKeyStore,
    SessionRequestDispatcher dispatcher,
    ILogger<LayeredAuthKeyIdMessageEventHandler> logger)
    : IEventHandler<LayeredAuthKeyIdMessageCreatedIntegrationEvent>, ITransientDependency
{
    public async Task HandleEventAsync(LayeredAuthKeyIdMessageCreatedIntegrationEvent eventData)
    {
        var state = await authKeyStore.GetAsync(eventData.AuthKeyId);
        if (state is null || string.IsNullOrEmpty(state.ConnectionId))
        {
            logger.LogDebug("No live session for authKey push {AuthKeyId:x}", eventData.AuthKeyId);
            return;
        }

        await dispatcher.SendRawResultAsync(state, eventData.Data);
    }
}
