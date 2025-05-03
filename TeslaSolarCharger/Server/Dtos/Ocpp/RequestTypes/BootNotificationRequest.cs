namespace TeslaSolarCharger.Server.Dtos.Ocpp.RequestTypes;

public sealed record BootNotificationRequest(
    string ChargePointVendor,
    string ChargePointModel,
    string? FirmwareVersion = null) : IOcppMessage
{
    public string Action => "BootNotification";
}

public sealed record BootNotificationResponse(
    DateTime CurrentTime,
    int Interval,
    string Status);
