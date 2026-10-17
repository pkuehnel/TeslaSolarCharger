using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Linq;
using TeslaSolarCharger.Server.Services.SolarValueGathering;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Tests.Helper;

/// <summary>
/// Devices in the value registry as the app wires them: value handling services holding devices, all reached through
/// one generic value service.
/// </summary>
public static class GenericValueTestDevices
{
    /// <summary>
    /// A device that read <paramref name="readings"/> at <paramref name="readAt"/>, or that currently fails to deliver
    /// when <paramref name="hasError"/> is set.
    /// </summary>
    public static IGenericValue<decimal> Device(ConfigurationType configurationType, DateTimeOffset readAt, bool hasError = false,
        int sourceId = 1, params (ValueUsage Usage, decimal Value)[] readings)
    {
        var device = new Mock<IGenericValue<decimal>>();
        device.Setup(v => v.SourceValueKey).Returns(new SourceValueKey(sourceId, configurationType));
        device.Setup(v => v.HasError).Returns(hasError);
        device.Setup(v => v.HistoricValues).Returns(readings.ToDictionary(
            r => new ValueKey(r.Usage, null, sourceId),
            r => new DtoHistoricValue<decimal>(readAt, r.Value, 1)));
        return device.Object;
    }

    public static IDecimalValueHandlingService HandlingService(params IGenericValue<decimal>[] devices)
    {
        var handlingService = new Mock<IDecimalValueHandlingService>();
        handlingService.Setup(s => s.GetSnapshot()).Returns(devices.ToList());
        return handlingService.Object;
    }

    public static GenericValueService Registry(params IDecimalValueHandlingService[] handlingServices) =>
        new(NullLogger<GenericValueService>.Instance, handlingServices);
}
