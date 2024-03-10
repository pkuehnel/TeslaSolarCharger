using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.GridPrice.Dtos;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.ChargingCost;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.SharedBackend.MappingExtensions;
using ChargingProcess = TeslaSolarCharger.Model.Entities.TeslaMate.ChargingProcess;

namespace TeslaSolarCharger.Server.Services;

public class ChargingCostService : IChargingCostService
{
    private readonly ILogger<ChargingCostService> _logger;
    private readonly ITeslaSolarChargerContext _teslaSolarChargerContext;
    private readonly ITeslamateContext _teslamateContext;
    private readonly IMapperConfigurationFactory _mapperConfigurationFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConstants _constants;
    private readonly ITscOnlyChargingCostService _tscOnlyChargingCostService;

    public ChargingCostService(ILogger<ChargingCostService> logger,
        ITeslaSolarChargerContext teslaSolarChargerContext, ITeslamateContext teslamateContext,
        IMapperConfigurationFactory mapperConfigurationFactory, IServiceProvider serviceProvider, IConstants constants,
        ITscOnlyChargingCostService tscOnlyChargingCostService)
    {
        _logger = logger;
        _teslaSolarChargerContext = teslaSolarChargerContext;
        _teslamateContext = teslamateContext;
        _mapperConfigurationFactory = mapperConfigurationFactory;
        _serviceProvider = serviceProvider;
        _constants = constants;
        _tscOnlyChargingCostService = tscOnlyChargingCostService;
    }

    public async Task ConvertToNewChargingProcessStructure()
    {
        var chargingProcessesConverted =
            await _teslaSolarChargerContext.TscConfigurations.AnyAsync(c => c.Key == _constants.HandledChargesConverted).ConfigureAwait(false);
        if (chargingProcessesConverted)
        {
            return;
        }
        var convertedChargingProcesses = await _teslaSolarChargerContext.ChargingProcesses
            .Where(c => c.ConvertedFromOldStructure)
            .ToListAsync();
        var gcCounter = 0;
        foreach (var convertedChargingProcess in convertedChargingProcesses)
        {
            using var scope = _serviceProvider.CreateScope();
            var scopedTscContext = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
            var chargingDetails = await scopedTscContext.ChargingDetails
                .Where(cd => cd.ChargingProcessId == convertedChargingProcess.Id)
                .ToListAsync().ConfigureAwait(false);
            scopedTscContext.ChargingDetails.RemoveRange(chargingDetails);
            await scopedTscContext.SaveChangesAsync().ConfigureAwait(false);
            _teslaSolarChargerContext.ChargingProcesses.Remove(convertedChargingProcess);
            await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
            if (gcCounter++ % 20 == 0)
            {
                _logger.LogInformation("Deleted {counter} converted charging processes before restarting conversion", gcCounter);
                GC.Collect();
            }
        }
        var handledCharges = await _teslaSolarChargerContext.HandledCharges
            .Include(h => h.PowerDistributions)
            .AsNoTracking()
            .ToListAsync();
        gcCounter = 0;
        foreach (var handledCharge in handledCharges)
        {
            var teslaMateChargingProcess = _teslamateContext.ChargingProcesses.FirstOrDefault(c => c.Id == handledCharge.ChargingProcessId);
            if (teslaMateChargingProcess == default)
            {
                _logger.LogWarning("Could not find charging process in TeslaMate with ID {id} for handled charge with ID {handledChargeId}", handledCharge.ChargingProcessId, handledCharge.Id);
                continue;
            }

            var newChargingProcess = new TeslaSolarCharger.Model.Entities.TeslaSolarCharger.ChargingProcess()
            {
                CarId = handledCharge.CarId,
                StartDate = teslaMateChargingProcess.StartDate,
                EndDate = teslaMateChargingProcess.EndDate,
                UsedGridEnergyKwh = handledCharge.UsedGridEnergy,
                UsedSolarEnergyKwh = handledCharge.UsedSolarEnergy,
                Cost = handledCharge.CalculatedPrice,
                ConvertedFromOldStructure = true,
            };
            var chargingDetails = handledCharge.PowerDistributions.Select(p => new ChargingDetail()
            {
                TimeStamp = p.TimeStamp,
                SolarPower = p.ChargingPower - (p.PowerFromGrid < 0 ? 0 : p.PowerFromGrid),
                GridPower = (p.PowerFromGrid < 0 ? 0 : p.PowerFromGrid),
            }).ToList();
            newChargingProcess.ChargingDetails = chargingDetails;
            try
            {
                await SaveNewChargingProcess(newChargingProcess);
                if (gcCounter++ % 20 == 0)
                {
                    _logger.LogInformation("Converted {counter} charging processes...", gcCounter);
                    GC.Collect();
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while converting handled charge with ID {handledChargeId} to new charging process structure", handledCharge.Id);
            }
        }
        _teslaSolarChargerContext.TscConfigurations.Add(new TscConfiguration()
        {
            Key = _constants.HandledChargesConverted,
            Value = "true",
        });
        await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
    }

    private async Task SaveNewChargingProcess(Model.Entities.TeslaSolarCharger.ChargingProcess newChargingProcess)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
        context.ChargingProcesses.Add(newChargingProcess);
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task UpdateChargePrice(DtoChargePrice dtoChargePrice)
    {
        _logger.LogTrace("{method}({@dtoChargePrice})",
            nameof(UpdateChargePrice), dtoChargePrice);
        ChargePrice chargePrice;
        if (dtoChargePrice.Id == null)
        {
            chargePrice = new ChargePrice();
            _teslaSolarChargerContext.ChargePrices.Add(chargePrice);
        }
        else
        {
            chargePrice = await _teslaSolarChargerContext.ChargePrices.FirstAsync(c => c.Id == dtoChargePrice.Id).ConfigureAwait(false);
        }

        //Can not be null as declared as Required in DTO
        chargePrice.GridPrice = (decimal)dtoChargePrice.GridPrice!;
        chargePrice.SolarPrice = (decimal)dtoChargePrice.SolarPrice!;
        chargePrice.ValidSince = dtoChargePrice.ValidSince;
        chargePrice.EnergyProvider = dtoChargePrice.EnergyProvider;
        chargePrice.AddSpotPriceToGridPrice = dtoChargePrice.AddSpotPriceToGridPrice;
        chargePrice.SpotPriceCorrectionFactor = (dtoChargePrice.SpotPriceSurcharge ?? 0) / 100;
        chargePrice.EnergyProviderConfiguration = dtoChargePrice.EnergyProviderConfiguration;
        await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);

        await _tscOnlyChargingCostService.UpdateChargePricesOfAllChargingProcesses().ConfigureAwait(false);
    }

    public async Task DeleteChargePriceById(int id)
    {
        var chargePrice = await _teslaSolarChargerContext.ChargePrices
            .FirstAsync(c => c.Id == id).ConfigureAwait(false);
        _teslaSolarChargerContext.ChargePrices.Remove(chargePrice);
        await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);

        await _tscOnlyChargingCostService.UpdateChargePricesOfAllChargingProcesses().ConfigureAwait(false);
    }

    public async Task<List<DtoChargePrice>> GetChargePrices()
    {
        _logger.LogTrace("{method}()", nameof(GetChargePrices));
        var mapper = _mapperConfigurationFactory.Create(cfg =>
        {
            cfg.CreateMap<ChargePrice, DtoChargePrice>()
                .ForMember(d => d.Id, opt => opt.MapFrom(c => c.Id));
        });
        var chargePrices = await _teslaSolarChargerContext.ChargePrices
            .ProjectTo<DtoChargePrice>(mapper)
            .ToListAsync().ConfigureAwait(false);
        return chargePrices.OrderBy(p => p.ValidSince).ToList();
    }

    public async Task DeleteDuplicatedHandleCharges()
    {
        var handledChargeChargingProcessIDs = await _teslaSolarChargerContext.HandledCharges
            .Select(h => h.ChargingProcessId)
            .ToListAsync().ConfigureAwait(false);

        if (handledChargeChargingProcessIDs.Count == handledChargeChargingProcessIDs.Distinct().Count())
        {
            return;
        }

        var handledCharges = await _teslaSolarChargerContext.HandledCharges
            .ToListAsync().ConfigureAwait(false);

        var duplicates = handledCharges
            .GroupBy(t => new { t.ChargingProcessId })
            .Where(t => t.Count() > 1)
            .SelectMany(x => x)
            .ToList();

        foreach (var duplicate in duplicates)
        {
            var chargeDistributions = await _teslaSolarChargerContext.PowerDistributions
                .Where(p => p.HandledChargeId == duplicate.Id)
                .ToListAsync().ConfigureAwait(false);
            _teslaSolarChargerContext.PowerDistributions.RemoveRange(chargeDistributions);
            _teslaSolarChargerContext.HandledCharges.Remove(duplicate);
        }

        await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<List<SpotPrice>> GetSpotPrices()
    {
        return await _teslaSolarChargerContext.SpotPrices.ToListAsync().ConfigureAwait(false);
    }

    public async Task<List<DtoHandledCharge>> GetHandledCharges(int carId)
    {
        var mapper = _mapperConfigurationFactory.Create(cfg =>
        {
            //ToDo: Maybe possible null exceptions as not all members that are nullable in database are also nullable in dto
            cfg.CreateMap<HandledCharge, DtoHandledCharge>()
                .ForMember(d => d.ChargingProcessId, opt => opt.MapFrom(h => h.ChargingProcessId))
                .ForMember(d => d.CalculatedPrice, opt => opt.MapFrom(h => h.CalculatedPrice))
                .ForMember(d => d.UsedGridEnergy, opt => opt.MapFrom(h => h.UsedGridEnergy))
                .ForMember(d => d.UsedSolarEnergy, opt => opt.MapFrom(h => h.UsedSolarEnergy))
                .ForMember(d => d.AverageSpotPrice, opt => opt.MapFrom(h => h.AverageSpotPrice))
                ;
        });
        var handledCharges = await _teslaSolarChargerContext.HandledCharges
            .Where(h => h.CarId == carId && h.CalculatedPrice != null)
            .ProjectTo<DtoHandledCharge>(mapper)
            .ToListAsync().ConfigureAwait(false);

        handledCharges.RemoveAll(c => (c.UsedGridEnergy + c.UsedSolarEnergy) < (decimal)0.1);

        var chargingProcesses = await _teslamateContext.ChargingProcesses
            .Where(c => handledCharges.Select(h => h.ChargingProcessId).Contains(c.Id))
            .Select(c => new { c.StartDate, ChargingProcessId = c.Id })
            .ToListAsync().ConfigureAwait(false);

        foreach (var dtoHandledCharge in handledCharges)
        {
            var chargingProcess = chargingProcesses
                .FirstOrDefault(c => c.ChargingProcessId == dtoHandledCharge.ChargingProcessId);
            dtoHandledCharge.StartTime = chargingProcess?.StartDate.ToLocalTime();
            dtoHandledCharge.PricePerKwh =
                dtoHandledCharge.CalculatedPrice / (dtoHandledCharge.UsedGridEnergy + dtoHandledCharge.UsedSolarEnergy);
        }
        return handledCharges.OrderByDescending(d => d.StartTime).ToList();
    }

    public async Task<DtoChargeSummary> GetChargeSummary(int carId)
    {
        var handledCharges = await _teslaSolarChargerContext.HandledCharges
            .Where(h => h.CalculatedPrice != null && h.CarId == carId)
            .ToListAsync().ConfigureAwait(false);

        return GetChargeSummary(handledCharges);
    }

    private DtoChargeSummary GetChargeSummary(List<HandledCharge> handledCharges)
    {
        var dtoChargeSummary = new DtoChargeSummary()
        {
            ChargeCost = handledCharges.Sum(h => h.CalculatedPrice ?? 0),
            ChargedGridEnergy = handledCharges.Sum(h => h.UsedGridEnergy ?? 0),
            ChargedSolarEnergy = handledCharges.Sum(h => h.UsedSolarEnergy ?? 0),
        };

        return dtoChargeSummary;
    }

    public async Task<Dictionary<int, DtoChargeSummary>> GetChargeSummaries()
    {
        var handledChargeGroups = (await _teslaSolarChargerContext.HandledCharges
                .Where(h => h.CalculatedPrice != null)
                .ToListAsync().ConfigureAwait(false))
            .GroupBy(h => h.CarId).ToList();

        var chargeSummaries = new Dictionary<int, DtoChargeSummary>();

        foreach (var handledChargeGroup in handledChargeGroups)
        {
            var list = handledChargeGroup.ToList();
            chargeSummaries.Add(handledChargeGroup.Key, GetChargeSummary(list));
        }

        return chargeSummaries;
    }

    public async Task<DtoChargePrice> GetChargePriceById(int id)
    {
        _logger.LogTrace("{method}({id})", nameof(GetChargePriceById), id);
        var mapper = _mapperConfigurationFactory.Create(cfg =>
        {
            cfg.CreateMap<ChargePrice, DtoChargePrice>()
                .ForMember(d => d.SpotPriceSurcharge, opt => opt.MapFrom(c => c.SpotPriceCorrectionFactor * 100))
                ;
        });
        var chargePrices = await _teslaSolarChargerContext.ChargePrices
            .Where(c => c.Id == id)
            .ProjectTo<DtoChargePrice>(mapper)
            .FirstAsync().ConfigureAwait(false);
        switch (chargePrices.EnergyProvider)
        {
            case EnergyProvider.Octopus:
                break;
            case EnergyProvider.Tibber:
                break;
            case EnergyProvider.FixedPrice:
                break;
            case EnergyProvider.Awattar:
                break;
            case EnergyProvider.Energinet:
                break;
            case EnergyProvider.HomeAssistant:
                break;
            case EnergyProvider.OldTeslaSolarChargerConfig:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }


        return chargePrices;
    }
}
