using Serilog.Events;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingSchedulesService;

public class OptimizeChargingSchedulesTests : TestBase
{
    protected OptimizeChargingSchedulesTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }
}
