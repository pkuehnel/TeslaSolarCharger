using TeslaSolarCharger.Shared.Enums;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Helper;

public class NodePatternTypeHelper : TestBase
{
    public NodePatternTypeHelper(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData(" ", null)]
    [InlineData(null, "")]
    [InlineData(null, " ")]
    public void Decides_Correct_Note_Pattern_Type_None(string jsonPattern, string xmlPattern)
    {
        var nodePatternTypeHelper = Mock.Create<Shared.Helper.NodePatternTypeHelper>();
        var nodePatternType = nodePatternTypeHelper.DecideNodePatternType(jsonPattern, xmlPattern);

        Assert.Equal(NodePatternType.Direct, nodePatternType);
    }

    [Theory]
    [InlineData("$.data.overage", null)]
    [InlineData("$.data.overage", "")]
    [InlineData("$.data.overage", " ")]
    public void Decides_Correct_Note_Pattern_Type_Json(string jsonPattern, string xmlPattern)
    {
        var nodePatternTypeHelper = Mock.Create<Shared.Helper.NodePatternTypeHelper>();
        var nodePatternType = nodePatternTypeHelper.DecideNodePatternType(jsonPattern, xmlPattern);

        Assert.Equal(NodePatternType.Json, nodePatternType);
    }

    [Theory]
    [InlineData(null, "Device/Measurements/Measurement")]
    [InlineData("", "Device/Measurements/Measurement")]
    [InlineData(" ", "Device/Measurements/Measurement")]
    public void Decides_Correct_Note_Pattern_Type_Xml(string jsonPattern, string xmlPattern)
    {
        var nodePatternTypeHelper = Mock.Create<Shared.Helper.NodePatternTypeHelper>();
        var nodePatternType = nodePatternTypeHelper.DecideNodePatternType(jsonPattern, xmlPattern);

        Assert.Equal(NodePatternType.Xml, nodePatternType);
    }
}
