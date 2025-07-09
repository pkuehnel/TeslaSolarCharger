using System.Reflection;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.SignalRClients;

namespace TeslaSolarCharger.Server.Services;

public class ChangeTrackingService : IChangeTrackingService
{
    private readonly Dictionary<string, object> _previousStates = new();
    private readonly ILogger<ChangeTrackingService> _logger;

    public ChangeTrackingService(ILogger<ChangeTrackingService> logger)
    {
        _logger = logger;
    }

    public StateUpdateDto? DetectChanges<T>(string dataType, string entityId, T currentState)
        where T : class
    {
        var key = $"{dataType}:{entityId}";
        var changedProperties = new Dictionary<string, object?>();

        if (_previousStates.TryGetValue(key, out var previousState) && previousState is T typedPreviousState)
        {
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                var previousValue = property.GetValue(typedPreviousState);
                var currentValue = property.GetValue(currentState);

                if (!Equals(previousValue, currentValue))
                {
                    changedProperties[property.Name] = currentValue;
                    _logger.LogTrace("Property {PropertyName} changed from {OldValue} to {NewValue}",
                        property.Name, previousValue, currentValue);
                }
            }
        }
        else
        {
            // First time seeing this entity, all properties are "changed"
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var property in properties)
            {
                changedProperties[property.Name] = property.GetValue(currentState);
            }
        }

        // Update stored state
        _previousStates[key] = currentState;

        if (changedProperties.Count > 0)
        {
            return new StateUpdateDto
            {
                DataType = dataType,
                EntityId = entityId,
                ChangedProperties = changedProperties,
                Timestamp = DateTimeOffset.UtcNow
            };
        }

        return null;
    }

    public void ClearState(string dataType, string entityId)
    {
        var key = $"{dataType}:{entityId}";
        _previousStates.Remove(key);
    }
}
