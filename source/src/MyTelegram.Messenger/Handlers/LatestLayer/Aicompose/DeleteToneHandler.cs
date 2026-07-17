namespace MyTelegram.Messenger.Handlers.LatestLayer.Aicompose;

internal sealed class DeleteToneHandler
    : RpcResultObjectHandler<MyTelegram.Schema.Aicompose.RequestDeleteTone, IBool>
{
    protected override Task<IBool> HandleCoreAsync(
        IRequestInput input,
        MyTelegram.Schema.Aicompose.RequestDeleteTone obj)
    {
        return Task.FromResult<IBool>(new TBoolTrue());
    }
}
