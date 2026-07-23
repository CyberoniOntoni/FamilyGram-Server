using MyTelegram.Services.Services.IdGenerator;

namespace MyTelegram.Messenger.Services.Impl;

public class IdGenerator(
    IHiLoValueGeneratorCache cache,
    IHiLoValueGeneratorFactory factory,
    IQueryProcessor queryProcessor,
    IEventStore eventStore,
    ISnapshotStore snapshotStore,
    IHiLoStateBlockSizeHelper stateBlockSizeHelper,
    ILogger<IdGenerator> logger)
    : IIdGenerator, ITransientDependency
{
    public async Task<int> NextIdAsync(IdType idType,
        long id,
        int step = 1,
        CancellationToken cancellationToken = default)
    {
        return (int)await NextLongIdAsync(idType, id, step, cancellationToken);
    }

    public async Task<long> NextLongIdAsync(IdType idType,
        long id = 0,
        int step = 1,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        HiLoValueGeneratorState state;
        if (idType == IdType.MessageId)
        {
            state = await GetMessageIdStateAsync(idType, id);
        }
        else
        {
            state = cache.GetOrAdd(idType, id);
        }

        var generator = factory.Create(state);
        var nextId = await generator.NextAsync(idType, id, cancellationToken);
        sw.Stop();

        if (sw.Elapsed.TotalMilliseconds > 100)
        {
            logger.LogWarning("[{Timespan}] Generate id too slow, idType: {IdType}, id: {Id}", sw.Elapsed, idType, id);
        }

        return nextId + GetInitId(idType);
    }

    private static long GetInitId(IdType idType)
    {
        return idType switch
        {
            IdType.ChannelId => MyTelegramConsts.ChannelInitId,
            IdType.UserId => MyTelegramConsts.UserIdInitId + 10000, // First 10000 for testing
            IdType.BotUserId => MyTelegramConsts.BotUserInitId,
            IdType.ChatId => MyTelegramConsts.ChatIdInitId,
            IdType.Pts => MyTelegramConsts.PtsInitId,
            IdType.FolderId => MyTelegramConsts.FolderInitId,
            _ => 0
        };
    }

    private async Task<int> GetMaxMessageIdAsync(long ownerPeerId)
    {
        int? maxId = await queryProcessor.ProcessAsync(new GetMaxMessageIdByPeerIdQuery(ownerPeerId));

        return maxId ?? 0;
    }
    private async Task<HiLoValueGeneratorState> GetMessageIdStateAsync(IdType idType, long id)
    {
        // Seed HiLo from the highest known message id so we never re-issue IDs that already
        // exist in the event store (read-model lag / prior cold-cache bugs caused AggregateIsNew failures).
        var maxId = await GetMaxMessageIdAsync(id);
        var nextFreeLow = await FindMessageIdLowWatermarkAsync(id, maxId);

        var blockSize = stateBlockSizeHelper.GetBlockSize(idType);
        var high = nextFreeLow <= 0 ? 0 : nextFreeLow / blockSize;
        var highExclusive = (high + 1L) * blockSize + 1;

        return await cache.GetOrAddAsync(idType, id, () =>
            Task.FromResult(new HiLoValueGeneratorState(blockSize, nextFreeLow, highExclusive)));
    }

    /// <summary>
    /// Returns a HiLo "low" value such that the next generated id (low+1) is free in the event store.
    /// </summary>
    private async Task<long> FindMessageIdLowWatermarkAsync(long ownerPeerId, int maxFromReadModel)
    {
        var low = Math.Max(0, maxFromReadModel);

        // Walk forward a short window if MaxMessageId from PtsReadModel lags the event store.
        const int maxProbe = 64;
        for (var i = 0; i < maxProbe; i++)
        {
            var candidate = (int)low + 1;
            if (candidate <= 0)
            {
                break;
            }

            var aggregate = new MessageAggregate(MessageId.Create(ownerPeerId, candidate));
            await aggregate.LoadAsync(eventStore, snapshotStore, CancellationToken.None);
            if (aggregate.IsNew)
            {
                return low;
            }

            low = candidate;
        }

        if (low > maxFromReadModel)
        {
            logger.LogWarning(
                "MessageId low watermark advanced past Pts MaxMessageId due to event-store occupancy. peerId={PeerId} ptsMax={PtsMax} low={Low}",
                ownerPeerId,
                maxFromReadModel,
                low);
        }

        return low;
    }
}