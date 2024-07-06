using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Shared.Dtos.ChargingCost;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.SharedBackend.MappingExtensions;

namespace TeslaSolarCharger.Server.Services;

public class ChargingCostService(
    ILogger<ChargingCostService> logger,
    ITeslaSolarChargerContext teslaSolarChargerContext,
    ITeslamateContext teslamateContext,
    IMapperConfigurationFactory mapperConfigurationFactory,
    IServiceProvider serviceProvider,
    IConstants constants,
    ITscOnlyChargingCostService tscOnlyChargingCostService,
    ISettings settings)
    : IChargingCostService
{
    public async Task ConvertToNewChargingProcessStructure()
    {
        var chargingProcessesConverted =
            await teslaSolarChargerContext.TscConfigurations.AnyAsync(c => c.Key == constants.HandledChargesConverted).ConfigureAwait(false);
        if (chargingProcessesConverted)
        {
            return;
        }
        var convertedChargingProcesses = await teslaSolarChargerContext.ChargingProcesses
            .Where(c => c.OldHandledChargeId != null)
            .ToListAsync();
        var gcCounter = 0;
        foreach (var convertedChargingProcess in convertedChargingProcesses)
        {
            using var scope = serviceProvider.CreateScope();
            var scopedTscContext = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
            var chargingDetails = await scopedTscContext.ChargingDetails
                .Where(cd => cd.ChargingProcessId == convertedChargingProcess.Id)
                .ToListAsync().ConfigureAwait(false);
            scopedTscContext.ChargingDetails.RemoveRange(chargingDetails);
            await scopedTscContext.SaveChangesAsync().ConfigureAwait(false);
            teslaSolarChargerContext.ChargingProcesses.Remove(convertedChargingProcess);
            await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
            if (gcCounter++ % 20 == 0)
            {
                logger.LogInformation("Deleted {counter} converted charging processes before restarting conversion", gcCounter);
                GC.Collect();
            }
        }
        var handledCharges = await teslaSolarChargerContext.HandledCharges
            .Include(h => h.PowerDistributions)
            .AsNoTracking()
            .ToListAsync();
        gcCounter = 0;
        foreach (var handledCharge in handledCharges)
        {
            if (handledCharge.CalculatedPrice == null || handledCharge.UsedGridEnergy == null || handledCharge.UsedSolarEnergy == null)
            {
                logger.LogWarning("Handled charge with ID {handledChargeId} has missing data and will not be converted", handledCharge.Id);
                continue;
            }

            var newChargingProcess = new ChargingProcess()
            {
                CarId = handledCharge.CarId,
                UsedGridEnergyKwh = handledCharge.UsedGridEnergy,
                UsedSolarEnergyKwh = handledCharge.UsedSolarEnergy,
                Cost = handledCharge.CalculatedPrice,
                OldHandledChargeId = handledCharge.Id,
            };
            var chargingDetails = handledCharge.PowerDistributions.Select(p => new ChargingDetail()
            {
                TimeStamp = p.TimeStamp,
                SolarPower = p.ChargingPower - (p.PowerFromGrid < 0 ? 0 : p.PowerFromGrid),
                GridPower = (p.PowerFromGrid < 0 ? 0 : p.PowerFromGrid),
            })
                .OrderBy(c => c.TimeStamp)
                .ToList();
            newChargingProcess.StartDate = chargingDetails.First().TimeStamp;
            newChargingProcess.EndDate = chargingDetails.Last().TimeStamp;
            newChargingProcess.ChargingDetails = chargingDetails;
            try
            {
                await SaveNewChargingProcess(newChargingProcess);
                if (gcCounter++ % 20 == 0)
                {
                    logger.LogInformation("Converted {counter} charging processes...", gcCounter);
                    GC.Collect();
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error while converting handled charge with ID {handledChargeId} to new charging process structure", handledCharge.Id);
            }
        }
        teslaSolarChargerContext.TscConfigurations.Add(new TscConfiguration()
        {
            Key = constants.HandledChargesConverted,
            Value = "true",
        });
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
    }

    private async Task SaveNewChargingProcess(ChargingProcess newChargingProcess)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
        context.ChargingProcesses.Add(newChargingProcess);
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task UpdateChargePrice(DtoChargePrice dtoChargePrice)
    {
        logger.LogTrace("{method}({@dtoChargePrice})",
            nameof(UpdateChargePrice), dtoChargePrice);
        if (!string.IsNullOrEmpty(settings.ChargePricesUpdateText))
        {
            logger.LogWarning("Can not update charge price as currently updating due to previous change");
            return;
        }
        ChargePrice chargePrice;
        if (dtoChargePrice.Id == null)
        {
            chargePrice = new ChargePrice();
            teslaSolarChargerContext.ChargePrices.Add(chargePrice);
        }
        else
        {
            chargePrice = await teslaSolarChargerContext.ChargePrices.FirstAsync(c => c.Id == dtoChargePrice.Id).ConfigureAwait(false);
        }

        //Can not be null as declared as Required in DTO
        chargePrice.GridPrice = (decimal)dtoChargePrice.GridPrice!;
        chargePrice.SolarPrice = (decimal)dtoChargePrice.SolarPrice!;
        chargePrice.ValidSince = dtoChargePrice.ValidSince;
        chargePrice.EnergyProvider = dtoChargePrice.EnergyProvider;
        chargePrice.AddSpotPriceToGridPrice = dtoChargePrice.AddSpotPriceToGridPrice;
        chargePrice.SpotPriceCorrectionFactor = (dtoChargePrice.SpotPriceSurcharge ?? 0) / 100;
        chargePrice.EnergyProviderConfiguration = dtoChargePrice.EnergyProviderConfiguration;
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);

        await tscOnlyChargingCostService.UpdateChargePricesOfAllChargingProcesses().ConfigureAwait(false);
    }

    public async Task AddFirstChargePrice()
    {
        logger.LogTrace("{method}()", nameof(AddFirstChargePrice));
        var chargePrices = await teslaSolarChargerContext.ChargePrices
            .ToListAsync().ConfigureAwait(false);
        if (IsFirstChargePriceSet(chargePrices))
        {
            return;
        }
        var chargePrice = new DtoChargePrice()
        {
            GridPrice = 0.25m,
            SolarPrice = 0.25m,
            ValidSince = DateTime.SpecifyKind(new DateTime(2020, 1, 1), DateTimeKind.Utc),
            EnergyProvider = EnergyProvider.OldTeslaSolarChargerConfig,
            AddSpotPriceToGridPrice = false,
            SpotPriceSurcharge = 0.19m,
            EnergyProviderConfiguration = null,
        };
        await UpdateChargePrice(chargePrice).ConfigureAwait(false);
    }

    private static bool IsFirstChargePriceSet(List<ChargePrice> chargePrices)
    {
        return chargePrices.Any(c => c.ValidSince < new DateTime(2022, 2, 1));
    }

    public async Task FixConvertedChargingDetailSolarPower()
    {
        logger.LogTrace("{method}()", nameof(FixConvertedChargingDetailSolarPower));
        var convertedChargingProcesses = await teslaSolarChargerContext.ChargingProcesses
            .Where(c => c.OldHandledChargeId != null)
            .ToListAsync().ConfigureAwait(false);

        foreach (var convertedChargingProcess in convertedChargingProcesses)
        {
            var scope = serviceProvider.CreateScope();
            var scopedTscContext = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
            var chargingDetails = await scopedTscContext.ChargingDetails
                .Where(cd => cd.ChargingProcessId == convertedChargingProcess.Id)
                .ToListAsync().ConfigureAwait(false);
            foreach (var chargingDetail in chargingDetails)
            {
                if (chargingDetail.SolarPower < 0)
                {
                    chargingDetail.GridPower += chargingDetail.SolarPower;
                    chargingDetail.SolarPower = 0;
                }
            }
            await scopedTscContext.SaveChangesAsync().ConfigureAwait(false);
        }
        
    }

    public async Task DeleteChargePriceById(int id)
    {
        var chargePrice = await teslaSolarChargerContext.ChargePrices
            .FirstAsync(c => c.Id == id).ConfigureAwait(false);
        teslaSolarChargerContext.ChargePrices.Remove(chargePrice);
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);

        await tscOnlyChargingCostService.UpdateChargePricesOfAllChargingProcesses().ConfigureAwait(false);
    }

    public async Task<List<DtoChargePrice>> GetChargePrices()
    {
        logger.LogTrace("{method}()", nameof(GetChargePrices));
        var mapper = mapperConfigurationFactory.Create(cfg =>
        {
            cfg.CreateMap<ChargePrice, DtoChargePrice>()
                .ForMember(d => d.Id, opt => opt.MapFrom(c => c.Id));
        });
        var chargePrices = await teslaSolarChargerContext.ChargePrices
            .ProjectTo<DtoChargePrice>(mapper)
            .ToListAsync().ConfigureAwait(false);
        return chargePrices.OrderBy(p => p.ValidSince).ToList();
    }

    public async Task DeleteDuplicatedHandleCharges()
    {
        var handledChargeChargingProcessIDs = await teslaSolarChargerContext.HandledCharges
            .Select(h => h.ChargingProcessId)
            .ToListAsync().ConfigureAwait(false);

        if (handledChargeChargingProcessIDs.Count == handledChargeChargingProcessIDs.Distinct().Count())
        {
            return;
        }

        var handledCharges = await teslaSolarChargerContext.HandledCharges
            .ToListAsync().ConfigureAwait(false);

        var duplicates = handledCharges
            .GroupBy(t => new { t.ChargingProcessId })
            .Where(t => t.Count() > 1)
            .SelectMany(x => x)
            .ToList();

        foreach (var duplicate in duplicates)
        {
            var chargeDistributions = await teslaSolarChargerContext.PowerDistributions
                .Where(p => p.HandledChargeId == duplicate.Id)
                .ToListAsync().ConfigureAwait(false);
            teslaSolarChargerContext.PowerDistributions.RemoveRange(chargeDistributions);
            teslaSolarChargerContext.HandledCharges.Remove(duplicate);
        }

        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<List<SpotPrice>> GetSpotPrices()
    {
        return await teslaSolarChargerContext.SpotPrices.ToListAsync().ConfigureAwait(false);
    }
    
    public async Task<DtoChargePrice> GetChargePriceById(int id)
    {
        logger.LogTrace("{method}({id})", nameof(GetChargePriceById), id);
        var mapper = mapperConfigurationFactory.Create(cfg =>
        {
            cfg.CreateMap<ChargePrice, DtoChargePrice>()
                .ForMember(d => d.SpotPriceSurcharge, opt => opt.MapFrom(c => c.SpotPriceCorrectionFactor * 100))
                ;
        });
        var chargePrices = await teslaSolarChargerContext.ChargePrices
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
