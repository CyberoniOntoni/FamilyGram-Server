using MyTelegram.Abstractions;
using MyTelegram.SessionServer.Services;

namespace MyTelegram.SessionServer.EventHandlers;

public sealed class EncryptedMessageEventHandler(
    ISessionRequestDispatcher dispatcher,
    ILogger<EncryptedMessageEventHandler> logger)
    : IEventHandler<EncryptedMessage>, ITransientDependency
{
    public async Task HandleEventAsync(EncryptedMessage eventData)
    {
        try
        {
            await dispatcher.HandleEncryptedAsync(eventData);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Encrypted message handling failed authKey={AuthKeyId:x}", eventData.AuthKeyId);
        }
    }
}
