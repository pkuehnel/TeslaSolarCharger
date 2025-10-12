using System;
using System.Reflection;
using System.Resources;

namespace TeslaSolarCharger.Shared.Attributes;

public class HelperTextAttribute : Attribute
{
    private readonly string? helperText;
    private readonly Type? resourceType;
    private readonly string? resourceName;

    public HelperTextAttribute()
    {
        helperText = string.Empty;
    }

    public HelperTextAttribute(string helperText)
    {
        this.helperText = helperText;
    }

    public HelperTextAttribute(Type resourceType, string resourceName)
    {
        this.resourceType = resourceType ?? throw new ArgumentNullException(nameof(resourceType));
        this.resourceName = resourceName ?? throw new ArgumentNullException(nameof(resourceName));
    }

    public string HelperText => resourceType == null
        ? helperText ?? string.Empty
        : LookupResourceValue(resourceType, resourceName!);

    private static string LookupResourceValue(Type resourceType, string resourceKey)
    {
        var property = resourceType.GetProperty(resourceKey, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (property?.PropertyType == typeof(string) && property.GetValue(null, null) is string propertyValue)
        {
            return propertyValue;
        }

        var resourceManagerProperty = resourceType.GetProperty("ResourceManager", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (resourceManagerProperty?.GetValue(null, null) is ResourceManager resourceManager)
        {
            var resourceValue = resourceManager.GetString(resourceKey);
            if (resourceValue != null)
            {
                return resourceValue;
            }
        }

        return resourceKey;
    }
}
