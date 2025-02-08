using Newtonsoft.Json;

namespace TeslaSolarCharger.Server.Dtos.FleetTelemetry;

public class DtoGetFleetTelemetryConfiguration
{
    [JsonProperty("synced")]
    public bool Synced { get; set; }
    [JsonProperty("config")]
    public Config? Config { get; set; }
}

public class Config(string hostname, string ca, long exp, int port, bool preferTyped)
{
    [JsonProperty("hostname")]
    public string Hostname { get; set; } = hostname;

    [JsonProperty("ca")]
    public string Ca { get; set; } = ca;

    [JsonProperty("exp")]
    public long Exp { get; set; } = exp;

    [JsonProperty("fields")]
    public Dictionary<string, IntervalSetting> Fields { get; set; } = new();
    [JsonProperty("alert_types")]
    public List<string> AlertTypes { get; set; } = new();
    [JsonProperty("port")]
    public int Port { get; set; } = port;
    [JsonProperty("prefer_typed")]
    public bool PreferTyped { get; set; } = preferTyped;
}

public class IntervalSetting
{
    [JsonConstructor]
    // ReSharper disable once ConvertToPrimaryConstructor
    public IntervalSetting(int intervalSeconds, int? resendIntervalSeconds = null, int? minimumDelta = null)
    {
        IntervalSeconds = intervalSeconds;
        ResendIntervalSeconds = resendIntervalSeconds;
        MinimumDelta = minimumDelta;
    }

    public IntervalSetting(TimeSpan interval)
    {
        IntervalSeconds = (int)interval.TotalSeconds;
    }
    public IntervalSetting(TimeSpan interval, TimeSpan resendInterval)
    {
        IntervalSeconds = (int)interval.TotalSeconds;
        ResendIntervalSeconds = (int)resendInterval.TotalSeconds;
    }
    public IntervalSetting(TimeSpan interval, int minimumDelta)
    {
        IntervalSeconds = (int)interval.TotalSeconds;
        MinimumDelta = minimumDelta;
    }
    public IntervalSetting(TimeSpan interval, TimeSpan resendInterval, int minimumDelta)
    {
        IntervalSeconds = (int)interval.TotalSeconds;
        ResendIntervalSeconds = (int)resendInterval.TotalSeconds;
        MinimumDelta = minimumDelta;
    }

    [JsonProperty("interval_seconds")]
    public int IntervalSeconds { get; set; }
    [JsonProperty("resend_interval_seconds", NullValueHandling = NullValueHandling.Ignore)]
    public int? ResendIntervalSeconds { get; set; }
    [JsonProperty("minimum_delta", NullValueHandling = NullValueHandling.Ignore)]
    public int? MinimumDelta { get; set; }
}
