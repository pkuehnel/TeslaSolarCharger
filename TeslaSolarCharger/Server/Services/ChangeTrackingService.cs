using System.Collections.Concurrent;
using System.Reflection;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.SignalRClients;

namespace TeslaSolarCharger.Server.Services;

public class ChangeTrackingService : IChangeTrackingService
{
    private readonly ConcurrentDictionary<string, object> _previousStates = new();
    private readonly ILogger<ChangeTrackingService> _logger;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ChangeTrackingService(ILogger<ChangeTrackingService> logger, IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _dateTimeProvider = dateTimeProvider;
    }

    public StateUpdateDto? DetectChanges<T>(string dataType, string? entityId, T currentState)
        where T : class
    {
        _logger.LogTrace("{method}<{type}>({dataType}, {entityId}, {@currentState})", nameof(DetectChanges), typeof(T), dataType, entityId, currentState);
        var key = $"{dataType}:{entityId ?? string.Empty}";
        var changedProperties = new Dictionary<string, object?>();

        if (_previousStates.TryGetValue(key, out var previousState) && previousState is T typedPreviousState)
        {
            _logger.LogTrace("Previous state found for entity {EntityId} of type {DataType}", entityId, dataType);
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                var previousValue = property.GetValue(typedPreviousState);
                var currentValue = property.GetValue(currentState);

                if (!EqualityComparer<object>.Default.Equals(previousValue, currentValue))
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
            _logger.LogTrace("First time seeing entity {EntityId} of type {DataType}, all properties are considered changed", entityId, dataType);
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
                Timestamp = _dateTimeProvider.DateTimeOffSetUtcNow(),
            };
        }

        return null;
    }

    public void ClearState(string dataType, string entityId)
    {
        var key = $"{dataType}:{entityId}";
        _previousStates.Remove(key, out _);
    }
}
