using System.Net;

namespace TeslaSolarCharger.Server.Contracts;

public interface ITelegramService
{
    Task<HttpStatusCode> SendMessage(string message);
}