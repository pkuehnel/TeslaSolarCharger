using TeslaSolarCharger.Server.Dtos.TscBackend;
using TeslaSolarCharger.Shared.Contracts;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

public class BackendApiService : TestBase
{
    public BackendApiService(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Fact]
    public void CanEnCodeCorrectUrl()
    {
        var backendApiService = Mock.Create<TeslaSolarCharger.Server.Services.BackendApiService>();
        var configMock = Mock.Mock<IConfigurationWrapper>();
        configMock.Setup(x => x.BackendApiBaseUrl()).Returns("https://api.solar4car.com/api/");
        var url = backendApiService.GenerateAuthUrl("8774fbe7-f9aa-4e36-8e88-5c8b27137f20");
        var expectedUrl = "https://api.solar4car.com/api/AuthRedeem/Redeem?code=8774fbe7-f9aa-4e36-8e88-5c8b27137f20";
        Assert.Equal(expectedUrl, url);
    }
}
