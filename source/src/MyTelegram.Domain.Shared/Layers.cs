namespace MyTelegram;

/// <summary>
/// MTProto API layer constants for FamilyGram-Server.
/// Latest schema is layer 228 with dual-registration of layer-224 constructor IDs
/// for bit-compatible request/response interop (see docs/LAYER_228_UPGRADE.md).
/// </summary>
public class Layers
{
    /// <summary>Lowest client layer we keep working via aliases / LayerN converters.</summary>
    public const int LayerMinSupported = 224;

    /// <summary>Layer implemented by LatestLayer schema/handlers.</summary>
    public const int LayerLatest = 228;

    /// <summary>Same as <see cref="LayerLatest"/> (tdesktop / tdlib target).</summary>
    public const int LayerTarget = 228;
}
