using System;
using System.Collections.Generic;
using Autofac.Extras.Moq;
using Moq;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Settings;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingSchedulesService;

public class GetMinimumEnergyToChargeTests : TestBase
{
    public GetMinimumEnergyToChargeTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    public static IEnumerable<object[]> TestData
    {
        get
        {
            var baseDate = new DateTimeOffset(2023, 2, 2, 8, 0, 0, TimeSpan.Zero);
            var targetTime = baseDate.AddHours(2); // 10:00

            // 1. Basic case: No schedules, 0 loss
            yield return new object[]
            {
                "Basic case: Target 50%, Current 20%, 50kWh usable, No Schedules. Expected 15kWh.",
                baseDate, // currentDate
                new DtoTimeZonedChargingTarget { TargetSoc = 50, NextExecutionTime = targetTime }, // nextTarget
                CreateCar(soc: 20, socLimit: 80, isCharging: false), // car
                50, // carUsableEnergy (kWh)
                0, // chargeLossPercent
                new List<DtoChargingSchedule>(), // schedules
                15000 // expected (30% * 50kWh = 15kWh = 15000Wh)
            };

            // 2. With Loss
            yield return new object[]
            {
                "With Loss: 10% loss. 15kWh * 1.1 = 16.5kWh.",
                baseDate,
                new DtoTimeZonedChargingTarget { TargetSoc = 50, NextExecutionTime = targetTime },
                CreateCar(soc: 20, socLimit: 80, isCharging: false),
                50,
                10, // 10% loss
                new List<DtoChargingSchedule>(),
                16500 // 15000 * 1.1
            };

            // 3. Target SoC null
            yield return new object[]
            {
                "Target SoC null. Expected 0.",
                baseDate,
                new DtoTimeZonedChargingTarget { TargetSoc = null, NextExecutionTime = targetTime },
                CreateCar(soc: 20, socLimit: 80, isCharging: false),
                50,
                0,
                new List<DtoChargingSchedule>(),
                0
            };

             // 4. Target <= Current
            yield return new object[]
            {
                "Target <= Current. Expected 0.",
                baseDate,
                new DtoTimeZonedChargingTarget { TargetSoc = 20, NextExecutionTime = targetTime },
                CreateCar(soc: 30, socLimit: 80, isCharging: false),
                50,
                0,
                new List<DtoChargingSchedule>(),
                0
            };

             // 5. Force Charge (Limit == Target)
            yield return new object[]
            {
                "Force Charge: Limit == Target and IsCharging. Target bumped +1%. Expected 500Wh.",
                baseDate,
                new DtoTimeZonedChargingTarget { TargetSoc = 80, NextExecutionTime = targetTime },
                CreateCar(soc: 80, socLimit: 80, isCharging: true),
                50,
                0,
                new List<DtoChargingSchedule>(),
                500 // (81 - 80)% * 50kWh = 0.5kWh = 500Wh
            };

            // 6. Force Charge but not charging
            yield return new object[]
            {
                "Force Charge condition met but IsCharging false. Target not bumped. Expected 0.",
                baseDate,
                new DtoTimeZonedChargingTarget { TargetSoc = 80, NextExecutionTime = targetTime },
                CreateCar(soc: 80, socLimit: 80, isCharging: false),
                50,
                0,
                new List<DtoChargingSchedule>(),
                0 // 80 - 80 = 0
            };

            // 7. Full Schedule Overlap
            yield return new object[]
            {
                "Full Schedule Overlap. Existing schedule covers entire requirement. Expected 0.",
                baseDate,
                new DtoTimeZonedChargingTarget { TargetSoc = 50, NextExecutionTime = targetTime },
                CreateCar(soc: 20, socLimit: 80, isCharging: false),
                50,
                0,
                new List<DtoChargingSchedule>
                {
                    // 8:00 - 10:00 @ 7500W => 2h * 7500 = 15000Wh
                    CreateSchedule(baseDate, targetTime, 7500)
                },
                0 // 15000 needed - 15000 scheduled
            };

            // 8. Partial Schedule Overlap
            yield return new object[]
            {
                "Partial Schedule Overlap. Existing schedule covers 5kWh. Expected 10kWh.",
                baseDate,
                new DtoTimeZonedChargingTarget { TargetSoc = 50, NextExecutionTime = targetTime },
                CreateCar(soc: 20, socLimit: 80, isCharging: false),
                50,
                0,
                new List<DtoChargingSchedule>
                {
                    // 8:00 - 9:00 @ 5000W => 5000Wh
                    CreateSchedule(baseDate, baseDate.AddHours(1), 5000)
                },
                10000 // 15000 - 5000
            };

             // 9. Schedule Outside Window (Future)
            yield return new object[]
            {
                "Schedule Outside Window (Future). Ignored. Expected 15kWh.",
                baseDate,
                new DtoTimeZonedChargingTarget { TargetSoc = 50, NextExecutionTime = targetTime },
                CreateCar(soc: 20, socLimit: 80, isCharging: false),
                50,
                0,
                new List<DtoChargingSchedule>
                {
                    // 10:00 - 11:00 @ 5000W. Starts after target time.
                    CreateSchedule(targetTime, targetTime.AddHours(1), 5000)
                },
                15000 // Ignored
            };

             // 10. Schedule In Past
            yield return new object[]
            {
                "Schedule In Past. Ignored. Expected 15kWh.",
                baseDate,
                new DtoTimeZonedChargingTarget { TargetSoc = 50, NextExecutionTime = targetTime },
                CreateCar(soc: 20, socLimit: 80, isCharging: false),
                50,
                0,
                new List<DtoChargingSchedule>
                {
                    CreateSchedule(baseDate.AddHours(-1), baseDate, 5000)
                },
                15000
            };

             // 11. Schedule Crossing Start Boundary
            yield return new object[]
            {
                "Schedule Crossing Start Boundary. Only overlapping part counted (1h). Expected 10kWh.",
                baseDate, // 8:00
                new DtoTimeZonedChargingTarget { TargetSoc = 50, NextExecutionTime = targetTime }, // 10:00
                CreateCar(soc: 20, socLimit: 80, isCharging: false),
                50,
                0,
                new List<DtoChargingSchedule>
                {
                    // 7:00 - 9:00 @ 5000W. Overlap 8:00-9:00 (1h) => 5000Wh
                    CreateSchedule(baseDate.AddHours(-1), baseDate.AddHours(1), 5000)
                },
                10000 // 15000 - 5000
            };

             // 12. Schedule Crossing End Boundary
            yield return new object[]
            {
                "Schedule Crossing End Boundary. Only overlapping part counted (1h). Expected 10kWh.",
                baseDate, // 8:00
                new DtoTimeZonedChargingTarget { TargetSoc = 50, NextExecutionTime = targetTime }, // 10:00
                CreateCar(soc: 20, socLimit: 80, isCharging: false),
                50,
                0,
                new List<DtoChargingSchedule>
                {
                    // 9:00 - 11:00 @ 5000W. Overlap 9:00-10:00 (1h) => 5000Wh
                    CreateSchedule(baseDate.AddHours(1), targetTime.AddHours(1), 5000)
                },
                10000 // 15000 - 5000
            };

            // 13. Multiple Schedules
            yield return new object[]
            {
                "Multiple Schedules. Sum of overlaps. Expected 5kWh.",
                baseDate, // 8:00
                new DtoTimeZonedChargingTarget { TargetSoc = 50, NextExecutionTime = targetTime }, // 10:00
                CreateCar(soc: 20, socLimit: 80, isCharging: false),
                50,
                0,
                new List<DtoChargingSchedule>
                {
                    CreateSchedule(baseDate, baseDate.AddMinutes(30), 10000), // 0.5h * 10k = 5000
                    CreateSchedule(baseDate.AddMinutes(60), baseDate.AddMinutes(90), 10000) // 0.5h * 10k = 5000
                },
                5000 // 15000 - 10000
            };

            // 14. CarUsableEnergy is null
            yield return new object[]
            {
                "Car Usable Energy Null. Expected 0.",
                baseDate,
                new DtoTimeZonedChargingTarget { TargetSoc = 50, NextExecutionTime = targetTime },
                CreateCar(soc: 20, socLimit: 80, isCharging: false),
                null!, // usable energy null
                0,
                new List<DtoChargingSchedule>(),
                0
            };

            // 15. Car SoC is null (Defaults to 0)
            yield return new object[]
            {
                "Car SoC Null. Defaults to 0. Target 50%, SoC 0%. Expected 25kWh.",
                baseDate,
                new DtoTimeZonedChargingTarget { TargetSoc = 50, NextExecutionTime = targetTime },
                CreateCar(soc: null, socLimit: 80, isCharging: false),
                50,
                0,
                new List<DtoChargingSchedule>(),
                25000 // 50 * 50 * 10 = 25000
            };
        }
    }

    private static DtoCar CreateCar(int? soc, int? socLimit, bool? isCharging)
    {
        return new DtoCar
        {
            SoC = new DtoTimeStampedValue<int?>(DateTimeOffset.MinValue, soc),
            SocLimit = new DtoTimeStampedValue<int?>(DateTimeOffset.MinValue, socLimit),
            IsCharging = new DtoTimeStampedValue<bool?>(DateTimeOffset.MinValue, isCharging)
        };
    }

    private static DtoChargingSchedule CreateSchedule(DateTimeOffset start, DateTimeOffset end, int power)
    {
        return new DtoChargingSchedule
        {
            ValidFrom = start,
            ValidTo = end,
            TargetMinPower = power,
            EstimatedSolarPower = power,
            MaxPossiblePower = 20000
        };
    }

    [Theory]
    [MemberData(nameof(TestData))]
    public void GetMinimumEnergyToCharge_CalculatesCorrectly(
        string description,
        DateTimeOffset currentDate,
        DtoTimeZonedChargingTarget nextTarget,
        DtoCar car,
        int? carUsableEnergy,
        int chargeLossPercent,
        List<DtoChargingSchedule> schedules,
        int expectedResult)
    {
        // Arrange
        Mock.Mock<IConfigurationWrapper>()
            .Setup(x => x.CarChargeLoss())
            .Returns(chargeLossPercent);

        var service = Mock.Create<ChargingScheduleService>();

        // Act
        var result = service.GetMinimumEnergyToCharge(currentDate, nextTarget, car, carUsableEnergy, schedules);

        // Assert
        Assert.True(result == expectedResult, $"{description} failed. Expected {expectedResult}, but got {result}.");
    }
}
