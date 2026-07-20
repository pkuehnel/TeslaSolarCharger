using System;
using Microsoft.Extensions.Logging.Abstractions;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.TimeProviding;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

public class FleetApiRateLimitServiceTests
{
    //Winter date so no DST change can occur within the tested time ranges
    private static readonly DateTime BaseTime = new(2026, 2, 2, 8, 0, 0);

    private static TeslaSolarCharger.Server.Services.FleetApiRateLimitService CreateService(DateTime currentTime)
    {
        return new(NullLogger<TeslaSolarCharger.Server.Services.FleetApiRateLimitService>.Instance, new FakeDateTimeProvider(currentTime));
    }

    private static DateTime UtcAt(DateTime currentTime)
    {
        return new FakeDateTimeProvider(currentTime).UtcNow();
    }

    [Fact]
    public void AllowsFirstCommand()
    {
        var car = new DtoCar();
        var service = CreateService(BaseTime);
        Assert.Null(service.GetNextAllowedUtc(car));
    }

    [Fact]
    public void GetNextAllowedUtcDoesNotConsumeBudget()
    {
        var car = new DtoCar();
        var service = CreateService(BaseTime);
        Assert.Null(service.GetNextAllowedUtc(car));
        Assert.Null(service.GetNextAllowedUtc(car));
        Assert.Null(car.LastCountedFleetApiCommand);
    }

    [Fact]
    public void RecordedCommandConsumesHourlySlot()
    {
        var car = new DtoCar();
        CreateService(BaseTime).RecordSuccessfulCommand(car);
        Assert.Equal(UtcAt(BaseTime), car.LastCountedFleetApiCommand);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void AllowsCommandsWithinGraceWindow(int minutesAfterCountedCommand)
    {
        var car = new DtoCar();
        CreateService(BaseTime).RecordSuccessfulCommand(car);
        var service = CreateService(BaseTime.AddMinutes(minutesAfterCountedCommand));
        Assert.Null(service.GetNextAllowedUtc(car));
    }

    [Fact]
    public void CommandsWithinGraceWindowDoNotExtendWindow()
    {
        var car = new DtoCar();
        CreateService(BaseTime).RecordSuccessfulCommand(car);
        CreateService(BaseTime.AddMinutes(4)).RecordSuccessfulCommand(car);
        Assert.Equal(UtcAt(BaseTime), car.LastCountedFleetApiCommand);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(30)]
    [InlineData(59)]
    public void BlocksCommandsAfterGraceWindowUntilHourIsOver(int minutesAfterCountedCommand)
    {
        var car = new DtoCar();
        CreateService(BaseTime).RecordSuccessfulCommand(car);
        var service = CreateService(BaseTime.AddMinutes(minutesAfterCountedCommand));
        Assert.Equal(UtcAt(BaseTime).AddMinutes(60), service.GetNextAllowedUtc(car));
    }

    [Fact]
    public void AllowsCommandAfterOneHour()
    {
        var car = new DtoCar();
        CreateService(BaseTime).RecordSuccessfulCommand(car);
        var service = CreateService(BaseTime.AddMinutes(60));
        Assert.Null(service.GetNextAllowedUtc(car));
    }

    [Fact]
    public void CommandAfterOneHourConsumesNewSlot()
    {
        var car = new DtoCar();
        CreateService(BaseTime).RecordSuccessfulCommand(car);
        CreateService(BaseTime.AddMinutes(61)).RecordSuccessfulCommand(car);
        Assert.Equal(UtcAt(BaseTime.AddMinutes(61)), car.LastCountedFleetApiCommand);
        //The new slot opens its own grace window and blocks again afterwards
        Assert.Null(CreateService(BaseTime.AddMinutes(63)).GetNextAllowedUtc(car));
        Assert.Equal(UtcAt(BaseTime.AddMinutes(61)).AddMinutes(60), CreateService(BaseTime.AddMinutes(70)).GetNextAllowedUtc(car));
    }

    [Fact]
    public void WakeUpWithFollowUpCommandsScenario()
    {
        var car = new DtoCar();
        //Wake up succeeds and consumes the hourly slot
        CreateService(BaseTime).RecordSuccessfulCommand(car);
        //Set charging amps two minutes later is allowed and does not consume the slot
        Assert.Null(CreateService(BaseTime.AddMinutes(2)).GetNextAllowedUtc(car));
        CreateService(BaseTime.AddMinutes(2)).RecordSuccessfulCommand(car);
        //Charge start three minutes later is allowed and does not consume the slot
        Assert.Null(CreateService(BaseTime.AddMinutes(3)).GetNextAllowedUtc(car));
        CreateService(BaseTime.AddMinutes(3)).RecordSuccessfulCommand(car);
        //Ten minutes later the grace window is over, the next command is only allowed one hour after the wake up
        Assert.Equal(UtcAt(BaseTime).AddMinutes(60), CreateService(BaseTime.AddMinutes(10)).GetNextAllowedUtc(car));
    }
}
