namespace MyTelegram.Core;

public interface IMtpHelper
{
    void CalcTempAesKeyData(byte[] newNonce,
        byte[] serverNonce, Span<byte> aesKey, Span<byte> aesIv);

    long ComputeSalt(byte[] newNonce,
        byte[] serverNonce);

    void Encrypt(long authKeyId, byte[] authKeyData, ReadOnlySpan<byte> data, Span<byte> outputBuffer);

    /// <summary>
    /// Decrypt client→server encrypted payload (msg_key + encrypted_data without auth_key_id prefix).
    /// Returns plaintext length written to <paramref name="destination"/>.
    /// </summary>
    int Decrypt(byte[] authKeyData, ReadOnlySpan<byte> msgKey, ReadOnlySpan<byte> encryptedData, Span<byte> destination);
}