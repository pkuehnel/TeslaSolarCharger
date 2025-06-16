using TeslaSolarCharger.Shared.Dtos;

namespace TeslaSolarCharger.Shared.Helper.Contracts;

public interface IValidFromToHelper
{
    Dictionary<DateTimeOffset, decimal> GetHourlyAverages<T>(
        IEnumerable<T> entries,
        DateTimeOffset from,
        DateTimeOffset to,
        Func<T, decimal> valueSelector
    ) where T : ValidFromToBase;
}
