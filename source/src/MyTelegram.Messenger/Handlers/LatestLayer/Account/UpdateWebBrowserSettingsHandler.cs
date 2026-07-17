namespace MyTelegram.Messenger.Handlers.LatestLayer.Account;

internal sealed class UpdateWebBrowserSettingsHandler
    : RpcResultObjectHandler<MyTelegram.Schema.Account.RequestUpdateWebBrowserSettings, MyTelegram.Schema.Account.IWebBrowserSettings>
{
    protected override Task<MyTelegram.Schema.Account.IWebBrowserSettings> HandleCoreAsync(
        IRequestInput input,
        MyTelegram.Schema.Account.RequestUpdateWebBrowserSettings obj)
    {
        return Task.FromResult<MyTelegram.Schema.Account.IWebBrowserSettings>(
            new MyTelegram.Schema.Account.TWebBrowserSettings
            {
                OpenExternalBrowser = obj.OpenExternalBrowser,
                DisplayCloseButton = obj.DisplayCloseButton,
                ExternalExceptions = [],
                InappExceptions = [],
                Hash = 1
            });
    }
}
