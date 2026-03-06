using System.Net;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Resources;

namespace TeslaSolarCharger.Server.Services;

public class TelegramService(ILogger<TelegramService> logger,
    IConfigurationWrapper configurationWrapper,
    IHttpClientFactory httpClientFactory)
    : ITelegramService
{
    public async Task<HttpStatusCode> SendMessage(string message)
    {
        logger.LogTrace("{method}({param})", nameof(SendMessage), message);
        var httpClient = httpClientFactory.CreateClient(StaticConstants.HttpClientNameShortTimeout);
        var botKey = configurationWrapper.TelegramBotKey();
        var channel = configurationWrapper.TelegramChannelId();
        if (string.IsNullOrWhiteSpace(botKey))
        {
            logger.LogInformation("Can not send Telegram Message because botkey is empty.");
            return HttpStatusCode.Unauthorized;
        }

        if (string.IsNullOrWhiteSpace(channel))
        {
            logger.LogInformation("Can not send Telegram Message because channel is empty.");
            return HttpStatusCode.Unauthorized;
        }

        var requestUri = CreateRequestUri(message, botKey, channel);

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
