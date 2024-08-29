namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class LoggedError
{
    public int Id { get; set; }
    public DateTime StartTimeStamp { get; set; }
    public DateTime? EndTimeStamp { get; set; }
    public string IssueKey { get; set; }
    public string? Vin { get; set; }
    public bool TelegramNotificationSent { get; set; }
    public bool TelegramResolvedMessageSent { get; set; }
}
