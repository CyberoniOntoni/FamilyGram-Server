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
            { 0x7600b9d3, 0x7600b9d3 }, // message 228 → 224
            { 0xd49f34c6, 0xd49f34c6 }, // channel
            { 0xa04e8d3a, 0xa04e8d3a }, // channelFull
            { 0x60fe3294, 0x60fe3294 }, // draftMessage
            { 0x966e2dbf, 0x966e2dbf }, // poll
            { 0xb1b8cc83, 0xb1b8cc83 }, // user
            { 0x033ed001, 0x033ed001 }, // connectedBot
            { 0x7cb34d79, 0x7cb34d79 }, // updateBotChatInviteRequester
            { 0x3fc18057, 0x3fc18057 }, // inputStorePaymentAuthCode
            { 0xf8827ebf, 0xf8827ebf }, // auth.sentCodePaymentRequired
            { 0x9cb490e9, 0x7600b9d3 }, // pre-223 message
            { 0xfef48f62, 0xfef48f62 }, // messages.sendMessage
            { 0xb106e66c, 0xb106e66c }, // messages.editMessage
            { 0xa423bb51, 0xa423bb51 }, // messages.editInlineBotMessage
            { 0xad0fa15c, 0xad0fa15c }, // messages.saveDraft
            { 0x6126a43c, 0x6126a43c }, // messages.searchGlobal
            { 0xdaecc589, 0xdaecc589 }, // messages.composeMessageWithAI
            { 0x05f58d0f, 0x05f58d0f }, // contacts.search
            { 0x0ecc2618, 0x0ecc2618 }, // channels.toggleJoinRequest
            { 0x7f6a1e22, 0x7f6a1e22 }, // channels.joinChannel
            { 0xde91436e, 0xde91436e }, // messages.importChatInvite
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
