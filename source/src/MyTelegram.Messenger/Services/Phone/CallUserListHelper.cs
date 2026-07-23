namespace MyTelegram.Messenger.Services.Phone;

/// <summary>
/// Builds user vectors for phone-call updates with the correct Self flag for the recipient.
/// Pushing users built for the actor's viewpoint marks the contact as isSelf on the peer client,
/// which makes the web UI show "Saved messages" as the chat title.
/// </summary>
public static class CallUserListHelper
{
    public static RequestInfo ForViewer(IRequestInput actor, long viewerUserId, long viewerPermAuthKeyId = 0)
    {
        var accessHashKeyId = viewerPermAuthKeyId != 0 ? viewerPermAuthKeyId : viewerUserId;
        return actor.ToRequestInfo() with
        {
            UserId = viewerUserId,
            AccessHashKeyId = accessHashKeyId,
            PermAuthKeyId = viewerPermAuthKeyId,
            ReqMsgId = 0
        };
    }

    public static Task<List<ILayeredUser>> GetUserListForViewerAsync(
        IUserConverterService userConverterService,
        IRequestInput actor,
        long viewerUserId,
        IReadOnlyList<long> userIds,
        int layer,
        long viewerPermAuthKeyId = 0)
    {
        var request = ForViewer(actor, viewerUserId, viewerPermAuthKeyId);
        return userConverterService.GetUserListAsync(request, userIds.ToList(), false, false, layer);
    }
}
