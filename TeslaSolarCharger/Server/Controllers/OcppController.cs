using Microsoft.AspNetCore.Mvc;
using System.Net;
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
        logger.LogTrace("{method}({chargePointId})", nameof(Get), chargePointId);
        if (!HttpContext.WebSockets.IsWebSocketRequest)
            throw new ProtocolViolationException("WebSocket required");

        using var ws = await HttpContext.WebSockets.AcceptWebSocketAsync();

        var lifetime = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        await ocppWebSocketConnectionHandlingService.AddWebSocket(chargePointId, ws, lifetime, HttpContext.RequestAborted);

        // keep HTTP request open until the socket (or the client) drops
        await lifetime.Task;
    }
}
