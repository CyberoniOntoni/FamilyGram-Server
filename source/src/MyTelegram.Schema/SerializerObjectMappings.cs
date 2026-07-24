using System.Diagnostics.CodeAnalysis;

namespace MyTelegram.Schema;

public static class SerializerObjectMappings
{
    private const uint VectorConstructorId = 0x1cb5c415;
    private static readonly ConcurrentDictionary<Type, Func<IObject>> GenericTypeOfTConstructors = new();
    private static readonly ConcurrentDictionary<uint, Type> TypeMappingDict = new();
    private static readonly ConcurrentDictionary<uint, Func<IObject>> TypeToConstructors = new();

    static SerializerObjectMappings()
    {
        InitTypeMappings();
    }

    public static void CreateConstructIdToTypeMappingsFromAssembly(Assembly tlObjectInThisAssembly)
    {
        var types = tlObjectInThisAssembly.GetTypes();

        foreach (var type in types)
        {
            // FamilyGram is layer 228 only — do not register LayerN / pre-Latest TL types.
            if (IsLegacyLayerType(type))
            {
                continue;
            }

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
    }

    private static bool IsLegacyLayerType(Type type)
    {
        var ns = type.Namespace;
        if (string.IsNullOrEmpty(ns))
        {
            return false;
        }

        // e.g. MyTelegram.Schema.Updates.LayerN, MyTelegram.Schema.LayerN, ...Layer160...
        return ns.Contains(".LayerN", StringComparison.Ordinal)
               || ns.Contains(".LayerN.", StringComparison.Ordinal)
               || ns.EndsWith(".LayerN", StringComparison.Ordinal)
               || System.Text.RegularExpressions.Regex.IsMatch(ns, @"\.Layer\d+");
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
