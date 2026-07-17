namespace MyTelegram.Messenger.Services;

/// <summary>
/// Layer 228 changed channels.joinChannel / messages.importChatInvite return type from
/// Updates to messages.ChatInviteJoinResult. Wrap only the RPC result (not fan-out pushes).
/// </summary>
public static class ChatInviteJoinResultHelper
{
    public static IObject WrapForRpc(IUpdates updates, int layer)
    {
        // invokeWithLayer 0 / unset is treated as Latest (228).
        if (layer == 0 || layer >= 228)
        {
            return new MyTelegram.Schema.Messages.TChatInviteJoinResultOk
            {
                Updates = updates
            };
        }

        return updates;
    }
}
