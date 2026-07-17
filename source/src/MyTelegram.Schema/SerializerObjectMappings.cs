using System.Diagnostics.CodeAnalysis;

namespace MyTelegram.Schema;

public static class SerializerObjectMappings
{
    private const uint VectorConstructorId = 0x1cb5c415;
    private static readonly ConcurrentDictionary<Type, Func<IObject>> GenericTypeOfTConstructors = new();
    private static readonly ConcurrentDictionary<uint, Type> TypeMappingDict = new();
    private static readonly ConcurrentDictionary<uint, Func<IObject>> TypeToConstructors = new();

    /// <summary>
    /// Layer-228 constructor IDs → current Latest (224 wire) types when bit-compatible.
    /// Session-server cannot deserialize 228 IDs; keep wire IDs at 224 until it is rebuilt.
    /// </summary>
    public static readonly IReadOnlyDictionary<uint, uint> LegacyToLatestConstructorIds =
        new Dictionary<uint, uint>
        {
            { 0x7600b9d3, 0x3ae56482 }, // message 228 → 224
            { 0xd49f34c6, 0x1c32b11c }, // channel
            { 0xa04e8d3a, 0xe4e0b29d }, // channelFull
            { 0x60fe3294, 0x96eaa5eb }, // draftMessage
            { 0x966e2dbf, 0xb8425be9 }, // poll
            { 0xb1b8cc83, 0x31774388 }, // user
            { 0x033ed001, 0xcd64636c }, // connectedBot
            { 0x7cb34d79, 0x11dfa986 }, // updateBotChatInviteRequester
            { 0x3fc18057, 0x9bb2636d }, // inputStorePaymentAuthCode
            { 0xf8827ebf, 0xe0955a3c }, // auth.sentCodePaymentRequired
            { 0x9cb490e9, 0x3ae56482 }, // pre-223 message
            { 0xfef48f62, 0x545cd15a }, // messages.sendMessage
            { 0xb106e66c, 0x51e842e1 }, // messages.editMessage
            { 0xa423bb51, 0x83557dba }, // messages.editInlineBotMessage
            { 0xad0fa15c, 0x54ae308e }, // messages.saveDraft
            { 0x6126a43c, 0x4bc6589a }, // messages.searchGlobal
            { 0xdaecc589, 0xfd426afe }, // messages.composeMessageWithAI
            { 0x05f58d0f, 0x11f812d8 }, // contacts.search
            { 0x0ecc2618, 0x4c2985b6 }, // channels.toggleJoinRequest
            { 0x7f6a1e22, 0x24b524c5 }, // channels.joinChannel
            { 0xde91436e, 0x6c50051c }, // messages.importChatInvite
        };

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
