using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.ChargingStation;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server.Setup;

public class SetupCapabilityProbeTests : TestBase
{
    private readonly Mock<ITokenHelper> _tokenHelper = new();
    private readonly Mock<IBackendApiService> _backendApiService = new();
    private readonly Mock<IGenericValueService> _genericValueService = new();
    private readonly Mock<IOcppChargingStationConfigurationService> _chargingStationConfigurationService = new();
    private readonly Mock<IConfigurationWrapper> _configurationWrapper = new();

    public SetupCapabilityProbeTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
        _tokenHelper.Setup(h => h.GetBackendTokenState(It.IsAny<bool>())).ReturnsAsync(TokenState.UpToDate);
        _tokenHelper.Setup(h => h.GetFleetApiTokenState(It.IsAny<bool>())).ReturnsAsync(TokenState.UpToDate);
        _backendApiService.Setup(s => s.IsBaseAppLicensed(It.IsAny<bool>())).ReturnsAsync(new Result<bool?>(true, null, null));
        _genericValueService
            .Setup(s => s.GetAllByPredicate(It.IsAny<Expression<Func<IGenericValue<decimal>, bool>>>()))
            .Returns(new List<IGenericValue<decimal>>());
        _chargingStationConfigurationService.Setup(s => s.GetChargingStations()).ReturnsAsync(new List<DtoChargingStation>());
    }

    private SetupCapabilityProbe NewProbe() => new(
        Moq.Mock.Of<ILogger<SetupCapabilityProbe>>(),
        _tokenHelper.Object,
        _backendApiService.Object,
        _genericValueService.Object,
        _chargingStationConfigurationService.Object,
        _configurationWrapper.Object,
        Context);

    /// <summary>A gathered value that reports the given measurements, which is how a configured source shows up.</summary>
    private static IGenericValue<decimal> ValueProviding(params ValueUsage[] usages)
    {
        var value = new Mock<IGenericValue<decimal>>();
        var historicValues = usages.ToDictionary(
            u => new ValueKey(u, null, 1),
            _ => new DtoHistoricValue<decimal>(DateTimeOffset.UtcNow, 0, 1));
        value.Setup(v => v.HistoricValues).Returns(historicValues);
        return value.Object;
    }

    [Fact]
    public async Task ConfiguredMeasurementsAreReportedAsAvailable()
    {
        _genericValueService
            .Setup(s => s.GetAllByPredicate(It.IsAny<Expression<Func<IGenericValue<decimal>, bool>>>()))
            .Returns(new List<IGenericValue<decimal>>
            {
                ValueProviding(ValueUsage.GridPower, ValueUsage.InverterPower),
                ValueProviding(ValueUsage.HomeBatterySoc, ValueUsage.HomeBatteryPower),
            });

        var capabilities = await NewProbe().GetCapabilities();

        Assert.True(capabilities.HasGridPowerSource);
        Assert.True(capabilities.HasSolarGenerationSource);
        Assert.True(capabilities.HasHomeBatterySocSource);
        Assert.True(capabilities.HasHomeBatteryPowerSource);
    }

    [Fact]
    public async Task MissingMeasurementsAreReportedAsMissing()
    {
        var capabilities = await NewProbe().GetCapabilities();

        Assert.False(capabilities.HasGridPowerSource);
        Assert.False(capabilities.HasHomeBatterySocSource);
    }

    [Fact]
    public async Task ALicenceLookupThatFailsIsReportedAsUnknownRatherThanUnlicensed()
    {
        _backendApiService.Setup(s => s.IsBaseAppLicensed(It.IsAny<bool>())).ThrowsAsync(new InvalidOperationException("backend down"));

        var capabilities = await NewProbe().GetCapabilities();

        Assert.Null(capabilities.IsBaseAppLicensed);
    }

    [Fact]
    public async Task ATokenStateThatCannotBeDeterminedIsNotReportedAsNotConnected()
    {
        _tokenHelper.Setup(h => h.GetFleetApiTokenState(It.IsAny<bool>())).ThrowsAsync(new InvalidOperationException("no answer"));

        var capabilities = await NewProbe().GetCapabilities();

        Assert.Null(capabilities.FleetApiTokenState);
        //One failed check must not take the others down with it.
        Assert.Equal(TokenState.UpToDate, capabilities.BackendTokenState);
    }

    [Fact]
    public async Task ConnectorsOfEveryConnectedStationAreListed()
    {
        _chargingStationConfigurationService
            .Setup(s => s.GetChargingStations())
            .ReturnsAsync(new List<DtoChargingStation> { new("CP1") { Id = 1, }, new("CP2") { Id = 2, }, });
        _chargingStationConfigurationService
            .Setup(s => s.GetChargingStationConnectors(1))
            .ReturnsAsync(new List<DtoChargingStationConnector> { new("C1") { Id = 10, }, });
        _chargingStationConfigurationService
            .Setup(s => s.GetChargingStationConnectors(2))
            .ReturnsAsync(new List<DtoChargingStationConnector> { new("C1") { Id = 20, }, new("C2") { Id = 21, }, });

        var capabilities = await NewProbe().GetCapabilities();

        Assert.Equal(new[] { 10, 20, 21, }, capabilities.KnownChargingStationConnectorIds);
    }

    [Fact]
    public async Task FailingToListChargingStationsLeavesTheRestOfTheAnswerIntact()
    {
        _chargingStationConfigurationService.Setup(s => s.GetChargingStations()).ThrowsAsync(new InvalidOperationException("db down"));

        var capabilities = await NewProbe().GetCapabilities();

        Assert.Empty(capabilities.KnownChargingStationConnectorIds);
        Assert.Equal(TokenState.UpToDate, capabilities.BackendTokenState);
    }

    [Fact]
    public async Task ACarThatHasNotBeenSavedYetHasNothingToReport()
    {
        //A draft without a row is the normal state mid setup, not an error to show the user.
        Assert.Null(await NewProbe().GetCarCapabilities(404));
    }

    [Fact]
    public async Task WhatACarCanDoIsDetectedRatherThanAsked()
    {
        Context.Cars.Add(new TeslaSolarCharger.Model.Entities.TeslaSolarCharger.Car
        {
            Id = 7,
            Vin = "VIN7",
            IsFleetTelemetryHardwareIncompatible = true,
            UseFleetTelemetry = false,
            TeslaFleetApiState = TeslaCarFleetApiState.Ok,
        });
        await Context.SaveChangesAsync();
        _backendApiService.Setup(s => s.IsFleetApiLicensed("VIN7", It.IsAny<bool>())).ReturnsAsync(true);

        var capabilities = await NewProbe().GetCarCapabilities(7);

        Assert.NotNull(capabilities);
        Assert.True(capabilities!.IsFleetTelemetryHardwareIncompatible);
        Assert.False(capabilities.UsesFleetTelemetry);
        Assert.Equal(TeslaCarFleetApiState.Ok, capabilities.FleetApiState);
        Assert.True(capabilities.IsFleetApiLicensed);
    }

    [Fact]
    public async Task APerCarLicenceLookupThatFailsIsReportedAsUnknown()
    {
        Context.Cars.Add(new TeslaSolarCharger.Model.Entities.TeslaSolarCharger.Car { Id = 7, Vin = "VIN7", });
        await Context.SaveChangesAsync();
        _backendApiService.Setup(s => s.IsFleetApiLicensed("VIN7", It.IsAny<bool>())).ThrowsAsync(new InvalidOperationException("backend down"));

        var capabilities = await NewProbe().GetCarCapabilities(7);

        Assert.Null(capabilities!.IsFleetApiLicensed);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public async Task TeslaMateCountsAsTheDataSourceOnlyWhenTeslaIsNot(bool useTeslaMate,
        bool getVehicleDataFromTesla,
        bool expected)
    {
        _configurationWrapper.Setup(w => w.UseTeslaMateIntegration()).Returns(useTeslaMate);
        _configurationWrapper.Setup(w => w.GetVehicleDataFromTesla()).Returns(getVehicleDataFromTesla);

        var capabilities = await NewProbe().GetCapabilities();

        Assert.Equal(expected, capabilities.UsesTeslaMateAsDataSource);
    }
}
