using FluentValidation;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.ServerValidators;

public class CarBasicConfigurationValidator : Shared.Dtos.CarBasicConfigurationValidator
{
    private readonly IBackendApiService _backendApiService;
    private readonly IConfigurationWrapper _configurationWrapper;


    public CarBasicConfigurationValidator(IConfigurationWrapper configurationWrapper,
        IBleService bleService,
        ITokenHelper tokenHelper,
        IBackendApiService backendApiService)
    {
        _backendApiService = backendApiService;
        _configurationWrapper = configurationWrapper;
        When(x => x.ShouldBeManaged && x.CarType == CarType.Tesla, () =>
        {
            var isTeslaMateDataSource = configurationWrapper.UseTeslaMateIntegration() && !configurationWrapper.GetVehicleDataFromTesla();
            if (isTeslaMateDataSource)
            {
                RuleFor(x => x.UseFleetTelemetry).Equal(false)
                    .WithMessage("As TeslaMate is selected as data source in the Base Configuration, you cannot enable Fleet Telemetry.");
            }
            RuleFor(x => x.UseBle)
                .MustAsync(async (_, useBle, context, _) =>
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
                When(x => !IsBleDataCollectionConfigured(x), () =>
                {
                    RuleFor(x => x.HomeDetectionVia)
                        .Equal(HomeDetectionVia.GpsLocation)
                        .WithMessage("Without Fleet Telemetry only home detection via GPS location is supported.");
                });
            });

            When(x => !IsBleDataCollectionConfigured(x), () =>
            {
                RuleFor(x => x.HomeDetectionVia)
                    .NotEqual(HomeDetectionVia.BlePresence)
                    .WithMessage("Home detection via BLE presence is only supported for cars with enabled BLE and disabled tracking relevant fields while Get data via BLE is enabled in the advanced Base Configuration settings.");
            });

            When(x => x.UseFleetTelemetry, () =>
            {
                RuleFor(x => x.IncludeTrackingRelevantFields)
                    .MustAsync(async (_, includeTrackingRelevantFields, context, _) =>
                    {
                        var hasFleetApiLicense = await GetFleetApiLicenseCachedAsync(context);
                        return !includeTrackingRelevantFields || hasFleetApiLicense;
                    })
                    .WithMessage("Car not licensed for Fleet API. Manage Fleet API subscriptions via https://solar4car.com/subscriptions.");

                When(x => x.IncludeTrackingRelevantFields == false && isTeslaMateDataSource == false, () =>
                {
                    RuleFor(x => x.HomeDetectionVia)
                        .NotEqual(HomeDetectionVia.GpsLocation)
                        .WithMessage("GPS location cannot be used for home detection if tracking relevant fields are not enabled.");
                });

            });


            RuleFor(x => x.UseFleetTelemetry)
                .CustomAsync(async (fleetTelemetryEnabled, context, _) =>
                {
                    var tokenState = await tokenHelper.GetFleetApiTokenState(true);
                    if (tokenState != TokenState.UpToDate)
                    {
                        context.AddFailure("You need a valid Fleet API token to use Fleet Telemetry. Go to BaseConfiguration to Generate a new Fleet API Token.");
                    }
                    //Cars whose data is collected via BLE do not use Fleet Telemetry, so it must not be forced on for them.
                    if (fleetTelemetryEnabled != true && !IsBleDataCollectionConfigured(context.InstanceToValidate))
                    {
                        context.AddFailure("Enabling Fleet Telemetry is required and will be autodisabled if your car does not support it");
                    }
                });

            When(x => x.UseBle, () =>
            {
                RuleFor(x => x.BleApiBaseUrl)
                    .NotEmpty()
                    .CustomAsync(async (bleApiBaseUrl, context, _) =>
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

    private bool IsBleDataCollectionConfigured(CarBasicConfiguration config)
    {
        return _configurationWrapper.GetVehicleDataViaBle()
               && config.UseBle
               && !config.IncludeTrackingRelevantFields;
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
