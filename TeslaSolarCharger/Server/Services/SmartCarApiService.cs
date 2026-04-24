using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class SmartCarApiService : ISmartCarApiService
{
    private readonly ILogger<SmartCarApiService> _logger;
    private readonly ITokenHelper _tokenHelper;
    private readonly ITeslaSolarChargerContext _teslaSolarChargerContext;

    public SmartCarApiService(ILogger<SmartCarApiService> logger,
        ITokenHelper tokenHelper,
        ITeslaSolarChargerContext teslaSolarChargerContext)
    {
        _logger = logger;
        _tokenHelper = tokenHelper;
        _teslaSolarChargerContext = teslaSolarChargerContext;
    }

    public async Task UpdateSmartCarCarTypes()
    {
        _logger.LogTrace("{method}()", nameof(UpdateSmartCarCarTypes));
        try
        {
            var tokens = await _tokenHelper.GetSmartCarTokenStates(true).ConfigureAwait(false);
            var vins = tokens.SelectMany(t => t.Vins).ToHashSet();
            _logger.LogTrace("Found {count} smartcar tokens with VINs: {vins}", tokens.Count, vins);
            var dbCars = await _teslaSolarChargerContext.Cars.ToListAsync().ConfigureAwait(false);
            foreach (var vin in vins)
            {
                var dbCar = dbCars.FirstOrDefault(c => c.Vin == vin);
                if (dbCar == default)
                {
                    _logger.LogWarning("Could not find car with VIN {vin} in database", vin);
                    continue;
                }
                if (dbCar.CarType != CarType.SmartCar)
                {
                    dbCar.CarType = CarType.SmartCar;
                }
            }
            foreach (var smartCarCar in dbCars.Where(c => c.CarType == CarType.SmartCar))
            {
                if (string.IsNullOrEmpty(smartCarCar.Vin) || !vins.Contains(smartCarCar.Vin))
                {
                    smartCarCar.CarType = CarType.Manual;
                }
            }
            await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not get SmartCar token states");
        }
        
    }
}
