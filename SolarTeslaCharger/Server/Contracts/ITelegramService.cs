using System.Net;

namespace SolarTeslaCharger.Server.Contracts;

public interface ITelegramService
{
    Task<HttpStatusCode> SendMessage(string message);
}