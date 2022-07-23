using Microsoft.AspNetCore.Mvc;
using Plugins.Modbus.Contracts;

namespace Plugins.Modbus.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class ModbusController : ControllerBase
    {
        private readonly IModbusService _modbusService;

        public ModbusController(IModbusService modbusService)
        {
            _modbusService = modbusService;
        }

        /// <summary>
        /// Gets a Modbus Integer value
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier is used to communicate via devices such as bridges, routers and gateways that use a single IP address to support multiple independent Modbus end units. Thus, the unit identifier is the address of a remote slave connected on a serial line or on other buses. Use the default values 0x00 or 0xFF when communicating to a Modbus server that is directly connected to a TCP/IP network.</param>
        /// <param name="startingAddress">The holding register start address for the read operation.</param>
        /// <param name="quantity">The number of holding registers (16 bit per register) to read.</param>
        /// <param name="ipAddress">The ip address of the modbus device</param>
        /// <param name="port">The modbus port of the modbus device</param>
        /// <param name="factor">The factor to multiply the outcoming modbus value with (e.g. if value is 0.1 W you have to use 10 as factor)</param>
        /// <param name="connectDelaySeconds"></param>
        /// <param name="timeoutSeconds"></param>
        /// <param name="minimumResult">Sets a minimum return result. This ist important, if your inverter does not send 0 as power if it is off.</param>
        /// <returns></returns>
        [Obsolete]
        [HttpGet]
        public Task<int> GetValue(byte unitIdentifier, ushort startingAddress, ushort quantity, string ipAddress,
            int port, float factor, int connectDelaySeconds, int timeoutSeconds, int? minimumResult = null) 
            => GetInt32Value(unitIdentifier, startingAddress, quantity, ipAddress, port, factor, connectDelaySeconds, timeoutSeconds, minimumResult);

        /// <summary>
        /// Gets a Modbus Int32 value
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier is used to communicate via devices such as bridges, routers and gateways that use a single IP address to support multiple independent Modbus end units. Thus, the unit identifier is the address of a remote slave connected on a serial line or on other buses. Use the default values 0x00 or 0xFF when communicating to a Modbus server that is directly connected to a TCP/IP network.</param>
        /// <param name="startingAddress">The holding register start address for the read operation.</param>
        /// <param name="quantity">The number of holding registers (16 bit per register) to read.</param>
        /// <param name="ipAddress">The ip address of the modbus device</param>
        /// <param name="port">The modbus port of the modbus device</param>
        /// <param name="factor">The factor to multiply the outcoming modbus value with (e.g. if value is 0.1 W you have to use 10 as factor)</param>
        /// <param name="connectDelaySeconds"></param>
        /// <param name="timeoutSeconds"></param>
        /// <param name="minimumResult">Sets a minimum return result. This ist important, if your inverter does not send 0 as power if it is off.</param>
        /// <returns>Modbus value converted to Int32</returns>
        [HttpGet]
        public Task<int> GetInt32Value(byte unitIdentifier, ushort startingAddress, ushort quantity, string ipAddress,
            int port, float factor, int connectDelaySeconds, int timeoutSeconds, int? minimumResult = null)
            => _modbusService.ReadInt32Value(unitIdentifier, startingAddress, quantity, ipAddress, port, factor, connectDelaySeconds, timeoutSeconds, minimumResult);

        /// <summary>
        /// Gets a Modbus Int16 value
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier is used to communicate via devices such as bridges, routers and gateways that use a single IP address to support multiple independent Modbus end units. Thus, the unit identifier is the address of a remote slave connected on a serial line or on other buses. Use the default values 0x00 or 0xFF when communicating to a Modbus server that is directly connected to a TCP/IP network.</param>
        /// <param name="startingAddress">The holding register start address for the read operation.</param>
        /// <param name="quantity">The number of holding registers (16 bit per register) to read.</param>
        /// <param name="ipAddress">The ip address of the modbus device</param>
        /// <param name="port">The modbus port of the modbus device</param>
        /// <param name="factor">The factor to multiply the outcoming modbus value with (e.g. if value is 0.1 W you have to use 10 as factor)</param>
        /// <param name="connectDelaySeconds"></param>
        /// <param name="timeoutSeconds"></param>
        /// <param name="minimumResult">Sets a minimum return result. This ist important, if your inverter does not send 0 as power if it is off.</param>
        /// <returns>Modbus value converted to Int32</returns>
        [HttpGet]
        public Task<short> GetInt16Value(byte unitIdentifier, ushort startingAddress, ushort quantity, string ipAddress,
            int port, float factor, int connectDelaySeconds, int timeoutSeconds, int? minimumResult = null)
            => _modbusService.ReadInt16Value(unitIdentifier, startingAddress, quantity, ipAddress, port, factor, connectDelaySeconds, timeoutSeconds, minimumResult);

    }
}
