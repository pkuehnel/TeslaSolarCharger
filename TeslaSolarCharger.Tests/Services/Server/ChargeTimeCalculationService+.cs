using System;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server;

public class ChargeTimeCalculationService : TestBase
{
    public ChargeTimeCalculationService(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Theory]
    [InlineData(0, 30, 75, 3, 16, 0)]
    [InlineData(31, 30, 100, 3, 16, 326)]
    [InlineData(32, 30, 100, 3, 32, 326)]
    [InlineData(32, 30, 50, 3, 16, 326)]
    [InlineData(42, 40, 50, 3, 16, 326)]
    [InlineData(42, 40, 50, 1, 16, 978)]
    public void Calculates_Correct_Full_Speed_Charge_Durations(int minimumSoc, int? acutalSoc, int usableEnergy,
        int chargerPhases, int maximumAmpere, double expectedTotalSeconds)
    {
        var car = new Car()
        {
            CarConfiguration = new CarConfiguration()
            {
                MinimumSoC = minimumSoc,
                UsableEnergy = usableEnergy,
                MaximumAmpere = maximumAmpere,
            },
            CarState = new CarState()
            {
                SoC = acutalSoc,
                ChargerPhases = chargerPhases,
            },
        };

        var chargeTimeCalculationService = Mock.Create<TeslaSolarCharger.Server.Services.ChargeTimeCalculationService>();
        var chargeDuration = chargeTimeCalculationService.CalculateTimeToReachMinSocAtFullSpeedCharge(car);

        var expectedTimeSpan = TimeSpan.FromSeconds(expectedTotalSeconds);
        var maximumErrorTime = TimeSpan.FromSeconds(1);
        var minimum = expectedTimeSpan - maximumErrorTime;
        var maximum = expectedTimeSpan + maximumErrorTime;
        Assert.InRange(chargeDuration, minimum, maximum);
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
        var chargeTimeCalculationService = Mock.Create<TeslaSolarCharger.Server.Services.ChargeTimeCalculationService>();

        chargeTimeCalculationService.UpdateChargeTime(car);

        var lowerMinutes = 60 * (3 / numberOfPhases);

#pragma warning disable CS8629
        Assert.InRange((DateTime)car.CarState.ReachingMinSocAtFullSpeedCharge, dateTime.AddMinutes(lowerMinutes), dateTime.AddMinutes(lowerMinutes + 1));
#pragma warning restore CS8629
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
        var chargeTimeCalculationService = Mock.Create<TeslaSolarCharger.Server.Services.ChargeTimeCalculationService>();

        chargeTimeCalculationService.UpdateChargeTime(car);

        Assert.Equal(dateTime, car.CarState.ReachingMinSocAtFullSpeedCharge);
    }
}
