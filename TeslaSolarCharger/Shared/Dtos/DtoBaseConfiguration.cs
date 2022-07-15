using System.ComponentModel.DataAnnotations;

namespace TeslaSolarCharger.Shared.Dtos;

public class DtoBaseConfiguration
{
    [Required]
    public string? CurrentPowerToGridUrl { get; set; }
    public string? CurrentInverterPowerUrl { get; set; }
    [Required]
    public string? TeslaMateApiBaseUrl { get; set; }
    [Required]
    [Range(30, int.MaxValue)]
    public int? UpdateIntervalSeconds { get; set; } = 30;
    [Required]
    [Range(1, int.MaxValue)]
    public int? PvValueUpdateIntervalSeconds { get; set; } = 1;
    [Required]
    public List<int> CarPriorities { get; set; } = new();
    [Required]
    public string? GeoFence { get; set; }
    [Required]
    [Range(1, int.MaxValue)]
    public int? MinutesUntilSwitchOn { get; set; } = 5;
    [Required]
    [Range(1, int.MaxValue)]
    public int? MinutesUntilSwitchOff { get; set; } = 5;
    [Required]
    public int? PowerBuffer { get; set; } = 0;
    public string? CurrentPowerToGridJsonPattern { get; set; }
    public bool? CurrentPowerToGridInvertValue { get; set; }
    public string? CurrentInverterPowerJsonPattern { get; set; }
    public string? TelegramBotKey { get; set; }
    public string? TelegramChannelId { get; set; }
    [Required]
    public string? TeslaMateDbServer { get; set; } = "database";
    [Required]
    public int? TeslaMateDbPort { get; set; } = 5432;
    [Required]
    public string? TeslaMateDbDatabaseName { get; set; } = "teslamate";
    [Required]
    public string? TeslaMateDbUser { get; set; } = "teslamate";
    [Required]
    public string? TeslaMateDbPassword { get; set; } = "secret";
    [Required]
    public string? MqqtClientId { get; set; } = "TeslaSolarCharger";
    [Required]
    public string? MosquitoServer { get; set; } = "mosquitto";
    public string? CurrentPowerToGridXmlPattern { get; set; }
    public string? CurrentPowerToGridXmlAttributeHeaderName { get; set; }
    public string? CurrentPowerToGridXmlAttributeHeaderValue { get; set; }
    public string? CurrentPowerToGridXmlAttributeValueName { get; set; }
    public string? CurrentInverterPowerXmlPattern { get; set; }
    public string? CurrentInverterPowerXmlAttributeHeaderName { get; set; }
    public string? CurrentInverterPowerXmlAttributeHeaderValue { get; set; }
    public string? CurrentInverterPowerXmlAttributeValueName { get; set; }
}