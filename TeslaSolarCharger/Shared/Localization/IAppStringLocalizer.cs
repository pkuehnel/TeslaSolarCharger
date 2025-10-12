namespace TeslaSolarCharger.Shared.Localization;

public interface IAppStringLocalizer
{
    string this[string key] { get; }
    string this[string key, params object[] arguments] { get; }
    string this[LocalizationKey key] { get; }
    string this[LocalizationKey key, params object[] arguments] { get; }
    bool TryGetValue(string key, out string value);
    bool TryGetValue(LocalizationKey key, out string value);
}
