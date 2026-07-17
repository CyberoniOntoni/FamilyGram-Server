using MyTelegram.SessionServer.Services;

namespace MyTelegram.SessionServer.EventHandlers;

public sealed class DataResultWithUserIdEventHandler(
    IAuthKeyStore authKeyStore,
    SessionRequestDispatcher dispatcher,
    ILogger<DataResultWithUserIdEventHandler> logger)
    : IEventHandler<DataResultResponseWithUserIdReceivedEvent>, ITransientDependency
{
    public async Task HandleEventAsync(DataResultResponseWithUserIdReceivedEvent eventData)
    {
        var state = await authKeyStore.GetAsync(eventData.TempAuthKeyId)
                    ?? await authKeyStore.GetAsync(eventData.AuthKeyId);
        if (state is null)
        {
            logger.LogWarning("No session for DataResultWithUserId authKey={AuthKeyId:x}", eventData.TempAuthKeyId);
            return;
        }

        state.ConnectionId = eventData.ConnectionId;
        if (eventData.SessionId != 0)
        {
            state.SessionId = eventData.SessionId;
        }

        if (eventData.UserId != 0)
        {
            state.UserId = eventData.UserId;
        }

        await dispatcher.SendRawResultAsync(state, eventData.Data);
    }
}
