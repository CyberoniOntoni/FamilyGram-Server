using MyTelegram.Messenger.Services.Caching;
using MyTelegram.Schema.Updates;

namespace MyTelegram.Messenger.Handlers.LatestLayer.Updates;
/// <summary>
/// Returns a current state of updates.
/// <para><c>See <a href="https://corefork.telegram.org/method/updates.getState"/> </c></para>
/// </summary>
/// <remarks>
/// Access: [User ✔] [Bot ✔] [Anonymous ✖]
/// </remarks>
internal sealed class GetStateHandler(IPtsHelper ptsHelper, IQueryProcessor queryProcessor)
    : RpcResultObjectHandler<MyTelegram.Schema.Updates.RequestGetState, MyTelegram.Schema.Updates.IState>
{
    protected override async Task<IState> HandleCoreAsync(IRequestInput input, RequestGetState obj)
    {
        if (input.UserId == 0)
        {
            RpcErrors.RpcErrors403.UserInvalid.ThrowRpcError();
        //RpcErrors.RpcErrors401.AuthKeyInvalid.ThrowRpcError();
        }

        var cacheItem = await ptsHelper.GetPtsForUserAsync(input.UserId);
        // Advertise the true mailbox high-water pts so clients with a lower local pts
        // call getDifference. getDifference must actually return the rows (and must NOT
        // report this high pts on an empty difference — that combo was the stuck loop).
        var maxMessagePts = await queryProcessor.ProcessAsync(new GetMaxPtsByPeerIdQuery(input.UserId));
        var ptsReadModel = await queryProcessor.ProcessAsync(new GetPtsByPeerIdQuery(input.UserId));
        var pts = Math.Max(cacheItem.Pts, Math.Max(ptsReadModel?.Pts ?? 0, maxMessagePts));
        if (pts > cacheItem.Pts)
        {
            await ptsHelper.IncrementPtsAsync(input.UserId, pts);
        }

        var state = new TState
        {
            Date = CurrentDate,
            Pts = pts > 0 ? pts : 1,
            Qts = cacheItem.Qts,
            Seq = 1,
            UnreadCount = cacheItem.UnreadCount,
        };
        return state;
    }
}