using MyTelegram.Messenger.Converters.ConverterServices.Messages;
using RequestGetRichMessage = MyTelegram.Schema.Messages.RequestGetRichMessage;

namespace MyTelegram.Messenger.Handlers.LatestLayer.Messages;

/// <summary>
/// Fetch a single message (layer 228+ rich-message path). Reuses getMessages storage.
/// <para>See <a href="https://corefork.telegram.org/method/messages.getRichMessage"/></para>
/// </summary>
internal sealed class GetRichMessageHandler(
    IMessageAppService messageAppService,
    IPeerHelper peerHelper,
    IAccessHashHelper accessHashHelper,
    IChannelAppService channelAppService,
    IQueryProcessor queryProcessor,
    IGetHistoryConverterService getHistoryConverterService)
    : RpcResultObjectHandler<RequestGetRichMessage, Schema.Messages.IMessages>
{
    protected override async Task<IMessages> HandleCoreAsync(IRequestInput input, RequestGetRichMessage obj)
    {
        await accessHashHelper.CheckAccessHashAsync(input, obj.Peer);
        var peer = peerHelper.GetPeer(obj.Peer, input.UserId);
        var ownerPeerId = peer.PeerType == PeerType.Channel ? peer.PeerId : input.UserId;

        if (peer.PeerType == PeerType.Channel)
        {
            var channelReadModel = await channelAppService.GetAsync(peer.PeerId);
            if (channelReadModel == null)
            {
                RpcErrors.RpcErrors400.ChannelInvalid.ThrowRpcError();
            }

            var channelMember = await queryProcessor.ProcessAsync(
                new GetChannelMemberByUserIdQuery(peer.PeerId, input.UserId));
            if (channelMember?.Kicked == true)
            {
                RpcErrors.RpcErrors400.ChannelPrivate.ThrowRpcError();
            }
        }

        var getMessageOutput = await messageAppService.GetMessagesAsync(
            new GetMessagesInput(input.UserId, ownerPeerId, [obj.Id], peer) { Limit = 1 });

        return getHistoryConverterService.ToMessages(input, getMessageOutput, input.Layer);
    }
}
