using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Moq;
using TeslaSolarCharger.Server.Helper.Contracts;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Dtos.Settings;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingServiceV2;

public class AddNotChargingReasonsTests : TestBase
{
    public AddNotChargingReasonsTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    [Theory]
    // IsHomeGeofence, PluggedIn, ExpectedReasons
    [InlineData(true, true, 0)]
    [InlineData(false, true, 1)] // Car is not at home
    [InlineData(null, true, 1)] // Car is not at home (null != true)
    [InlineData(true, false, 1)] // Car is not plugged in
    [InlineData(true, null, 1)] // Car is not plugged in (null != true)
    [InlineData(false, false, 2)] // Car is not at home AND Car is not plugged in
    public void AddNotChargingReasons_CarScenarios(bool? isHomeGeofence, bool? pluggedIn, int expectedReasonCount)
    {
        // Arrange
        var carId = 1;
        var car = new DtoCar
        {
            Id = carId,
            IsHomeGeofence = new DtoTimeStampedValue<bool?>(DateTimeOffset.MinValue, isHomeGeofence),
            PluggedIn = new DtoTimeStampedValue<bool?>(DateTimeOffset.MinValue, pluggedIn)
        };

        var settingsMock = Mock.Mock<ISettings>();
        settingsMock.Setup(x => x.CarsToManage).Returns(new List<DtoCar> { car });
        settingsMock.Setup(x => x.OcppConnectorStates).Returns(new ConcurrentDictionary<int, DtoOcppConnectorState>());

        var reasonHelperMock = Mock.Mock<INotChargingWithExpectedPowerReasonHelper>();

        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();

        // Act
        service.AddNotChargingReasons();

        // Assert
        reasonHelperMock.Verify(x => x.AddLoadPointSpecificReason(
            It.IsAny<int?>(),
            It.IsAny<int?>(),
            It.IsAny<NotChargingWithExpectedPowerReasonTemplate>()),
            Times.Exactly(expectedReasonCount));

        if (isHomeGeofence != true)
        {
            reasonHelperMock.Verify(x => x.AddLoadPointSpecificReason(
                carId,
                null,
                It.Is<NotChargingWithExpectedPowerReasonTemplate>(r => r.LocalizationKey == "Car is not at home")),
                Times.Once);
        }

        if (pluggedIn != true)
        {
             reasonHelperMock.Verify(x => x.AddLoadPointSpecificReason(
                carId,
                null,
                It.Is<NotChargingWithExpectedPowerReasonTemplate>(r => r.LocalizationKey == "Car is not plugged in")),
                Times.Once);
        }
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    public void AddNotChargingReasons_OcppConnectorScenarios(bool isPluggedIn, int expectedReasonCount)
    {
        // Arrange
        var connectorId = 100;
        var connectorState = new DtoOcppConnectorState
        {
             IsPluggedIn = new DtoTimeStampedValue<bool>(DateTimeOffset.MinValue, isPluggedIn)
        };

        var settingsMock = Mock.Mock<ISettings>();
        settingsMock.Setup(x => x.CarsToManage).Returns(new List<DtoCar>());
        var dict = new ConcurrentDictionary<int, DtoOcppConnectorState>();
        dict.TryAdd(connectorId, connectorState);
        settingsMock.Setup(x => x.OcppConnectorStates).Returns(dict);

        var reasonHelperMock = Mock.Mock<INotChargingWithExpectedPowerReasonHelper>();

        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();

        // Act
        service.AddNotChargingReasons();

        // Assert
         reasonHelperMock.Verify(x => x.AddLoadPointSpecificReason(
            It.IsAny<int?>(),
            It.IsAny<int?>(),
            It.IsAny<NotChargingWithExpectedPowerReasonTemplate>()),
            Times.Exactly(expectedReasonCount));

        if (!isPluggedIn)
        {
            reasonHelperMock.Verify(x => x.AddLoadPointSpecificReason(
                null,
                connectorId,
                It.Is<NotChargingWithExpectedPowerReasonTemplate>(r => r.LocalizationKey == "Charging connector is not plugged in")),
                Times.Once);
        }
    }
}
