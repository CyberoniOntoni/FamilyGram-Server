using MyTelegram.Core;

namespace MyTelegram.FileServer.Services;

public interface IMtpFileRequestHandler
{
    Task HandleUploadAsync(UploadDataReceivedEvent eventData, CancellationToken cancellationToken = default);
    Task HandleDownloadAsync(DownloadDataReceivedEvent eventData, CancellationToken cancellationToken = default);
}
