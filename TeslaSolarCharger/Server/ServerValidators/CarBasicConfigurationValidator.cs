using FluentValidation;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;

namespace TeslaSolarCharger.Server.ServerValidators;

public class CarBasicConfigurationValidator : Shared.Dtos.CarBasicConfigurationValidator
{
    public CarBasicConfigurationValidator(IConfigurationWrapper configurationWrapper, IBleService bleService)
    {
        RuleFor(x => x.MaximumAmpere).LessThanOrEqualTo(7);
        var isTeslaMateDataSource = configurationWrapper.UseTeslaMateIntegration() && !configurationWrapper.GetVehicleDataFromTesla();
        if (isTeslaMateDataSource)
        {
            RuleFor(x => x.UseFleetTelemetry).Equal(false)
                .WithMessage("As TeslaMate is selected as DataSource in BaseConfiguration you can not enable Fleet Telemetry");
        }

        When(x => (x.UseFleetTelemetry == false), () =>
        {
            RuleFor(x => x.IncludeTrackingRelevantFields)
                    .Equal(false)
                .WithMessage("Tracking relevant fields can only be included if Fleet Telemetry is enabled.");
        });

        When(x => (x.UseBle == true), () =>
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
    }
}
