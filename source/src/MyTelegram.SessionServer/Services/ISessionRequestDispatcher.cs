using MyTelegram.Abstractions;

namespace MyTelegram.SessionServer.Services;

public interface ISessionRequestDispatcher
{
    Task HandleEncryptedAsync(EncryptedMessage message, CancellationToken cancellationToken = default);
}
