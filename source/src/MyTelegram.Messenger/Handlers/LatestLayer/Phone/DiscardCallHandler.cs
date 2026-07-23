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

        var participantIds = new List<long> { session.CallerId, session.CalleeId };
        // Self response: Self flags relative to discarder.
        var usersForSelf = await userConverterService.GetUserListAsync(input, participantIds, false, false, input.Layer);
        var usersVector = new TVector<MyTelegram.Schema.IUser>(usersForSelf);

        var updatePhoneCall = new TUpdatePhoneCall { PhoneCall = discardedCall };

        var otherUserId = input.UserId == session.CallerId ? session.CalleeId : session.CallerId;
        var otherPermAuthKeyId = otherUserId == session.CallerId
            ? session.CallerPermAuthKeyId
            : session.CalleePermAuthKeyId;
        // Push to peer: Self flags MUST be relative to the recipient, or the web client
        // marks the contact as isSelf and shows the chat as "Saved messages".
        var usersForOther = await CallUserListHelper.GetUserListForViewerAsync(
            userConverterService,
            input,
            otherUserId,
            participantIds,
            input.Layer,
            otherPermAuthKeyId);
        var otherPeer = new Peer(PeerType.User, otherUserId);
        await objectMessageSender.PushMessageToPeerAsync(otherPeer,
            new TUpdates
            {
                Updates = new TVector<IUpdate> { updatePhoneCall },
                Users = new TVector<IUser>(usersForOther),
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
        // Claim once per call (not per hangup user). History direction must follow the
        // caller (outgoing for caller / incoming for callee), independent of who hung up.
        var claimFilter = Builders<CallSessionDocument>.Filter.And(
            Builders<CallSessionDocument>.Filter.Eq(s => s.CallId, callId),
            Builders<CallSessionDocument>.Filter.Or(
                Builders<CallSessionDocument>.Filter.Exists(s => s.HistoryWrittenUserIds, false),
                Builders<CallSessionDocument>.Filter.Size(s => s.HistoryWrittenUserIds, 0)));

        var claimed = await _callCollection.FindOneAndUpdateAsync(
            claimFilter,
            Builders<CallSessionDocument>.Update.Set(s => s.HistoryWrittenUserIds, new List<long> { callerId, calleeId }),
            new FindOneAndUpdateOptions<CallSessionDocument>
            {
                ReturnDocument = ReturnDocument.After
            });

        if (claimed == null)
        {
            logger.LogDebug(
                "Skipping call history message; already written for callId={CallId}",
                callId);
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
                    // Deterministic per call (not per discarder) so redelivery is idempotent.
                    attempt == 1 ? HistoryRandomId(callId, callerId) : Random.Shared.NextInt64());
                return;
            }
            catch (Exception ex) when (IsRetryableHistoryFailure(ex))
            {
                lastError = ex;
                logger.LogWarning(
                    ex,
                    "Call history message failed (attempt {Attempt}/{Max}) callId={CallId} callerId={CallerId}",
                    attempt,
                    HistorySendMaxAttempts,
                    callId,
                    callerId);
            }
        }

        // Release claim so a later discard / client retry can try again.
        await _callCollection.UpdateOneAsync(
            Builders<CallSessionDocument>.Filter.Eq(s => s.CallId, callId),
            Builders<CallSessionDocument>.Update.Set(s => s.HistoryWrittenUserIds, new List<long>()));

        // Do not fail phone.discardCall: media path already tore down and peer was notified.
        logger.LogError(
            lastError,
            "Call history message abandoned after {Max} attempts; discard still succeeds. callId={CallId} callerId={CallerId}",
            HistorySendMaxAttempts,
            callId,
            callerId);
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
        // Always attribute the service message to the caller so each side gets the
        // correct Out flag: caller outbox Out=true, callee inbox Out=false.
        // (Previously used the hangup user as sender, so the party that hangs up
        // always saw "Outgoing call" and the peer always saw "Incoming call".)
        var action = new TMessageActionPhoneCall
        {
            CallId = callId,
            Reason = reason,
            Duration = duration,
            Video = video
        };

        var sendInput = new SendMessageInput(
            input.ToRequestInfo() with { ReqMsgId = 0, UserId = callerId },
            callerId,
            new Peer(PeerType.User, calleeId),
            string.Empty,
            randomId,
            sendMessageType: SendMessageType.MessageService,
            messageType: MessageType.PhoneCall,
            messageAction: action
        );
        await messageAppService.SendMessageAsync([sendInput]);
    }
}
