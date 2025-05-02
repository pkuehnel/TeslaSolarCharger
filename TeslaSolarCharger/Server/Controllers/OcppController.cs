using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.WebSockets;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

[ApiController]
[Route("api/[controller]/{chargePointId}")]
public class OcppController(ILogger<OcppController> logger, IOcppWebSocketConnectionHandlingService ocppWebSocketConnectionHandlingService) : ApiBaseController
{
    [HttpGet]
    public async Task Get(string chargePointId)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
            throw new ProtocolViolationException("WebSocket required");

        using var ws = await HttpContext.WebSockets.AcceptWebSocketAsync();

        var lifetime = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        ocppWebSocketConnectionHandlingService.AddWebSocket(chargePointId, ws, lifetime);

        // keep HTTP request open until the socket (or the client) drops
        await lifetime.Task;
    }
}
