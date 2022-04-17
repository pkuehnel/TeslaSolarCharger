using System.Net;

namespace SmartTeslaAmpSetter.Server.Contracts;

public interface ITelegramService
{
    Task<HttpStatusCode> SendMessage(string message);
}