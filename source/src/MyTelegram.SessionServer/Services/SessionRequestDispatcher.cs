using System.Buffers.Binary;
using MyTelegram.Abstractions;
using MyTelegram.EventBus;
using MyTelegram.Schema;
using MyTelegram.Schema.Extensions;

namespace MyTelegram.SessionServer.Services;

public sealed class SessionRequestDispatcher(
    IAuthKeyStore authKeyStore,
    IMtProtoSessionCrypto crypto,
    IEventBus eventBus,
    IMessageIdHelper messageIdHelper,
    IGZipHelper gzipHelper,
    ILogger<SessionRequestDispatcher> logger) : ISessionRequestDispatcher
{
    private static readonly HashSet<uint> UploadObjectIds =
    [
        ObjectIdConsts.SaveFilePartObjectId,
        ObjectIdConsts.SaveBigFilePartObjectId,
        ObjectIdConsts.GetFileObjectId,
        ObjectIdConsts.GetFileObjectIdLayer143,
        0x24e6818d, // upload.getWebFile
        0x9156982a, // upload.getFileHashes
    ];

    public async Task HandleEncryptedAsync(EncryptedMessage message, CancellationToken cancellationToken = default)
    {
        var state = await authKeyStore.GetAsync(message.AuthKeyId, cancellationToken);
        if (state is null)
        {
            logger.LogWarning("Auth key not found: {AuthKeyId:x}", message.AuthKeyId);
            await eventBus.PublishAsync(new AuthKeyNotFoundEvent(message.AuthKeyId, message.ConnectionId));
            return;
        }

        state.ConnectionId = message.ConnectionId;
        state.ConnectionType = message.ConnectionType;
        state.ClientIp = message.ClientIp;
        state.DcId = message.DcId;

        byte[] plain;
        try
        {
            plain = crypto.DecryptClientPayload(state.AuthKey, message.MsgKey.Span, message.EncryptedData.Span);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Decrypt failed for authKey {AuthKeyId:x}", message.AuthKeyId);
            return;
        }

        if (plain.Length < 32)
        {
            logger.LogWarning("Decrypted payload too short ({Length})", plain.Length);
            return;
        }

        var salt = BinaryPrimitives.ReadInt64LittleEndian(plain.AsSpan(0, 8));
        var sessionId = BinaryPrimitives.ReadInt64LittleEndian(plain.AsSpan(8, 8));
        var msgId = BinaryPrimitives.ReadInt64LittleEndian(plain.AsSpan(16, 8));
        var seqNo = BinaryPrimitives.ReadInt32LittleEndian(plain.AsSpan(24, 4));
        var bodyLen = BinaryPrimitives.ReadInt32LittleEndian(plain.AsSpan(28, 4));
        if (bodyLen < 0 || bodyLen > plain.Length - 32)
        {
            logger.LogWarning("Invalid body length {BodyLen}", bodyLen);
            return;
        }

        if (state.ServerSalt == 0)
        {
            state.ServerSalt = salt;
        }

        state.SessionId = sessionId;
        var body = plain.AsMemory(32, bodyLen);

        IObject obj;
        try
        {
            obj = body.ToTObject<IObject>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deserialize TL body, authKey={AuthKeyId:x} msgId={MsgId}",
                state.AuthKeyId, msgId);
            return;
        }

        await ProcessObjectAsync(state, message, msgId, seqNo, obj, cancellationToken);
    }

    private async Task ProcessObjectAsync(
        SessionState state,
        EncryptedMessage envelope,
        long msgId,
        int seqNo,
        IObject obj,
        CancellationToken cancellationToken)
    {
        // Unwrap wrappers
        while (true)
        {
            switch (obj)
            {
                case TMsgContainer container:
                    foreach (var item in container.Messages ?? [])
                    {
                        if (item.Body is not null)
                        {
                            await ProcessObjectAsync(state, envelope, item.MsgId, item.SeqNo, item.Body, cancellationToken);
                        }
                    }

                    return;
                case TGzipPacked gzip:
                {
                    var dest = new byte[Math.Max(gzip.PackedData.Length * 8, 4096)];
                    gzipHelper.Decompress(gzip.PackedData, dest, out var count);
                    obj = dest.AsMemory(0, count).ToTObject<IObject>();
                    continue;
                }
                case RequestInvokeWithLayer invokeWithLayer:
                    state.Layer = invokeWithLayer.Layer;
                    obj = invokeWithLayer.Query;
                    continue;
                case RequestInvokeAfterMsg invokeAfterMsg:
                    obj = invokeAfterMsg.Query;
                    continue;
                case RequestInvokeAfterMsgs invokeAfterMsgs:
                    obj = invokeAfterMsgs.Query;
                    continue;
                case RequestInvokeWithoutUpdates invokeWithoutUpdates:
                    obj = invokeWithoutUpdates.Query;
                    continue;
                case RequestInitConnection initConnection:
                    obj = initConnection.Query;
                    continue;
                default:
                    break;
            }

            break;
        }

        var constructorId = obj.ConstructorId;

        if (constructorId is ObjectIdConsts.PingId or ObjectIdConsts.PingDelayId)
        {
            await HandlePingAsync(state, msgId, obj);
            return;
        }

        if (constructorId == ObjectIdConsts.MsgAcks)
        {
            return;
        }

        var dataBytes = obj.ToBytes();

        var requestId = envelope.RequestId;
        var permAuthKeyId = state.PermAuthKeyId != 0 ? state.PermAuthKeyId : state.AuthKeyId;
        var accessHashKeyId = state.AccessHashKeyId != 0 ? state.AccessHashKeyId : state.AuthKeyId;
        var date = envelope.Date;

        if (UploadObjectIds.Contains(constructorId) ||
            ObjectIdConsts.GetFileObjectId == constructorId ||
            ObjectIdConsts.GetFileObjectIdLayer143 == constructorId)
        {
            if (constructorId is ObjectIdConsts.SaveFilePartObjectId or ObjectIdConsts.SaveBigFilePartObjectId)
            {
                await eventBus.PublishAsync(new UploadDataReceivedEvent(
                    envelope.ConnectionId, envelope.ConnectionType, requestId, constructorId,
                    state.UserId, msgId, seqNo, state.AuthKeyId, permAuthKeyId,
                    dataBytes, state.Layer, date, state.DeviceType, envelope.ClientIp,
                    state.SessionId, accessHashKeyId));
            }
            else
            {
                await eventBus.PublishAsync(new DownloadDataReceivedEvent(
                    envelope.ConnectionId, envelope.ConnectionType, requestId, constructorId,
                    state.UserId, msgId, seqNo, state.AuthKeyId, permAuthKeyId,
                    dataBytes, state.Layer, date, state.DeviceType, envelope.ClientIp,
                    state.SessionId, accessHashKeyId));
            }

            return;
        }

        if (ObjectIdConsts.CommandServerHandlers.ContainsKey(constructorId))
        {
            await eventBus.PublishAsync(new MessengerCommandDataReceivedEvent(
                envelope.ConnectionId, envelope.ConnectionType, requestId, constructorId,
                state.UserId, msgId, seqNo, state.AuthKeyId, permAuthKeyId,
                dataBytes, state.Layer, date, state.DeviceType, envelope.ClientIp,
                state.SessionId, accessHashKeyId));
            return;
        }

        await eventBus.PublishAsync(new MessengerQueryDataReceivedEvent(
            envelope.ConnectionId, envelope.ConnectionType, requestId, constructorId,
            state.UserId, msgId, seqNo, state.AuthKeyId, permAuthKeyId,
            dataBytes, state.Layer, date, state.DeviceType, envelope.ClientIp,
            state.SessionId, accessHashKeyId));
    }

    private async Task HandlePingAsync(SessionState state, long reqMsgId, IObject obj)
    {
        long pingId = obj switch
        {
            RequestPing p => p.PingId,
            RequestPingDelayDisconnect d => d.PingId,
            _ => 0
        };

        var pong = new TPong { MsgId = reqMsgId, PingId = pingId };
        await SendObjectAsync(state, pong, contentRelated: false);
        logger.LogDebug("Ping handled authKey={AuthKeyId:x} user={UserId}", state.AuthKeyId, state.UserId);
    }

    public async Task SendObjectAsync(SessionState state, IObject body, bool contentRelated)
    {
        var bodyBytes = body.ToBytes();
        var seqNo = NextServerSeq(state, contentRelated);
        var msgId = messageIdHelper.GenerateMessageId();
        var inner = crypto.BuildInnerMessage(state.ServerSalt, state.SessionId, msgId, seqNo, bodyBytes);
        var encrypted = crypto.EncryptServerPayload(state.AuthKeyId, state.AuthKey, inner);

        await eventBus.PublishAsync(new EncryptedMessageResponse(
            state.AuthKeyId,
            encrypted,
            state.ConnectionId,
            seqNo));
    }

    public async Task SendRawResultAsync(SessionState state, ReadOnlyMemory<byte> resultTlBytes)
    {
        // Result is already a serialized TL object (often rpc_result)
        var seqNo = NextServerSeq(state, contentRelated: true);
        var msgId = messageIdHelper.GenerateMessageId();
        var inner = crypto.BuildInnerMessage(state.ServerSalt, state.SessionId, msgId, seqNo, resultTlBytes.Span);
        var encrypted = crypto.EncryptServerPayload(state.AuthKeyId, state.AuthKey, inner);
        await eventBus.PublishAsync(new EncryptedMessageResponse(
            state.AuthKeyId,
            encrypted,
            state.ConnectionId,
            seqNo));
    }

    private static int NextServerSeq(SessionState state, bool contentRelated)
    {
        // MTProto: content-related messages use odd seq_no
        if (contentRelated)
        {
            if ((state.ServerSeqNo & 1) == 0)
            {
                state.ServerSeqNo++;
            }
            else
            {
                state.ServerSeqNo += 2;
            }
        }
        else
        {
            if ((state.ServerSeqNo & 1) == 1)
            {
                state.ServerSeqNo++;
            }
            else
            {
                state.ServerSeqNo += 2;
            }
        }

        return state.ServerSeqNo;
    }
}
