﻿using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Contracts;

public interface IPvValueService
{
    Task UpdatePvValues();
    int GetAveragedOverage();
    void AddOverageValueToInMemoryList(int overage);

    int? GetIntegerValueByString(string valueString, string? jsonPattern, string? xmlPattern, double correctionFactor,
        NodePatternType nodePatternType, string? xmlAttributeHeaderName, string? xmlAttributeHeaderValue, string? xmlAttributeValueName);

    void ClearOverageValues();
}
