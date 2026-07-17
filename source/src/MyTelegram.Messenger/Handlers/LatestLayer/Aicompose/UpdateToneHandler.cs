namespace MyTelegram.Messenger.Handlers.LatestLayer.Aicompose;

internal sealed class UpdateToneHandler
    : RpcResultObjectHandler<MyTelegram.Schema.Aicompose.RequestUpdateTone, MyTelegram.Schema.IAiComposeTone>
{
    protected override Task<MyTelegram.Schema.IAiComposeTone> HandleCoreAsync(
        IRequestInput input,
        MyTelegram.Schema.Aicompose.RequestUpdateTone obj)
    {
        long id = 0;
        long accessHash = 0;
        string slug = "local";
        switch (obj.Tone)
        {
            case TInputAiComposeToneID toneId:
                id = toneId.Id;
                accessHash = toneId.AccessHash;
                break;
            case TInputAiComposeToneSlug toneSlug:
                slug = toneSlug.Slug;
                break;
        }

        return Task.FromResult<MyTelegram.Schema.IAiComposeTone>(new TAiComposeTone
        {
            Creator = true,
            Id = id == 0 ? Random.Shared.NextInt64(1, long.MaxValue) : id,
            AccessHash = accessHash,
            Slug = slug,
            Title = obj.Title ?? string.Empty,
            Prompt = obj.Prompt,
            EmojiId = obj.EmojiId
        });
    }
}
