using FluentValidation;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.ServerValidators;

public class CarBasicConfigurationValidator : Shared.Dtos.CarBasicConfigurationValidator
{
    public CarBasicConfigurationValidator(IConfigurationWrapper configurationWrapper, IBleService bleService, ITokenHelper tokenHelper)
    {
        When(x => x.ShouldBeManaged, () =>
        {
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

            RuleFor(x => x.UseFleetTelemetry)
                .CustomAsync(async (fleetTelemetryEnabled, context, cancellationToken) =>
                {
                    if (fleetTelemetryEnabled != true)
                    {
                        return;
                    }
                    var tokenState = await tokenHelper.GetFleetApiTokenState(true);
                    if (tokenState != TokenState.UpToDate)
                    {
                        context.AddFailure("You need a valid Fleet API token to use Fleet Telemetry. Go to BaseConfiguration to Generate a new Fleet API Token.");
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
}
