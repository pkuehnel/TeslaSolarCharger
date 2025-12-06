using TeslaSolarCharger.Server.Services.SolarValueGathering.Contracts;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering;

public class ResultValueCalculationService (ILogger<ResultValueCalculationService> logger) : IResultValueCalculationService
{
    public decimal MakeCalculationsOnRawValue(decimal correctionFactor, ValueOperator valueOperator, decimal rawValue)
    {
        rawValue = correctionFactor * rawValue;
        switch (valueOperator)
        {
            case ValueOperator.Plus:
                return rawValue;
            case ValueOperator.Minus:
                return -rawValue;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}
