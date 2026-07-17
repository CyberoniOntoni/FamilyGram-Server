namespace MyTelegram.Messenger.Handlers.LatestLayer.Aicompose;

internal sealed class GetToneHandler
    : RpcResultObjectHandler<MyTelegram.Schema.Aicompose.RequestGetTone, MyTelegram.Schema.Aicompose.ITones>
{
    protected override Task<MyTelegram.Schema.Aicompose.ITones> HandleCoreAsync(
        IRequestInput input,
        MyTelegram.Schema.Aicompose.RequestGetTone obj)
    {
        return Task.FromResult<MyTelegram.Schema.Aicompose.ITones>(new MyTelegram.Schema.Aicompose.TTones
        {
            Hash = 0,
            Tones = [],
            Users = []
        });
    }
}
