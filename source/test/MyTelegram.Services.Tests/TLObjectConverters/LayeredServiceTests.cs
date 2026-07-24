using Moq;
using MyTelegram.Services.TLObjectConverters;

namespace MyTelegram.Services.Tests.TLObjectConverters;

public class LayeredServiceTests : TestsFor<LayeredService<ILayeredConverter>>
{
    private List<ILayeredConverter> _converters = [];

    private void SetupConverters(List<int> layers)
    {
        _converters =
        [
            .. layers.Select(layer =>
            {
                var mockConverter = new Mock<ILayeredConverter>();
                mockConverter.SetupGet(c => c.Layer).Returns(layer);
                return mockConverter.Object;
            })
        ];
    }

    protected override LayeredService<ILayeredConverter> CreateSut()
    {
        return new LayeredService<ILayeredConverter>(_converters);
    }

    [Theory]
    [InlineData(new int[] { 180, 184, 195, 198 }, 0, 198)]
    [InlineData(new int[] { 180, 184, 195, 198 }, 184, 198)]
    [InlineData(new int[] { 180, 184, 195, 198 }, 100, 198)]
    [InlineData(new int[] { 180, 184, 195, 198 }, 250, 198)]
    [InlineData(new int[] { 180, 184, 195, 198 }, 190, 198)]
    [InlineData(new int[] { 228 }, 228, 228)]
    [InlineData(new int[] { 228 }, 0, 228)]
    public void GetConverter_AlwaysReturnsLatestLayer(int[] layers, int requestLayer, int expectedLayer)
    {
        SetupConverters([.. layers]);
        var sut = CreateSut();

        var result = sut.GetConverter(requestLayer);

        result.Layer.ShouldBe(expectedLayer);
    }
}
