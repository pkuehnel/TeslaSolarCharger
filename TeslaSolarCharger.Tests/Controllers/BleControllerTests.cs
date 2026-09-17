using Moq;
using PkSoftwareService.Custom.Backend.Ble;
using System.Threading.Tasks;
using TeslaSolarCharger.Server.Controllers;
using TeslaSolarCharger.Server.Services.Contracts;
using Xunit;

namespace TeslaSolarCharger.Tests.Controllers;

public class BleControllerTests : TestBase
{
    private const string TestVin = "TESTVIN123456789A";

    public BleControllerTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Fact]
    public async Task OpenChargePortDoorReturnsTheResultOfTheBleService()
    {
        var expected = new DtoBleCommandResult { Success = true, Outcome = BleCommandOutcome.Ok, };
        var bleService = Mock.Mock<IBleService>();
        bleService.Setup(s => s.OpenChargePortDoor(TestVin)).ReturnsAsync(expected);
        var controller = Mock.Create<BleController>();

        var result = await controller.OpenChargePortDoor(TestVin);

        Assert.Same(expected, result);
        bleService.Verify(s => s.OpenChargePortDoor(TestVin), Times.Once);
        bleService.Verify(s => s.FlashLights(It.IsAny<string>()), Times.Never);
    }
}
