using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class MeterValueImportService : IMeterValueImportService
{
    private readonly ILogger<MeterValueImportService> _logger;
    private readonly ITscConfigurationService _tscConfigurationService;
    private readonly ITeslaSolarChargerContext _context;
    private readonly IServiceProvider _serviceProvider;

    private const string CarMeterValuesImportedKey = "CarMeterValuesImported";


    public MeterValueImportService(ILogger<MeterValueImportService> logger,
        ITscConfigurationService tscConfigurationService,
        ITeslaSolarChargerContext context,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _tscConfigurationService = tscConfigurationService;
        _context = context;
        _serviceProvider = serviceProvider;
    }

    public async Task ImportMeterValuesFromChargingDetailsAsync()
    {
        _logger.LogTrace("{method}()", nameof(ImportMeterValuesFromChargingDetailsAsync));
        var valuesAlreadyImported = await _tscConfigurationService.GetConfigurationValueByKey(CarMeterValuesImportedKey).ConfigureAwait(false);
        const string alreadyUpdatedValue = "true";
        if (string.Equals(valuesAlreadyImported, alreadyUpdatedValue))
        {
            _logger.LogDebug("Charging Details Meter values already imported, skipping.");
            return;
        }
        var chargingProcesses = await _context.ChargingProcesses
            .OrderBy(cp => cp.StartDate)
            .AsNoTracking()
            .ToListAsync();

        var latestCarMeterValues = new Dictionary<int, MeterValue>();
        var latestChargingStationMeterValues = new Dictionary<int, MeterValue>();
        foreach (var chargingProcess in chargingProcesses)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
            var meterValueEstimationService = scope.ServiceProvider.GetRequiredService<IMeterValueEstimationService>();
            var chargingDetails = await context.ChargingDetails
                .Where(cd => cd.ChargingProcessId == chargingProcess.Id)
                .AsNoTracking()
                .ToListAsync();
            if (!chargingDetails.Any())
            {
                continue;
            }
            chargingDetails = chargingDetails.OrderBy(cd => cd.TimeStamp).ToList();
            var carMeterValuesToSave = new List<MeterValue>();
            var chargingStationMeterValuesToSave = new List<MeterValue>();
            var index = 0;
            foreach (var chargingDetail in chargingDetails)
            {
                if (chargingProcess.CarId != default)
                {
                    var meterValue = GenerateMeterValueFromChargingDetail(chargingDetail, MeterValueKind.Car);
                    if (index == 0)
                    {
                        var dummyMeterValue = GenerateDefaultMeterValue(chargingProcess.CarId, null, meterValue.Timestamp);
                        carMeterValuesToSave.Add(dummyMeterValue);
                        meterValue.Timestamp = meterValue.Timestamp.AddMilliseconds(1);
                    }
                    meterValue.CarId = chargingProcess.CarId;
                    carMeterValuesToSave.Add(meterValue);

                    if ((index != 0) && (index == chargingDetails.Count - 1))
                    {
                        var dummyMeterValue = GenerateDefaultMeterValue(chargingProcess.CarId, null, meterValue.Timestamp);
                        carMeterValuesToSave.Add(dummyMeterValue);
                        meterValue.Timestamp = meterValue.Timestamp.AddMilliseconds(-1);
                    }
                    
                }
                if (chargingProcess.OcppChargingStationConnectorId != default)
                {
                    var meterValue = GenerateMeterValueFromChargingDetail(chargingDetail, MeterValueKind.ChargingConnector);
                    if (index == 0)
                    {
                        var dummyMeterValue = GenerateDefaultMeterValue(null, chargingProcess.OcppChargingStationConnectorId, meterValue.Timestamp);
                        chargingStationMeterValuesToSave.Add(dummyMeterValue);
                        meterValue.Timestamp = meterValue.Timestamp.AddMilliseconds(1);
                    }
                    meterValue.ChargingConnectorId = chargingProcess.OcppChargingStationConnectorId;
                    chargingStationMeterValuesToSave.Add(meterValue);

                    if ((index != 0) && (index == chargingDetails.Count - 1))
                    {
                        var dummyMeterValue = GenerateDefaultMeterValue(null, chargingProcess.OcppChargingStationConnectorId, meterValue.Timestamp);
                        chargingStationMeterValuesToSave.Add(dummyMeterValue);
                        meterValue.Timestamp = meterValue.Timestamp.AddMilliseconds(-1);
                    }
                    
                }

                index++;
            }
            foreach (var meterValue in carMeterValuesToSave)
            {
                latestCarMeterValues[chargingProcess.CarId!.Value] =
                    await meterValueEstimationService.UpdateMeterValueEstimation(meterValue, latestCarMeterValues.GetValueOrDefault(chargingProcess.CarId!.Value));
            }
            foreach (var meterValue in chargingStationMeterValuesToSave)
            {
                latestChargingStationMeterValues[chargingProcess.OcppChargingStationConnectorId!.Value] =
                    await meterValueEstimationService.UpdateMeterValueEstimation(meterValue, latestChargingStationMeterValues.GetValueOrDefault(chargingProcess.OcppChargingStationConnectorId!.Value));
            }
            context.MeterValues.AddRange(carMeterValuesToSave);
            context.MeterValues.AddRange(chargingStationMeterValuesToSave);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }
        await _tscConfigurationService.SetConfigurationValueByKey(CarMeterValuesImportedKey, alreadyUpdatedValue).ConfigureAwait(false);
    }


    private MeterValue GenerateDefaultMeterValue(int? carId, int? chargingConnectorId, DateTimeOffset timestamp)
    {
        if (carId == default && chargingConnectorId == default)
        {
            throw new ArgumentException("Either carId or chargingConnectorId must be provided.");
        }
        return new MeterValue(timestamp,
            carId != default ? MeterValueKind.Car : MeterValueKind.ChargingConnector,
            0)
        {
            EstimatedEnergyWs = 0,
            EstimatedGridEnergyWs = 0,
            EstimatedHomeBatteryEnergyWs = 0,
            CarId = carId,
            ChargingConnectorId = chargingConnectorId,
        };
    }


    private MeterValue GenerateMeterValueFromChargingDetail(ChargingDetail chargingDetail, MeterValueKind meterValueKind)
    {
        return new MeterValue(new DateTimeOffset(chargingDetail.TimeStamp, TimeSpan.Zero),
            meterValueKind,
            chargingDetail.SolarPower + chargingDetail.HomeBatteryPower + chargingDetail.GridPower)
        {
            MeasuredHomeBatteryPower = chargingDetail.HomeBatteryPower,
            MeasuredGridPower = chargingDetail.GridPower,
        };
    }
}
