using System.Collections.Concurrent;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Modbus.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Template.SunSpec.Contracts;
using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Template.SunSpec;

/// <summary>
/// Minimal SunSpec client: discovers the model chain on a device and reads/writes points from the supported models
/// (see <see cref="SunSpecModels"/>). SunSpec is always big endian on the wire, which the underlying
/// <see cref="IModbusValueExecutionService"/> decodes correctly when configured with big endian.
/// Needs to be a singleton so the discovered layout is cached across refreshes.
/// </summary>
public class SunSpecClient : ISunSpecClient
{
    //The "SunS" identifier marks the start of the SunSpec model chain
    private const uint SunSpecMarker = 0x53756E53;
    private const int EndModelId = 0xFFFF;
    private static readonly int[] BaseAddressCandidates = { 40000, 50000, 0 };

    private readonly ILogger<SunSpecClient> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ConcurrentDictionary<string, SunSpecDeviceLayout> _layoutCache = new();

    public SunSpecClient(ILogger<SunSpecClient> logger, IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task<decimal?> ReadValueAsync(DtoModbusConfiguration modbusConfig, string pointReference, CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({host}, {pointReference})", nameof(ReadValueAsync), modbusConfig.Host, pointReference);
        var reference = SunSpecPointReference.Parse(pointReference);
        var layout = await GetLayoutAsync(modbusConfig, cancellationToken).ConfigureAwait(false);
        if (!layout.Models.TryGetValue(reference.ModelId, out var modelLocation))
        {
            return default;
        }
        using var scope = _serviceScopeFactory.CreateScope();
        var modbusValueExecutionService = scope.ServiceProvider.GetRequiredService<IModbusValueExecutionService>();

        if (reference.ModelId == SunSpecModels.Model160.ModelId)
        {
            return await ReadModel160Async(modbusConfig, modbusValueExecutionService, modelLocation, reference, cancellationToken)
                .ConfigureAwait(false);
        }

        if (!SunSpecModels.Models.TryGetValue(reference.ModelId, out var model)
            || !model.Points.TryGetValue(reference.Point, out var point))
        {
            throw new InvalidOperationException($"Unknown SunSpec point {pointReference}");
        }
        var rawValue = await ReadRawAsync(modbusConfig, modbusValueExecutionService, modelLocation.DataAddress + point.Offset,
            point.ToModbusValueType(), cancellationToken).ConfigureAwait(false);
        var scaleFactor = await ReadScaleFactorAsync(modbusConfig, modbusValueExecutionService, modelLocation.DataAddress,
            point.ScaleFactorOffset, cancellationToken).ConfigureAwait(false);
        return ApplyScaleFactor(rawValue, scaleFactor);
    }

    public async Task WriteValueAsync(DtoModbusConfiguration modbusConfig, string pointReference, decimal value,
        ModbusWriteFunction writeFunction, CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({host}, {pointReference}, {value})", nameof(WriteValueAsync), modbusConfig.Host, pointReference, value);
        var reference = SunSpecPointReference.Parse(pointReference);
        var layout = await GetLayoutAsync(modbusConfig, cancellationToken).ConfigureAwait(false);
        if (!layout.Models.TryGetValue(reference.ModelId, out var modelLocation))
        {
            throw new InvalidOperationException($"SunSpec model {reference.ModelId} not present on device, can not write {pointReference}");
        }
        if (!SunSpecModels.Models.TryGetValue(reference.ModelId, out var model)
            || !model.Points.TryGetValue(reference.Point, out var point))
        {
            throw new InvalidOperationException($"Unknown SunSpec point {pointReference}");
        }
        using var scope = _serviceScopeFactory.CreateScope();
        var modbusValueExecutionService = scope.ServiceProvider.GetRequiredService<IModbusValueExecutionService>();
        var scaleFactor = await ReadScaleFactorAsync(modbusConfig, modbusValueExecutionService, modelLocation.DataAddress,
            point.ScaleFactorOffset, cancellationToken).ConfigureAwait(false);
        //Writing applies the inverse scale factor: raw = value / 10^sf
        var rawValue = scaleFactor == default ? value : Math.Round(value / (decimal)Math.Pow(10, scaleFactor.Value));
        await modbusValueExecutionService.WriteValue(modbusConfig, point.ToModbusValueType(),
            modelLocation.DataAddress + point.Offset, rawValue, writeFunction, false).ConfigureAwait(false);
    }

    public void InvalidateCache(DtoModbusConfiguration modbusConfig)
    {
        _layoutCache.TryRemove(CreateCacheKey(modbusConfig), out _);
    }

    private async Task<decimal?> ReadModel160Async(DtoModbusConfiguration modbusConfig,
        IModbusValueExecutionService modbusValueExecutionService, SunSpecModelLocation modelLocation,
        SunSpecPointReference reference, CancellationToken cancellationToken)
    {
        if (reference.Block == default || reference.Block < 1)
        {
            throw new InvalidOperationException($"SunSpec model 160 requires a block, e.g. 160:1:DCW");
        }
        var moduleCount = (int)await ReadRawAsync(modbusConfig, modbusValueExecutionService,
            modelLocation.DataAddress + SunSpecModels.Model160.ModuleCountOffset, ModbusValueType.UShort, cancellationToken)
            .ConfigureAwait(false);
        var blockLength = moduleCount > 0
            ? (modelLocation.Length - SunSpecModels.Model160.FixedHeaderLength) / moduleCount
            : SunSpecModels.Model160.DefaultBlockLength;
        if (reference.Block > moduleCount)
        {
            //Requested MPPT string is not present (e.g. an unused battery string)
            return default;
        }
        var blockDataAddress = modelLocation.DataAddress + SunSpecModels.Model160.FixedHeaderLength
                               + (reference.Block.Value - 1) * blockLength;
        var (pointOffset, valueType, scaleFactorOffset) = reference.Point switch
        {
            "DCW" => (SunSpecModels.Model160.DcwOffsetInBlock(blockLength), ModbusValueType.UShort, SunSpecModels.Model160.DcwScaleFactorOffset),
            "DCWH" => (SunSpecModels.Model160.DcwhOffsetInBlock(blockLength), ModbusValueType.UInt, SunSpecModels.Model160.DcwhScaleFactorOffset),
            _ => throw new InvalidOperationException($"Unsupported SunSpec model 160 point {reference.Point}"),
        };
        var rawValue = await ReadRawAsync(modbusConfig, modbusValueExecutionService, blockDataAddress + pointOffset,
            valueType, cancellationToken).ConfigureAwait(false);
        var scaleFactor = await ReadScaleFactorAsync(modbusConfig, modbusValueExecutionService, modelLocation.DataAddress,
            scaleFactorOffset, cancellationToken).ConfigureAwait(false);
        return ApplyScaleFactor(rawValue, scaleFactor);
    }

    private async Task<SunSpecDeviceLayout> GetLayoutAsync(DtoModbusConfiguration modbusConfig, CancellationToken cancellationToken)
    {
        var cacheKey = CreateCacheKey(modbusConfig);
        if (_layoutCache.TryGetValue(cacheKey, out var cachedLayout))
        {
            return cachedLayout;
        }
        var layout = await DiscoverAsync(modbusConfig, cancellationToken).ConfigureAwait(false);
        _layoutCache[cacheKey] = layout;
        return layout;
    }

    private async Task<SunSpecDeviceLayout> DiscoverAsync(DtoModbusConfiguration modbusConfig, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Discovering SunSpec models on {host}:{port}", modbusConfig.Host, modbusConfig.Port);
        using var scope = _serviceScopeFactory.CreateScope();
        var modbusValueExecutionService = scope.ServiceProvider.GetRequiredService<IModbusValueExecutionService>();
        foreach (var baseAddress in BaseAddressCandidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            uint marker;
            try
            {
                marker = (uint)await ReadRawAsync(modbusConfig, modbusValueExecutionService, baseAddress,
                    ModbusValueType.UInt, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not read SunSpec marker at base address {baseAddress}", baseAddress);
                continue;
            }
            if (marker != SunSpecMarker)
            {
                continue;
            }
            var models = await WalkModelChainAsync(modbusConfig, modbusValueExecutionService, baseAddress + 2, cancellationToken)
                .ConfigureAwait(false);
            _logger.LogInformation("Discovered SunSpec device at base {baseAddress} on {host} with models {models}",
                baseAddress, modbusConfig.Host, string.Join(", ", models.Keys));
            return new SunSpecDeviceLayout(models);
        }
        throw new InvalidDataException($"No SunSpec marker found on {modbusConfig.Host}:{modbusConfig.Port}");
    }

    private async Task<Dictionary<int, SunSpecModelLocation>> WalkModelChainAsync(DtoModbusConfiguration modbusConfig,
        IModbusValueExecutionService modbusValueExecutionService, int address, CancellationToken cancellationToken)
    {
        var models = new Dictionary<int, SunSpecModelLocation>();
        //Guard against malformed chains
        for (var i = 0; i < 128; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var modelId = (int)await ReadRawAsync(modbusConfig, modbusValueExecutionService, address, ModbusValueType.UShort, cancellationToken)
                .ConfigureAwait(false);
            if (modelId == EndModelId)
            {
                break;
            }
            var length = (int)await ReadRawAsync(modbusConfig, modbusValueExecutionService, address + 1, ModbusValueType.UShort, cancellationToken)
                .ConfigureAwait(false);
            var dataAddress = address + 2;
            //Keep the first occurrence of a model
            models.TryAdd(modelId, new SunSpecModelLocation(dataAddress, length));
            address = dataAddress + length;
        }
        return models;
    }

    private static async Task<decimal> ReadRawAsync(DtoModbusConfiguration modbusConfig,
        IModbusValueExecutionService modbusValueExecutionService, int address, ModbusValueType valueType,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var length = valueType is ModbusValueType.Int or ModbusValueType.UInt or ModbusValueType.Float or ModbusValueType.Ulong ? 2 : 1;
        if (valueType == ModbusValueType.Ulong)
        {
            length = 4;
        }
        var resultConfiguration = new DtoModbusValueResultConfiguration
        {
            Id = 1,
            RegisterType = ModbusRegisterType.HoldingRegister,
            ValueType = valueType,
            Address = address,
            Length = length,
            Operator = ValueOperator.Plus,
            CorrectionFactor = 1,
        };
        var byteArray = await modbusValueExecutionService.GetResult(modbusConfig, resultConfiguration, false).ConfigureAwait(false);
        return await modbusValueExecutionService.GetValue(byteArray, resultConfiguration).ConfigureAwait(false);
    }

    private static async Task<int?> ReadScaleFactorAsync(DtoModbusConfiguration modbusConfig,
        IModbusValueExecutionService modbusValueExecutionService, int modelDataAddress, int? scaleFactorOffset,
        CancellationToken cancellationToken)
    {
        if (scaleFactorOffset == default)
        {
            return default;
        }
        var scaleFactor = (int)await ReadRawAsync(modbusConfig, modbusValueExecutionService,
            modelDataAddress + scaleFactorOffset.Value, ModbusValueType.Short, cancellationToken).ConfigureAwait(false);
        //A scale factor of the not implemented sentinel means no scaling
        return scaleFactor == SunSpecModels.NotImplementedInt16 ? default(int?) : scaleFactor;
    }

    private static decimal ApplyScaleFactor(decimal rawValue, int? scaleFactor)
    {
        return scaleFactor == default ? rawValue : rawValue * (decimal)Math.Pow(10, scaleFactor.Value);
    }

    private static string CreateCacheKey(DtoModbusConfiguration modbusConfig) =>
        $"{modbusConfig.Host}:{modbusConfig.Port}:{modbusConfig.UnitIdentifier}";
}

public class SunSpecDeviceLayout
{
    public SunSpecDeviceLayout(Dictionary<int, SunSpecModelLocation> models)
    {
        Models = models;
    }

    public Dictionary<int, SunSpecModelLocation> Models { get; }
}

public record SunSpecModelLocation(int DataAddress, int Length);

public class SunSpecPointReference
{
    public required int ModelId { get; init; }
    public int? Block { get; init; }
    public required string Point { get; init; }

    public static SunSpecPointReference Parse(string pointReference)
    {
        var parts = pointReference.Split(':');
        return parts.Length switch
        {
            2 => new SunSpecPointReference { ModelId = int.Parse(parts[0]), Point = parts[1] },
            3 => new SunSpecPointReference { ModelId = int.Parse(parts[0]), Block = int.Parse(parts[1]), Point = parts[2] },
            _ => throw new ArgumentException($"Invalid SunSpec point reference: {pointReference}", nameof(pointReference)),
        };
    }
}
