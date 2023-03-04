using Microsoft.AspNetCore.Mvc;
using Plugins.Modbus.Contracts;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace Plugins.Modbus.Controllers
{
    public class ModbusController : ApiBaseController
    {
        private readonly IModbusService _modbusService;

        public ModbusController(IModbusService modbusService)
        {
            _modbusService = modbusService;
        }

        [HttpGet]
        public Task<string> GetBinarySubString(byte unitIdentifier, ushort startingAddress, ushort quantity, string ipAddress,
            int port, int connectDelaySeconds, int timeoutSeconds, ModbusRegisterType modbusRegisterType, int startIndex, int length, bool registerSwap = false)
            => _modbusService.GetBinarySubString(unitIdentifier, startingAddress, quantity, ipAddress, port,
                connectDelaySeconds, timeoutSeconds, modbusRegisterType, registerSwap, startIndex, length);

        [HttpGet]
        public Task<string> GetBinaryString(byte unitIdentifier, ushort startingAddress, ushort quantity, string ipAddress,
            int port, int connectDelaySeconds, int timeoutSeconds, ModbusRegisterType modbusRegisterType, bool registerSwap = false)
            => _modbusService.GetBinaryString(unitIdentifier, startingAddress, quantity, ipAddress, port,
                connectDelaySeconds, timeoutSeconds, modbusRegisterType, registerSwap);

        [HttpGet]
        public Task<object> GetTypedValue(byte unitIdentifier, ushort startingAddress, ushort quantity, string ipAddress,
            int port, int connectDelaySeconds, int timeoutSeconds, ModbusValueType modbusValueType, ModbusRegisterType modbusRegisterType, bool registerSwap = false)
        {
            return modbusValueType switch
            {
                ModbusValueType.Int => _modbusService.ReadValue<int>(unitIdentifier, startingAddress, quantity, ipAddress, port,
                    connectDelaySeconds, timeoutSeconds, modbusRegisterType, registerSwap),
                ModbusValueType.Float => _modbusService.ReadValue<float>(unitIdentifier, startingAddress, quantity, ipAddress, port,
                    connectDelaySeconds, timeoutSeconds, modbusRegisterType, registerSwap),
                ModbusValueType.Short => _modbusService.ReadValue<short>(unitIdentifier, startingAddress, quantity, ipAddress, port,
                    connectDelaySeconds, timeoutSeconds, modbusRegisterType, registerSwap),
                ModbusValueType.UInt => _modbusService.ReadValue<uint>(unitIdentifier, startingAddress, quantity, ipAddress, port,
                    connectDelaySeconds, timeoutSeconds, modbusRegisterType, registerSwap),
                ModbusValueType.UShort => _modbusService.ReadValue<ushort>(unitIdentifier, startingAddress, quantity, ipAddress, port,
                    connectDelaySeconds, timeoutSeconds, modbusRegisterType, registerSwap),
                ModbusValueType.Ulong => _modbusService.ReadValue<ulong>(unitIdentifier, startingAddress, quantity, ipAddress, port,
                    connectDelaySeconds, timeoutSeconds, modbusRegisterType, registerSwap),
                _ => throw new ArgumentOutOfRangeException(nameof(modbusValueType), modbusValueType, null)
            };
        }

        /// <summary>
        /// Gets a Modbus Integer value
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier is used to communicate via devices such as bridges, routers and gateways that use a single IP address to support multiple independent Modbus end units. Thus, the unit identifier is the address of a remote slave connected on a serial line or on other buses. Use the default values 0x00 or 0xFF when communicating to a Modbus server that is directly connected to a TCP/IP network.</param>
        /// <param name="startingAddress">The holding register start address for the read operation.</param>
        /// <param name="quantity">The number of holding registers (16 bit per register) to read.</param>
        /// <param name="ipAddress">The ip address of the modbus device</param>
        /// <param name="port">The modbus port of the modbus device</param>
        /// <param name="connectDelaySeconds"></param>
        /// <param name="timeoutSeconds"></param>
        /// <param name="registerSwap">Swap from little-endian (CDAB) to big-endian (ABCD)</param>
        /// <returns></returns>
        [Obsolete]
        [HttpGet]
        public Task<object> GetValue(byte unitIdentifier, ushort startingAddress, ushort quantity, string ipAddress,
            int port, int connectDelaySeconds, int timeoutSeconds, bool registerSwap = false)
            => GetTypedValue(unitIdentifier, startingAddress, quantity, ipAddress, port, connectDelaySeconds, timeoutSeconds, ModbusValueType.Int, ModbusRegisterType.HoldingRegister, registerSwap);

        /// <summary>
        /// Gets a Modbus Int32 value
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier is used to communicate via devices such as bridges, routers and gateways that use a single IP address to support multiple independent Modbus end units. Thus, the unit identifier is the address of a remote slave connected on a serial line or on other buses. Use the default values 0x00 or 0xFF when communicating to a Modbus server that is directly connected to a TCP/IP network.</param>
        /// <param name="startingAddress">The holding register start address for the read operation.</param>
        /// <param name="quantity">The number of holding registers (16 bit per register) to read. Should be 2 for int32</param>
        /// <param name="ipAddress">The ip address of the modbus device</param>
        /// <param name="port">The modbus port of the modbus device</param>
        /// <param name="connectDelaySeconds"></param>
        /// <param name="timeoutSeconds"></param>
        /// <param name="registerSwap">Swap from little-endian (CDAB) to big-endian (ABCD)</param>
        /// <returns>Modbus value converted to Int32</returns>
        [Obsolete]
        [HttpGet]
        public Task<object> GetInt32Value(byte unitIdentifier, ushort startingAddress, ushort quantity, string ipAddress,
            int port, int connectDelaySeconds, int timeoutSeconds, bool registerSwap = false)
            => GetTypedValue(unitIdentifier, startingAddress, quantity, ipAddress, port, connectDelaySeconds, timeoutSeconds, ModbusValueType.Int, ModbusRegisterType.HoldingRegister, registerSwap);

        /// <summary>
        /// Gets a Modbus Int16 value
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier is used to communicate via devices such as bridges, routers and gateways that use a single IP address to support multiple independent Modbus end units. Thus, the unit identifier is the address of a remote slave connected on a serial line or on other buses. Use the default values 0x00 or 0xFF when communicating to a Modbus server that is directly connected to a TCP/IP network.</param>
        /// <param name="startingAddress">The holding register start address for the read operation.</param>
        /// <param name="quantity">The number of holding registers (16 bit per register) to read. Should be 1 for int 16.</param>
        /// <param name="ipAddress">The ip address of the modbus device</param>
        /// <param name="port">The modbus port of the modbus device</param>
        /// <param name="connectDelaySeconds"></param>
        /// <param name="timeoutSeconds"></param>
        /// <param name="minimumResult">Sets a minimum return result. This ist important, if your inverter does not send 0 as power if it is off.</param>
        /// <param name="registerSwap">Swap from little-endian (CDAB) to big-endian (ABCD)</param>
        /// <returns>Modbus value converted to Int16</returns>
        [Obsolete]
        [HttpGet]
        public Task<object> GetInt16Value(byte unitIdentifier, ushort startingAddress, ushort quantity, string ipAddress,
            int port, int connectDelaySeconds, int timeoutSeconds, int? minimumResult = null, bool registerSwap = false)
            => GetTypedValue(unitIdentifier, startingAddress, quantity, ipAddress, port, connectDelaySeconds, timeoutSeconds, ModbusValueType.Short, ModbusRegisterType.HoldingRegister, registerSwap);

        /// <summary>
        /// Gets a Modbus Float value
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier is used to communicate via devices such as bridges, routers and gateways that use a single IP address to support multiple independent Modbus end units. Thus, the unit identifier is the address of a remote slave connected on a serial line or on other buses. Use the default values 0x00 or 0xFF when communicating to a Modbus server that is directly connected to a TCP/IP network.</param>
        /// <param name="startingAddress">The holding register start address for the read operation.</param>
        /// <param name="quantity">The number of holding registers (16 bit per register) to read. Should be 1 for int 16.</param>
        /// <param name="ipAddress">The ip address of the modbus device</param>
        /// <param name="port">The modbus port of the modbus device</param>
        /// <param name="connectDelaySeconds"></param>
        /// <param name="timeoutSeconds"></param>
        /// <param name="minimumResult">Sets a minimum return result. This ist important, if your inverter does not send 0 as power if it is off.</param>
        /// <param name="registerSwap">Swap from little-endian (CDAB) to big-endian (ABCD)</param>
        /// <returns>Modbus value converted to float</returns>
        [Obsolete]
        [HttpGet]
        public Task<object> GetFloatValue(byte unitIdentifier, ushort startingAddress, ushort quantity, string ipAddress,
            int port, int connectDelaySeconds, int timeoutSeconds, int? minimumResult = null, bool registerSwap = false)
            => GetTypedValue(unitIdentifier, startingAddress, quantity, ipAddress, port, connectDelaySeconds, timeoutSeconds, ModbusValueType.Float, ModbusRegisterType.HoldingRegister, registerSwap);

    }
}
