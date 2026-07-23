using System.Collections.Concurrent;
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
    private const int MessageIdMaxProbe = 512;
    private const int MessageIdCollisionRetries = 128;
    private const int PtsFloorBurnLimit = 2048;

    /// <summary>
    /// Per-peer gates so concurrent sagas cannot interleave HiLo blocks and
    /// issue non-monotonic pts (which breaks client getDifference / push apply).
    /// </summary>
    private static readonly ConcurrentDictionary<long, SemaphoreSlim> PtsGates = new();

    /// <summary>
    /// In-process floor of the highest pts issued (or observed) per peer.
    /// </summary>
    private static readonly ConcurrentDictionary<long, int> PtsFloor = new();

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

        long result;
        if (idType == IdType.MessageId)
        {
            result = await NextFreeMessageIdAsync(id, cancellationToken);
        }
        else if (idType == IdType.Pts)
        {
            result = await NextMonotonicPtsAsync(id, cancellationToken);
        }
        else
        {
            var state = cache.GetOrAdd(idType, id);
            var generator = factory.Create(state);
            result = await generator.NextAsync(idType, id, cancellationToken) + GetInitId(idType);
        }

        sw.Stop();

        if (sw.Elapsed.TotalMilliseconds > 100)
        {
            logger.LogWarning("[{Timespan}] Generate id too slow, idType: {IdType}, id: {Id}", sw.Elapsed, idType, id);
        }

        return result;
    }

    private async Task<long> NextMonotonicPtsAsync(long peerId, CancellationToken cancellationToken)
    {
        var gate = PtsGates.GetOrAdd(peerId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var floor = Math.Max(
                PtsFloor.TryGetValue(peerId, out var memFloor) ? memFloor : 0,
                await GetPtsFloorFromStoreAsync(peerId, cancellationToken));

            var state = cache.GetOrAdd(IdType.Pts, peerId);
            var generator = factory.Create(state);
            var init = GetInitId(IdType.Pts);

            long next = 0;
            for (var attempt = 0; attempt < PtsFloorBurnLimit; attempt++)
            {
                next = await generator.NextAsync(IdType.Pts, peerId, cancellationToken) + init;
                if (next > floor)
                {
                    break;
                }
            }

            if (next <= floor)
            {
                // HiLo block exhausted without clearing the floor (e.g. DB max far above
                // current block). Jump to floor+1 and record it — rare path.
                next = floor + 1L;
                logger.LogWarning(
                    "Pts HiLo could not clear floor; forcing next. peerId={PeerId} floor={Floor} forced={Next}",
                    peerId,
                    floor,
                    next);
            }

            PtsFloor[peerId] = (int)next;
            return next;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<int> GetPtsFloorFromStoreAsync(long peerId, CancellationToken cancellationToken)
    {
        var maxFromMessages = await queryProcessor.ProcessAsync(new GetMaxPtsByPeerIdQuery(peerId));
        var ptsReadModel = await queryProcessor.ProcessAsync(new GetPtsByPeerIdQuery(peerId));
        var maxFromReadModel = ptsReadModel?.Pts ?? 0;
        return Math.Max(maxFromMessages, maxFromReadModel);
    }

    private async Task<long> NextFreeMessageIdAsync(long ownerPeerId, CancellationToken cancellationToken)
    {
        var state = await GetMessageIdStateAsync(IdType.MessageId, ownerPeerId);
        var generator = factory.Create(state);

        for (var attempt = 0; attempt < MessageIdCollisionRetries; attempt++)
        {
            var nextId = (int)await generator.NextAsync(IdType.MessageId, ownerPeerId, cancellationToken);
            if (nextId <= 0)
            {
                continue;
            }

            if (await IsMessageAggregateNewAsync(ownerPeerId, nextId, cancellationToken))
            {
                return nextId;
            }

            logger.LogWarning(
                "MessageId already occupied in event store, advancing HiLo. peerId={PeerId} messageId={MessageId} attempt={Attempt}",
                ownerPeerId,
                nextId,
                attempt + 1);
        }

        throw new InvalidOperationException(
            $"Unable to allocate a free MessageId for peer {ownerPeerId} after {MessageIdCollisionRetries} attempts.");
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

        for (var i = 0; i < MessageIdMaxProbe; i++)
        {
            var candidate = (int)low + 1;
            if (candidate <= 0)
            {
                break;
            }

            if (await IsMessageAggregateNewAsync(ownerPeerId, candidate, CancellationToken.None))
            {
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

            low = candidate;
        }

        logger.LogWarning(
            "MessageId low watermark probe exhausted; using low={Low} for peerId={PeerId} ptsMax={PtsMax}",
            low,
            ownerPeerId,
            maxFromReadModel);

        return low;
    }

    private async Task<bool> IsMessageAggregateNewAsync(long ownerPeerId, int messageId, CancellationToken cancellationToken)
    {
        var aggregate = new MessageAggregate(MessageId.Create(ownerPeerId, messageId));
        await aggregate.LoadAsync(eventStore, snapshotStore, cancellationToken);
        return aggregate.IsNew;
    }
}
