namespace TeslaSolarCharger.Shared.Resources;

public static class StaticConstants
{
    public const string InMemoryLogDependencyInjectionKey = "InMemory";
    public const string FileLogDependencyInjectionKey = "File";
    public const string MeterValueIndexName = "IX_MeterValues_CarId_MeterValueKind_Timestamp";
    public const string LongTimeOutHttpClientName = "LongTimeOutHttpClient";
    public const string NormalTimeOutHttpClientName = "NormalTimeOutHttpClient";
    /// <summary>
    /// MQTT allows 23 chars max, but there should be a randomized part of at least 8 chars to avoid client id collisions.
    /// </summary>
    public const int MaxMqttPrefixLength = 15;
}
