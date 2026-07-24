namespace MyTelegram.Services.TLObjectConverters;

public class LayeredService<TLayeredConverter> : ILayeredService<TLayeredConverter>
    where TLayeredConverter : ILayeredConverter
{
    private readonly Dictionary<int, TLayeredConverter> _layeredConverters;
    private readonly int[] _layers;
    private readonly int _minLayer;
    private readonly int _maxLayer;
    private readonly TLayeredConverter _maxLayerConverter;
    private readonly TLayeredConverter _minLayerConverter;

    public LayeredService(IEnumerable<TLayeredConverter> converters)
    {
        var allConverters = converters.OrderBy(p => p.Layer).ToList();
        _layeredConverters = allConverters.ToDictionary(k => k.Layer);
        _layers = allConverters.Select(p => p.Layer).ToArray();

        _minLayer = _layers[0];
        _maxLayer = _layers[^1];

        Converter = allConverters[^1];
        _minLayerConverter = allConverters[0];
        _maxLayerConverter = Converter;
    }

    public TLayeredConverter Converter { get; }

    public TLayeredConverter GetConverter(int layer)
    {
        // FamilyGram is layer 228 only: always serialize Latest constructors.
        // Ignore client-reported lower layers so we never emit pre-228 wire shapes.
        _ = layer;
        return _maxLayerConverter;
    }
}
