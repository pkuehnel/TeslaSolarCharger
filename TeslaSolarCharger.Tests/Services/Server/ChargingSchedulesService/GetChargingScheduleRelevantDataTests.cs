using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Autofac.Extras.Moq;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingSchedulesService;

public class GetChargingScheduleRelevantDataTests : TestBase
{
    public GetChargingScheduleRelevantDataTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    [Theory]
    // 1. All null inputs -> All null outputs
    [InlineData(null, null, null, null, null, null, null, null)]

    // 2. Connector Only (Id=1: 3ph, 16A, Min 6A, NoSwitch) -> Uses connector values where applicable
    [InlineData(null, 1, null, null, 3, 16, 3, 6)]

    // 3. Car Only (Id=1: Tesla, 16A, 50kWh, Min 5A, 3ph) -> Uses car values
    [InlineData(1, null, 50000, 50, 3, 16, 3, 5)]

    // 4. Car 1 + Connector 1 (Both 3ph, 16A match)
    [InlineData(1, 1, 50000, 50, 3, 16, 3, 5)]

    // 5. Car 2 (Manual, 32A, 1ph) + Connector 2 (32A, 3ph, AutoSwitch)
    // Car MaxPhases=1 (DB).
    // connectorMinPhases = 1 (AutoSwitch=True & Car!=Tesla)
    // minPhases = Min(1, 1) = 1.
    [InlineData(2, 2, 75000, 80, 1, 32, 1, 6)]

    // 6. Car 1 (Tesla, 3ph) + Connector 2 (AutoSwitch)
    // connectorMinPhases = 3 (AutoSwitch=True but Car==Tesla -> returns ConnectedPhasesCount=3)
    // minPhases = Min(3, 3) = 3.
    // MaxCurrent: Car=16, Conn=32 -> Min is 16.
    [InlineData(1, 2, 50000, 50, 3, 16, 3, 5)]

    // 7. Unknown Car ID -> Returns nulls (if Connector is also null or partial)
    [InlineData(999, null, null, null, null, null, null, null)]

    // 8. Unknown Connector ID
    [InlineData(null, 999, null, null, null, null, null, null)]

    // 9. Car 3 (Manual, 3ph capable) + Connector 2 (AutoSwitch)
    // Car 3: MaxPhases 3.
    // connectorMinPhases = 1 (AutoSwitch=True & NotTesla).
    // minPhases = Min(1, 3) = 1.
    // maxPhases = Min(3, 3) = 3.
    // maxCurrent = Min(32, 32) = 32.
    // minCurrent = Min(6, 6) = 6.
    [InlineData(3, 2, 60000, 60, 3, 32, 1, 6)]

    // 10. Car 1 (Tesla) + Connector 3 (Null values)
    // Connector 3: All nulls.
    // Result should match Car 1 values.
    [InlineData(1, 3, 50000, 50, 3, 16, 3, 5)]

    public async Task GetChargingScheduleRelevantData_ReturnsExpectedValues(
        int? carId, int? connectorId,
        int? expectedUsableEnergy, int? expectedCarSoC,
        int? expectedMaxPhases, int? expectedMaxCurrent,
        int? expectedMinPhases, int? expectedMinCurrent)
    {
        // Arrange
        await SetupData();
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();

        // Act
        var result = await service.GetChargingScheduleRelevantData(carId, connectorId);

        // Assert
        Assert.Equal(expectedUsableEnergy, result.UsableEnergy);
        Assert.Equal(expectedCarSoC, result.carSoC);
        Assert.Equal(expectedMaxPhases, result.maxPhases);
        Assert.Equal(expectedMaxCurrent, result.maxCurrent);
        Assert.Equal(expectedMinPhases, result.minPhases);
        Assert.Equal(expectedMinCurrent, result.minCurrent);
    }

    private async Task SetupData()
    {
        // Connectors
        Context.OcppChargingStationConnectors.Add(new OcppChargingStationConnector("C1")
        {
            Id = 1,
            ConnectedPhasesCount = 3,
            MaxCurrent = 16,
            MinCurrent = 6,
            AutoSwitchBetween1And3PhasesEnabled = false
        });
        Context.OcppChargingStationConnectors.Add(new OcppChargingStationConnector("C2")
        {
            Id = 2,
            ConnectedPhasesCount = 3,
            MaxCurrent = 32,
            MinCurrent = 6,
            AutoSwitchBetween1And3PhasesEnabled = true
        });
        Context.OcppChargingStationConnectors.Add(new OcppChargingStationConnector("C3")
        {
            Id = 3,
            ConnectedPhasesCount = null,
            MaxCurrent = null,
            MinCurrent = null,
            AutoSwitchBetween1And3PhasesEnabled = false
        });

        // Cars (DB)
        Context.Cars.Add(new Car
        {
            Id = 1,
            MaximumAmpere = 16,
            UsableEnergy = 50000,
            MinimumAmpere = 5,
            MaximumPhases = 3,
            CarType = CarType.Tesla
        });
        Context.Cars.Add(new Car
        {
            Id = 2,
            MaximumAmpere = 32,
            UsableEnergy = 75000,
            MinimumAmpere = 6,
            MaximumPhases = 1,
            CarType = CarType.Manual
        });
        Context.Cars.Add(new Car
        {
            Id = 3,
            MaximumAmpere = 32,
            UsableEnergy = 60000,
            MinimumAmpere = 6,
            MaximumPhases = 3,
            CarType = CarType.Manual
        });

        await Context.SaveChangesAsync();

        // Cars (Settings)
        var settingsCars = new List<DtoCar>
        {
            new DtoCar
            {
                Id = 1,
                SoC = new DtoTimeStampedValue<int?>(DateTimeOffset.MinValue, 50),
                ChargerPhases = new DtoTimeStampedValue<int?>(DateTimeOffset.MinValue, 3)
            },
            new DtoCar
            {
                Id = 2,
                SoC = new DtoTimeStampedValue<int?>(DateTimeOffset.MinValue, 80),
                ChargerPhases = new DtoTimeStampedValue<int?>(DateTimeOffset.MinValue, null)
            },
             new DtoCar
            {
                Id = 3,
                SoC = new DtoTimeStampedValue<int?>(DateTimeOffset.MinValue, 60),
                ChargerPhases = new DtoTimeStampedValue<int?>(DateTimeOffset.MinValue, null)
            }
        };

        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(settingsCars);
    }
}
