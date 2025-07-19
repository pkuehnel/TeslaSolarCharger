using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Helper.Contracts;

namespace TeslaSolarCharger.Shared.Helper;

public class EntityKeyGenerationHelper : IEntityKeyGenerationHelper
{
    private const string LoadPointEntityKeyDelimiter = "_";
    private const string DataKeyDelimiter = ":";
    private const string NullPlaceholder = "null";

    public string GetDataKey(string dataType, string? entityId)
    {
        return string.IsNullOrEmpty(entityId) ? dataType : $"{dataType}{DataKeyDelimiter}{entityId}";
    }

    public (string dataType, string? entityId) GetDataKeyParts(string dataKey)
    {
        if (string.IsNullOrWhiteSpace(dataKey))
        {
            throw new ArgumentException("Data key cannot be null or empty", nameof(dataKey));
        }
        var parts = dataKey.Split(DataKeyDelimiter);
        if (parts.Length == 1)
        {
            return (parts[0], null);
        }
        return (parts[0], parts[1]);
    }

    public string GetLoadPointEntityKey(int? carId, int? connectorId)
    {
        var carIdStr = carId?.ToString() ?? NullPlaceholder;
        var connectorIdStr = connectorId?.ToString() ?? NullPlaceholder;
        return $"{carIdStr}{LoadPointEntityKeyDelimiter}{connectorIdStr}";
    }

    public DtoLoadpointCombination GetCombinationByKey(string entityKey)
    {
        if (string.IsNullOrWhiteSpace(entityKey))
        {
            throw new ArgumentException("Entity key cannot be null or empty", nameof(entityKey));
        }

        var results = entityKey.Split(LoadPointEntityKeyDelimiter);
        if (results.Length != 2)
        {
            throw new ArgumentException($"Invalid entity key format: {entityKey}", nameof(entityKey));
        }

        var carId = ParseNullableInt(results[0], entityKey);
        var connectorId = ParseNullableInt(results[1], entityKey);

        return new DtoLoadpointCombination(carId, connectorId);
    }

    private int? ParseNullableInt(string value, string entityKey)
    {
        if (value == NullPlaceholder)
        {
            return null;
        }

        if (int.TryParse(value, out var result))
        {
            return result;
        }

        throw new ArgumentException($"Invalid entity key format: {entityKey} - could not parse '{value}' as integer");
    }
}
