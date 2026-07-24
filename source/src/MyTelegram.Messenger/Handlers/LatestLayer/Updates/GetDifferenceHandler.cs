using MyTelegram.Schema.Updates;

namespace MyTelegram.Messenger.Handlers.LatestLayer.Updates;
/// <summary>
/// Get new <a href="https://corefork.telegram.org/api/updates">updates</a>.
/// Possible errors
/// Code Type Description
/// 400 CDN_METHOD_INVALID You can't call this method in a CDN DC.
/// 400 CHANNEL_INVALID The provided channel is invalid.
/// 400 CHANNEL_PRIVATE You haven't joined this channel/supergroup.
/// 400 CHAT_NOT_MODIFIED No changes were made to chat information because the new information you passed is identical to the current information.
/// 403 CHAT_WRITE_FORBIDDEN You can't write in this chat.
/// 400 DATE_EMPTY Date empty.
/// 400 MSG_ID_INVALID Invalid message ID provided.
/// 400 PERSISTENT_TIMESTAMP_EMPTY Persistent timestamp empty.
/// 400 PERSISTENT_TIMESTAMP_INVALID Persistent timestamp invalid.
/// 500 RANDOM_ID_DUPLICATE You provided a random ID that was already used.
/// 400 USERNAME_INVALID The provided username is not valid.
/// 400 USER_NOT_PARTICIPANT You're not a member of this supergroup/channel.
/// <para><c>See <a href="https://corefork.telegram.org/method/updates.getDifference"/> </c></para>
/// </summary>
/// <remarks>
/// Access: [User ✔] [Bot ✔] [Anonymous ✖]
/// </remarks>
internal sealed class GetDifferenceHandler(IMessageAppService messageAppService, IPtsHelper ptsHelper, IQueryProcessor queryProcessor, IAckCacheService ackCacheService, IDifferenceConverterService differenceConverterService) : RpcResultObjectHandler<RequestGetDifference, IDifference>
{
    /// <summary>
    /// When the client already sits at (or past) max message pts but still missed
    /// intermediate messages (out-of-order push + GetState pts jump), re-offer the
    /// last N mailbox rows. Clients de-dupe by message id.
    /// </summary>
    private const int HoleRecoveryWindow = 50;

    protected override async Task<IDifference> HandleCoreAsync(IRequestInput input, RequestGetDifference obj)
    {
        var userId = input.UserId;
        if (userId == 0)
        {
            return new TDifferenceEmpty
            {
                Date = CurrentDate
            };
        }

        var cachedPts = ptsHelper.GetCachedPts(userId);
        var ptsReadModel = await queryProcessor.ProcessAsync(new GetPtsByPeerIdQuery(userId));
        var ptsForAuthKeyIdReadModel = await queryProcessor.ProcessAsync(new GetPtsByPermAuthKeyIdQuery(userId, input.PermAuthKeyId));
        var globalSeqNo = ptsForAuthKeyIdReadModel?.GlobalSeqNo ?? 0;
        IReadOnlyCollection<IUpdatesReadModel> userUpdates = new List<IUpdatesReadModel>();
        var joinedChannelIdList = await queryProcessor.ProcessAsync(new GetChannelIdListByMemberUserIdQuery(input.UserId));
        var limit = obj.PtsTotalLimit ?? MyTelegramConsts.DefaultPtsTotalLimit;
        limit = Math.Min(limit, MyTelegramConsts.DefaultPtsTotalLimit);
        var updatesReadModels = await queryProcessor.ProcessAsync(new GetUpdatesQuery(input.UserId, input.UserId, obj.Pts, obj.Date, limit));

        // Inbox pushes are often stored as UpdatesType.Updates (full TUpdates with
        // updateNewMessage) after EnrichInboxUpdates — not only NewMessages. Collect
        // every row that has a MessageId so getDifference can load the mailbox rows.
        var messageIds = new List<int>();
        foreach (var update in updatesReadModels)
        {
            if (update.MessageId is > 0 and var mid && !messageIds.Contains(mid))
            {
                messageIds.Add(mid);
            }
        }

        // Recovery: concurrent HiLo / out-of-order completion can leave the client at a
        // high pts while mailbox rows with lower pts were never applied. Also pull rows
        // after PtsReadModel.MaxMessageId and rows with Pts > clientPts directly from
        // the message store (updates table can lag or be typed differently).
        var maxMessageIdWatermark = ptsReadModel?.MaxMessageId ?? 0;
        var orphanMessageIds = await queryProcessor.ProcessAsync(
            new GetMessageIdListAfterIdQuery(userId, maxMessageIdWatermark, limit));
        AddMessageIds(messageIds, orphanMessageIds);

        var byPtsMessageIds = await queryProcessor.ProcessAsync(
            new GetMessageIdListByMinPtsQuery(userId, obj.Pts, limit));
        AddMessageIds(messageIds, byPtsMessageIds);

        // Hole recovery: client already at/above max pts but may still be missing messages
        // (GetState used to advertise max message pts without delivering the rows).
        var ownerMaxMessageId = await queryProcessor.ProcessAsync(new GetOwnerMaxMessageIdQuery(userId));
        var maxMessagePts = await queryProcessor.ProcessAsync(new GetMaxPtsByPeerIdQuery(userId));
        if (ownerMaxMessageId > 0 && obj.Pts >= maxMessagePts && messageIds.Count == 0)
        {
            var afterId = Math.Max(0, ownerMaxMessageId - HoleRecoveryWindow);
            // Prefer the lower of pts watermark and recent-window so we still catch
            // rows that MaxMessageId already covered but the client never applied.
            afterId = Math.Min(afterId, maxMessageIdWatermark > 0 ? maxMessageIdWatermark - 1 : afterId);
            afterId = Math.Max(0, afterId);
            var recentIds = await queryProcessor.ProcessAsync(
                new GetMessageIdListAfterIdQuery(userId, afterId, HoleRecoveryWindow));
            AddMessageIds(messageIds, recentIds);
        }
        else if (ownerMaxMessageId > maxMessageIdWatermark)
        {
            // Pts read-model MaxMessageId lag — already covered by orphan query above;
            // if watermark is far behind, also expand the window.
            var afterId = Math.Max(0, ownerMaxMessageId - HoleRecoveryWindow);
            if (afterId < maxMessageIdWatermark)
            {
                var recentIds = await queryProcessor.ProcessAsync(
                    new GetMessageIdListAfterIdQuery(userId, afterId, HoleRecoveryWindow));
                AddMessageIds(messageIds, recentIds);
            }
        }

        // all channel updates
        var channelUpdatesReadModels = await queryProcessor.ProcessAsync(new GetChannelUpdatesByGlobalSeqNoQuery(joinedChannelIdList.ToList(), globalSeqNo, limit, input.UserId));
        if (channelUpdatesReadModels.Any(p => p.OnlySendToUserId.HasValue))
        {
            var tempChannelReadModels = channelUpdatesReadModels.ToList();
            tempChannelReadModels.RemoveAll(p => p.OnlySendToUserId.HasValue && p.OnlySendToUserId != input.UserId);
            channelUpdatesReadModels = tempChannelReadModels;
        }

        var users = updatesReadModels.SelectMany(p => p.Users ?? []).ToList();
        var chats = updatesReadModels.SelectMany(p => p.Chats ?? []).ToList();
        users.AddRange(channelUpdatesReadModels.SelectMany(p => p.Users ?? []).ToList());
        chats.AddRange(channelUpdatesReadModels.SelectMany(p => p.Chats ?? []).ToList());
        chats.AddRange(channelUpdatesReadModels.Select(p => p.OwnerPeerId));
        var dto = await messageAppService.GetChannelDifferenceAsync(new GetDifferenceInput(input.UserId, input.UserId, obj.Pts, limit, messageIds, users, chats));

        // Prefer loading messages via NewMessages; strip updateNewMessage from OtherUpdates
        // when the same id is already in MessageList to avoid double-apply. Keep non-message updates.
        var recoveredMessageIdSet = dto.MessageList.Select(p => p.MessageId).ToHashSet();
        var allUpdateList = new List<IUpdate>();
        foreach (var update in updatesReadModels.Where(p => p.UpdatesType == UpdatesType.Updates).SelectMany(p => p.Updates ?? []))
        {
            if (update is TUpdateNewMessage unm && recoveredMessageIdSet.Contains(unm.Message.Id))
            {
                continue;
            }

            allUpdateList.Add(update);
        }

        allUpdateList.AddRange(channelUpdatesReadModels.Where(p => p.UpdatesType == UpdatesType.Updates).SelectMany(p => p.Updates ?? []));
        allUpdateList.AddRange(userUpdates.SelectMany(p => p.Updates ?? []));
        if (updatesReadModels.Count > 0 || channelUpdatesReadModels.Count > 0 || userUpdates.Count > 0 || dto.MessageList.Count > 0)
        {
            var maxPts = updatesReadModels.Count > 0 ? updatesReadModels.Max(p => p.Pts) : obj.Pts;
            if (dto.MessageList.Count > 0)
            {
                maxPts = Math.Max(maxPts, dto.MessageList.Max(p => p.Pts));
            }
            var channelMaxGlobalSeqNo = channelUpdatesReadModels.Count > 0 ? channelUpdatesReadModels.Max(p => p.GlobalSeqNo) : 0;
            var userGlobalSeqNo = userUpdates.Count > 0 ? userUpdates.Max(p => p.GlobalSeqNo) : 0;
            var maxGlobalSeqNo = Math.Max(channelMaxGlobalSeqNo, userGlobalSeqNo);
            await ackCacheService.AddRpcPtsToCacheAsync(input.ReqMsgId, maxPts, maxGlobalSeqNo, new Peer(PeerType.User, input.UserId), true);
        }

        dto.MessageList = dto.MessageList.OrderBy(p => p.MessageId).ToList();

        // CRITICAL: never advertise maxMessagePts on an empty difference. That was advancing
        // client pts past undelivered messages (GetState/getDifference loop) so they never
        // requested the rows again. Only raise cached pts when we actually return content,
        // or when both watermarks agree the mailbox is fully covered for this client pts.
        if (dto.MessageList.Count > 0 || allUpdateList.Count > 0)
        {
            cachedPts = Math.Max(cachedPts, maxMessagePts);
        }
        else
        {
            // Empty: stay at read-model / cache, do not jump to bare max message pts.
            cachedPts = Math.Max(cachedPts, ptsReadModel?.Pts ?? 0);
            if (obj.Pts >= maxMessagePts && ownerMaxMessageId <= maxMessageIdWatermark)
            {
                // Truly caught up — safe to report the high-water pts.
                cachedPts = Math.Max(cachedPts, maxMessagePts);
            }
        }

        var r = differenceConverterService.ToDifference(input, dto, ptsReadModel, cachedPts, limit, allUpdateList, [], [], layer: input.Layer);
        return r;
    }

    private static void AddMessageIds(List<int> target, IReadOnlyCollection<int> source)
    {
        foreach (var messageId in source)
        {
            if (messageId > 0 && !target.Contains(messageId))
            {
                target.Add(messageId);
            }
        }
    }
}
