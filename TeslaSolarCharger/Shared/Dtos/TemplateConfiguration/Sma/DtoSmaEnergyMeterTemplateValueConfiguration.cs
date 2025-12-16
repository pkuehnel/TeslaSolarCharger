using FluentValidation;

namespace TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Sma;

public class DtoSmaEnergyMeterTemplateValueConfiguration
{
    public uint? SerialNumber { get; set; }
}


public class DtoSmaEnergyMeterTemplateValueConfigurationValidator : AbstractValidator<DtoSmaEnergyMeterTemplateValueConfiguration>
{
    public DtoSmaEnergyMeterTemplateValueConfigurationValidator()
    {
    }
}
