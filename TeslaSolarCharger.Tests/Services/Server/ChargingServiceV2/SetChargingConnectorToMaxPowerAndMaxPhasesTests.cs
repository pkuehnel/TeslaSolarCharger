using System;
using System.Threading;
using System.Threading.Tasks;
using Autofac.Extras.Moq;
using Moq;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.Ocpp;
using TeslaSolarCharger.Server.Services.ChargepointAction;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Settings;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingServiceV2;

public class SetChargingConnectorToMaxPowerAndMaxPhasesTests : TestBase
{
    public SetChargingConnectorToMaxPowerAndMaxPhasesTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    public enum ExpectedMethodCall
    {
        None,
        StartCharging,
        SetChargingCurrent
    }

    [Theory]
    [InlineData(false, 1, 3, true, 60, 10, false, false, false, ExpectedMethodCall.None)] // Cooldown active
    [InlineData(false, 1, 3, true, 60, 70, false, false, true, ExpectedMethodCall.StartCharging)] // Cooldown expired
    [InlineData(false, 3, 3, true, 60, 10, false, false, true, ExpectedMethodCall.StartCharging)] // Phases match, ignore cooldown
    [InlineData(false, 1, 3, false, 60, 10, false, false, true, ExpectedMethodCall.StartCharging)] // AutoSwitch disabled, ignore cooldown
    [InlineData(false, 1, 3, true, 60, 70, true, false, false, ExpectedMethodCall.StartCharging)] // StartCharging fails
    [InlineData(true, 1, 3, true, 60, 10, false, false, true, ExpectedMethodCall.SetChargingCurrent)] // Charging, set current
    [InlineData(true, 1, 3, true, 60, 10, false, true, false, ExpectedMethodCall.SetChargingCurrent)] // SetCurrent fails
    public async Task SetChargingConnectorToMaxPowerAndMaxPhases_Scenarios(
        bool isCharging,
        int lastSetPhases,
        int connectedPhasesCount,
        bool autoSwitchEnabled,
        int cooldownSeconds,
        int secondsSinceLastChange,
        bool startChargingFails,
        bool setChargingCurrentFails,
        bool expectedResult,
        ExpectedMethodCall expectedCall)
    {
        // Arrange
        var connectorId = 123;
        var maxCurrent = 16; // Changed from 16.0 to 16 (int)

        // Setup Context
        var connector = new OcppChargingStationConnector("TestConnector")
        {
            Id = connectorId,
            MaxCurrent = maxCurrent,
            ConnectedPhasesCount = connectedPhasesCount,
            AutoSwitchBetween1And3PhasesEnabled = autoSwitchEnabled,
            PhaseSwitchCoolDownTimeSeconds = cooldownSeconds
        };
        Context.OcppChargingStationConnectors.Add(connector);
        await Context.SaveChangesAsync();

        // Setup OcppState
        var ocppState = new DtoOcppConnectorState
        {
            IsCharging = CreateTimeStampedValue(isCharging, secondsSinceLastChange),
            LastSetPhases = new DtoTimeStampedValue<int?>(CurrentFakeDate, lastSetPhases),
            LastSetCurrent = new DtoTimeStampedValue<decimal?>(CurrentFakeDate, (decimal)maxCurrent)
        };

        // Mock Action Service
        var actionServiceMock = Mock.Mock<IOcppChargePointActionService>();

        var startResult = startChargingFails
            ? new Result<RemoteStartTransactionResponse?>(null, "Error", null)
            : new Result<RemoteStartTransactionResponse?>(new RemoteStartTransactionResponse(), null, null);

        actionServiceMock
            .Setup(x => x.StartCharging(connectorId, (decimal)maxCurrent, It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(startResult);

        var setResult = setChargingCurrentFails
            ? new Result<SetChargingProfileResponse?>(null, "Error", null)
            : new Result<SetChargingProfileResponse?>(new SetChargingProfileResponse(), null, null);

        actionServiceMock
            .Setup(x => x.SetChargingCurrent(connectorId, (decimal)maxCurrent, It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(setResult);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();

        // Act
        var result = await service.SetChargingConnectorToMaxPowerAndMaxPhases(
            connectorId,
            CurrentFakeDate,
            CancellationToken.None,
            ocppState);

        // Assert
        Assert.Equal(expectedResult, result);

        if (expectedCall == ExpectedMethodCall.StartCharging)
        {
            var expectedPhases = autoSwitchEnabled ? (int?)connectedPhasesCount : null;
            actionServiceMock.Verify(x => x.StartCharging(connectorId, (decimal)maxCurrent, expectedPhases, It.IsAny<CancellationToken>()), Times.Once);
            actionServiceMock.Verify(x => x.SetChargingCurrent(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        else if (expectedCall == ExpectedMethodCall.SetChargingCurrent)
        {
            actionServiceMock.Verify(x => x.SetChargingCurrent(connectorId, (decimal)maxCurrent, null, It.IsAny<CancellationToken>()), Times.Once);
            actionServiceMock.Verify(x => x.StartCharging(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        else
        {
            actionServiceMock.Verify(x => x.StartCharging(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
            actionServiceMock.Verify(x => x.SetChargingCurrent(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    private DtoTimeStampedValue<bool> CreateTimeStampedValue(bool value, int secondsSinceLastChange)
    {
         var ts = new DtoTimeStampedValue<bool>(CurrentFakeDate.AddSeconds(-secondsSinceLastChange - 10), !value);
         ts.Update(CurrentFakeDate.AddSeconds(-secondsSinceLastChange), value);
         return ts;
    }
}
