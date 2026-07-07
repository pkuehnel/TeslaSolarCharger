using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Template.GenericModbus;

/// <summary>
/// Fixed Modbus TCP register maps per vendor, transcribed from the evcc templates
/// (https://github.com/evcc-io/evcc/tree/master/templates/definition/meter).
/// Sign conventions in TSC: GridPower positive = export to grid, HomeBatteryPower positive = charging.
/// evcc uses the opposite conventions (grid positive = import, battery positive = discharging), so operators and
/// correction factors are flipped accordingly.
/// </summary>
public static class ModbusTemplateDefinitions
{
    private const decimal Int16Nan = -32768;
    private const decimal Uint16Nan = 65535;
    private const decimal Int32Nan = -2147483648;
    private const decimal Uint32Nan = 4294967295;

    public static IReadOnlyDictionary<TemplateValueGatherType, ModbusTemplateDefinition> Definitions { get; } = new Dictionary<TemplateValueGatherType, ModbusTemplateDefinition>()
    {
        //evcc template sungrow-hybrid (SH series): word swapped 32 bit registers
        {
            TemplateValueGatherType.SungrowHybridInverterModbus, new()
            {
                Endianess = ModbusEndianess.LittleEndian,
                ReadTimeoutMilliseconds = 5000,
                ValueRegisters = new()
                {
                    //13009 export power: positive = export
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Int, Address = 13009, Length = 2, UsedFor = ValueUsage.GridPower },
                    //5016 total DC power
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UInt, Address = 5016, Length = 2, UsedFor = ValueUsage.InverterPower },
                    //13021 battery power: positive = discharging (requires current WiNet firmware reporting signed values)
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Short, Address = 13021, Length = 1, UsedFor = ValueUsage.HomeBatteryPower, Operator = ValueOperator.Minus },
                    //13022 battery level, 0.1 %
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Short, Address = 13022, Length = 1, UsedFor = ValueUsage.HomeBatterySoc, CorrectionFactor = 0.1m },
                },
                BatteryControl = new()
                {
                    NormalWrites = new()
                    {
                        //13050 charge/discharge command: 0xCC = stop (default)
                        ModbusBatteryModeWrite.Constant(13050, 0xCC, ModbusWriteFunction.WriteSingleRegister),
                        //13049 EMS mode: 0 = self consumption (default)
                        ModbusBatteryModeWrite.Constant(13049, 0, ModbusWriteFunction.WriteSingleRegister),
                        //33047 battery max discharge power, 10 W units
                        ModbusBatteryModeWrite.Dynamic(33047, BatteryModeWriteValueSource.MaxDischargePowerW, ModbusWriteFunction.WriteSingleRegister, factor: 0.1m),
                    },
                    HoldWrites = new()
                    {
                        ModbusBatteryModeWrite.Constant(13049, 0, ModbusWriteFunction.WriteSingleRegister),
                        ModbusBatteryModeWrite.Constant(13050, 0xCC, ModbusWriteFunction.WriteSingleRegister),
                        //Min allowed value of 10 W effectively stops discharging
                        ModbusBatteryModeWrite.Constant(33047, 1, ModbusWriteFunction.WriteSingleRegister),
                    },
                    ChargeWrites = new()
                    {
                        //13049 EMS mode: 2 = forced mode
                        ModbusBatteryModeWrite.Constant(13049, 2, ModbusWriteFunction.WriteSingleRegister),
                        //13050 charge/discharge command: 0xAA = charge
                        ModbusBatteryModeWrite.Constant(13050, 0xAA, ModbusWriteFunction.WriteSingleRegister),
                        //13051 battery max forced (dis)charge power, W
                        ModbusBatteryModeWrite.Dynamic(13051, BatteryModeWriteValueSource.MaxChargePowerW, ModbusWriteFunction.WriteSingleRegister),
                    },
                },
            }
        },
        //evcc template sungrow-inverter (SG series)
        {
            TemplateValueGatherType.SungrowInverterModbus, new()
            {
                Endianess = ModbusEndianess.LittleEndian,
                ReadTimeoutMilliseconds = 5000,
                ValueRegisters = new()
                {
                    //5031 (address 5030) total active power
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UInt, Address = 5030, Length = 2, UsedFor = ValueUsage.InverterPower },
                    //5082 meter power: positive = import
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Int, Address = 5082, Length = 2, UsedFor = ValueUsage.GridPower, Operator = ValueOperator.Minus },
                },
            }
        },
        //evcc template huawei-sun2000-hybrid: straight big endian registers with NaN encodings.
        //Grid and battery values require the Huawei Smart Power Sensor. Modbus TCP needs to be activated via
        //installer account. Only energy storage unit 1 is supported.
        {
            TemplateValueGatherType.HuaweiSun2000HybridInverterModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ReadTimeoutMilliseconds = 15000,
                ConnectDelayMilliseconds = 1000,
                ValueRegisters = new()
                {
                    //37113 grid import/export power: positive = export
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 37113, Length = 2, UsedFor = ValueUsage.GridPower, NotAvailableValue = Int32Nan },
                    //32064 input power DC
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 32064, Length = 2, UsedFor = ValueUsage.InverterPower, NotAvailableValue = Int32Nan },
                    //37001 battery power: positive = charging
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 37001, Length = 2, UsedFor = ValueUsage.HomeBatteryPower, NotAvailableValue = Int32Nan },
                    //37004 battery soc, 0.1 %
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UShort, Address = 37004, Length = 1, UsedFor = ValueUsage.HomeBatterySoc, CorrectionFactor = 0.1m, NotAvailableValue = Uint16Nan },
                },
                BatteryControl = new()
                {
                    //Forcible charging needs to be retriggered periodically
                    RewriteInterval = TimeSpan.FromSeconds(30),
                    NormalWrites = new()
                    {
                        //47100 forcible charge/discharge: 0 = stop
                        ModbusBatteryModeWrite.Constant(47100, 0, ModbusWriteFunction.WriteSingleRegister),
                        //47077 max discharge power. Values above the inverter rated max are rejected.
                        ModbusBatteryModeWrite.Dynamic(47077, BatteryModeWriteValueSource.MaxDischargePowerW, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.UInt),
                    },
                    HoldWrites = new()
                    {
                        ModbusBatteryModeWrite.Constant(47100, 0, ModbusWriteFunction.WriteSingleRegister),
                        ModbusBatteryModeWrite.Constant(47077, 0, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.UInt),
                    },
                    ChargeWrites = new()
                    {
                        //47247 forcible charge power
                        ModbusBatteryModeWrite.Dynamic(47247, BatteryModeWriteValueSource.MaxChargePowerW, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.UInt),
                        //47083 forced charging and discharging period, minutes
                        ModbusBatteryModeWrite.Constant(47083, 1, ModbusWriteFunction.WriteSingleRegister),
                        //47246 forcible charge/discharge setting mode: 0 = duration
                        ModbusBatteryModeWrite.Constant(47246, 0, ModbusWriteFunction.WriteSingleRegister),
                        //47100 forcible charge/discharge: 1 = charge
                        ModbusBatteryModeWrite.Constant(47100, 1, ModbusWriteFunction.WriteSingleRegister),
                    },
                },
            }
        },
        //evcc template huawei-sun2000-inverter (without battery)
        {
            TemplateValueGatherType.HuaweiSun2000InverterModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ReadTimeoutMilliseconds = 15000,
                ConnectDelayMilliseconds = 1000,
                ValueRegisters = new()
                {
                    //32080 active generation power AC
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 32080, Length = 2, UsedFor = ValueUsage.InverterPower, NotAvailableValue = Int32Nan },
                },
            }
        },
        //evcc template goodwe-hybrid (ET/EH/BH/BT). Only battery 1 is supported.
        {
            TemplateValueGatherType.GoodweHybridInverterModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ValueRegisters = new()
                {
                    //36025 meter total active power: positive = export
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 36025, Length = 2, UsedFor = ValueUsage.GridPower },
                    //35105/35109/35113/35117 PV1-4 power
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UInt, Address = 35105, Length = 2, UsedFor = ValueUsage.InverterPower, NotAvailableValue = Uint32Nan },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UInt, Address = 35109, Length = 2, UsedFor = ValueUsage.InverterPower, NotAvailableValue = Uint32Nan },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UInt, Address = 35113, Length = 2, UsedFor = ValueUsage.InverterPower, NotAvailableValue = Uint32Nan },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UInt, Address = 35117, Length = 2, UsedFor = ValueUsage.InverterPower, NotAvailableValue = Uint32Nan },
                    //35182 battery 1 power: positive = discharging
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 35182, Length = 2, UsedFor = ValueUsage.HomeBatteryPower, Operator = ValueOperator.Minus },
                    //37007 battery 1 soc
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UShort, Address = 37007, Length = 1, UsedFor = ValueUsage.HomeBatterySoc, NotAvailableValue = Uint16Nan },
                },
                BatteryControl = new()
                {
                    NormalWrites = new()
                    {
                        //47511 EMS power mode: 1 = normal operation
                        ModbusBatteryModeWrite.Constant(47511, 1, ModbusWriteFunction.WriteSingleRegister),
                        //47512 EMS power set: max allowed power from grid in W
                        ModbusBatteryModeWrite.Constant(47512, 0, ModbusWriteFunction.WriteMultipleRegisters),
                    },
                    HoldWrites = new()
                    {
                        //47511 EMS power mode: 2 = charge PV mode. With EMS power set 0 only PV is used to charge.
                        ModbusBatteryModeWrite.Constant(47511, 2, ModbusWriteFunction.WriteSingleRegister),
                        ModbusBatteryModeWrite.Constant(47512, 0, ModbusWriteFunction.WriteMultipleRegisters),
                    },
                    ChargeWrites = new()
                    {
                        ModbusBatteryModeWrite.Constant(47511, 2, ModbusWriteFunction.WriteSingleRegister),
                        ModbusBatteryModeWrite.Dynamic(47512, BatteryModeWriteValueSource.MaxChargePowerW, ModbusWriteFunction.WriteMultipleRegisters),
                    },
                },
            }
        },
        //evcc template goodwe-dt (SDT/DT)
        {
            TemplateValueGatherType.GoodweDtInverterModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ValueRegisters = new()
                {
                    //781 actual power
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UShort, Address = 781, Length = 1, UsedFor = ValueUsage.InverterPower },
                },
            }
        },
        //evcc template growatt-hybrid (SPH). Battery control requires a one time manual setup: registers 1100, 1101
        //and 1102 need to be set to 0, 5947 and 0 within a single write multiple (FC 16) transaction.
        {
            TemplateValueGatherType.GrowattSphHybridInverterModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ValueRegisters = new()
                {
                    //1029 AC power to grid - 1021 AC power to user, 0.1 W: positive = export
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UInt, Address = 1029, Length = 2, UsedFor = ValueUsage.GridPower, CorrectionFactor = 0.1m },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UInt, Address = 1021, Length = 2, UsedFor = ValueUsage.GridPower, Operator = ValueOperator.Minus, CorrectionFactor = 0.1m },
                    //1 PV input power, 0.1 W
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UInt, Address = 1, Length = 2, UsedFor = ValueUsage.InverterPower, CorrectionFactor = 0.1m },
                    //1011 charge power - 1009 discharge power, 0.1 W: positive = charging
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UInt, Address = 1011, Length = 2, UsedFor = ValueUsage.HomeBatteryPower, CorrectionFactor = 0.1m },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UInt, Address = 1009, Length = 2, UsedFor = ValueUsage.HomeBatteryPower, Operator = ValueOperator.Minus, CorrectionFactor = 0.1m },
                    //1014 soc
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UShort, Address = 1014, Length = 1, UsedFor = ValueUsage.HomeBatterySoc },
                },
                BatteryControl = new()
                {
                    NormalWrites = new()
                    {
                        //1102 battery first enabled, 1092 AC charge enabled
                        ModbusBatteryModeWrite.Constant(1102, 0, ModbusWriteFunction.WriteMultipleRegisters),
                        ModbusBatteryModeWrite.Constant(1092, 0, ModbusWriteFunction.WriteMultipleRegisters),
                    },
                    HoldWrites = new()
                    {
                        ModbusBatteryModeWrite.Constant(1102, 1, ModbusWriteFunction.WriteMultipleRegisters),
                        ModbusBatteryModeWrite.Constant(1092, 0, ModbusWriteFunction.WriteMultipleRegisters),
                    },
                    ChargeWrites = new()
                    {
                        ModbusBatteryModeWrite.Constant(1102, 1, ModbusWriteFunction.WriteMultipleRegisters),
                        ModbusBatteryModeWrite.Constant(1092, 1, ModbusWriteFunction.WriteMultipleRegisters),
                    },
                },
            }
        },
        //evcc template growatt-hybrid-tlxh (TL-X(H))
        {
            TemplateValueGatherType.GrowattTlxhHybridInverterModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ValueRegisters = new()
                {
                    //3043 total reverse power - 3041 total forward power, 0.1 W: positive = export
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UInt, Address = 3043, Length = 2, UsedFor = ValueUsage.GridPower, CorrectionFactor = 0.1m },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UInt, Address = 3041, Length = 2, UsedFor = ValueUsage.GridPower, Operator = ValueOperator.Minus, CorrectionFactor = 0.1m },
                    //3005/3009/3013/3017 PV1-4 power, 0.1 W
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UInt, Address = 3005, Length = 2, UsedFor = ValueUsage.InverterPower, CorrectionFactor = 0.1m },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UInt, Address = 3009, Length = 2, UsedFor = ValueUsage.InverterPower, CorrectionFactor = 0.1m },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UInt, Address = 3013, Length = 2, UsedFor = ValueUsage.InverterPower, CorrectionFactor = 0.1m },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UInt, Address = 3017, Length = 2, UsedFor = ValueUsage.InverterPower, CorrectionFactor = 0.1m },
                    //3180 charge power - 3178 discharge power, 0.1 W: positive = charging
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UInt, Address = 3180, Length = 2, UsedFor = ValueUsage.HomeBatteryPower, CorrectionFactor = 0.1m },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UInt, Address = 3178, Length = 2, UsedFor = ValueUsage.HomeBatteryPower, Operator = ValueOperator.Minus, CorrectionFactor = 0.1m },
                    //3171 soc
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UShort, Address = 3171, Length = 1, UsedFor = ValueUsage.HomeBatterySoc },
                },
                BatteryControl = new()
                {
                    //3038/3039 battery first time slot: start and end time words need to be written in a single
                    //FC 16 transaction. Bit 15 of the start word enables the slot.
                    NormalWrites = new()
                    {
                        //(8192 << 16) | 5947: slot disabled
                        ModbusBatteryModeWrite.Constant(3038, 536876859, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.UInt),
                        //3049 AC charge
                        ModbusBatteryModeWrite.Constant(3049, 0, ModbusWriteFunction.WriteMultipleRegisters),
                    },
                    HoldWrites = new()
                    {
                        //(40960 << 16) | 5947: slot enabled
                        ModbusBatteryModeWrite.Constant(3038, 2684360507, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.UInt),
                        ModbusBatteryModeWrite.Constant(3049, 0, ModbusWriteFunction.WriteMultipleRegisters),
                    },
                    ChargeWrites = new()
                    {
                        ModbusBatteryModeWrite.Constant(3038, 2684360507, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.UInt),
                        ModbusBatteryModeWrite.Constant(3049, 1, ModbusWriteFunction.WriteMultipleRegisters),
                    },
                },
            }
        },
        //evcc template deye-hybrid-3p (Deye/Sunsynk 3p). Values assume a low voltage battery, only storage unit 1 is
        //supported. Battery control requires additional vendor specific settings and is not supported yet.
        {
            TemplateValueGatherType.DeyeHybridInverterModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ValueRegisters = new()
                {
                    //625 grid side total power: positive = import
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Short, Address = 625, Length = 1, UsedFor = ValueUsage.GridPower, Operator = ValueOperator.Minus },
                    //672-675 PV1-4 input power
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UShort, Address = 672, Length = 1, UsedFor = ValueUsage.InverterPower },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UShort, Address = 673, Length = 1, UsedFor = ValueUsage.InverterPower },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UShort, Address = 674, Length = 1, UsedFor = ValueUsage.InverterPower },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UShort, Address = 675, Length = 1, UsedFor = ValueUsage.InverterPower },
                    //590 battery output power: positive = discharging
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Short, Address = 590, Length = 1, UsedFor = ValueUsage.HomeBatteryPower, Operator = ValueOperator.Minus },
                    //588 battery capacity (soc)
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UShort, Address = 588, Length = 1, UsedFor = ValueUsage.HomeBatterySoc },
                },
            }
        },
        //evcc template fox-ess-h3
        {
            TemplateValueGatherType.FoxEssH3HybridInverterModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ValueRegisters = new()
                {
                    //31026-31028 meter power R/S/T: positive = export
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Short, Address = 31026, Length = 1, UsedFor = ValueUsage.GridPower },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Short, Address = 31027, Length = 1, UsedFor = ValueUsage.GridPower },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Short, Address = 31028, Length = 1, UsedFor = ValueUsage.GridPower },
                    //31002/31005 PV1/PV2 power
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Short, Address = 31002, Length = 1, UsedFor = ValueUsage.InverterPower },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Short, Address = 31005, Length = 1, UsedFor = ValueUsage.InverterPower },
                    //31036 battery charge/discharge power: positive = discharging
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Short, Address = 31036, Length = 1, UsedFor = ValueUsage.HomeBatteryPower, Operator = ValueOperator.Minus },
                    //31038 soc
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Short, Address = 31038, Length = 1, UsedFor = ValueUsage.HomeBatterySoc },
                },
                //Reserve based control via the limit soc register: the battery does not discharge below the limit.
                BatteryControl = new()
                {
                    NormalWrites = new()
                    {
                        ModbusBatteryModeWrite.Dynamic(41009, BatteryModeWriteValueSource.MinSoc, ModbusWriteFunction.WriteSingleRegister),
                    },
                    HoldWrites = new()
                    {
                        ModbusBatteryModeWrite.Dynamic(41009, BatteryModeWriteValueSource.CurrentSoc, ModbusWriteFunction.WriteSingleRegister),
                    },
                    ChargeWrites = new()
                    {
                        ModbusBatteryModeWrite.Dynamic(41009, BatteryModeWriteValueSource.MaxChargeSoc, ModbusWriteFunction.WriteSingleRegister),
                    },
                },
            }
        },
        //evcc template solax (Hybrid X1/X3 G3/G4, also covers Qcells Q.HOME ESS HYB-G3): word swapped 32 bit
        //registers. A third PV input (MPPT3) is not supported. Only battery 1 is supported.
        {
            TemplateValueGatherType.SolaxHybridInverterModbus, new()
            {
                Endianess = ModbusEndianess.LittleEndian,
                ValueRegisters = new()
                {
                    //70 feed in power (meter): positive = export
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Int, Address = 70, Length = 2, UsedFor = ValueUsage.GridPower },
                    //10/11 PV1/PV2 DC power
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UShort, Address = 10, Length = 1, UsedFor = ValueUsage.InverterPower },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UShort, Address = 11, Length = 1, UsedFor = ValueUsage.InverterPower },
                    //22 battery 1 power: positive = charging
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Short, Address = 22, Length = 1, UsedFor = ValueUsage.HomeBatteryPower },
                    //28 battery 1 capacity (soc)
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UShort, Address = 28, Length = 1, UsedFor = ValueUsage.HomeBatterySoc },
                },
                BatteryControl = new()
                {
                    NormalWrites = new()
                    {
                        //0x001F solar charge use mode: 0 = self use
                        ModbusBatteryModeWrite.Constant(0x001F, 0, ModbusWriteFunction.WriteSingleRegister),
                    },
                    HoldWrites = new()
                    {
                        //0x0020 manual mode: 0 = stop force charge and discharge
                        ModbusBatteryModeWrite.Constant(0x0020, 0, ModbusWriteFunction.WriteSingleRegister),
                        //0x001F solar charge use mode: 3 = manual mode
                        ModbusBatteryModeWrite.Constant(0x001F, 3, ModbusWriteFunction.WriteSingleRegister),
                    },
                    ChargeWrites = new()
                    {
                        //0x0056 wake battery from standby
                        ModbusBatteryModeWrite.Constant(0x0056, 1, ModbusWriteFunction.WriteSingleRegister),
                        //0x0020 manual mode: 1 = force charge
                        ModbusBatteryModeWrite.Constant(0x0020, 1, ModbusWriteFunction.WriteSingleRegister),
                        ModbusBatteryModeWrite.Constant(0x001F, 3, ModbusWriteFunction.WriteSingleRegister),
                    },
                },
            }
        },
        //evcc template alpha-ess-smile (Storion SMILE). Battery control requires a one time setup of continuous grid
        //charging time slots via the app or web interface with the "grid charging" switch disabled.
        {
            TemplateValueGatherType.AlphaEssSmileModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ValueRegisters = new()
                {
                    //33 total active power (grid meter): positive = import
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 33, Length = 2, UsedFor = ValueUsage.GridPower, Operator = ValueOperator.Minus },
                    //161 total active power (PV meter) + 1055-1075 PV1-6 power
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 161, Length = 2, UsedFor = ValueUsage.InverterPower },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UInt, Address = 1055, Length = 2, UsedFor = ValueUsage.InverterPower },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UInt, Address = 1059, Length = 2, UsedFor = ValueUsage.InverterPower },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UInt, Address = 1063, Length = 2, UsedFor = ValueUsage.InverterPower },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UInt, Address = 1067, Length = 2, UsedFor = ValueUsage.InverterPower },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UInt, Address = 1071, Length = 2, UsedFor = ValueUsage.InverterPower },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UInt, Address = 1075, Length = 2, UsedFor = ValueUsage.InverterPower },
                    //294 battery power: positive = discharging
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Short, Address = 294, Length = 1, UsedFor = ValueUsage.HomeBatteryPower, Operator = ValueOperator.Minus },
                    //258 battery soc, 0.1 %
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UShort, Address = 258, Length = 1, UsedFor = ValueUsage.HomeBatterySoc, CorrectionFactor = 0.1m },
                },
                BatteryControl = new()
                {
                    NormalWrites = new()
                    {
                        //2127 time period control flag
                        ModbusBatteryModeWrite.Constant(2127, 0, ModbusWriteFunction.WriteMultipleRegisters),
                    },
                    HoldWrites = new()
                    {
                        //Grid charging with the min soc as target: does not start charging but prevents discharging
                        ModbusBatteryModeWrite.Constant(2127, 1, ModbusWriteFunction.WriteMultipleRegisters),
                        //2133 charge cut soc
                        ModbusBatteryModeWrite.Dynamic(2133, BatteryModeWriteValueSource.MinSoc, ModbusWriteFunction.WriteMultipleRegisters),
                    },
                    ChargeWrites = new()
                    {
                        ModbusBatteryModeWrite.Constant(2127, 1, ModbusWriteFunction.WriteMultipleRegisters),
                        ModbusBatteryModeWrite.Dynamic(2133, BatteryModeWriteValueSource.MaxChargeSoc, ModbusWriteFunction.WriteMultipleRegisters),
                    },
                },
            }
        },
        //evcc template saj-h2 (SAJ H2, Ampere.StoragePro)
        {
            TemplateValueGatherType.SajH2HybridInverterModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ValueRegisters = new()
                {
                    //0x40AD system grid power: positive = import
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Short, Address = 0x40AD, Length = 1, UsedFor = ValueUsage.GridPower, Operator = ValueOperator.Minus },
                    //0x40A5 total PV power
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Short, Address = 0x40A5, Length = 1, UsedFor = ValueUsage.InverterPower },
                    //0x40A6 total battery power: positive = discharging
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Short, Address = 0x40A6, Length = 1, UsedFor = ValueUsage.HomeBatteryPower, Operator = ValueOperator.Minus },
                    //0xA00C battery 1 soc, 0.01 %
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UShort, Address = 0xA00C, Length = 1, UsedFor = ValueUsage.HomeBatterySoc, CorrectionFactor = 0.01m },
                },
                BatteryControl = new()
                {
                    NormalWrites = new()
                    {
                        //13895 app mode: 2 = self use (default)
                        ModbusBatteryModeWrite.Constant(13895, 2, ModbusWriteFunction.WriteSingleRegister),
                        //13905 battery soc keep limit
                        ModbusBatteryModeWrite.Dynamic(13905, BatteryModeWriteValueSource.MinSoc, ModbusWriteFunction.WriteSingleRegister),
                    },
                    HoldWrites = new()
                    {
                        ModbusBatteryModeWrite.Constant(13895, 2, ModbusWriteFunction.WriteSingleRegister),
                        //Keeping the soc limit at the max charge soc prevents discharging
                        ModbusBatteryModeWrite.Dynamic(13905, BatteryModeWriteValueSource.MaxChargeSoc, ModbusWriteFunction.WriteSingleRegister),
                    },
                    ChargeWrites = new()
                    {
                        //13895 app mode: 1 = time mode
                        ModbusBatteryModeWrite.Constant(13895, 1, ModbusWriteFunction.WriteSingleRegister),
                        //13828 charge time enable control
                        ModbusBatteryModeWrite.Constant(13828, 1, ModbusWriteFunction.WriteSingleRegister),
                        //13830/13831 first charge start (00:00) and end (23:59) time
                        ModbusBatteryModeWrite.Constant(13830, 0, ModbusWriteFunction.WriteSingleRegister),
                        ModbusBatteryModeWrite.Constant(13831, 0x173B, ModbusWriteFunction.WriteSingleRegister),
                        //13832 first charge power/soc (0x7F / 100 %)
                        ModbusBatteryModeWrite.Constant(13832, 0x7F64, ModbusWriteFunction.WriteSingleRegister),
                    },
                },
            }
        },
        //evcc template marstek-venus-e-v3 (battery only device, provides no grid or pv values)
        {
            TemplateValueGatherType.MarstekVenusModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ValueRegisters = new()
                {
                    //30006 AC power: positive = discharging
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Short, Address = 30006, Length = 1, UsedFor = ValueUsage.HomeBatteryPower, Operator = ValueOperator.Minus },
                    //34002 battery soc, 0.1 %
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UShort, Address = 34002, Length = 1, UsedFor = ValueUsage.HomeBatterySoc, CorrectionFactor = 0.1m },
                },
                BatteryControl = new()
                {
                    NormalWrites = new()
                    {
                        //42000 RS485 control mode: 21930 = enable, 21947 = disable
                        ModbusBatteryModeWrite.Constant(42000, 21930, ModbusWriteFunction.WriteSingleRegister),
                        //43000 user work mode: 1 = anti feed (self consumption)
                        ModbusBatteryModeWrite.Constant(43000, 1, ModbusWriteFunction.WriteSingleRegister),
                        ModbusBatteryModeWrite.Constant(42000, 21947, ModbusWriteFunction.WriteSingleRegister),
                    },
                    //RS485 control mode stays enabled in hold and charge because disabling it resets the device.
                    HoldWrites = new()
                    {
                        ModbusBatteryModeWrite.Constant(42000, 21930, ModbusWriteFunction.WriteSingleRegister),
                        //42010 force charge/discharge: 0 = stop
                        ModbusBatteryModeWrite.Constant(42010, 0, ModbusWriteFunction.WriteSingleRegister),
                    },
                    ChargeWrites = new()
                    {
                        ModbusBatteryModeWrite.Constant(42000, 21930, ModbusWriteFunction.WriteSingleRegister),
                        //42010 force charge/discharge: 1 = charge
                        ModbusBatteryModeWrite.Constant(42010, 1, ModbusWriteFunction.WriteSingleRegister),
                        //42020 forcible charge power
                        ModbusBatteryModeWrite.Dynamic(42020, BatteryModeWriteValueSource.MaxChargePowerW, ModbusWriteFunction.WriteSingleRegister),
                    },
                },
            }
        },
        //evcc template sma-sbs-modbus (Sunny Boy Storage, battery only device)
        {
            TemplateValueGatherType.SmaSunnyBoyStorageModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ReadTimeoutMilliseconds = 10000,
                ValueRegisters = new()
                {
                    //30775 AC power: positive = discharging
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Int, Address = 30775, Length = 2, UsedFor = ValueUsage.HomeBatteryPower, Operator = ValueOperator.Minus, NotAvailableValue = Int32Nan },
                    //30845 battery soc
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UInt, Address = 30845, Length = 2, UsedFor = ValueUsage.HomeBatterySoc, NotAvailableValue = Uint32Nan },
                },
                BatteryControl = new()
                {
                    //External setpoints fall back to the default behavior when not refreshed
                    RewriteInterval = TimeSpan.FromSeconds(60),
                    NormalWrites = new()
                    {
                        //40236 CmpBMS operating mode: 2424 = default
                        ModbusBatteryModeWrite.Constant(40236, 2424, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.UInt),
                        ModbusBatteryModeWrite.Constant(40793, 0, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.UInt),
                        ModbusBatteryModeWrite.Dynamic(40795, BatteryModeWriteValueSource.MaxChargePowerW, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.UInt),
                        ModbusBatteryModeWrite.Constant(40797, 0, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.UInt),
                        ModbusBatteryModeWrite.Dynamic(40799, BatteryModeWriteValueSource.MaxDischargePowerW, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.UInt),
                        ModbusBatteryModeWrite.Constant(40801, 0, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.UInt),
                    },
                    HoldWrites = new()
                    {
                        ModbusBatteryModeWrite.Constant(40236, 2424, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.UInt),
                        ModbusBatteryModeWrite.Constant(40793, 0, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.UInt),
                        ModbusBatteryModeWrite.Dynamic(40795, BatteryModeWriteValueSource.MaxChargePowerW, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.UInt),
                        ModbusBatteryModeWrite.Constant(40797, 0, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.UInt),
                        ModbusBatteryModeWrite.Constant(40799, 0, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.UInt),
                        ModbusBatteryModeWrite.Constant(40801, 0, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.UInt),
                    },
                    ChargeWrites = new()
                    {
                        //40236 CmpBMS operating mode: 2289 = battery charge
                        ModbusBatteryModeWrite.Constant(40236, 2289, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.UInt),
                        ModbusBatteryModeWrite.Dynamic(40793, BatteryModeWriteValueSource.MaxChargePowerW, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.UInt),
                        ModbusBatteryModeWrite.Dynamic(40795, BatteryModeWriteValueSource.MaxChargePowerW, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.UInt),
                        ModbusBatteryModeWrite.Constant(40797, 0, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.UInt),
                        ModbusBatteryModeWrite.Constant(40799, 0, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.UInt),
                        ModbusBatteryModeWrite.Constant(40801, 0, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.UInt),
                    },
                },
            }
        },
    };
}
