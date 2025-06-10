using FluentValidation;

namespace TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

public class DtoBaseConfiguration : BaseConfigurationBase
{
    
}


public class BaseConfigurationValidator : AbstractValidator<DtoBaseConfiguration>
{
    public BaseConfigurationValidator()
    {
        When(x => x.UseTeslaMateAsDataSource, () =>
        {
            RuleFor(x => x.UseTeslaMateIntegration)
                .Equal(true);
        });

        When(x => x.UseTeslaMateIntegration, () =>
        {
            RuleFor(x => x.TeslaMateDbServer)
                .NotEmpty();
            RuleFor(x => x.TeslaMateDbPort)
                .NotEmpty();
            RuleFor(x => x.TeslaMateDbDatabaseName)
                .NotEmpty();
            RuleFor(x => x.TeslaMateDbUser)
                .NotEmpty();
            RuleFor(x => x.TeslaMateDbPassword)
                .NotEmpty();
            RuleFor(x => x.MosquitoServer)
                .NotEmpty();
        });

        RuleFor(x => x.HomeGeofenceRadius)
            .GreaterThan(0);

        When(x => x.UsePredictedSolarPowerGenerationForChargingSchedules, () =>
        {
            RuleFor(x => x.PredictSolarPowerGeneration)
                .Equal(true)
                .WithMessage("When Use Predicted Solar Power Generation For Charging Schedules is enabled you also need to enable Predict Solar Power Generation.");
        });

        When(x => x.DynamicHomeBatteryMinSoc == true, () =>
        {
            RuleFor(x => x.PredictSolarPowerGeneration)
                .Equal(true)
                .WithMessage("When Dynamic Home Battery Min Soc is set, Solar Power Prediction is required.");
            RuleFor(x => x.HomeBatteryUsableEnergy)
                .NotEmpty();
            RuleFor(x => x.HomeBatteryChargingPower)
                .NotEmpty();
        });

    }
}
