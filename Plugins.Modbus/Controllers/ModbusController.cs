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
        /// <param name="ipAddresString">The ip address of the modbus device</param>
        /// <param name="port">The modbus port of the modbus device</param>
        /// <param name="factor">The factor to multiply the outcoming modbus value with (e.g. if value is 0.1 W you have to use 10 as factor)</param>
        /// <returns></returns>
        [HttpGet]
        public int GetValue(byte unitIdentifier, ushort startingAddress, ushort quantity, string ipAddresString,
            int port,
            float factor) => _modbusService.ReadIntegerValue(unitIdentifier, startingAddress, quantity, ipAddresString, port, factor);
    }
}
