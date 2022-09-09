namespace TeslaSolarCharger.Server.Contracts;

public interface IPvValueService
{
    Task UpdatePvValues();
    int GetAveragedOverage();
    int? GetIntegerValueByString(string valueString, string? jsonPattern, string? xmlPattern, double correctionFactor);
}