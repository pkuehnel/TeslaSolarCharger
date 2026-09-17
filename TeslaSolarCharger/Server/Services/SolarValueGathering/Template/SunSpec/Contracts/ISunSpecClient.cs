using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Template.SunSpec.Contracts;

public interface ISunSpecClient
{
    /// <summary>
    /// Reads a value referenced as "model:point" or "model:block:point" (e.g. "203:W" or "160:3:DCW").
    /// The scale factor is applied automatically. Returns null if the model is not present on the device.
    /// </summary>
    Task<decimal?> ReadValueAsync(DtoModbusConfiguration modbusConfig, string pointReference, CancellationToken cancellationToken);

    /// <summary>
    /// Writes a value to a "model:point" reference (e.g. "124:0:OutWRte"). The scale factor is applied automatically.
    /// </summary>
    Task WriteValueAsync(DtoModbusConfiguration modbusConfig, string pointReference, decimal value,
        ModbusWriteFunction writeFunction, CancellationToken cancellationToken);

    /// <summary>
    /// Clears the cached model layout for a device, forcing a rediscovery on the next access.
    /// </summary>
    void InvalidateCache(DtoModbusConfiguration modbusConfig);
}
