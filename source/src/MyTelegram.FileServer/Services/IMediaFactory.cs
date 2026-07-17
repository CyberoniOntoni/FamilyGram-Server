using MyTelegram.Schema;

namespace MyTelegram.FileServer.Services;

public interface IMediaFactory
{
    Task<(IPhoto Photo, long PhotoId, long Size)> CreatePhotoAsync(long userId, long clientFileId, int parts, string? name, CancellationToken cancellationToken = default);
    Task<IMessageMedia> CreateMediaAsync(IInputMedia media, long userId, CancellationToken cancellationToken = default);
    string ObjectKeyForFile(long fileId);
    Task EnsureStoredAsync(long fileId, byte[] data, string contentType, CancellationToken cancellationToken = default);
}
