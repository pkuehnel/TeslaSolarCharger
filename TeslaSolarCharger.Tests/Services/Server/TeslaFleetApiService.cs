using Newtonsoft.Json;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using TeslaSolarCharger.Server.Dtos.TeslaFleetApi;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server;

[SuppressMessage("ReSharper", "UseConfigureAwaitFalse")]
public class TeslaFleetApiService(ITestOutputHelper outputHelper) : TestBase(outputHelper)
{
    [Theory]
    [InlineData(28155, "Retry in 28155 seconds")]
    [InlineData(1, "Retry in 1 seconds")]
    [InlineData(0, "Retry in 0 seconds")]
    [InlineData(5641451, "Retry in 5641451 seconds")]
    public void CanCreateCorrectRetryDateTime(int correctRetryIn, string responseString)
    {
        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaFleetApiService>();
        var seconds = service.RetryInSeconds(responseString);
        Assert.Equal(correctRetryIn, seconds);
    }
}
