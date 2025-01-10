using FluentValidation;

namespace TeslaSolarCharger.Server.ServerValidators;

public class CarBasicConfigurationValidator : Shared.Dtos.CarBasicConfigurationValidator
{
    public CarBasicConfigurationValidator()
    {
        RuleFor(x => x.MaximumAmpere).LessThanOrEqualTo(7);
    }
}
