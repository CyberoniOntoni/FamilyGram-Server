namespace MyTelegram.Messenger.Handlers.LatestLayer.Auth;
/// <summary>
/// Resend the login code via another medium, the phone code type is determined by the return value of the previous auth.sendCode/auth.resendCode: see <a href="https://corefork.telegram.org/api/auth">login</a> for more info.
/// Possible errors
/// Code Type Description
/// 400 EMAIL_INSTALL_MISSING  
/// 400 PHONE_CODE_EMPTY phone_code is missing.
/// 400 PHONE_CODE_EXPIRED The phone code you provided has expired.
/// 400 PHONE_CODE_HASH_EMPTY phone_code_hash is missing.
/// 406 PHONE_NUMBER_INVALID The phone number is invalid.
/// 406 SEND_CODE_UNAVAILABLE Returned when all available options for this type of number were already used (e.g. flash-call, then SMS, then this error might be returned to trigger a second resend).
/// <para><c>See <a href="https://corefork.telegram.org/method/auth.resendCode"/> </c></para>
/// </summary>
/// <remarks>
/// Access: [User ✔] [Bot ✖] [Anonymous ✔]
/// </remarks>
internal sealed class ResendCodeHandler(
    ICommandBus commandBus,
    IQueryProcessor queryProcessor,
    IVerificationCodeGenerator verificationCodeGenerator,
    IOptionsMonitor<MyTelegramMessengerServerOptions> options)
    : RpcResultObjectHandler<MyTelegram.Schema.Auth.RequestResendCode, MyTelegram.Schema.Auth.ISentCode>
{
    protected override async Task<MyTelegram.Schema.Auth.ISentCode> HandleCoreAsync(IRequestInput input, MyTelegram.Schema.Auth.RequestResendCode obj)
    {
        var phoneNumber = obj.PhoneNumber.ToPhoneNumber();
        if (!long.TryParse(phoneNumber, out _))
            RpcErrors.RpcErrors400.PhoneNumberInvalid.ThrowRpcError();

        if (string.IsNullOrWhiteSpace(obj.PhoneCodeHash))
            RpcErrors.RpcErrors400.PhoneCodeHashEmpty.ThrowRpcError();

        var appCode = await queryProcessor.ProcessAsync(new GetLatestAppCodeQuery(phoneNumber, obj.PhoneCodeHash));
        if (appCode == null || appCode.Expire < DateTime.UtcNow.ToTimestamp())
            RpcErrors.RpcErrors400.PhoneCodeExpired.ThrowRpcError();

        var userReadModel = await queryProcessor.ProcessAsync(new GetUserByPhoneNumberQuery(phoneNumber));
        var userId = userReadModel?.UserId ?? 0;
        var code = verificationCodeGenerator.Generate();

        await commandBus.PublishAsync(new ResendCodeCommand(
            AppCodeId.Create(phoneNumber, obj.PhoneCodeHash),
            input.ToRequestInfo() with { UserId = userId },
            userId,
            phoneNumber,
            code,
            obj.PhoneCodeHash,
            DateTime.UtcNow.ToTimestamp()));

        return new TSentCode
        {
            Type = new TSentCodeTypeSms { Length = code.Length },
            PhoneCodeHash = obj.PhoneCodeHash,
            Timeout = options.CurrentValue.VerificationCodeExpirationSeconds
        };
    }
}