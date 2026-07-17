namespace MyTelegram.Messenger.Handlers.LatestLayer.Aicompose;

internal sealed class GetToneExampleHandler
    : RpcResultObjectHandler<MyTelegram.Schema.Aicompose.RequestGetToneExample, MyTelegram.Schema.IAiComposeToneExample>
{
    protected override Task<MyTelegram.Schema.IAiComposeToneExample> HandleCoreAsync(
        IRequestInput input,
        MyTelegram.Schema.Aicompose.RequestGetToneExample obj)
    {
        // Empty example pair — UI can still open without hard-failing.
        return Task.FromResult<MyTelegram.Schema.IAiComposeToneExample>(new TAiComposeToneExample
        {
            From = new TTextWithEntities { Text = string.Empty, Entities = [] },
            To = new TTextWithEntities { Text = string.Empty, Entities = [] }
        });
    }
}
