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
        // Reconcile lagging PtsReadModel against actual message pts (HiLo jumps after restarts).
        var maxMessagePts = await queryProcessor.ProcessAsync(new GetMaxPtsByPeerIdQuery(input.UserId));
        var pts = Math.Max(cacheItem.Pts, maxMessagePts);
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