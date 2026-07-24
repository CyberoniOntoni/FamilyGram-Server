using MongoDB.Bson;
using MongoDB.Driver;
using MyTelegram.Messenger.Services.Phone;

namespace MyTelegram.Messenger.Handlers.LatestLayer.Upload;

/// <summary>
/// Returns content of a whole file or its part.
/// See https://core.telegram.org/method/upload.getFile
/// </summary>
internal sealed class GetFileHandler : RpcResultObjectHandler<MyTelegram.Schema.Upload.RequestGetFile, MyTelegram.Schema.Upload.IFile>
{
    private readonly IMongoDatabase _database;
    private readonly ILogger<GetFileHandler> _logger;

    public GetFileHandler(IMongoDatabase database, ILogger<GetFileHandler> logger)
    {
        _database = database;
        _logger = logger;
    }

    protected override async Task<MyTelegram.Schema.Upload.IFile> HandleCoreAsync(IRequestInput input, MyTelegram.Schema.Upload.RequestGetFile obj)
    {
        // Validate input
        if (obj.Limit <= 0 || obj.Limit > 1024 * 1024) // Max 1MB per request
        {
            RpcErrors.RpcErrors400.LimitInvalid.ThrowRpcError();
        }

        if (obj.Offset < 0)
        {
            RpcErrors.RpcErrors400.OffsetInvalid.ThrowRpcError();
        }

        if (obj.Location is TInputGroupCallStream groupCallStream)
        {
            return await HandleGroupCallStreamAsync(input, groupCallStream);
        }

        // Extract file ID from location
        long fileId = obj.Location switch
        {
            TInputDocumentFileLocation doc => doc.Id,
            TInputPhotoFileLocation photo => photo.Id,
            TInputFileLocation file => file.VolumeId, // Legacy
            TInputPeerPhotoFileLocation peerPhoto => peerPhoto.PhotoId,
            _ => 0
        };

        if (fileId == 0)
        {
            RpcErrors.RpcErrors400.LocationInvalid.ThrowRpcError();
        }

        // Prefer parts by FileId only (NOT UserId). Uploads are stored under the
        // sender's UserId; recipients must still be able to download the same FileId.
        var partsCollection = _database.GetCollection<BsonDocument>("file_parts");
        var partsFilter = Builders<BsonDocument>.Filter.Eq("FileId", fileId);
        var parts = await partsCollection.Find(partsFilter)
            .Sort(Builders<BsonDocument>.Sort.Ascending("FilePart"))
            .ToListAsync();

        if (parts.Count > 0)
        {
            var allBytes = new List<byte>();
            foreach (var part in parts)
            {
                var partBytes = part["Bytes"].AsByteArray;
                allBytes.AddRange(partBytes);
            }

            var fileBytes = allBytes.ToArray();
            return SliceFile(fileBytes, obj.Offset, obj.Limit, fileId, "parts");
        }

        // Fallback: known photo/document metadata exists but parts were purged after
        // MinIO store. Point clients at FileServer path by logging; still try empty fail.
        var documentsCollection = _database.GetCollection<BsonDocument>("eventflow-documentreadmodel");
        var docFilter = Builders<BsonDocument>.Filter.Eq("DocumentId", fileId);
        var document = await documentsCollection.Find(docFilter).FirstOrDefaultAsync();

        if (document == null)
        {
            var photosCollection = _database.GetCollection<BsonDocument>("eventflow-photoreadmodel");
            var photoFilter = Builders<BsonDocument>.Filter.Eq("PhotoId", fileId);
            var photo = await photosCollection.Find(photoFilter).FirstOrDefaultAsync();

            if (photo == null)
            {
                _logger.LogWarning(
                    "GetFile FILE_ID_INVALID user={UserId} fileId={FileId} (no parts, no photo/document)",
                    input.UserId,
                    fileId);
                RpcErrors.RpcErrors400.FileIdInvalid.ThrowRpcError();
            }
        }

        // Parts missing after MinIO promotion — messenger has no MinIO client.
        // Return empty slice only when offset past EOF is ambiguous; otherwise invalid.
        _logger.LogWarning(
            "GetFile has metadata but no file_parts for fileId={FileId} user={UserId} offset={Offset}; " +
            "client should hit FileServer MinIO path. Returning FILE_ID_INVALID.",
            fileId,
            input.UserId,
            obj.Offset);
        RpcErrors.RpcErrors400.FileIdInvalid.ThrowRpcError();

        throw new InvalidOperationException();
    }

    private MyTelegram.Schema.Upload.TFile SliceFile(byte[] fileBytes, long offset, int limit, long fileId, string source)
    {
        var start = (int)Math.Min(offset, fileBytes.Length);
        var length = Math.Min(limit, Math.Max(0, fileBytes.Length - start));
        var resultBytes = length <= 0 ? Array.Empty<byte>() : new byte[length];
        if (length > 0)
        {
            Array.Copy(fileBytes, start, resultBytes, 0, length);
        }

        _logger.LogInformation(
            "GetFile served from {Source}: FileId={FileId}, Offset={Offset}, Limit={Limit}, Returned={Length}, Total={Total}",
            source,
            fileId,
            offset,
            limit,
            resultBytes.Length,
            fileBytes.Length);

        return new MyTelegram.Schema.Upload.TFile
        {
            Type = new MyTelegram.Schema.Storage.TFilePartial(),
            Mtime = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Bytes = resultBytes
        };
    }

    private async Task<MyTelegram.Schema.Upload.IFile> HandleGroupCallStreamAsync(
        IRequestInput input,
        TInputGroupCallStream location)
    {
        if (location.Call is not TInputGroupCall inputGroupCall)
        {
            RpcErrors.RpcErrors400.LocationInvalid.ThrowRpcError();
            return null!;
        }

        var groupCalls = _database.GetCollection<GroupCallDocument>("group_calls");
        var groupCall = await groupCalls.Find(GroupCallStateHelper.Filter(inputGroupCall)).FirstOrDefaultAsync();
        if (groupCall == null || !groupCall.Active)
        {
            RpcErrors.RpcErrors400.LocationInvalid.ThrowRpcError();
            return null!;
        }

        if (!GroupCallStateHelper.IsJoinedByUser(groupCall, input.UserId))
        {
            RpcErrors.RpcErrors400.GroupcallJoinMissing.ThrowRpcError();
            return null!;
        }

        _logger.LogDebug(
            "Returning empty group call stream chunk: CallId={CallId}, TimeMs={TimeMs}, Scale={Scale}, VideoChannel={VideoChannel}, VideoQuality={VideoQuality}",
            inputGroupCall.Id,
            location.TimeMs,
            location.Scale,
            location.VideoChannel,
            location.VideoQuality);

        return new MyTelegram.Schema.Upload.TFile
        {
            Type = new MyTelegram.Schema.Storage.TFilePartial(),
            Mtime = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Bytes = ReadOnlyMemory<byte>.Empty
        };
    }
}
