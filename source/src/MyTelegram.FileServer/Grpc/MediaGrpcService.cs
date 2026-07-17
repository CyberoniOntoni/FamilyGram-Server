using Google.Protobuf;
using MyTelegram.FileServer.Services;
using MyTelegram.GrpcService;
using MyTelegram.Schema;
using MyTelegram.Schema.Extensions;
using GrpcStatus = Grpc.Core.Status;
using GrpcStatusCode = Grpc.Core.StatusCode;
using GrpcRpcException = Grpc.Core.RpcException;
using ServerCallContext = Grpc.Core.ServerCallContext;

namespace MyTelegram.FileServer.Grpc;

public sealed class MediaGrpcService(
    IMediaFactory mediaFactory,
    IObjectStorage objectStorage,
    ILogger<MediaGrpcService> logger) : MediaService.MediaServiceBase
{
    public override async Task<SavePhotoResponse> SavePhoto(SavePhotoRequest request, ServerCallContext context)
    {
        try
        {
            var (photo, photoId, size) = await mediaFactory.CreatePhotoAsync(
                request.UserId,
                request.FileId,
                request.Parts,
                request.Name,
                context.CancellationToken);

            return new SavePhotoResponse
            {
                Photo = ByteString.CopyFrom(photo.ToBytes()),
                PhotoId = photoId,
                Size = size
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SavePhoto failed fileId={FileId}", request.FileId);
            throw new GrpcRpcException(new GrpcStatus(GrpcStatusCode.InvalidArgument, "FILE_ID_INVALID"));
        }
    }

    public override async Task<SaveMediaResponse> SaveMedia(SaveMediaRequest request, ServerCallContext context)
    {
        try
        {
            var input = request.Media.Memory.ToTObject<IInputMedia>();
            var result = await mediaFactory.CreateMediaAsync(input, request.UserId, context.CancellationToken);
            return new SaveMediaResponse
            {
                Media = ByteString.CopyFrom(result.ToBytes())
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveMedia failed");
            throw new GrpcRpcException(new GrpcStatus(GrpcStatusCode.InvalidArgument, "FILE_ID_INVALID"));
        }
    }

    public override async Task<CheckFileExistsResponse> Exists(CheckFileExistsRequest request, ServerCallContext context)
    {
        var exists = await objectStorage.ExistsAsync(request.FileName, context.CancellationToken);
        return new CheckFileExistsResponse { Exists = exists };
    }

    public override Task<SaveEncryptedFileResponse> SaveEncryptedFile(SaveEncryptedFileRequest request, ServerCallContext context)
    {
        logger.LogWarning("SaveEncryptedFile not implemented in MVP");
        throw new GrpcRpcException(new GrpcStatus(GrpcStatusCode.Unimplemented, "SaveEncryptedFile not implemented"));
    }

    public override Task<SaveStickerFileResponse> SaveStickerFile(SaveStickerFileRequest request, ServerCallContext context)
    {
        logger.LogWarning("SaveStickerFile not implemented in MVP");
        return Task.FromResult(new SaveStickerFileResponse { Success = false });
    }

    public override Task<CreateDocumentResponse> CreateDocument(CreateDocumentRequest request, ServerCallContext context)
    {
        logger.LogWarning("CreateDocument not implemented in MVP");
        return Task.FromResult(new CreateDocumentResponse { Success = false });
    }

    public override async Task<SaveFileDataResponse> SaveFile(SaveFileDataRequest request, ServerCallContext context)
    {
        try
        {
            var key = mediaFactory.ObjectKeyForFile(request.Id);
            await objectStorage.PutBytesAsync(key, request.Data.Memory, "application/octet-stream", context.CancellationToken);
            return new SaveFileDataResponse { Success = true };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveFile failed id={Id}", request.Id);
            return new SaveFileDataResponse { Success = false };
        }
    }
}
