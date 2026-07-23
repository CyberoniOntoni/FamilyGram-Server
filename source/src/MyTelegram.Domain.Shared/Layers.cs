namespace MyTelegram;

/// <summary>
/// MTProto API layer constants for FamilyGram-Server.
/// Wire layer 228 requires the open MyTelegram.SessionServer (and messenger/*)
/// built against this Schema. Layer 224 remains min-supported for dual object-id
/// registration during client migration.
/// </summary>
public class Layers
{
    public const int LayerMinSupported = 224;
    /// <summary>Layer for wire constructors + invokeWithLayer.</summary>
    public const int LayerLatest = 228;
    /// <summary>Same as LayerLatest once open session-server is production default.</summary>
    public const int LayerTarget = 228;
}
