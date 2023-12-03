using TeslaSolarCharger.Server.Dtos.TscBackend;
using Xunit;
using Xunit.Abstractions;

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
        var requestInformation = new DtoTeslaOAuthRequestInformation()
        {
            ClientId = "f29f71d6285a-4873-8b6b-80f15854892e",
            Prompt = "login",
            RedirectUri = "https://www.teslasolarcharger.de/",
            ResponseType = "code",
            Scope = "offline_access vehicle_device_data vehicle_cmds vehicle_charging_cmds",
            State = "8774fbe7-f9aa-4e36-8e88-5c8b27137f20",
        };
        var url = backendApiService.GenerateAuthUrl(requestInformation, "en-US");
        var expectedUrl = "https://auth.tesla.com/oauth2/v3/authorize?&client_id=f29f71d6285a-4873-8b6b-80f15854892e&locale=en-US&prompt=login&redirect_uri=https%3A%2F%2Fwww.teslasolarcharger.de%2F&response_type=code&scope=offline_access%20vehicle_device_data%20vehicle_cmds%20vehicle_charging_cmds&state=8774fbe7-f9aa-4e36-8e88-5c8b27137f20";
        Assert.Equal(expectedUrl, url);
    }
}
