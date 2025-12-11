using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Autofac.Extras.Moq;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Dtos.Settings;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server.TargetChargingValueCalculationService;

public class CalculateEstimatedCurrentUsageTests : TestBase
{
    public CalculateEstimatedCurrentUsageTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    [Theory]
    // 1. TargetValues is null -> Returns 0. TargetCurrent -1 (ignored).
    [InlineData(true, false, 0, -1, false, null, 0, null, 0, 0)]

    // 2. StopCharging is true -> Returns 0
    [InlineData(false, true, 10, 16, false, null, 0, null, 0, 0)]

    // 3. Managed by Car: LastSet=16, Actual=16. NotUsed=0. Returns Target=20.
    [InlineData(false, false, 16, 20, true, 1, 16, null, 0, 20)]

    // 4. Managed by Car: LastSet=16, Actual=10. NotUsed=6 (>1). Returns Actual=10.
    [InlineData(false, false, 10, 20, true, 1, 16, null, 0, 10)]

    // 5. Managed by Car: LastSet=16, Actual=15. NotUsed=1 (<=1). Returns Target=20.
    [InlineData(false, false, 15, 20, true, 1, 16, null, 0, 20)]

    // 6. Managed by Connector: LastSet=16, Actual=16. NotUsed=0. Returns Target=20.
    [InlineData(false, false, 16, 20, false, null, 0, 101, 16, 20)]

    // 7. Managed by Connector: LastSet=16, Actual=10. NotUsed=6 (>1). Returns Actual=10.
    [InlineData(false, false, 10, 20, false, null, 0, 101, 16, 10)]

    // 8. No Car/Connector logic (or CarId/ConnectorId null). LastSet defaults to 0. Actual=10. NotUsed=-10 (<=1). Returns Target=20.
    [InlineData(false, false, 10, 20, false, null, 0, null, 0, 20)]

    // 9. TargetCurrent null -> Returns ActualCurrent (when NotUsed <= 1). TargetCurrent -1 (means null).
    [InlineData(false, false, 12, -1, false, null, 0, null, 0, 12)]

    // 10. Managed by Connector but ConnectorId not found (999).
    // LastSet defaults to 0. Actual=10. NotUsed=-10. Returns Target=20.
    [InlineData(false, false, 10, 20, false, null, 0, 999, 0, 20)]

    public void CalculateEstimatedCurrentUsage_CalculatesCorrectly(
        bool targetValuesIsNull,
        bool stopCharging,
        double actualCurrentDouble,
        double targetCurrentDouble, // -1 means null
        bool manageByCar,
        int? carId,
        int carLastSetAmp,
        int? connectorId,
        double connectorLastSetCurrentDouble,
        double expectedResultDouble)
    {
        var actualCurrent = (decimal)actualCurrentDouble;
        var targetCurrent = targetCurrentDouble == -1 ? (decimal?)null : (decimal)targetCurrentDouble;
        var connectorLastSetCurrent = (decimal)connectorLastSetCurrentDouble;
        var expectedResult = (decimal)expectedResultDouble;

        // Arrange
        var loadPointOverview = new DtoLoadPointOverview
        {
            ActualCurrent = actualCurrent,
            ManageChargingPowerByCar = manageByCar,
            CarId = carId,
            ChargingConnectorId = connectorId
        };

        var dto = new DtoTargetChargingValues(loadPointOverview);
        if (!targetValuesIsNull)
        {
            dto.TargetValues = new TargetValues
            {
                StopCharging = stopCharging,
                TargetCurrent = targetCurrent
            };
        }

        // Setup Settings Mock
        var settingsMock = Mock.Mock<ISettings>();

        var cars = new List<DtoCar>();
        if (carId.HasValue)
        {
            cars.Add(new DtoCar
            {
                Id = carId.Value,
                LastSetAmp = new DtoTimeStampedValue<int>(DateTimeOffset.MinValue, carLastSetAmp)
            });
        }
        settingsMock.Setup(s => s.Cars).Returns(cars);

        var connectors = new ConcurrentDictionary<int, DtoOcppConnectorState>();
        if (connectorId.HasValue && connectorId.Value != 999)
        {
             connectors.TryAdd(connectorId.Value, new DtoOcppConnectorState
             {
                 LastSetCurrent = new DtoTimeStampedValue<decimal?>(DateTimeOffset.MinValue, connectorLastSetCurrent)
             });
        }
        settingsMock.Setup(s => s.OcppConnectorStates).Returns(connectors);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.TargetChargingValueCalculationService>();

        // Act
        var result = service.CalculateEstimatedCurrentUsage(dto, new ConstraintValues());

        // Assert
        Assert.Equal(expectedResult, result);
    }
}
