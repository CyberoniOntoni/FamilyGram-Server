namespace MyTelegram.QueryHandlers.InMemory.Messaging;

public class GetOwnerMaxMessageIdQueryHandler(IQueryOnlyReadModelStore<MessageReadModel> store)
    : IQueryHandler<GetOwnerMaxMessageIdQuery, int>
{
    public async Task<int> ExecuteQueryAsync(GetOwnerMaxMessageIdQuery query,
        CancellationToken cancellationToken)
    {
        return await store.FirstOrDefaultAsync(
            p => p.OwnerPeerId == query.OwnerPeerId,
            p => p.MessageId,
            sort: new SortOptions<MessageReadModel>(p => p.MessageId, SortType.Descending),
            cancellationToken: cancellationToken);
    }
}
