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

public class DtoSmaInverterTemplateValueConfigurationValidator : DtoModbusConfigurationBaseValidator<DtoSmaInverterTemplateValueConfiguration>
{
}
