using System.Buffers.Binary;
using System.Security.Cryptography;

namespace MyTelegram.SessionServer.Services;

public sealed class MtProtoSessionCrypto(IMtpHelper mtpHelper) : IMtProtoSessionCrypto
{
    public byte[] DecryptClientPayload(byte[] authKey, ReadOnlySpan<byte> msgKey, ReadOnlySpan<byte> encryptedData)
    {
        var plain = new byte[encryptedData.Length];
        mtpHelper.Decrypt(authKey, msgKey, encryptedData, plain);
        return plain;
    }

    public byte[] EncryptServerPayload(long authKeyId, byte[] authKey, ReadOnlySpan<byte> plainInner)
    {
        var padded = PadTo16(plainInner);
        var output = new byte[24 + padded.Length];
        mtpHelper.Encrypt(authKeyId, authKey, padded, output);
        return output;
    }

    public byte[] BuildInnerMessage(long salt, long sessionId, long messageId, int seqNo, ReadOnlySpan<byte> body)
    {
        var len = 8 + 8 + 8 + 4 + 4 + body.Length;
        var buffer = new byte[len];
        var span = buffer.AsSpan();
        BinaryPrimitives.WriteInt64LittleEndian(span, salt);
        BinaryPrimitives.WriteInt64LittleEndian(span[8..], sessionId);
        BinaryPrimitives.WriteInt64LittleEndian(span[16..], messageId);
        BinaryPrimitives.WriteInt32LittleEndian(span[24..], seqNo);
        BinaryPrimitives.WriteInt32LittleEndian(span[28..], body.Length);
        body.CopyTo(span[32..]);
        return buffer;
    }

    private static byte[] PadTo16(ReadOnlySpan<byte> data)
    {
        var pad = 16 - (data.Length % 16);
        if (pad < 12)
        {
            pad += 16;
        }

        var result = new byte[data.Length + pad];
        data.CopyTo(result);
        RandomNumberGenerator.Fill(result.AsSpan(data.Length));
        return result;
    }
}
