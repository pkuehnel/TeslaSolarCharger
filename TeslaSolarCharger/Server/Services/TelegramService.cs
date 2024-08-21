using System.Net;
using Quartz.Util;
using System.Diagnostics;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Shared.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class TelegramService(ILogger<TelegramService> logger,
    IConfigurationWrapper configurationWrapper)
    : ITelegramService
{
    public async Task<HttpStatusCode> SendMessage(string message)
    {
        logger.LogTrace("{method}({param})", nameof(SendMessage), message);
        using var httpClient = new HttpClient();
        var botKey = configurationWrapper.TelegramBotKey();
        var channel = configurationWrapper.TelegramChannelId();
        if (botKey.IsNullOrWhiteSpace())
        {
            logger.LogInformation("Can not send Telegram Message because botkey is empty.");
            return HttpStatusCode.Unauthorized;
        }

        if (channel.IsNullOrWhiteSpace())
        {
            logger.LogInformation("Can not send Telegram Message because channel is empty.");
            return HttpStatusCode.Unauthorized;
        }

        Debug.Assert(botKey != null, nameof(botKey) + " != null");
        Debug.Assert(channel != null, nameof(channel) + " != null");
        var requestUri = CreateRequestUri(message, botKey, channel);

        httpClient.Timeout = TimeSpan.FromSeconds(1);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(
                requestUri).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not send Telegram message.");
            return HttpStatusCode.GatewayTimeout;
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Can not send Telegram message: {statusCode}, {reasonphrase}, {body}", response.StatusCode, response.ReasonPhrase, await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        }

        return response.StatusCode;
    }

    internal string CreateRequestUri(string message, string botKey, string channel)
    {
        var requestUri = $"https://api.telegram.org/bot{botKey}/sendMessage?chat_id={channel}&text={message}";
        return requestUri;
    }
}
