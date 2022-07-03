using System.Net;
using SmartTeslaAmpSetter.Server.Contracts;
using SmartTeslaAmpSetter.Shared.Contracts;

namespace SmartTeslaAmpSetter.Server.Services;

public class TelegramService : ITelegramService
{
    private readonly ILogger<TelegramService> _logger;
    private readonly IConfigurationWrapper _configurationWrapper;

    public TelegramService(ILogger<TelegramService> logger, IConfigurationWrapper configurationWrapper)
    {
        _logger = logger;
        _configurationWrapper = configurationWrapper;
    }

    public async Task<HttpStatusCode> SendMessage(string message)
    {
        _logger.LogTrace("{method}({param})", nameof(SendMessage), message);
        using var httpClient = new HttpClient();
        var botKey = _configurationWrapper.TelegramBotKey();
        var channel = _configurationWrapper.TelegramChannelId();
        if (botKey == null)
        {
            _logger.LogInformation("Can not send Telegram Message because botkey is null.");
            return HttpStatusCode.Unauthorized;
        }

        if (channel == null)
        {
            _logger.LogInformation("Can not send Telegram Message because channel is null.");
            return HttpStatusCode.Unauthorized;
        }

        var requestUri = CreateRequestUri(message, botKey, channel);

        httpClient.Timeout = TimeSpan.FromSeconds(1);

        var response = await httpClient.GetAsync(
            requestUri);

        response.EnsureSuccessStatusCode();
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Can not send Telegram message: {statusCode}, {reasonphrase}, {body}", response.StatusCode, response.ReasonPhrase, await response.Content.ReadAsStringAsync());
        }

        return response.StatusCode;
    }

    internal string CreateRequestUri(string message, string botKey, string channel)
    {
        var requestUri = $"https://api.telegram.org/bot{botKey}/sendMessage?chat_id={channel}&text={message}";
        return requestUri;
    }
}