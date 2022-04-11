﻿using System.Net;
using SmartTeslaAmpSetter.Server.Contracts;

namespace SmartTeslaAmpSetter.Server.Services;

public class TelegramService : ITelegramService
{
    private readonly ILogger<TelegramService> _logger;
    private readonly IConfiguration _configuration;

    public TelegramService(ILogger<TelegramService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<HttpStatusCode> SendMessage(string message)
    {
        _logger.LogTrace("{method}({param})", nameof(SendMessage), message);
        using var httpClient = new HttpClient();
        var botKey = _configuration.GetValue<string>("TelegramBotKey");
        var channel = _configuration.GetValue<string>("TelegramChannelId");
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

        var response = await httpClient.GetAsync(
            $"https://api.telegram.org/bot{botKey}/sendMessage?chat_id={channel}&text={message}");

        response.EnsureSuccessStatusCode();
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Can not send Telegram message: {statusCode}, {reasonphrase}, {body}", response.StatusCode, response.ReasonPhrase, await response.Content.ReadAsStringAsync());
        }

        return response.StatusCode;
    }
}