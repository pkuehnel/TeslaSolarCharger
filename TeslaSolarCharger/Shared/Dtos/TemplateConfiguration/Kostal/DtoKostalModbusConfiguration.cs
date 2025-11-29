using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Generic;

namespace TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Kostal;

public class DtoKostalModbusConfiguration : DtoModbusConfigurationBase
{
    public DtoKostalModbusConfiguration()
    {
        Port = 1502;
        UnitId = 71;
    }
}
