using Xunit;
using Xunit.Abstractions;

namespace SmartTeslaAmpSetter.Tests.Services;

public class GridService : TestBase
{
    public GridService(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Theory]
    [InlineData("384.8746")]
    [InlineData("384")]
    [InlineData("384.0")]
    [InlineData("384.147")]
    public void Can_extract_Integers_From_String(string value)
    {
        var gridService = Mock.Create<Server.Services.GridService>();
        var intValue = gridService.GetIntegerFromString(value);

        Assert.Equal(384, intValue);
    }

}