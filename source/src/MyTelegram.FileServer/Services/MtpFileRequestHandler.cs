using MyTelegram.Core;
using MyTelegram.EventBus;
using MyTelegram.Schema;
using MyTelegram.Schema.Extensions;
using MyTelegram.Schema.Upload;
using TFile = MyTelegram.Schema.Upload.TFile;
using TFilePartial = MyTelegram.Schema.Storage.TFilePartial;

namespace MyTelegram.FileServer.Services;

public sealed class MtpFileRequestHandler(
    IFilePartStore partStore,
    IObjectStorage objectStorage,
    IMediaFactory mediaFactory,
    IEventBus eventBus,
    ILogger<MtpFileRequestHandler> logger) : IMtpFileRequestHandler
{
    public Task HandleUploadAsync(UploadDataReceivedEvent eventData, CancellationToken cancellationToken = default)
        => HandleAsync(eventData, cancellationToken);

    public Task HandleDownloadAsync(DownloadDataReceivedEvent eventData, CancellationToken cancellationToken = default)
        => HandleAsync(eventData, cancellationToken);

    private async Task HandleAsync(DataReceivedEvent eventData, CancellationToken cancellationToken)
    {
        try
        {
            var obj = eventData.Data.ToTObject<IObject>();
            switch (obj)
            {
                case RequestSaveFilePart save:
                    await partStore.SavePartAsync(eventData.UserId, save.FileId, save.FilePart, save.Bytes, cancellationToken);
                    await PublishRpcAsync(eventData, new TBoolTrue());
                    return;
                case RequestSaveBigFilePart saveBig:
                    await partStore.SaveBigPartAsync(eventData.UserId, saveBig.FileId, saveBig.FilePart, saveBig.FileTotalParts, saveBig.Bytes, cancellationToken);
                    await PublishRpcAsync(eventData, new TBoolTrue());
                    return;
                case RequestGetFile getFile:
                    await HandleGetFileAsync(eventData, getFile, cancellationToken);
                    return;
                default:
                    logger.LogWarning(
                        "Unhandled file-lane objectId={ObjectId:x8} type={Type} reqMsgId={ReqMsgId}",
                        eventData.ObjectId,
                        obj.GetType().Name,
                        eventData.ReqMsgId);
                    await PublishRpcAsync(eventData, new TRpcError
                    {
                        ErrorCode = 400,
                        ErrorMessage = "FILE_ID_INVALID"
                    });
                    return;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "File request failed objectId={ObjectId:x8} reqMsgId={ReqMsgId}", eventData.ObjectId, eventData.ReqMsgId);
            await PublishRpcAsync(eventData, new TRpcError
            {
                ErrorCode = 400,
                ErrorMessage = "FILE_ID_INVALID"
            });
        }
    }

    private async Task HandleGetFileAsync(DataReceivedEvent eventData, RequestGetFile getFile, CancellationToken cancellationToken)
    {
        var limit = getFile.Limit;
        if (limit <= 0 || limit > 1024 * 1024)
        {
            await PublishRpcAsync(eventData, new TRpcError { ErrorCode = 400, ErrorMessage = "LIMIT_INVALID" });
            return;
        }

        if (getFile.Offset < 0)
        {
            await PublishRpcAsync(eventData, new TRpcError { ErrorCode = 400, ErrorMessage = "OFFSET_INVALID" });
            return;
        }

        var fileId = ExtractFileId(getFile.Location);
        if (fileId == 0)
        {
            await PublishRpcAsync(eventData, new TRpcError { ErrorCode = 400, ErrorMessage = "LOCATION_INVALID" });
            return;
        }

        byte[]? chunk = null;
        var key = mediaFactory.ObjectKeyForFile(fileId);
        chunk = await objectStorage.GetRangeAsync(key, getFile.Offset, limit, cancellationToken);

        if (chunk is null)
        {
            var assembled = await partStore.AssembleAsync(eventData.UserId, fileId, cancellationToken)
                            ?? await partStore.AssembleByFileIdAsync(fileId, cancellationToken);
            if (assembled is not null)
            {
                await mediaFactory.EnsureStoredAsync(fileId, assembled, "application/octet-stream", cancellationToken);
                var start = (int)Math.Min(getFile.Offset, assembled.Length);
                var length = Math.Min(limit, assembled.Length - start);
                chunk = length <= 0 ? [] : assembled.AsSpan(start, length).ToArray();
            }
        }

        if (chunk is null)
        {
            await PublishRpcAsync(eventData, new TRpcError { ErrorCode = 400, ErrorMessage = "FILE_ID_INVALID" });
            return;
        }

        var file = new TFile
        {
            Type = new TFilePartial(),
            Mtime = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Bytes = chunk
        };

        // File downloads use FileDataResult path (not RpcResult wrapper) like closed file-server.
        await eventBus.PublishAsync(new FileDataResultResponseReceivedEvent(
            eventData.ConnectionId,
            eventData.AuthKeyId,
            eventData.SessionId,
            eventData.ReqMsgId,
            file.ToBytes()));
    }

    private static long ExtractFileId(IInputFileLocation location) =>
        location switch
        {
            TInputDocumentFileLocation doc => doc.Id,
            TInputPhotoFileLocation photo => photo.Id,
            TInputPeerPhotoFileLocation peerPhoto => peerPhoto.PhotoId,
            TInputFileLocation legacy => legacy.VolumeId,
            TInputSecureFileLocation secure => secure.Id,
            TInputEncryptedFileLocation encrypted => encrypted.Id,
            TInputStickerSetThumb => 0,
            _ => 0
        };

    private async Task PublishRpcAsync(DataReceivedEvent eventData, IObject result)
    {
        var rpc = new TRpcResult
        {
            ReqMsgId = eventData.ReqMsgId,
            Result = result
        };
        await eventBus.PublishAsync(new DataResultResponseReceivedEvent(
            eventData.ConnectionId,
            eventData.AuthKeyId,
            eventData.SessionId,
            eventData.ReqMsgId,
            rpc.ToBytes()));
    }
}
