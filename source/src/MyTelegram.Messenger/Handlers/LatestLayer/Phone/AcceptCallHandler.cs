using MongoDB.Driver;
using MyTelegram.Messenger.Services.Phone;
using MyTelegram.Schema;
using MyTelegram.Schema.Phone;
using MyTelegram.Services.Phone;
using MyTelegram.Services.Services;

namespace MyTelegram.Messenger.Handlers.LatestLayer.Phone;

internal sealed class AcceptCallHandler(
    IMongoDatabase mongoDatabase,
    IUserConverterService userConverterService,
    IObjectMessageSender objectMessageSender,
    IAccessHashHelper2 accessHashHelper2)
    : RpcResultObjectHandler<MyTelegram.Schema.Phone.RequestAcceptCall, MyTelegram.Schema.Phone.IPhoneCall>
{
    private readonly IMongoCollection<CallSessionDocument> _callCollection =
        mongoDatabase.GetCollection<CallSessionDocument>("call_sessions");

    protected override async Task<MyTelegram.Schema.Phone.IPhoneCall> HandleCoreAsync(IRequestInput input, MyTelegram.Schema.Phone.RequestAcceptCall obj)
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

        if (session.CalleeId != input.UserId)
        {
            RpcErrors.RpcErrors400.CallPeerInvalid.ThrowRpcError();
            return null!;
        }

        ValidateHandshakeProtocol(obj.Protocol);

        if (session.State == "accepted" || session.State == "confirmed")
        {
            RpcErrors.RpcErrors400.CallAlreadyAccepted.ThrowRpcError();
            return null!;
        }

        if (session.State == "discarded")
        {
            RpcErrors.RpcErrors400.CallAlreadyDeclined.ThrowRpcError();
            return null!;
        }

        var update = Builders<CallSessionDocument>.Update
            .Set(s => s.GB, obj.GB)
            .Set(s => s.CalleeLibraryVersions, [.. PhoneCallProtocolHelper.GetLibraryVersions(obj.Protocol)])
            .Set(s => s.CalleePermAuthKeyId, input.PermAuthKeyId)
            .Set(s => s.State, "accepted");

        await _callCollection.UpdateOneAsync(filter, update);

        var currentDate = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var phoneCallAcceptedForCaller = CreatePhoneCallAccepted(
            session,
            session.GetAccessHashForUser(session.CallerId),
            obj.GB,
            obj.Protocol,
            currentDate);
        var phoneCallWaitingForCallee = CreatePhoneCallWaiting(
            session,
            session.GetAccessHashForUser(session.CalleeId),
            obj.Protocol,
            currentDate);

        var participantIds = new List<long> { session.CallerId, session.CalleeId };
        var usersForCallee = await userConverterService.GetUserListAsync(input, participantIds, false, false, input.Layer);
        var usersForCaller = await CallUserListHelper.GetUserListForViewerAsync(
            userConverterService,
            input,
            session.CallerId,
            participantIds,
            input.Layer,
            session.CallerPermAuthKeyId);
        var usersVectorForCallee = new TVector<MyTelegram.Schema.IUser>(usersForCallee);

        var updatePhoneCall = new MyTelegram.Schema.TUpdatePhoneCall { PhoneCall = phoneCallAcceptedForCaller };

        var callerPeer = new Peer(PeerType.User, session.CallerId);
        var callerUpdates = new TUpdates
        {
            Updates = new TVector<IUpdate> { updatePhoneCall },
            Users = new TVector<IUser>(usersForCaller),
            Chats = new TVector<IChat>(),
            Date = currentDate
        };

        await objectMessageSender.PushMessageToPeerAsync(
            callerPeer,
            callerUpdates,
            onlySendToUserId: session.CallerId,
            onlySendToThisAuthKeyId: session.CallerPermAuthKeyId > 0 ? session.CallerPermAuthKeyId : null);

        // Chat history is written only on discard (see DiscardCallHandler), not on accept.

        return new MyTelegram.Schema.Phone.TPhoneCall
        {
            PhoneCall = phoneCallWaitingForCallee,
            Users = usersVectorForCallee
        };
    }

    private static Schema.TPhoneCallAccepted CreatePhoneCallAccepted(
        CallSessionDocument session,
        long accessHash,
        byte[] gb,
        IPhoneCallProtocol? protocol,
        int currentDate)
    {
        return new Schema.TPhoneCallAccepted
        {
            Id = session.CallId,
            AccessHash = accessHash,
            AdminId = session.CallerId,
            ParticipantId = session.CalleeId,
            GB = gb,
            Protocol = PhoneCallProtocolHelper.Negotiate(session.CallerLibraryVersions, protocol),
            Date = currentDate,
            Video = session.Video
        };
    }

    private static Schema.TPhoneCallWaiting CreatePhoneCallWaiting(
        CallSessionDocument session,
        long accessHash,
        IPhoneCallProtocol? protocol,
        int currentDate)
    {
        return new Schema.TPhoneCallWaiting
        {
            Id = session.CallId,
            AccessHash = accessHash,
            AdminId = session.CallerId,
            ParticipantId = session.CalleeId,
            Protocol = PhoneCallProtocolHelper.Negotiate(session.CallerLibraryVersions, protocol),
            Date = currentDate,
            Video = session.Video
        };
    }

    private static void ValidateHandshakeProtocol(IPhoneCallProtocol? protocol)
    {
        if (!PhoneCallProtocolHelper.HasValidLegacyFlags(protocol))
        {
            RpcErrors.RpcErrors400.CallProtocolFlagsInvalid.ThrowRpcError();
        }

        if (!PhoneCallProtocolHelper.HasValidLegacyLayers(protocol))
        {
            RpcErrors.RpcErrors400.CallProtocolLayerInvalid.ThrowRpcError();
        }
    }

}
