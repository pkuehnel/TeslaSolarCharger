using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Contracts;

public interface IPvValueService
{
    Task UpdatePvValues();
    int GetAveragedOverage();
    int? GetIntegerValueByString(string valueString, string? jsonPattern, string? xmlPattern, double correctionFactor, NodePatternType nodePatternType);
    void AddOverageValueToInMemoryList(int overage);
}
