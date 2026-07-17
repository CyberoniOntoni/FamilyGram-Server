using MyTelegram.SessionServer.Services;

namespace MyTelegram.SessionServer.EventHandlers;

public sealed class LayeredPushMessageEventHandler(
    IAuthKeyStore authKeyStore,
    SessionRequestDispatcher dispatcher,
    ILogger<LayeredPushMessageEventHandler> logger)
    : IEventHandler<LayeredPushMessageCreatedIntegrationEvent>, ITransientDependency
{
    public async Task HandleEventAsync(LayeredPushMessageCreatedIntegrationEvent eventData)
    {
        if (eventData.PeerType != PeerType.User)
        {
            // Channels/groups: closed server fans out to members; MVP only handles user peers.
            logger.LogDebug("Skip push for peer type {PeerType} id={PeerId}", eventData.PeerType, eventData.PeerId);
            return;
        }

        var sessions = await authKeyStore.GetOnlineByUserIdAsync(eventData.PeerId);
        if (sessions.Count == 0)
        {
            logger.LogDebug("No online sessions for user {UserId}", eventData.PeerId);
            return;
        }

        foreach (var session in sessions)
        {
            if (eventData.ExcludeAuthKeyId is long excludeKey && excludeKey == session.AuthKeyId)
            {
                continue;
            }

            if (eventData.OnlySendToThisAuthKeyId is long only && only != 0 && only != session.AuthKeyId)
            {
                continue;
            }

            if (string.IsNullOrEmpty(session.ConnectionId))
            {
                continue;
            }

            try
            {
                await dispatcher.SendRawResultAsync(session, eventData.Data);
            }
            catch (Exception pushEx)
            {
                logger.LogWarning(pushEx, "Push failed user={UserId} authKey={AuthKeyId:x}", eventData.PeerId, session.AuthKeyId);
            }
        }
    }
}
