using FluentValidation;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Generic;

namespace TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Sma;

public class DtoSmaInverterTemplateValueConfiguration : DtoModbusConfigurationBase
{
    public DtoSmaInverterTemplateValueConfiguration()
    {
        Port = 502;
        UnitId = 3;
    }
}

//public class DtoSmaInverterTemplateValueConfigurationValidator : AbstractValidator<DtoSmaInverterTemplateValueConfiguration>
//{
//    public DtoSmaInverterTemplateValueConfigurationValidator()
//    {
//        RuleFor(x => x.Host).NotEmpty();
//        RuleFor(x => x.Port).NotEmpty();
//        RuleFor(x => x.UnitId).NotEmpty();
//        RuleFor(x => x.Port).GreaterThanOrEqualTo(0);
//        RuleFor(x => x.Port).LessThanOrEqualTo(65535);
//    }
//}
