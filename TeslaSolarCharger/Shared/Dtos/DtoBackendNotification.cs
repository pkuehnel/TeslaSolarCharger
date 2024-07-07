using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos;

public class DtoBackendNotification
{
    public int Id { get; set; }
    public BackendNotificationType Type { get; set; }
    public string Headline { get; set; }
    public string DetailText { get; set; }
    public DateTime? ValidFromDate { get; set; }
    public DateTime? ValidToDate { get; set; }
    public string? ValidFromVersion { get; set; }
    public string? ValidToVersion { get; set; }
}
