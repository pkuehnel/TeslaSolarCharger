using TeslaSolarCharger.Shared.Dtos;

namespace TeslaSolarCharger.Client.Helper;

public static class SmartCarCompatibleVehicleSearch
{
    /// <summary>
    /// The vehicles whose "make model" contains <paramref name="query"/>, sorted by make and model.
    /// An empty query matches all of them; there is no result limit.
    /// </summary>
    public static IEnumerable<DtoSmartCarCompatibleVehicle> Search(IEnumerable<DtoSmartCarCompatibleVehicle> vehicles, string? query)
    {
        var result = vehicles;
        if (!string.IsNullOrWhiteSpace(query))
        {
            var trimmedQuery = query.Trim();
            result = result.Where(v => $"{v.Make} {v.Model}".Contains(trimmedQuery, StringComparison.OrdinalIgnoreCase));
        }
        return result.OrderBy(v => v.Make).ThenBy(v => v.Model);
    }
}
