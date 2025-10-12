using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Resources;
using Microsoft.Extensions.Logging;

namespace TeslaSolarCharger.Shared.Localization;

public sealed class AppStringLocalizer : IAppStringLocalizer
{
    private const string ResourceBaseName = "TeslaSolarCharger.Shared.Localization.AppStrings";
    private readonly ILogger<AppStringLocalizer> _logger;
    private readonly ResourceManager _resourceManager;
    private readonly HashSet<string> _availableKeys;
    private readonly HashSet<string> _missingKeys = new(StringComparer.OrdinalIgnoreCase);

    public AppStringLocalizer(ILogger<AppStringLocalizer> logger)
    {
        _logger = logger;
        _resourceManager = new ResourceManager(ResourceBaseName, typeof(AppStringLocalizer).Assembly);
        _availableKeys = LoadAvailableKeys();
    }

    public string this[string key] => GetString(key);

    public string this[string key, params object[] arguments] => FormatString(key, arguments);

    public string this[LocalizationKey key] => GetString(key.Value);

    public string this[LocalizationKey key, params object[] arguments] => FormatString(key.Value, arguments);

    public bool TryGetValue(string key, out string value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            value = string.Empty;
            return false;
        }

        if (!_availableKeys.Contains(key))
        {
            value = string.Empty;
            return false;
        }

        value = GetString(key);
        return true;
    }

    public bool TryGetValue(LocalizationKey key, out string value) => TryGetValue(key.Value, out value);

    private HashSet<string> LoadAvailableKeys()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var resourceSet = _resourceManager.GetResourceSet(CultureInfo.InvariantCulture, true, true);
            if (resourceSet != null)
            {
                foreach (DictionaryEntry entry in resourceSet)
                {
                    if (entry.Key is string key)
                    {
                        result.Add(key);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load localization resources");
        }

        return result;
    }

    private string GetString(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        var value = _resourceManager.GetString(key, CultureInfo.CurrentUICulture)
                    ?? _resourceManager.GetString(key, CultureInfo.InvariantCulture);

        if (value == null)
        {
            if (_missingKeys.Add(key))
            {
                _logger.LogWarning("Missing localization string for key '{key}' in culture '{culture}'", key, CultureInfo.CurrentUICulture.Name);
            }

            return key;
        }

        return value;
    }

    private string FormatString(string key, params object[] arguments)
    {
        var format = GetString(key);
        if (arguments == null || arguments.Length == 0)
        {
            return format;
        }

        try
        {
            return string.Format(CultureInfo.CurrentCulture, format, arguments);
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "Failed to format localized string for key '{key}'", key);
            return format;
        }
    }
}
