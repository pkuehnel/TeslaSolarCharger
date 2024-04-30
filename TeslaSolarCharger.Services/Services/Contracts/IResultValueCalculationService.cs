using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Services.Services.Contracts;

public interface IResultValueCalculationService
{
    decimal MakeCalculationsOnRawValue(decimal correctionFactor, ValueOperator valueOperator, decimal rawValue);
}
