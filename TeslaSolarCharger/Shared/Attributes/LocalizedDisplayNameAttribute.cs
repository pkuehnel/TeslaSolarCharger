using System;
using System.ComponentModel;
using System.Reflection;
using System.Resources;

namespace TeslaSolarCharger.Shared.Attributes;

public sealed class LocalizedDisplayNameAttribute : DisplayNameAttribute
{
    private readonly Type resourceType;
    private readonly string resourceName;

    public LocalizedDisplayNameAttribute(Type resourceType, string resourceName)
        : base(resourceName)
    {
        this.resourceType = resourceType ?? throw new ArgumentNullException(nameof(resourceType));
        this.resourceName = resourceName ?? throw new ArgumentNullException(nameof(resourceName));
    }

    public override string DisplayName => LookupResourceValue(resourceType, resourceName) ?? base.DisplayName;

    private static string? LookupResourceValue(Type resourceType, string resourceName)
    {
        var property = resourceType.GetProperty(resourceName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (property?.PropertyType == typeof(string))
        {
            return property.GetValue(null, null) as string;
        }

        var resourceManagerProperty = resourceType.GetProperty("ResourceManager", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (resourceManagerProperty?.GetValue(null, null) is ResourceManager resourceManager)
        {
            return resourceManager.GetString(resourceName);
        }

        return null;
    }
}
