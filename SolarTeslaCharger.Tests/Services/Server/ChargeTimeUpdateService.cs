using System;
using SolarTeslaCharger.Shared.Contracts;
using SolarTeslaCharger.Shared.Dtos.Settings;
using Xunit;
using Xunit.Abstractions;

namespace SolarTeslaCharger.Tests.Services.Server;

public class ChargeTimeUpdateService : TestBase
{
    public ChargeTimeUpdateService(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Theory]
    [InlineData(3)]
    [InlineData(1)]
    public void Calculates_Correct_Charge_MaxSpeed_Charge_Time(int numberOfPhases)
    {
        var car = new Car()
        {
            Id = 1,
            CarState = new CarState()
            {
                PluggedIn = true,
                SoC = 30,
                ChargerPhases = numberOfPhases
            },
            CarConfiguration = new CarConfiguration()
            {
                MinimumSoC = 45,
                UsableEnergy = 74,
                MaximumAmpere = 16,
            }
        };


        var dateTime = new DateTime(2022, 4, 1, 14, 0, 0);
        Mock.Mock<IDateTimeProvider>().Setup(d => d.Now()).Returns(dateTime);
        var chargingService = Mock.Create<SolarTeslaCharger.Server.Services.ChargeTimeUpdateService>();

        chargingService.UpdateChargeTime(car);

        var lowerMinutes = 60 * (3 / numberOfPhases);

#pragma warning disable CS8629
        Assert.InRange((DateTime)car.CarState.ReachingMinSocAtFullSpeedCharge, dateTime.AddMinutes(lowerMinutes), dateTime.AddMinutes(lowerMinutes + 1));
#pragma warning restore CS8629
    }

    [Fact]
    public void Handles_Plugged_Out_Car()
    {
        var car = new Car()
        {
            Id = 1,
            CarState = new CarState()
            {
                PluggedIn = false,
                SoC = 30,
                ChargerPhases = 1
            },
            CarConfiguration = new CarConfiguration()
            {
                MinimumSoC = 45,
                UsableEnergy = 74,
                MaximumAmpere = 16,
            }
        };


        var dateTime = new DateTime(2022, 4, 1, 14, 0, 0);
        Mock.Mock<IDateTimeProvider>().Setup(d => d.Now()).Returns(dateTime);
        var chargingService = Mock.Create<SolarTeslaCharger.Server.Services.ChargeTimeUpdateService>();

        chargingService.UpdateChargeTime(car);

        Assert.Null(car.CarState.ReachingMinSocAtFullSpeedCharge);
    }

    [Fact]
    public void Handles_Reaced_Minimum_Soc()
    {
        var car = new Car()
        {
            Id = 1,
            CarState = new CarState()
            {
                PluggedIn = true,
                SoC = 30,
                ChargerPhases = 1
            },
            CarConfiguration = new CarConfiguration()
            {
                MinimumSoC = 30,
                UsableEnergy = 74,
                MaximumAmpere = 16,
            }
        };


        var dateTime = new DateTime(2022, 4, 1, 14, 0, 0);
        Mock.Mock<IDateTimeProvider>().Setup(d => d.Now()).Returns(dateTime);
        var chargingService = Mock.Create<SolarTeslaCharger.Server.Services.ChargeTimeUpdateService>();

        chargingService.UpdateChargeTime(car);

        Assert.Equal(dateTime, car.CarState.ReachingMinSocAtFullSpeedCharge);
    }
}