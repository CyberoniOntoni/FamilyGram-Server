namespace MyTelegram.Messenger.Handlers.LatestLayer.Aicompose;

/// <summary>
/// List AI compose tones. FamilyGram does not host AI compose yet — return empty list.
/// <para>See <a href="https://corefork.telegram.org/method/aicompose.getTones"/></para>
/// </summary>
internal sealed class GetTonesHandler
    : RpcResultObjectHandler<MyTelegram.Schema.Aicompose.RequestGetTones, MyTelegram.Schema.Aicompose.ITones>
{
    protected override Task<MyTelegram.Schema.Aicompose.ITones> HandleCoreAsync(
        IRequestInput input,
        MyTelegram.Schema.Aicompose.RequestGetTones obj)
    {
        // Empty catalog; hash 0 so clients re-fetch cleanly next time.
        if (obj.Hash != 0)
        {
            return Task.FromResult<MyTelegram.Schema.Aicompose.ITones>(
                new MyTelegram.Schema.Aicompose.TTonesNotModified());
        }

        return Task.FromResult<MyTelegram.Schema.Aicompose.ITones>(new MyTelegram.Schema.Aicompose.TTones
        {
            Hash = 0,
            Tones = [],
            Users = []
        });
    }
}
