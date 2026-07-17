using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using MyTelegram.FileServer.Options;
using MyTelegram.Schema;
using MyTelegram.Schema.Extensions;

namespace MyTelegram.FileServer.Services;

public sealed class MediaFactory(
    IFilePartStore partStore,
    IObjectStorage objectStorage,
    IOptions<FileServerAppOptions> appOptions,
    ILogger<MediaFactory> logger) : IMediaFactory
{
    private readonly FileServerAppOptions _app = appOptions.Value;

    public string ObjectKeyForFile(long fileId) => $"files/{fileId}";

    public async Task EnsureStoredAsync(long fileId, byte[] data, string contentType, CancellationToken cancellationToken = default)
    {
        var key = ObjectKeyForFile(fileId);
        if (await objectStorage.ExistsAsync(key, cancellationToken))
        {
            return;
        }

        await objectStorage.PutBytesAsync(key, data, contentType, cancellationToken);
        logger.LogInformation("Stored object {Key} size={Size} type={ContentType}", key, data.Length, contentType);
    }

    public async Task<(IPhoto Photo, long PhotoId, long Size)> CreatePhotoAsync(
        long userId,
        long clientFileId,
        int parts,
        string? name,
        CancellationToken cancellationToken = default)
    {
        var data = await partStore.AssembleAsync(userId, clientFileId, cancellationToken)
                   ?? await partStore.AssembleByFileIdAsync(clientFileId, cancellationToken)
                   ?? throw new InvalidOperationException($"No parts for fileId={clientFileId}");

        var photoId = clientFileId != 0 ? clientFileId : Random.Shared.NextInt64();
        await EnsureStoredAsync(photoId, data, GuessImageMime(name), cancellationToken);

        var photo = new TPhoto
        {
            Id = photoId,
            AccessHash = Random.Shared.NextInt64(),
            FileReference = RandomNumberGenerator.GetBytes(16),
            Date = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            DcId = _app.MediaDcId,
            Sizes =
            [
                new TPhotoSize
                {
                    Type = "x",
                    W = 800,
                    H = 800,
                    Size = data.Length
                }
            ],
            VideoSizes = []
        };

        return (photo, photoId, data.Length);
    }

    public async Task<IMessageMedia> CreateMediaAsync(IInputMedia media, long userId, CancellationToken cancellationToken = default)
    {
        switch (media)
        {
            case TInputMediaUploadedPhoto uploadedPhoto:
            {
                var (fileId, parts, name) = ExtractInputFile(uploadedPhoto.File);
                var (photo, _, _) = await CreatePhotoAsync(userId, fileId, parts, name, cancellationToken);
                return new TMessageMediaPhoto
                {
                    Photo = photo,
                    Spoiler = uploadedPhoto.Spoiler
                };
            }
            case TInputMediaUploadedDocument uploadedDoc:
            {
                var (fileId, _, name) = ExtractInputFile(uploadedDoc.File);
                var data = await partStore.AssembleAsync(userId, fileId, cancellationToken)
                           ?? await partStore.AssembleByFileIdAsync(fileId, cancellationToken)
                           ?? throw new InvalidOperationException($"No parts for document fileId={fileId}");

                var docId = fileId != 0 ? fileId : Random.Shared.NextInt64();
                var mime = string.IsNullOrWhiteSpace(uploadedDoc.MimeType)
                    ? GuessMime(name)
                    : uploadedDoc.MimeType;
                await EnsureStoredAsync(docId, data, mime, cancellationToken);

                var attributes = uploadedDoc.Attributes ?? [];
                if (!string.IsNullOrEmpty(name) && attributes.OfType<TDocumentAttributeFilename>().All(a => a.FileName != name))
                {
                    attributes = [.. attributes, new TDocumentAttributeFilename { FileName = name }];
                }

                return new TMessageMediaDocument
                {
                    Document = new TDocument
                    {
                        Id = docId,
                        AccessHash = Random.Shared.NextInt64(),
                        FileReference = RandomNumberGenerator.GetBytes(16),
                        Date = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        MimeType = mime,
                        Size = data.Length,
                        DcId = _app.MediaDcId,
                        Attributes = attributes,
                        Thumbs = [],
                        VideoThumbs = []
                    },
                    Spoiler = uploadedDoc.Spoiler
                };
            }
            case TInputMediaPhoto photo when photo.Id is TInputPhoto inputPhoto:
                return new TMessageMediaPhoto
                {
                    Photo = new TPhoto
                    {
                        Id = inputPhoto.Id,
                        AccessHash = inputPhoto.AccessHash,
                        FileReference = inputPhoto.FileReference.ToArray(),
                        Date = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        DcId = _app.MediaDcId,
                        Sizes = [],
                        VideoSizes = []
                    }
                };
            case TInputMediaDocument document when document.Id is TInputDocument inputDoc:
                return new TMessageMediaDocument
                {
                    Document = new TDocument
                    {
                        Id = inputDoc.Id,
                        AccessHash = inputDoc.AccessHash,
                        FileReference = inputDoc.FileReference,
                        Date = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        MimeType = "application/octet-stream",
                        Size = 0,
                        DcId = _app.MediaDcId,
                        Attributes = [],
                        Thumbs = [],
                        VideoThumbs = []
                    }
                };
            default:
                logger.LogWarning("Unsupported input media type {Type}", media.GetType().Name);
                throw new NotSupportedException($"Unsupported media type {media.GetType().Name}");
        }
    }

    private static (long FileId, int Parts, string Name) ExtractInputFile(IInputFile file) =>
        file switch
        {
            TInputFile f => (f.Id, f.Parts, f.Name ?? string.Empty),
            TInputFileBig big => (big.Id, big.Parts, big.Name ?? string.Empty),
            _ => throw new NotSupportedException($"Unsupported InputFile {file.GetType().Name}")
        };

    private static string GuessImageMime(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "image/jpeg";
        }

        if (name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            return "image/png";
        }

        if (name.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
        {
            return "image/webp";
        }

        if (name.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
        {
            return "image/gif";
        }

        return "image/jpeg";
    }

    private static string GuessMime(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "application/octet-stream";
        }

        if (name.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".oga", StringComparison.OrdinalIgnoreCase))
        {
            return "audio/ogg";
        }

        if (name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
        {
            return "video/mp4";
        }

        if (name.EndsWith(".tgs", StringComparison.OrdinalIgnoreCase))
        {
            return "application/x-tgsticker";
        }

        if (name.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
        {
            return "image/webp";
        }

        return GuessImageMime(name);
    }
}
