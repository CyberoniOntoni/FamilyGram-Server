namespace MyTelegram.FileServer.Services;

public interface IFilePartStore
{
    Task SavePartAsync(long userId, long fileId, int filePart, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default);
    Task SaveBigPartAsync(long userId, long fileId, int filePart, int fileTotalParts, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default);
    Task<byte[]?> AssembleAsync(long userId, long fileId, CancellationToken cancellationToken = default);
    Task<byte[]?> AssembleByFileIdAsync(long fileId, CancellationToken cancellationToken = default);
}
