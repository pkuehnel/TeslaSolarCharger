using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Modbus.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Mqtt.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Rest;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Rest.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Template.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.RestValueConfiguration;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

/// <summary>
/// The value each configured result currently delivers, as the value source pages show it.
/// </summary>
public class ValueOverviewServiceTests
{
    private const int ConfigurationId = 1;
    private const int GridResultId = 10;
    private const int SolarResultId = 11;
    private static readonly DateTimeOffset ReadAt = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IRestValueConfigurationService> _restValueConfigurationService = new();
    private readonly Mock<IGenericValueService> _genericValueService = new();
    private List<IGenericValue<decimal>> _values = new();

    public ValueOverviewServiceTests()
    {
        _restValueConfigurationService
            .Setup(s => s.GetFullRestValueConfigurationsByPredicate(It.IsAny<Expression<Func<RestValueConfiguration, bool>>>()))
            .ReturnsAsync([new DtoFullRestValueConfiguration { Id = ConfigurationId, Url = "http://inverter/api", },]);
        _restValueConfigurationService
            .Setup(s => s.GetRestResultConfigurationByPredicate(It.IsAny<Expression<Func<RestValueResultConfiguration, bool>>>()))
            .ReturnsAsync(
            [
                new DtoJsonXmlResultConfiguration { Id = GridResultId, UsedFor = ValueUsage.GridPower, },
                new DtoJsonXmlResultConfiguration { Id = SolarResultId, UsedFor = ValueUsage.InverterPower, },
            ]);
        _genericValueService
            .Setup(s => s.GetAllByPredicate(It.IsAny<Expression<Func<IGenericValue<decimal>, bool>>>()))
            .Returns(() => _values);
    }

    private ValueOverviewService CreateService() => new(
        NullLogger<ValueOverviewService>.Instance,
        _restValueConfigurationService.Object,
        Mock.Of<IMqttConfigurationService>(),
        Mock.Of<IModbusValueConfigurationService>(),
        Mock.Of<ITemplateValueConfigurationService>(),
        _genericValueService.Object);

    private static IGenericValue<decimal> RestValue(int configurationId,
        params (ValueUsage Usage, int ResultConfigurationId, decimal Value, DateTimeOffset Timestamp)[] readings)
    {
        var value = new Mock<IGenericValue<decimal>>();
        value.Setup(v => v.SourceValueKey).Returns(new SourceValueKey(configurationId, ConfigurationType.RestSolarValue));
        value.Setup(v => v.HistoricValues).Returns(readings.ToDictionary(
            r => new ValueKey(r.Usage, null, r.ResultConfigurationId),
            r => new DtoHistoricValue<decimal>(r.Timestamp, r.Value, 5)));
        return value.Object;
    }

    private async Task<DtoOverviewValueResult> ResultAsync(int resultConfigurationId)
    {
        var overview = Assert.Single(await CreateService().GetRestValueOverviews());
        return overview.Results.Single(r => r.Id == resultConfigurationId);
    }

    [Fact]
    public async Task AResultShowsItsValueAndWhenItWasRefreshed()
    {
        _values = [RestValue(ConfigurationId, (ValueUsage.GridPower, GridResultId, -350.5m, ReadAt)),];

        var result = await ResultAsync(GridResultId);

        Assert.Equal(ValueUsage.GridPower, result.UsedFor);
        Assert.NotNull(result.Value);
        Assert.Equal(-350.5m, result.Value.Value);
        Assert.Equal(ReadAt, result.Value.Timestamp);
    }

    [Fact]
    public async Task AResultThatDeliveredNothingYetHasNoValueRatherThanZero()
    {
        //Showing "0 W" refreshed in the year 1 hid that the device never answered.
        _values = [RestValue(ConfigurationId, (ValueUsage.GridPower, GridResultId, -350.5m, ReadAt)),];

        var result = await ResultAsync(SolarResultId);

        Assert.Null(result.Value);
    }

    [Fact]
    public async Task EveryReadingOfTheSameResultIsAddedUpAndAsRecentAsTheNewest()
    {
        _values =
        [
            RestValue(ConfigurationId, (ValueUsage.InverterPower, SolarResultId, 1000, ReadAt.AddSeconds(-10))),
            RestValue(ConfigurationId, (ValueUsage.InverterPower, SolarResultId, 500, ReadAt)),
        ];

        var result = await ResultAsync(SolarResultId);

        Assert.Equal(1500, result.Value!.Value);
        Assert.Equal(ReadAt, result.Value.Timestamp);
    }

    [Fact]
    public async Task ValuesOfOtherConfigurationsAndResultsAreNotMixedIn()
    {
        _values =
        [
            RestValue(ConfigurationId, (ValueUsage.GridPower, GridResultId, 100, ReadAt), (ValueUsage.InverterPower, SolarResultId, 2000, ReadAt)),
            RestValue(ConfigurationId + 1, (ValueUsage.GridPower, GridResultId, 9999, ReadAt)),
        ];

        var result = await ResultAsync(GridResultId);

        Assert.Equal(100, result.Value!.Value);
    }
}
