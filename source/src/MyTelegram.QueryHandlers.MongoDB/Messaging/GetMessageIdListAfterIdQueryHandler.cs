namespace MyTelegram.QueryHandlers.MongoDB.Messaging;

public class GetMessageIdListAfterIdQueryHandler(IQueryOnlyReadModelStore<MessageReadModel> store)
    : IQueryHandler<GetMessageIdListAfterIdQuery, IReadOnlyCollection<int>>
{
    public async Task<IReadOnlyCollection<int>> ExecuteQueryAsync(GetMessageIdListAfterIdQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Limit <= 0)
        {
            return Array.Empty<int>();
        }

        return await store.FindAsync(
            p => p.OwnerPeerId == query.OwnerPeerId && p.MessageId > query.AfterMessageId,
            p => p.MessageId,
            skip: 0,
            limit: query.Limit,
            sort: new SortOptions<MessageReadModel>(p => p.MessageId, SortType.Ascending),
            cancellationToken: cancellationToken);
    }
}
