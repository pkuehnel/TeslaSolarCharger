namespace TeslaSolarCharger.Server.Dtos.Ocpp.RequestTypes;

public sealed record HeartbeatRequest() : IOcppMessage
{
    public string Action => "Heartbeat";
}

public sealed record HeartbeatResponse(DateTime CurrentTime);
