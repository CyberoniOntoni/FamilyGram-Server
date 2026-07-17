namespace MyTelegram;

/// <summary>
/// MTProto API layer constants for FamilyGram-Server.
/// Wire constructor IDs must stay at layer 224 until upstream session-server is rebuilt
/// with FamilyGram Schema (session-server is closed-source and rejects 228 IDs like
/// messages.sendMessage#fef48f62 and user#b1b8cc83).
/// </summary>
public class Layers
{
    public const int LayerMinSupported = 224;
    /// <summary>Layer for wire constructors + invokeWithLayer (session-server compatible).</summary>
    public const int LayerLatest = 224;
    /// <summary>Target when session-server can load layer-228 schema.</summary>
    public const int LayerTarget = 228;
}
