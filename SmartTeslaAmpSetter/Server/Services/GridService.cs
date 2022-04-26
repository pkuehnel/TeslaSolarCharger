using System.Globalization;
using Newtonsoft.Json.Linq;
using SmartTeslaAmpSetter.Server.Contracts;

namespace SmartTeslaAmpSetter.Server.Services;

public class GridService : IGridService
{
    private readonly ILogger<GridService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ITelegramService _telegramService;

    public GridService(ILogger<GridService> logger, IConfiguration configuration, ITelegramService telegramService)
    {
        _logger = logger;
        _configuration = configuration;
        _telegramService = telegramService;
    }

    public async Task<int> GetCurrentOverage()
    {
        _logger.LogTrace("{method}()", nameof(GetCurrentOverage));
        using var httpClient = new HttpClient();
        var requestUri = _configuration.GetValue<string>("CurrentPowerToGridUrl");
        _logger.LogDebug("Using {uri} to get current overage.", requestUri);
        var response = await httpClient.GetAsync(
                requestUri)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Could not get current overage. {statusCode}, {reasonPhrase}", response.StatusCode, response.ReasonPhrase);
            response.EnsureSuccessStatusCode();
        }

        var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        var jsonPattern = _configuration.GetValue<string>("CurrentPowerToGridJsonPattern");

        if (jsonPattern != null)
        {
            _logger.LogDebug("Extract overage value from {result} with {jsonPattern}", result, jsonPattern);
            result = (JObject.Parse(result).SelectToken(jsonPattern) ??
                      throw new InvalidOperationException("Extracted Json Value is null")).Value<string>();
        }

        try
        {
            var overage = GetIntegerFromString(result);
            if (_configuration.GetValue<bool>("CurrentPowerToGridInvertValue"))
            {
                overage = -overage;
            }
            return overage ;
        }
        catch (Exception)
        {
            throw new InvalidCastException($"Could not parse result {result} from uri {requestUri} to integer");
        }

    }

    internal int GetIntegerFromString(string? inputString)
    {
        _logger.LogTrace("{method}({param})", nameof(GetIntegerFromString), inputString);
        return (int) double.Parse(inputString ?? throw new ArgumentNullException(nameof(inputString)), CultureInfo.InvariantCulture);
    }

    public async Task<int?> GetCurrentInverterPower()
    {
        _logger.LogTrace("{method}()", nameof(GetCurrentInverterPower));
        using var httpClient = new HttpClient();
        var requestUri = _configuration.GetValue<string>("CurrentInverterPowerUrl");
        if (requestUri == null)
        {
            return null;
        }
        var response = await httpClient.GetAsync(
                requestUri)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Getting current inverter power did result in statuscode {statusCode} with reason {reasonPhrase}", response.StatusCode, response.ReasonPhrase);
            await _telegramService.SendMessage(
                $"Getting current inverter power did result in statuscode {response.StatusCode} with reason {response.ReasonPhrase}");
            return null;
        }
        var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        try
        {
            return GetIntegerFromString(result);
        }
        catch (Exception)
        {
            throw new InvalidCastException($"Could not parse result {result} from uri {requestUri} to integer");
        }
    }
}