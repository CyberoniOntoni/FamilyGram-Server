namespace MyTelegram.Messenger.Handlers.LatestLayer.Auth;
/// <summary>
/// Cancel the login verification code
/// Possible errors
/// Code Type Description
/// 400 PHONE_CODE_EXPIRED The phone code you provided has expired.
/// 406 PHONE_NUMBER_INVALID The phone number is invalid.
/// <para><c>See <a href="https://corefork.telegram.org/method/auth.cancelCode"/> </c></para>
/// </summary>
/// <remarks>
/// Access: [User ✔] [Bot ✖] [Anonymous ✔]
/// </remarks>
internal sealed class CancelCodeHandler(ICommandBus commandBus, IQueryProcessor queryProcessor)
    : RpcResultObjectHandler<MyTelegram.Schema.Auth.RequestCancelCode, IBool>
{
    protected override async Task<IBool> HandleCoreAsync(IRequestInput input, RequestCancelCode obj)
    {
        var phoneNumber = obj.PhoneNumber.ToPhoneNumber();
        if (!long.TryParse(phoneNumber, out _))
            RpcErrors.RpcErrors400.PhoneNumberInvalid.ThrowRpcError();

        var appCode = await queryProcessor.ProcessAsync(new GetLatestAppCodeQuery(phoneNumber, obj.PhoneCodeHash));
        if (appCode == null || appCode.Expire < DateTime.UtcNow.ToTimestamp())
            RpcErrors.RpcErrors400.PhoneCodeExpired.ThrowRpcError();

        await commandBus.PublishAsync(new CancelCodeCommand(
            AppCodeId.Create(phoneNumber, obj.PhoneCodeHash),
            input.ToRequestInfo(),
            phoneNumber,
            obj.PhoneCodeHash));

        return new TBoolTrue();
    }
}