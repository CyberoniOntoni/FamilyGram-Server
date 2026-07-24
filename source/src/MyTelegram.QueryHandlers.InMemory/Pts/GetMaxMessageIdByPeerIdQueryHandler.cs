namespace MyTelegram.QueryHandlers.InMemory.Pts;

/// <summary>
/// Returns the highest MessageId for this owner from the message mailbox (not PtsReadModel).
/// </summary>
public class GetMaxMessageIdByPeerIdQueryHandler(IQueryOnlyReadModelStore<MessageReadModel> store)
    : IQueryHandler<GetMaxMessageIdByPeerIdQuery, int>
{
    public async Task<int> ExecuteQueryAsync(GetMaxMessageIdByPeerIdQuery query,
        CancellationToken cancellationToken)
    {
        return await store.FirstOrDefaultAsync(
            p => p.OwnerPeerId == query.PeerId,
            p => p.MessageId,
            sort: new SortOptions<MessageReadModel>(p => p.MessageId, SortType.Descending),
            cancellationToken: cancellationToken);
    }
}