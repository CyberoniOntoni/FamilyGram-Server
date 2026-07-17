using MyTelegram.SessionServer.Services;

namespace MyTelegram.SessionServer.EventHandlers;

public sealed class DataResultResponseEventHandler(
    IAuthKeyStore authKeyStore,
    SessionRequestDispatcher dispatcher,
    ILogger<DataResultResponseEventHandler> logger)
    : IEventHandler<DataResultResponseReceivedEvent>, ITransientDependency
{
    public async Task HandleEventAsync(DataResultResponseReceivedEvent eventData)
    {
        var state = await authKeyStore.GetAsync(eventData.TempAuthKeyId);
        if (state is null)
        {
            logger.LogWarning("No session for response authKey={AuthKeyId:x}", eventData.TempAuthKeyId);
            return;
        }

        state.ConnectionId = eventData.ConnectionId;
        if (eventData.SessionId != 0)
        {
            state.SessionId = eventData.SessionId;
        }

        ReadOnlyMemory<byte> bytes;
        if (eventData.DataObject is not null)
        {
            bytes = eventData.DataObject.ToBytes();
        }
        else
        {
            bytes = eventData.Data;
        }

        if (bytes.IsEmpty)
        {
            return;
        }

        await dispatcher.SendRawResultAsync(state, bytes);
    }
}
