namespace MyTelegram.QueryHandlers.InMemory.Messaging;

public class GetMessageIdListByMinPtsQueryHandler(IQueryOnlyReadModelStore<MessageReadModel> store)
    : IQueryHandler<GetMessageIdListByMinPtsQuery, IReadOnlyCollection<int>>
{
    public async Task<IReadOnlyCollection<int>> ExecuteQueryAsync(GetMessageIdListByMinPtsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Limit <= 0)
        {
            return Array.Empty<int>();
        }

        return await store.FindAsync(
            p => p.OwnerPeerId == query.OwnerPeerId && p.Pts > query.MinPts,
            p => p.MessageId,
            skip: 0,
            limit: query.Limit,
            sort: new SortOptions<MessageReadModel>(p => p.MessageId, SortType.Ascending),
            cancellationToken: cancellationToken);
    }
}
