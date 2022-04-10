namespace SmartTeslaAmpSetter.Shared.Dtos.Settings;

public interface ISettings
{
    int? InverterPower { get; set; }
    int Overage { get; set; }
    List<Car> Cars { get; set; }
}