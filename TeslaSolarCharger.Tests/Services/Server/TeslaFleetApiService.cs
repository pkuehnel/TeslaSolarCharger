using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using TeslaSolarCharger.Server.Dtos.TeslaFleetApi;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server;

[SuppressMessage("ReSharper", "UseConfigureAwaitFalse")]
public class TeslaFleetApiService(ITestOutputHelper outputHelper) : TestBase(outputHelper)
{
    [Fact]
    public async Task CanHandleUnsignedCommands()
    {
        var commandResult = JsonConvert.DeserializeObject<DtoGenericTeslaResponse<DtoVehicleCommandResult>>("{\"response\":{\"result\":false,\"reason\":\"unsigned_cmds_hardlocked\"}}");
        Assert.NotNull(commandResult?.Response);
        var fleetApiService = Mock.Create<TeslaSolarCharger.Server.Services.TeslaFleetApiService>();
        var fleetApiProxyNeeded = await fleetApiService.IsFleetApiProxyNeededInDatabase();
        Assert.False(fleetApiProxyNeeded);
        await fleetApiService.HandleUnsignedCommands(commandResult.Response);
        fleetApiProxyNeeded = await fleetApiService.IsFleetApiProxyNeededInDatabase();
        Assert.True(fleetApiProxyNeeded);

    }
}
