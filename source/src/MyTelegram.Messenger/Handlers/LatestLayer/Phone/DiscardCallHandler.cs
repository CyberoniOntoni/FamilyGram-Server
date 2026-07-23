using MongoDB.Driver;
using MyTelegram.Messenger.Services.Phone;
using MyTelegram.Messenger.Services;
using MyTelegram.Schema;
using MyTelegram.Schema.Phone;
using MyTelegram.Services.Services;

namespace MyTelegram.Messenger.Handlers.LatestLayer.Phone;

internal sealed class DiscardCallHandler(
    IMongoDatabase mongoDatabase,
    IUserConverterService userConverterService,
    IObjectMessageSender objectMessageSender,
    IMessageAppService messageAppService,
    IAccessHashHelper2 accessHashHelper2,
    ILogger<DiscardCallHandler> logger)
    : RpcResultObjectHandler<MyTelegram.Schema.Phone.RequestDiscardCall, IUpdates>
{
    private const int HistorySendMaxAttempts = 3;

    private readonly IMongoCollection<CallSessionDocument> _callCollection =
        mongoDatabase.GetCollection<CallSessionDocument>("call_sessions");

    protected override async Task<IUpdates> HandleCoreAsync(IRequestInput input, MyTelegram.Schema.Phone.RequestDiscardCall obj)
    {
        if (obj.Peer is not TInputPhoneCall inputPhoneCall)
        {
            RpcErrors.RpcErrors400.CallPeerInvalid.ThrowRpcError();
            return null!;
        }

        var filter = Builders<CallSessionDocument>.Filter.Eq(s => s.CallId, inputPhoneCall.Id);

        var session = await _callCollection.Find(filter).FirstOrDefaultAsync();
        if (session == null ||
            (!session.HasAccessHashForUser(input.UserId, inputPhoneCall.AccessHash) &&
             !await accessHashHelper2.IsAccessHashValidAsync(input, inputPhoneCall.Id, inputPhoneCall.AccessHash, AccessHashType.Call)))
        {
            RpcErrors.RpcErrors400.CallPeerInvalid.ThrowRpcError();
            return null!;
        }

        if (session.CallerId != input.UserId && session.CalleeId != input.UserId)
        {
            RpcErrors.RpcErrors400.CallPeerInvalid.ThrowRpcError();
            return null!;
        }

        var reason = ConvertReason(obj.Reason);
        var currentDate = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        // Web clients often send duration=0; derive from session start so history is not empty.
        var duration = obj.Duration > 0
            ? obj.Duration
            : Math.Max(0, currentDate - session.Date);

        var wasConfirmed = session.State == "confirmed";
        var video = session.Video || obj.Video;

        var update = Builders<CallSessionDocument>.Update
            .Set(s => s.State, "discarded")
            .Set(s => s.Duration, duration)
            .Set(s => s.DiscardReason, reason)
            .Set(s => s.Video, video);

        await _callCollection.UpdateOneAsync(filter, update);

        var discardedCall = new Schema.TPhoneCallDiscarded
        {
            Id = session.CallId,
            Reason = obj.Reason,
            Duration = duration,
            NeedRating = wasConfirmed,
            NeedDebug = wasConfirmed,
            Video = video
        };

        var users = await userConverterService.GetUserListAsync(input, new List<long> { session.CallerId, session.CalleeId }, false, false, input.Layer);
        var usersVector = new TVector<MyTelegram.Schema.IUser>(users);

        var updatePhoneCall = new TUpdatePhoneCall { PhoneCall = discardedCall };

        var otherUserId = input.UserId == session.CallerId ? session.CalleeId : session.CallerId;
        var otherPeer = new Peer(PeerType.User, otherUserId);
        await objectMessageSender.PushMessageToPeerAsync(otherPeer,
            new TUpdates
            {
                Updates = new TVector<IUpdate> { updatePhoneCall },
                Users = usersVector,
                Chats = new TVector<IChat>(),
                Date = currentDate
            });

        // Call signaling always succeeds; history write is best-effort and once per user per call.
        await TrySendCallDiscardedServiceMessageAsync(
            input,
            session.CallId,
            session.CallerId,
            session.CalleeId,
            duration,
            obj.Reason,
            video);

        return new TUpdates
        {
            Updates = new TVector<IUpdate> { updatePhoneCall },
            Users = usersVector,
            Chats = new TVector<IChat>(),
            Date = currentDate
        };
    }

    private static string? ConvertReason(IPhoneCallDiscardReason? reason)
    {
        return reason switch
        {
            TPhoneCallDiscardReasonMissed => "missed",
            TPhoneCallDiscardReasonDisconnect => "disconnect",
            TPhoneCallDiscardReasonHangup => "hangup",
            TPhoneCallDiscardReasonBusy => "busy",
            TPhoneCallDiscardReasonMigrateConferenceCall => "migrate",
            _ => null
        };
    }

    /// <summary>
    /// Deterministic random id so redelivery of the same discard does not create a second
    /// outbox row when CreateOutbox is idempotent on RandomId.
    /// </summary>
    private static long HistoryRandomId(long callId, long userId)
    {
        unchecked
        {
            // Stable, non-zero, distinct per (call, writer).
            var mixed = callId * 397L ^ userId * 7919L ^ 0xC411D15C0DEL;
            return mixed == 0 ? 1 : mixed;
        }
    }

    private async Task TrySendCallDiscardedServiceMessageAsync(
        IRequestInput input,
        long callId,
        long callerId,
        long calleeId,
        int? duration,
        IPhoneCallDiscardReason? reason,
        bool video)
    {
        // Claim once per user so double hangup / concurrent discards do not double-write history.
        var claimFilter = Builders<CallSessionDocument>.Filter.And(
            Builders<CallSessionDocument>.Filter.Eq(s => s.CallId, callId),
            Builders<CallSessionDocument>.Filter.Not(
                Builders<CallSessionDocument>.Filter.AnyEq(s => s.HistoryWrittenUserIds, input.UserId)));

        var claimed = await _callCollection.FindOneAndUpdateAsync(
            claimFilter,
            Builders<CallSessionDocument>.Update.AddToSet(s => s.HistoryWrittenUserIds, input.UserId),
            new FindOneAndUpdateOptions<CallSessionDocument>
            {
                ReturnDocument = ReturnDocument.After
            });

        if (claimed == null)
        {
            logger.LogDebug(
                "Skipping call history message; already written for callId={CallId} userId={UserId}",
                callId,
                input.UserId);
            return;
        }

        Exception? lastError = null;
        for (var attempt = 1; attempt <= HistorySendMaxAttempts; attempt++)
        {
            try
            {
                await SendCallDiscardedServiceMessageAsync(
                    input,
                    callId,
                    callerId,
                    calleeId,
                    duration,
                    reason,
                    video,
                    // First attempt uses deterministic RandomId; retries use fresh ids so a
                    // true AggregateIsNew (different content, same slot) can allocate a new message.
                    attempt == 1 ? HistoryRandomId(callId, input.UserId) : Random.Shared.NextInt64());
                return;
            }
            catch (Exception ex) when (IsRetryableHistoryFailure(ex))
            {
                lastError = ex;
                logger.LogWarning(
                    ex,
                    "Call history message failed (attempt {Attempt}/{Max}) callId={CallId} userId={UserId}",
                    attempt,
                    HistorySendMaxAttempts,
                    callId,
                    input.UserId);
            }
        }

        // Release claim so a later discard / client retry can try again.
        await _callCollection.UpdateOneAsync(
            Builders<CallSessionDocument>.Filter.Eq(s => s.CallId, callId),
            Builders<CallSessionDocument>.Update.Pull(s => s.HistoryWrittenUserIds, input.UserId));

        // Do not fail phone.discardCall: media path already tore down and peer was notified.
        logger.LogError(
            lastError,
            "Call history message abandoned after {Max} attempts; discard still succeeds. callId={CallId} userId={UserId}",
            HistorySendMaxAttempts,
            callId,
            input.UserId);
    }

    private static bool IsRetryableHistoryFailure(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException!)
        {
            var msg = e.Message;
            if (msg.Contains("AggregateIsNew", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("is not new", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("SagaPublishException", StringComparison.OrdinalIgnoreCase) ||
                e.GetType().Name.Contains("SagaPublish", StringComparison.OrdinalIgnoreCase) ||
                e.GetType().Name.Contains("DomainError", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private async Task SendCallDiscardedServiceMessageAsync(
        IRequestInput input,
        long callId,
        long callerId,
        long calleeId,
        int? duration,
        IPhoneCallDiscardReason? reason,
        bool video,
        long randomId)
    {
        var isCaller = input.UserId == callerId;
        var targetUserId = isCaller ? calleeId : callerId;

        var action = new TMessageActionPhoneCall
        {
            CallId = callId,
            Reason = reason,
            Duration = duration,
            Video = video
        };

        var sendInput = new SendMessageInput(
            input.ToRequestInfo() with { ReqMsgId = 0 },
            input.UserId,
            new Peer(PeerType.User, targetUserId),
            string.Empty,
            randomId,
            sendMessageType: SendMessageType.MessageService,
            messageType: MessageType.PhoneCall,
            messageAction: action
        );
        await messageAppService.SendMessageAsync([sendInput]);
    }
}
