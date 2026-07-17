namespace MyTelegram.Messenger.Handlers.LatestLayer.Aicompose;

internal sealed class SaveToneHandler
    : RpcResultObjectHandler<MyTelegram.Schema.Aicompose.RequestSaveTone, IBool>
{
    protected override Task<IBool> HandleCoreAsync(
        IRequestInput input,
        MyTelegram.Schema.Aicompose.RequestSaveTone obj)
    {
        return Task.FromResult<IBool>(new TBoolTrue());
    }
}
