using SmartTeslaAmpSetter.Shared.Dtos.Settings;

namespace SmartTeslaAmpSetter.Shared.Dtos.Contracts;

public interface ISettings
{
    int? InverterPower { get; set; }
    int? Overage { get; set; }
    List<Car> Cars { get; set; }
}