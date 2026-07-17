namespace MyTelegram.Messenger.Handlers.LatestLayer.Aicompose;

/// <summary>
/// Create a user AI compose tone. FamilyGram does not run AI compose backends;
/// return a synthetic tone so clients do not hard-fail.
/// </summary>
internal sealed class CreateToneHandler
    : RpcResultObjectHandler<MyTelegram.Schema.Aicompose.RequestCreateTone, MyTelegram.Schema.IAiComposeTone>
{
    protected override Task<MyTelegram.Schema.IAiComposeTone> HandleCoreAsync(
        IRequestInput input,
        MyTelegram.Schema.Aicompose.RequestCreateTone obj)
    {
        var id = Random.Shared.NextInt64(1, long.MaxValue);
        return Task.FromResult<MyTelegram.Schema.IAiComposeTone>(new TAiComposeTone
        {
            Creator = true,
            Id = id,
            AccessHash = id,
            Slug = $"local-{id:x}",
            Title = obj.Title ?? string.Empty,
            Prompt = obj.Prompt,
            EmojiId = obj.EmojiId,
            InstallsCount = 0
        });
    }
}
