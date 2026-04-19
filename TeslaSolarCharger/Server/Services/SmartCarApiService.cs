using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Dtos.Solar4CarBackend;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class SmartCarApiService : ISmartCarApiService
{
    private readonly ILogger<SmartCarApiService> _logger;
    private readonly IBackendApiService _backendApiService;
    private readonly ITokenHelper _tokenHelper;
    private readonly IConstants _constants;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ITscConfigurationService _tscConfigurationService;
    private readonly ITeslaSolarChargerContext _teslaSolarChargerContext;
    private readonly IMemoryCache _memoryCache;

    public SmartCarApiService(ILogger<SmartCarApiService> logger, IBackendApiService backendApiService,
        ITokenHelper tokenHelper, IConstants constants, IDateTimeProvider dateTimeProvider, ITscConfigurationService tscConfigurationService,
        ITeslaSolarChargerContext teslaSolarChargerContext, IMemoryCache memoryCache)
    {
        _logger = logger;
        _backendApiService = backendApiService;
        _tokenHelper = tokenHelper;
        _constants = constants;
        _dateTimeProvider = dateTimeProvider;
        _tscConfigurationService = tscConfigurationService;
        _teslaSolarChargerContext = teslaSolarChargerContext;
        _memoryCache = memoryCache;
    }

    public async Task RefreshTokensIfRequired()
    {
        _logger.LogTrace("{method}()", nameof(RefreshTokensIfRequired));

        List<DtoSmartCarTokenState> tokens;
        try
        {
            tokens = await _tokenHelper.GetSmartCarTokenStates(true).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not get SmartCar token states");
            return;
        }

        var currentDate = _dateTimeProvider.DateTimeOffSetUtcNow();
        foreach (var dtoSmartCarTokenState in tokens)
        {
            if (dtoSmartCarTokenState.ExpiresAt < currentDate.AddSeconds(_constants.TokenRefreshIntervalSeconds * 2))
            {
                try
                {
                    await RefreshToken(dtoSmartCarTokenState);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not refresh SmartCar token with id {id}", dtoSmartCarTokenState.Id);
                }
            }
        }
    }

    private async Task RefreshToken(DtoSmartCarTokenState dtoSmartCarTokenState)
    {
        _logger.LogTrace("{method}({@token})", nameof(RefreshToken), dtoSmartCarTokenState);
        var decryptionKey = await _tscConfigurationService.GetConfigurationValueByKey(_constants.TeslaTokenEncryptionKeyKey);
        if (string.IsNullOrEmpty(decryptionKey))
        {
            _logger.LogError("Decryption key not found do not send command");
            throw new InvalidOperationException("No Decryption key found.");
        }
        var token = await _teslaSolarChargerContext.BackendTokens.SingleOrDefaultAsync().ConfigureAwait(false);
        if (token == default)
        {
            throw new InvalidOperationException("Can not refresh smartcar token without backend token");
        }
        var result = await _backendApiService.SendRequestToBackend<object>(HttpMethod.Post, token.AccessToken,
            $"SmartCarRequests/RefreshToken?tokenId={dtoSmartCarTokenState.Id}&encryptionKey={Uri.EscapeDataString(decryptionKey)}", null);
        if (result.HasError)
        {
            throw new InvalidOperationException($"Could not refresh smartcar token: {result.ErrorMessage}");
        }
        _memoryCache.Remove(_constants.SmartCarTokenStatesKey);
    }
}
