namespace MyTelegram.SessionServer.Services;

public interface IMtProtoSessionCrypto
{
    byte[] DecryptClientPayload(byte[] authKey, ReadOnlySpan<byte> msgKey, ReadOnlySpan<byte> encryptedData);
    byte[] EncryptServerPayload(long authKeyId, byte[] authKey, ReadOnlySpan<byte> plainInner);
    byte[] BuildInnerMessage(long salt, long sessionId, long messageId, int seqNo, ReadOnlySpan<byte> body);
}
