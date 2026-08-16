namespace TeslaSolarCharger.Client.Helper;

/// <summary>
/// Deep links into the README sections that explain a setting, used by the info buttons next to the inputs.
/// </summary>
public static class ReadmeLinks
{
    private const string ReadmeBaseUrl = "https://github.com/pkuehnel/TeslaSolarCharger?tab=readme-ov-file#";

    public static string SolarValueSources => $"{ReadmeBaseUrl}setting-up-solar-power-values";
    public static string RestValues => $"{ReadmeBaseUrl}rest-values";
    public static string ModbusValues => $"{ReadmeBaseUrl}modbus-values";
    public static string JsonPath => $"{ReadmeBaseUrl}json-path";
    public static string XmlPath => $"{ReadmeBaseUrl}xml-path";
    public static string CorrectionFactors => $"{ReadmeBaseUrl}correction-factors";
}
