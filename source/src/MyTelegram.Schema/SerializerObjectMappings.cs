using System.Diagnostics.CodeAnalysis;

namespace MyTelegram.Schema;

public static class SerializerObjectMappings
{
    private const uint VectorConstructorId = 0x1cb5c415;
    private static readonly ConcurrentDictionary<Type, Func<IObject>> GenericTypeOfTConstructors = new();
    private static readonly ConcurrentDictionary<uint, Type> TypeMappingDict = new();
    private static readonly ConcurrentDictionary<uint, Func<IObject>> TypeToConstructors = new();

    /// <summary>
    /// Layer-224 constructor IDs that deserialize into the current (layer-228) types
    /// because the payload is flag-compatible (new fields only behind unset flag bits,
    /// or request optional fields at the end of the wire layout).
    /// </summary>
    public static readonly IReadOnlyDictionary<uint, uint> LegacyToLatestConstructorIds =
        new Dictionary<uint, uint>
        {
            // Entities
            { 0x3ae56482, 0x7600b9d3 }, // message
            { 0x1c32b11c, 0xd49f34c6 }, // channel
            { 0xe4e0b29d, 0xa04e8d3a }, // channelFull
            { 0x96eaa5eb, 0x60fe3294 }, // draftMessage
            { 0xb8425be9, 0x966e2dbf }, // poll
            { 0x31774388, 0xb1b8cc83 }, // user
            { 0xcd64636c, 0x033ed001 }, // connectedBot
            { 0x11dfa986, 0x7cb34d79 }, // updateBotChatInviteRequester
            { 0x9bb2636d, 0x3fc18057 }, // inputStorePaymentAuthCode
            { 0xe0955a3c, 0xf8827ebf }, // auth.sentCodePaymentRequired
            // pre-223 message still seen in persisted blobs
            { 0x9cb490e9, 0x7600b9d3 },
            // Requests (bit-compatible optional fields)
            { 0x545cd15a, 0xfef48f62 }, // messages.sendMessage
            { 0x51e842e1, 0xb106e66c }, // messages.editMessage
            { 0x83557dba, 0xa423bb51 }, // messages.editInlineBotMessage
            { 0x54ae308e, 0xad0fa15c }, // messages.saveDraft
            { 0x4bc6589a, 0x6126a43c }, // messages.searchGlobal
            { 0xfd426afe, 0xdaecc589 }, // messages.composeMessageWithAI
            { 0x11f812d8, 0x05f58d0f }, // contacts.search
            { 0x4c2985b6, 0x0ecc2618 }, // channels.toggleJoinRequest
            // Request IDs whose return type also changed — still accept the old request wire
            { 0x24b524c5, 0x7f6a1e22 }, // channels.joinChannel
            { 0x6c50051c, 0xde91436e }, // messages.importChatInvite
        };

    /// <summary>
    /// Request constructor IDs that should dispatch to the same handler as the latest ID.
    /// Includes structural-change request IDs that we still accept via alias deserialization.
    /// </summary>
    public static readonly IReadOnlyList<uint> DualRegisteredRequestLegacyIds =
        LegacyToLatestConstructorIds.Keys.ToList();

    static SerializerObjectMappings()
    {
        InitTypeMappings();
    }

    public static void CreateConstructIdToTypeMappingsFromAssembly(Assembly tlObjectInThisAssembly)
    {
        var types = tlObjectInThisAssembly.GetTypes();

        foreach (var type in types)
        {
            var attr = type.GetCustomAttribute<TlObjectAttribute>();
            if (attr != null)
            {
                TypeMappingDict.TryAdd(attr.ConstructorId, type);

                // TVector need process using other ways
                if (attr.ConstructorId != VectorConstructorId)
                {
                    TypeToConstructors.TryAdd(attr.ConstructorId,
                        MyReflectionHelper.CompileConstructor<IObject>(type));
                }
            }
        }
    }

    private static void InitTypeMappings()
    {
        CreateConstructIdToTypeMappingsFromAssembly(typeof(IObject).Assembly);
        RegisterLegacyConstructorAliases();
    }

    /// <summary>
    /// Some persisted blobs and older clients use prior-layer constructor IDs.
    /// New layers add fields only behind unused flag bits (for the entries in
    /// <see cref="LegacyToLatestConstructorIds"/>), so the current Deserialize
    /// routine is bit-compatible — we just route the old id to the current type.
    /// </summary>
    private static void RegisterLegacyConstructorAliases()
    {
        foreach (var (legacyId, latestId) in LegacyToLatestConstructorIds)
        {
            if (!TypeMappingDict.TryGetValue(latestId, out var type))
            {
                continue;
            }

            AliasConstructor(legacyId, type);
        }

        // Structural wire changes — still map so clients don't hard-fail; payloads
        // without the new required leading fields may not round-trip correctly.
        AliasConstructor(0xc27ac8c7, typeof(TBotCommand)); // botCommand layer 224 (no flags)
        AliasConstructor(0x9a8ae1e1, typeof(TPageBlockOrderedList));
        AliasConstructor(0x25e073fc, typeof(TPageListItemBlocks));
        AliasConstructor(0xb92fb6cd, typeof(TPageListItemText));
        AliasConstructor(0x98dd8936, typeof(TPageListOrderedItemBlocks));
        AliasConstructor(0x5e068047, typeof(TPageListOrderedItemText));
    }

    private static void AliasConstructor(uint legacyConstructorId, Type currentType)
    {
        if (TypeMappingDict.ContainsKey(legacyConstructorId)) return;
        TypeMappingDict.TryAdd(legacyConstructorId, currentType);
        TypeToConstructors.TryAdd(legacyConstructorId,
            MyReflectionHelper.CompileConstructor<IObject>(currentType));
    }

    public static void TryAddTlObjectFuncToCache(Type typeOfT,
        Func<IObject> func)
    {
        GenericTypeOfTConstructors.TryAdd(typeOfT, func);
    }

    public static bool TryGetTlObject(Type typeOfT,
        [NotNullWhen(true)] out Func<IObject>? func)
    {
        return GenericTypeOfTConstructors.TryGetValue(typeOfT, out func);
    }

    public static bool TryGetTlObject(uint constructorId,
        out Func<IObject>? func)
    {
        return TypeToConstructors.TryGetValue(constructorId, out func);
    }

    public static bool TryGetTlObjectType(uint constructorId, [NotNullWhen(true)] out Type? type)
    {
        return TypeMappingDict.TryGetValue(constructorId, out type);
    }
}
