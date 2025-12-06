using System;
using System.Collections.Generic;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server;

public class PowerToControlCalculationService : TestBase
{
    public PowerToControlCalculationService(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Theory]
    // =========================================================================================
    // SCENARIO 1: Basic Grid/Inverter (Battery Disabled/Not Present)
    // Formula: GridOverage + CurrentCharging - Buffer
    // =========================================================================================
    // 1. Grid Exporting 1000W, Car 0W -> 1000W
    [InlineData(1000, 0, 0, null, null, null, null, 10000, 1000)]
    // 2. Grid Importing -200W, Car 1000W -> 800W
    [InlineData(-200, 1000, 0, null, null, null, null, 10000, 800)]
    // 3. Inverter Logic (Grid=null): Inv 5000, Car 1000 -> 5000
    [InlineData(null, 1000, 0, 5000, null, null, null, 10000, 5000)]

    // =========================================================================================
    // SCENARIO 2: Battery Full (SoC > Min) -> Steal Power for Car
    // Logic: Since battery is full enough, we treat current battery charging power as available for the car.
    // Formula: Overage + BatteryChargingPower
    // =========================================================================================
    // 4. Grid 0, Car 0, Bat Charging 2000W, SoC 90% (Min 20%) -> Result 2000W
    [InlineData(0, 0, 0, null, 2000, 90, 20, 10000, 2000)]
    // 5. Grid -500 (Import), Car 0, Bat Charging 2000W, SoC 90% -> (-500 + 2000) = 1500W
    [InlineData(-500, 0, 0, null, 2000, 90, 20, 10000, 1500)]

    // =========================================================================================
    // SCENARIO 3: Battery Empty (SoC < Min) -> Preserve Power for Battery
    // Logic: Battery needs power. We cannot steal the full amount.
    // Formula: Overage + (CurrentBatPower - RequiredBatPower)
    // Assumption: RequiredBatPower = Configured Max Charge (e.g. 2000W)
    // =========================================================================================
    // 6. Grid 0, Car 0, Bat Charging 2000W, SoC 10% (Min 20%), MaxCharge 2000W
    //    Calc: 0 + (2000 - 2000) = 0W available for car.
    [InlineData(0, 0, 0, null, 2000, 10, 20, 10000, 0)]

    // 7. Grid 0, Car 0, Bat Charging 3000W (Faster than config), SoC 10%, MaxCharge 2000W
    //    Calc: 0 + (3000 - 2000) = 1000W available for car.
    [InlineData(0, 0, 0, null, 3000, 10, 20, 10000, 1000)]

    // =========================================================================================
    // SCENARIO 4: Inverter Overload (DC Clipping)
    // Logic: If InverterPower > MaxInverterAcPower, we must reduce the calculated overage.
    // Formula: Overage -= (InverterPower - MaxAC)
    // =========================================================================================
    // 8. Bat Charging 2000W (SoC 90%>20%, so we steal it all). 
    //    Inverter 11000W, MaxAC 10000W. Overload = 1000W.
    //    Calc: 2000 (from bat) - 1000 (overload) = 1000W
    [InlineData(0, 0, 0, 11000, 2000, 90, 20, 10000, 1000)]

    public void CanGetCorrectPowerToControl_WithBattery(
            int? gridOverage,           // Grid Power (Export positive, Import negative)
            int currentChargingPower,   // What the car is currently pulling
            int buffer,                 // Configured Buffer
            int? inverterPower,         // Current Inverter Generation
            int? batPower,              // Current Battery Power (Positive = Charging)
            int? batSoC,                // Current Battery %
            int? minSoC,                // Configured Min %
            int maxInverterAc,          // Configured Max AC Limit
            int expectedResult)
    {
        // 1. Prepare Service
        var service = Mock.Create<TeslaSolarCharger.Server.Services.PowerToControlCalculationService>();

        // 2. Setup Settings (Live Data)
        Mock.Mock<ISettings>().Setup(s => s.LastPvValueUpdate).Returns(DateTime.Now);
        Mock.Mock<ISettings>().Setup(s => s.Overage).Returns(gridOverage);
        Mock.Mock<ISettings>().Setup(s => s.InverterPower).Returns(inverterPower);
        Mock.Mock<ISettings>().Setup(s => s.HomeBatteryPower).Returns(batPower);
        Mock.Mock<ISettings>().Setup(s => s.HomeBatterySoc).Returns(batSoC);

        // 3. Setup Configuration (Static Config)
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.PowerBuffer()).Returns(buffer);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeBatteryMinSoc()).Returns(minSoC ?? 0);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.MaxInverterAcPower()).Returns(maxInverterAc);

        // To support Scenario 3 & 7 (Battery Priority), we assume the Max Charging Power 
        // matches the current battery power for simplicity in the 'Required' calculation, 
        // or we use a fixed value. Here, let's say the battery *wants* to charge at 2000W 
        // if it is empty.
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeBatteryChargingPower()).Returns(2000);

        // 4. Input Data
        var loadPoints = new List<DtoLoadPointWithCurrentChargingValues>
            {
                new DtoLoadPointWithCurrentChargingValues { ChargingPower = currentChargingPower },
            };

        // 5. Execute
        var result = service.CalculatePowerToControl(loadPoints);

        // 6. Assert
        Assert.Equal(expectedResult, result);
    }
}
