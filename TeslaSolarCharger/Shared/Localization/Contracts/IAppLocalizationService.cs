namespace TeslaSolarCharger.Shared.Localization.Contracts;

public interface IAppLocalizationService
{
    string this[string key] { get; }

    string GetString(string key, string? defaultValue = null);

    string GetString(string key, string? defaultValue, params object[] arguments);
}
