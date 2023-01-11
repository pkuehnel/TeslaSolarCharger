namespace TeslaSolarCharger.Server.Contracts;

public interface IPvValueService
{
    Task UpdatePvValues();
    int GetAveragedOverage();
    double? GetDoubleValueByString(string valueString, string? jsonPattern, string? xmlPattern, double correctionFactor);
    void AddOverageValueToInMemoryList(int overage);
}
