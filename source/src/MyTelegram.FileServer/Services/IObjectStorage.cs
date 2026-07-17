namespace MyTelegram.FileServer.Services;

public interface IObjectStorage
{
    Task EnsureBucketAsync(CancellationToken cancellationToken = default);
    Task PutAsync(string objectName, Stream data, long size, string contentType, CancellationToken cancellationToken = default);
    Task PutBytesAsync(string objectName, ReadOnlyMemory<byte> data, string contentType, CancellationToken cancellationToken = default);
    Task<byte[]?> GetRangeAsync(string objectName, long offset, int limit, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string objectName, CancellationToken cancellationToken = default);
    Task<long?> GetSizeAsync(string objectName, CancellationToken cancellationToken = default);
}
