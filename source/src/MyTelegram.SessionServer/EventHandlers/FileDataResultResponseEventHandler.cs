using MyTelegram.SessionServer.Services;

namespace MyTelegram.SessionServer.EventHandlers;

public sealed class FileDataResultResponseEventHandler(
    IAuthKeyStore authKeyStore,
    SessionRequestDispatcher dispatcher,
    ILogger<FileDataResultResponseEventHandler> logger)
    : IEventHandler<FileDataResultResponseReceivedEvent>, ITransientDependency
{
    public async Task HandleEventAsync(FileDataResultResponseReceivedEvent eventData)
    {
        var state = await authKeyStore.GetAsync(eventData.TempAuthKeyId);
        if (state is null)
        {
            logger.LogWarning("No session for file response authKey={AuthKeyId:x}", eventData.TempAuthKeyId);
            return;
        }

        state.ConnectionId = eventData.ConnectionId;
        if (eventData.SessionId != 0)
        {
            state.SessionId = eventData.SessionId;
        }

        // File lane sends the TL file object; wrap as rpc_result for the client RPC.
        IObject resultObj;
        try
        {
            resultObj = eventData.Data.ToTObject<IObject>();
        }
        catch
        {
            await dispatcher.SendRawResultAsync(state, eventData.Data);
            return;
        }

        var rpc = new TRpcResult
        {
            ReqMsgId = eventData.ReqMsgId,
            Result = resultObj
        };
        await dispatcher.SendRawResultAsync(state, rpc.ToBytes());
    }
}
