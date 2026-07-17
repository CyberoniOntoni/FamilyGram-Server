namespace MyTelegram;

/// <summary>
/// MTProto API layer constants for FamilyGram-Server.
/// Production still speaks <see cref="LayerLatest"/> = 224 until the layer-228
/// schema regeneration + multi-layer converters land (see docs/LAYER_228_UPGRADE.md).
/// </summary>
public class Layers
{
    /// <summary>Lowest client layer we intend to keep working once multi-layer is complete.</summary>
    public const int LayerMinSupported = 224;

    /// <summary>Layer currently implemented by LatestLayer schema/handlers (production).</summary>
    public const int LayerLatest = 224;

    /// <summary>tdesktop/tdlib target layer for the upgrade (schema + converters not complete yet).</summary>
    public const int LayerTarget = 228;
}