namespace TeslaSolarCharger.Shared.Enums;

public enum ModbusWriteFunction
{
    /// <summary>
    /// Modbus function code 16
    /// </summary>
    WriteMultipleRegisters,
    /// <summary>
    /// Modbus function code 6, only supported for single register (16 bit) values. Some devices only accept
    /// this function code for specific registers.
    /// </summary>
    WriteSingleRegister,
}
