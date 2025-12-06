using System.Globalization;
using System.Linq.Expressions;
using TeslaSolarCharger.Shared.Localization.Contracts;

namespace TeslaSolarCharger.Shared.Localization;

public record PropertyLocalization(string? DisplayName, string? HelperText);

public record PropertyLocalizationTranslation(string Language, string? DisplayName, string? HelperText);

public interface IPropertyLocalizationRegistry
{
    Type TargetType { get; }
    PropertyLocalization? Get(string propertyName, CultureInfo culture);
}

public abstract class PropertyLocalizationRegistry<T> : IPropertyLocalizationRegistry
{
    private readonly Dictionary<string, Dictionary<string, PropertyLocalization>> _localizations =
        new(StringComparer.Ordinal);

    protected PropertyLocalizationRegistry()
    {
        Configure();
    }

    public Type TargetType => typeof(T);

    public PropertyLocalization? Get(string propertyName, CultureInfo culture)
    {
        if (!_localizations.TryGetValue(propertyName, out var translations))
        {
            return null;
        }

        foreach (var languageKey in GetLanguageFallbacks(culture))
        {
            if (translations.TryGetValue(languageKey, out var localization))
            {
                return localization;
            }
        }

        return null;
    }

    protected abstract void Configure();

    protected void Register(Expression<Func<T, object?>> propertyExpression, params PropertyLocalizationTranslation[] translations)
    {
        var propertyName = GetPropertyName(propertyExpression);
        var propertyTranslations = _localizations.GetValueOrDefault(propertyName);
        if (propertyTranslations == null)
        {
            propertyTranslations = new Dictionary<string, PropertyLocalization>(StringComparer.OrdinalIgnoreCase);
            _localizations[propertyName] = propertyTranslations;
        }

        foreach (var translation in translations)
        {
            propertyTranslations[translation.Language] = new PropertyLocalization(translation.DisplayName, translation.HelperText);
        }
    }

    internal static string GetPropertyName(Expression<Func<T, object?>> propertyExpression)
    {
        return propertyExpression.Body switch
        {
            MemberExpression memberExpression => memberExpression.Member.Name,
            UnaryExpression { Operand: MemberExpression memberExpression } => memberExpression.Member.Name,
            _ => throw new ArgumentException("Expression must target a property", nameof(propertyExpression))
        };
    }

    private static IEnumerable<string> GetLanguageFallbacks(CultureInfo culture)
    {
        var evaluatedLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(culture.Name) && evaluatedLanguages.Add(culture.Name))
        {
            yield return culture.Name;
        }

        var twoLetterLanguage = culture.TwoLetterISOLanguageName;
        if (!string.IsNullOrWhiteSpace(twoLetterLanguage) && evaluatedLanguages.Add(twoLetterLanguage))
        {
            yield return twoLetterLanguage;
        }

        if (evaluatedLanguages.Add(LanguageCodes.English))
        {
            yield return LanguageCodes.English;
        }
    }
}

public class PropertyLocalizationService : IPropertyLocalizationService
{
    private readonly Dictionary<Type, IPropertyLocalizationRegistry> _registries;

    public PropertyLocalizationService(IEnumerable<IPropertyLocalizationRegistry> registries)
    {
        _registries = registries.ToDictionary(registry => registry.TargetType);
    }

    public PropertyLocalization? Get(Type type, string propertyName, CultureInfo? culture = null)
    {
        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        culture ??= CultureInfo.CurrentUICulture;
        var currentType = type;
        while (currentType != null)
        {
            if (_registries.TryGetValue(currentType, out var registry))
            {
                var metadata = registry.Get(propertyName, culture);
                if (metadata != null)
                {
                    return metadata;
                }
            }

            currentType = currentType.BaseType;
        }

        return null;
    }

    public PropertyLocalization? Get<T>(Expression<Func<T, object?>> propertyExpression, CultureInfo? culture = null)
    {
        if (propertyExpression == null)
        {
            throw new ArgumentNullException(nameof(propertyExpression));
        }

        var propertyName = PropertyLocalizationRegistry<T>.GetPropertyName(propertyExpression);
        return Get(typeof(T), propertyName, culture);
    }

    public string? GetDisplayName(Type type, string propertyName, CultureInfo? culture = null) =>
        Get(type, propertyName, culture)?.DisplayName;

    public string? GetHelperText(Type type, string propertyName, CultureInfo? culture = null) =>
        Get(type, propertyName, culture)?.HelperText;

    public string? GetDisplayName<T>(Expression<Func<T, object?>> propertyExpression, CultureInfo? culture = null) =>
        Get(propertyExpression, culture)?.DisplayName;

    public string? GetHelperText<T>(Expression<Func<T, object?>> propertyExpression, CultureInfo? culture = null) =>
        Get(propertyExpression, culture)?.HelperText;
}
