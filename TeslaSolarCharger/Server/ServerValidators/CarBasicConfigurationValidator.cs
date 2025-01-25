using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.ServerValidators;

public class CarBasicConfigurationValidator : Shared.Dtos.CarBasicConfigurationValidator
{
    private readonly IBackendApiService _backendApiService;


    public CarBasicConfigurationValidator(IConfigurationWrapper configurationWrapper,
        IBleService bleService,
        ITokenHelper tokenHelper,
        ITeslaSolarChargerContext dbContext,
        IBackendApiService backendApiService)
    {
        _backendApiService = backendApiService;
        When(x => x.ShouldBeManaged, () =>
        {
            var isTeslaMateDataSource = configurationWrapper.UseTeslaMateIntegration() && !configurationWrapper.GetVehicleDataFromTesla();
            if (isTeslaMateDataSource)
            {
                RuleFor(x => x.UseFleetTelemetry).Equal(false)
                    .WithMessage("As TeslaMate is selected as DataSource in BaseConfiguration you can not enable Fleet Telemetry");
            }
            RuleFor(x => x.UseBle)
                .MustAsync(async (model, useBle, context, ct) =>
                {
                    var hasFleetApiLicense = await GetFleetApiLicenseCachedAsync(context);
                    return hasFleetApiLicense || useBle;
                })
                .WithMessage("You need to use BLE on cars without Fleet API license.");



            When(x => (x.UseFleetTelemetry == false), () =>
            {
                RuleFor(x => x.IncludeTrackingRelevantFields)
                    .Equal(false)
                    .WithMessage("Tracking relevant fields can only be included if Fleet Telemetry is enabled.");
            });

            When(x => x.UseFleetTelemetry, () =>
            {
                RuleFor(x => x.IncludeTrackingRelevantFields)
                    .MustAsync(async (model, includeTrackingRelevantFields, context, ct) =>
                    {
                        var hasFleetApiLicense = await GetFleetApiLicenseCachedAsync(context);
                        return !includeTrackingRelevantFields || hasFleetApiLicense;
                    })
                    .WithMessage("Car not licensed for Fleet API. Manage Fleet API subscriptions via https://solar4car.com/subscriptions.");
            });


            RuleFor(x => x.UseFleetTelemetry)
                .CustomAsync(async (fleetTelemetryEnabled, context, cancellationToken) =>
                {
                    var tokenState = await tokenHelper.GetFleetApiTokenState(true);
                    if (tokenState != TokenState.UpToDate)
                    {
                        context.AddFailure("You need a valid Fleet API token to use Fleet Telemetry. Go to BaseConfiguration to Generate a new Fleet API Token.");
                    }
                    var isCarFleetTelemetryHardwareIncompatible = await dbContext.Cars
                        .Where(c => c.Vin == context.InstanceToValidate.Vin && c.IsFleetTelemetryHardwareIncompatible)
                        .Select(c => c.IsFleetTelemetryHardwareIncompatible)
                        .FirstOrDefaultAsync();
                    if (isCarFleetTelemetryHardwareIncompatible)
                    {
                        context.AddFailure("The selected car is not compatible with Fleet Telemetry. Please disable Fleet Telemetry.");
                    }
                    else if (fleetTelemetryEnabled != true)
                    {
                        context.AddFailure("Enabling Fleet Telemetry is required and will be autodisabled if your car does not support it");
                    }
                });

            When(x => x.UseBle, () =>
            {
                RuleFor(x => x.BleApiBaseUrl)
                    .NotEmpty()
                    .CustomAsync(async (bleApiBaseUrl, context, cancellationToken) =>
                    {
                        var errorMessage = await bleService.CheckBleApiVersionCompatibility(bleApiBaseUrl);
                        if (!string.IsNullOrEmpty(errorMessage))
                        {
                            // The validation failed; add your returned error message directly:
                            context.AddFailure(errorMessage);
                        }
                    });

            });
        });
    }

    private async Task<bool> GetFleetApiLicenseCachedAsync(ValidationContext<CarBasicConfiguration> context)
    {
        // Use a well-known key to store/retrieve your data.
        const string fleetApiLicenseKey = "HasFleetApiLicense";

        // 1. Check if we already have a cached value in RootContextData.
        if (context.RootContextData.TryGetValue(fleetApiLicenseKey, out var cachedValue))
        {
            return (bool)cachedValue;
        }

        // 2. If not cached yet, retrieve from the service.
        var model = context.InstanceToValidate;
        var hasFleetApiLicense = await _backendApiService.IsFleetApiLicensed(model.Vin, false);

        // 3. Store it in RootContextData for future rules to reuse.
        context.RootContextData[fleetApiLicenseKey] = hasFleetApiLicense;

        return hasFleetApiLicense;
    }
}
