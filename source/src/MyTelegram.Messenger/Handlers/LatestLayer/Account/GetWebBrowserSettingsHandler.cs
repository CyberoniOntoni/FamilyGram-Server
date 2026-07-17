namespace MyTelegram.Messenger.Handlers.LatestLayer.Account;

/// <summary>
/// Default empty in-app browser settings (layer 228+).
/// </summary>
internal sealed class GetWebBrowserSettingsHandler
    : RpcResultObjectHandler<MyTelegram.Schema.Account.RequestGetWebBrowserSettings, MyTelegram.Schema.Account.IWebBrowserSettings>
{
    protected override Task<MyTelegram.Schema.Account.IWebBrowserSettings> HandleCoreAsync(
        IRequestInput input,
        MyTelegram.Schema.Account.RequestGetWebBrowserSettings obj)
    {
        if (obj.Hash != 0)
        {
            return Task.FromResult<MyTelegram.Schema.Account.IWebBrowserSettings>(
                new MyTelegram.Schema.Account.TWebBrowserSettingsNotModified());
        }

        return Task.FromResult<MyTelegram.Schema.Account.IWebBrowserSettings>(
            new MyTelegram.Schema.Account.TWebBrowserSettings
            {
                OpenExternalBrowser = false,
                DisplayCloseButton = true,
                ExternalExceptions = [],
                InappExceptions = [],
                Hash = 1
            });
    }
}
