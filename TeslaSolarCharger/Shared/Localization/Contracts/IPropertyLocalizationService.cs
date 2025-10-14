using System.Globalization;
using System.Linq.Expressions;

namespace TeslaSolarCharger.Shared.Localization.Contracts;

public interface IPropertyLocalizationService
{
    PropertyLocalization? Get(Type type, string propertyName, CultureInfo? culture = null);
    PropertyLocalization? Get<T>(Expression<Func<T, object?>> propertyExpression, CultureInfo? culture = null);
    string? GetDisplayName(Type type, string propertyName, CultureInfo? culture = null);
    string? GetHelperText(Type type, string propertyName, CultureInfo? culture = null);
    string? GetDisplayName<T>(Expression<Func<T, object?>> propertyExpression, CultureInfo? culture = null);
    string? GetHelperText<T>(Expression<Func<T, object?>> propertyExpression, CultureInfo? culture = null);
}
