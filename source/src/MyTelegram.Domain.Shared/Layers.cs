namespace MyTelegram;

/// <summary>
/// MTProto API layer constants for FamilyGram-Server.
/// Wire layer is 228 only (open session-server + this Schema).
/// </summary>
public class Layers
{
    public const int LayerMinSupported = 228;
    /// <summary>Layer for wire constructors + invokeWithLayer.</summary>
    public const int LayerLatest = 228;
    /// <summary>Same as LayerLatest.</summary>
    public const int LayerTarget = 228;
}
