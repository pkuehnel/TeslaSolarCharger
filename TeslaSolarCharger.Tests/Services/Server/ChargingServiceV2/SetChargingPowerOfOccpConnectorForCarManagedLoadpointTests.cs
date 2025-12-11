using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Autofac.Extras.Moq;
using Microsoft.EntityFrameworkCore;
using Moq;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Server.Dtos.Ocpp;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.ChargepointAction;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Dtos.Settings;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingServiceV2;

public class SetChargingPowerOfOccpConnectorForCarManagedLoadpointTests : TestBase
{
    private readonly ITestOutputHelper _output;

    public SetChargingPowerOfOccpConnectorForCarManagedLoadpointTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
        _output = outputHelper;
    }

    [Theory]
    [MemberData(nameof(GetTestScenarios))]
    public async Task SetChargingPowerOfOccpConnectorForCarManagedLoadpoint_RunsCorrectly(
        string scenarioDescription,
        DtoTargetChargingValues targetChargingValue,
        DtoOcppConnectorState ocppState,
        bool ocppStateExists,
        OcppChargingStationConnector? connectorConfig,
        DateTimeOffset currentDate,
        bool? startChargingResult,
        bool? setCurrentResult,
        bool expectedResult,
        bool expectStartChargingCall,
        bool expectSetCurrentCall)
    {
        _output.WriteLine(scenarioDescription);
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();
        var settingsMock = Mock.Mock<ISettings>();

        var ocppConnectorStates = new ConcurrentDictionary<int, DtoOcppConnectorState>();
        if (ocppStateExists && targetChargingValue.LoadPoint.ChargingConnectorId.HasValue)
        {
            ocppConnectorStates.TryAdd(targetChargingValue.LoadPoint.ChargingConnectorId.Value, ocppState);
        }

        settingsMock.Setup(x => x.OcppConnectorStates).Returns(ocppConnectorStates);

        if (connectorConfig != null)
        {
            Context.OcppChargingStationConnectors.Add(connectorConfig);
            await Context.SaveChangesAsync();
        }

        var actionServiceMock = Mock.Mock<IOcppChargePointActionService>();
        if (expectStartChargingCall)
        {
            var result = startChargingResult == true
                ? new Result<RemoteStartTransactionResponse?>(new RemoteStartTransactionResponse { Status = RemoteStartStopStatus.Accepted }, null, null)
                : new Result<RemoteStartTransactionResponse?>(null, "Error", null);

            actionServiceMock.Setup(x => x.StartCharging(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);
        }
        if (expectSetCurrentCall)
        {
            var result = setCurrentResult == true
                ? new Result<SetChargingProfileResponse?>(new SetChargingProfileResponse { Status = ChargingProfileStatus.Accepted }, null, null)
                : new Result<SetChargingProfileResponse?>(null, "Error", null);

            actionServiceMock.Setup(x => x.SetChargingCurrent(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);
        }

        // Act
        var resultVal = await service.SetChargingPowerOfOccpConnectorForCarManagedLoadpoint(
            targetChargingValue,
            currentDate,
            CancellationToken.None);

        // Assert
        Assert.Equal(expectedResult, resultVal);

        if (expectStartChargingCall)
        {
            actionServiceMock.Verify(x => x.StartCharging(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
        }
        else
        {
            actionServiceMock.Verify(x => x.StartCharging(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        if (expectSetCurrentCall)
        {
             actionServiceMock.Verify(x => x.SetChargingCurrent(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
        }
        else
        {
             actionServiceMock.Verify(x => x.SetChargingCurrent(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    private static DtoTimeStampedValue<T> CreateValue<T>(DateTimeOffset timestamp, T value, DateTimeOffset? lastChanged = null)
    {
        var val = new DtoTimeStampedValue<T>(timestamp, value);
        if (lastChanged.HasValue)
        {
            typeof(DtoTimeStampedValue<T>).GetProperty(nameof(DtoTimeStampedValue<T>.LastChanged))?.SetValue(val, lastChanged);
        }
        return val;
    }

    public static IEnumerable<object[]> GetTestScenarios()
    {
        var currentDate = new DateTimeOffset(2023, 1, 1, 12, 0, 0, TimeSpan.Zero);

        // Scenario 1: Not managed by car
        yield return new object[]
        {
            "Not managed by car",
            new DtoTargetChargingValues(new DtoLoadPointOverview { ManageChargingPowerByCar = false, ChargingConnectorId = 1 }),
            new DtoOcppConnectorState(),
            true,
            null!,
            currentDate,
            null,
            null,
            true,
            false,
            false
        };

        // Scenario 2: Charging Connector ID is null
        yield return new object[]
        {
            "No connector ID",
            new DtoTargetChargingValues(new DtoLoadPointOverview { ManageChargingPowerByCar = true, ChargingConnectorId = null }),
            new DtoOcppConnectorState(),
            true,
            null!,
            currentDate,
            null,
            null,
            true,
            false,
            false
        };

        // Scenario 3: OCPP State not found
        yield return new object[]
        {
            "OCPP State not found",
            new DtoTargetChargingValues(new DtoLoadPointOverview { ManageChargingPowerByCar = true, ChargingConnectorId = 1 }),
            new DtoOcppConnectorState(),
            false,
            null!,
            currentDate,
            null,
            null,
            true,
            false,
            false
        };

        // Scenario 4: Current is sufficient
        // LastSetCurrent (10) >= TargetCurrent (10)
        yield return new object[]
        {
            "Current sufficient",
            new DtoTargetChargingValues(new DtoLoadPointOverview { ManageChargingPowerByCar = true, ChargingConnectorId = 1 })
            {
                TargetValues = new TargetValues { TargetCurrent = 10 }
            },
            new DtoOcppConnectorState
            {
                LastSetCurrent = new DtoTimeStampedValue<decimal?>(currentDate, 10)
            },
            true,
            null!,
            currentDate,
            null,
            null,
            true,
            false,
            false
        };

        // Scenario 5: Update needed, Charging, Success
        // LastSetCurrent (6) < TargetCurrent (10), IsCharging = true
        yield return new object[]
        {
            "Update needed, Charging, Success",
            new DtoTargetChargingValues(new DtoLoadPointOverview { ManageChargingPowerByCar = true, ChargingConnectorId = 1 })
            {
                TargetValues = new TargetValues { TargetCurrent = 10 }
            },
            new DtoOcppConnectorState
            {
                LastSetCurrent = new DtoTimeStampedValue<decimal?>(currentDate, 6),
                IsCharging = new DtoTimeStampedValue<bool>(currentDate, true)
            },
            true,
            new OcppChargingStationConnector("test1") { Id = 1, MaxCurrent = 16, ConnectedPhasesCount = 3 },
            currentDate,
            null,
            true, // SetChargingCurrent success
            true,
            false,
            true
        };

         // Scenario 6: Update needed, Not Charging, Success
         // LastSetCurrent (6) < TargetCurrent (10), IsCharging = false
        yield return new object[]
        {
            "Update needed, Not Charging, Success",
            new DtoTargetChargingValues(new DtoLoadPointOverview { ManageChargingPowerByCar = true, ChargingConnectorId = 2 })
            {
                TargetValues = new TargetValues { TargetCurrent = 10 }
            },
            new DtoOcppConnectorState
            {
                LastSetCurrent = new DtoTimeStampedValue<decimal?>(currentDate, 6),
                IsCharging = new DtoTimeStampedValue<bool>(currentDate, false)
            },
            true,
            new OcppChargingStationConnector("test2") { Id = 2, MaxCurrent = 16, ConnectedPhasesCount = 3, AutoSwitchBetween1And3PhasesEnabled = false },
            currentDate,
            true, // StartCharging success
            null,
            true,
            true,
            false
        };

        // Scenario 7: Update needed, Charging, Fail
        yield return new object[]
        {
            "Update needed, Charging, Fail",
            new DtoTargetChargingValues(new DtoLoadPointOverview { ManageChargingPowerByCar = true, ChargingConnectorId = 3 })
            {
                TargetValues = new TargetValues { TargetCurrent = 10 }
            },
            new DtoOcppConnectorState
            {
                LastSetCurrent = new DtoTimeStampedValue<decimal?>(currentDate, 6),
                IsCharging = new DtoTimeStampedValue<bool>(currentDate, true)
            },
            true,
            new OcppChargingStationConnector("test3") { Id = 3, MaxCurrent = 16, ConnectedPhasesCount = 3 },
            currentDate,
            null,
            false, // SetChargingCurrent fail
            false,
            false,
            true
        };

        // Scenario 8: Update needed, Not Charging, Fail
        yield return new object[]
        {
            "Update needed, Not Charging, Fail",
            new DtoTargetChargingValues(new DtoLoadPointOverview { ManageChargingPowerByCar = true, ChargingConnectorId = 4 })
            {
                TargetValues = new TargetValues { TargetCurrent = 10 }
            },
            new DtoOcppConnectorState
            {
                LastSetCurrent = new DtoTimeStampedValue<decimal?>(currentDate, 6),
                IsCharging = new DtoTimeStampedValue<bool>(currentDate, false)
            },
            true,
            new OcppChargingStationConnector("test4") { Id = 4, MaxCurrent = 16, ConnectedPhasesCount = 3 },
            currentDate,
            false, // StartCharging fail
            null,
            false,
            true,
            false
        };

        // Scenario 9: Cooldown active
        // IsCharging = false, LastSetPhases (1) != ConnectedPhasesCount (3), AutoSwitch enabled, Cooldown active
        var cooldownTime = 60;
        yield return new object[]
        {
            "Cooldown active",
            new DtoTargetChargingValues(new DtoLoadPointOverview { ManageChargingPowerByCar = true, ChargingConnectorId = 5 })
            {
                TargetValues = new TargetValues { TargetCurrent = 10 }
            },
            new DtoOcppConnectorState
            {
                LastSetCurrent = new DtoTimeStampedValue<decimal?>(currentDate, 6),
                IsCharging = CreateValue(currentDate.AddSeconds(-30), false, currentDate.AddSeconds(-30)), // LastChanged set!
                LastSetPhases = new DtoTimeStampedValue<int?>(currentDate, 1)
            },
            true,
            new OcppChargingStationConnector("test5") { Id = 5, MaxCurrent = 16, ConnectedPhasesCount = 3, AutoSwitchBetween1And3PhasesEnabled = true, PhaseSwitchCoolDownTimeSeconds = cooldownTime },
            currentDate, // Current date is only 30s after last change (implied relative to LastChanged)
            null,
            null,
            false,
            false,
            false
        };
    }
}
