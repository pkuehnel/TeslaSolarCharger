using System.Collections.Generic;
using TeslaSolarCharger.Shared.Dtos;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingSchedulesService;

public class AreSchedulesMergeableTests : TestBase
{
    public AreSchedulesMergeableTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    /// <summary>
    /// Verifies that AreSchedulesMergeable correctly identifies if two schedules can be merged
    /// based on their power properties and identifiers.
    /// </summary>
    [Theory]
    [MemberData(nameof(GetMergeableScenarios))]
    public void AreSchedulesMergeable_WithVariousScenarios_ReturnsExpectedResult(
        DtoChargingSchedule scheduleA,
        DtoChargingSchedule scheduleB,
        bool expectedResult,
        string description)
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();

        // Act
        var result = service.AreSchedulesMergeable(scheduleA, scheduleB);

        // Assert
        Assert.True(result == expectedResult, description);
    }

    public static IEnumerable<object[]> GetMergeableScenarios()
    {
        var baseSchedule = new DtoChargingSchedule(1, 1, 11000, 230, 3, new HashSet<ScheduleReason> { ScheduleReason.ExpectedSolarProduction })
        {
            TargetMinPower = 5000,
            TargetHomeBatteryPower = 1000,
            EstimatedSolarPower = 3000,
        };

        // 1. Identical - Mergeable
        yield return new object[]
        {
            Clone(baseSchedule),
            Clone(baseSchedule),
            true,
            "Identical schedules should be mergeable"
        };

        // 2. TargetMinPower Mismatch
        var s2 = Clone(baseSchedule);
        s2.TargetMinPower = 5001;
        yield return new object[] { Clone(baseSchedule), s2, false, "TargetMinPower mismatch should not be mergeable" };

        // 3. TargetHomeBatteryPower Mismatch (Value vs Value)
        var s3 = Clone(baseSchedule);
        s3.TargetHomeBatteryPower = 1001;
        yield return new object[] { Clone(baseSchedule), s3, false, "TargetHomeBatteryPower mismatch should not be mergeable" };

        // 4. TargetHomeBatteryPower Mismatch (Value vs Null)
        var s4 = Clone(baseSchedule);
        s4.TargetHomeBatteryPower = null;
        yield return new object[] { Clone(baseSchedule), s4, false, "TargetHomeBatteryPower mismatch (value vs null) should not be mergeable" };

        // 5. TargetHomeBatteryPower Match (Null vs Null)
        var s5a = Clone(baseSchedule);
        s5a.TargetHomeBatteryPower = null;
        var s5b = Clone(baseSchedule);
        s5b.TargetHomeBatteryPower = null;
        yield return new object[] { s5a, s5b, true, "TargetHomeBatteryPower match (null vs null) should be mergeable" };

        // 6. EstimatedSolarPower Mismatch
        var s6 = Clone(baseSchedule);
        s6.EstimatedSolarPower = 3001;
        yield return new object[] { Clone(baseSchedule), s6, false, "EstimatedSolarPower mismatch should not be mergeable" };

        // 7. MaxPossiblePower Mismatch
        var s7 = Clone(baseSchedule);
        s7.MaxPossiblePower = 11001;
        yield return new object[] { Clone(baseSchedule), s7, false, "MaxPossiblePower mismatch should not be mergeable" };

        // 8. CarId Mismatch
        var s8 = Clone(baseSchedule);
        s8.CarId = 2;
        yield return new object[] { Clone(baseSchedule), s8, false, "CarId mismatch should not be mergeable" };

        // 9. CarId Match (Null vs Null)
        var s9a = Clone(baseSchedule);
        s9a.CarId = null;
        var s9b = Clone(baseSchedule);
        s9b.CarId = null;
        yield return new object[] { s9a, s9b, true, "CarId match (null vs null) should be mergeable" };

        // 10. OcppChargingConnectorId Mismatch
        var s10 = Clone(baseSchedule);
        s10.OcppChargingConnectorId = 2;
        yield return new object[] { Clone(baseSchedule), s10, false, "OcppChargingConnectorId mismatch should not be mergeable" };

        // 11. OcppChargingConnectorId Match (Null vs Null)
        var s11a = Clone(baseSchedule);
        s11a.OcppChargingConnectorId = null;
        var s11b = Clone(baseSchedule);
        s11b.OcppChargingConnectorId = null;
        yield return new object[] { s11a, s11b, true, "OcppChargingConnectorId match (null vs null) should be mergeable" };

        // 12. Different ScheduleReasons - Mergeable
        // Base has ExpectedSolarProduction. We set this one to CheapGridPrice.
        var s12 = Clone(baseSchedule);
        s12.ScheduleReasons = new HashSet<ScheduleReason> { ScheduleReason.CheapGridPrice };
        yield return new object[] { Clone(baseSchedule), s12, true, "Schedules with different reasons should be mergeable provided other props match" };
    }

    private static DtoChargingSchedule Clone(DtoChargingSchedule s)
    {
        return new DtoChargingSchedule(s.CarId, s.OcppChargingConnectorId, s.MaxPossiblePower, 230, 3, new HashSet<ScheduleReason>(s.ScheduleReasons))
        {
            TargetMinPower = s.TargetMinPower,
            TargetHomeBatteryPower = s.TargetHomeBatteryPower,
            EstimatedSolarPower = s.EstimatedSolarPower,
            ValidFrom = s.ValidFrom,
            ValidTo = s.ValidTo
        };
    }
}
