namespace MyTelegram.SessionServer.Services;

public sealed class SessionState
{
    public required long AuthKeyId { get; init; }
    public required byte[] AuthKey { get; init; }
    public long ServerSalt { get; set; }
    public long SessionId { get; set; }
    public long UserId { get; set; }
    public long PermAuthKeyId { get; set; }
    public long AccessHashKeyId { get; set; }
    public int Layer { get; set; } = Layers.LayerLatest;
    public DeviceType DeviceType { get; set; } = DeviceType.Unknown;
    public string ConnectionId { get; set; } = string.Empty;
    public ConnectionType ConnectionType { get; set; } = ConnectionType.Generic;
    public string ClientIp { get; set; } = string.Empty;
    public int DcId { get; set; }
    public int ServerSeqNo { get; set; }
    public bool IsPermanent { get; set; }
}
