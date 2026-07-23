namespace MyTelegram.QueryHandlers.InMemory.Pts;

public class GetMaxPtsByPeerIdQueryHandler(IQueryOnlyReadModelStore<MessageReadModel> store)
    : IQueryHandler<GetMaxPtsByPeerIdQuery, int>
{
    public async Task<int> ExecuteQueryAsync(GetMaxPtsByPeerIdQuery query,
        CancellationToken cancellationToken)
    {
        var maxPts = await store.FirstOrDefaultAsync(
            p => p.OwnerPeerId == query.PeerId,
            p => p.Pts,
            sort: new SortOptions<MessageReadModel>(p => p.Pts, SortType.Descending),
            cancellationToken: cancellationToken);

        return maxPts;
    }
}
