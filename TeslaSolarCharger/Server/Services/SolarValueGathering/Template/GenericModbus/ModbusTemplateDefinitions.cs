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
        //evcc template sma-si-modbus (Sunny Island, battery only device). Also covers SBS 3.7/5.0/6.0.
        {
            TemplateValueGatherType.SmaSunnyIslandModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ReadTimeoutMilliseconds = 10000,
                ValueRegisters = new()
                {
                    //30775 AC power: positive = discharging
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Int, Address = 30775, Length = 2, UsedFor = ValueUsage.HomeBatteryPower, Operator = ValueOperator.Minus, NotAvailableValue = Int32Nan },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UInt, Address = 30845, Length = 2, UsedFor = ValueUsage.HomeBatterySoc, NotAvailableValue = Uint32Nan },
                },
                BatteryControl = new()
                {
                    RewriteInterval = TimeSpan.FromSeconds(60),
                    NormalWrites = new()
                    {
                        //40151 external power control: 803 = inactive
                        ModbusBatteryModeWrite.Constant(40151, 803, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.UInt),
                    },
                    HoldWrites = new()
                    {
                        //40149 active power setpoint
                        ModbusBatteryModeWrite.Constant(40149, 0, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.Int),
                        //40151 external power control: 802 = active
                        ModbusBatteryModeWrite.Constant(40151, 802, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.UInt),
                    },
                    ChargeWrites = new()
                    {
                        //Negative active power setpoint charges the battery
                        ModbusBatteryModeWrite.Dynamic(40149, BatteryModeWriteValueSource.MaxChargePowerW, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.Int, factor: -1),
                        ModbusBatteryModeWrite.Constant(40151, 802, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.UInt),
                    },
                },
            }
        },
        //evcc template sma-datamanager
        {
            TemplateValueGatherType.SmaDataManagerModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ReadTimeoutMilliseconds = 10000,
                ValueRegisters = new()
                {
                    //31249 grid power: positive = export
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 31249, Length = 2, UsedFor = ValueUsage.GridPower, NotAvailableValue = Int32Nan },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 30775, Length = 2, UsedFor = ValueUsage.InverterPower, NotAvailableValue = Int32Nan },
                    //31393 battery charge power - 31395 battery discharge power
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UInt, Address = 31393, Length = 2, UsedFor = ValueUsage.HomeBatteryPower, NotAvailableValue = Uint32Nan },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UInt, Address = 31395, Length = 2, UsedFor = ValueUsage.HomeBatteryPower, Operator = ValueOperator.Minus, NotAvailableValue = Uint32Nan },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UInt, Address = 30845, Length = 2, UsedFor = ValueUsage.HomeBatterySoc, NotAvailableValue = Uint32Nan },
                },
            }
        },
        //evcc template sma-webbox
        {
            TemplateValueGatherType.SmaWebBoxModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ReadTimeoutMilliseconds = 10000,
                ValueRegisters = new()
                {
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 30775, Length = 2, UsedFor = ValueUsage.InverterPower, NotAvailableValue = Int32Nan },
                },
            }
        },
        //evcc template sungrow-ihm (iHomeManager): word swapped 32 bit registers
        {
            TemplateValueGatherType.SungrowIhmModbus, new()
            {
                Endianess = ModbusEndianess.LittleEndian,
                ReadTimeoutMilliseconds = 5000,
                ValueRegisters = new()
                {
                    //8156 total active power at grid, 10 W units: positive = import
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Int, Address = 8156, Length = 2, UsedFor = ValueUsage.GridPower, Operator = ValueOperator.Minus, CorrectionFactor = 10 },
                    //8154 total active power, 10 W units
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Int, Address = 8154, Length = 2, UsedFor = ValueUsage.InverterPower, CorrectionFactor = 10 },
                },
            }
        },
        //evcc template huawei-smartlogger. The inverter values are read from unit 0, only storage unit 1 is supported.
        {
            TemplateValueGatherType.HuaweiSmartLoggerModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ReadTimeoutMilliseconds = 15000,
                ConnectDelayMilliseconds = 1000,
                ValueRegisters = new()
                {
                    //32278 active power: positive = import
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 32278, Length = 2, UsedFor = ValueUsage.GridPower, Operator = ValueOperator.Minus },
                    //40521 active power of all inverters, read from the logger unit 0
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 40521, Length = 2, UsedFor = ValueUsage.InverterPower, UnitIdOverride = 0 },
                    //37001 battery power: positive = charging
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 37001, Length = 2, UsedFor = ValueUsage.HomeBatteryPower, NotAvailableValue = Int32Nan },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UShort, Address = 37004, Length = 1, UsedFor = ValueUsage.HomeBatterySoc, CorrectionFactor = 0.1m, NotAvailableValue = Uint16Nan },
                },
                BatteryControl = new()
                {
                    RewriteInterval = TimeSpan.FromSeconds(30),
                    NormalWrites = new()
                    {
                        //47100 forcible charge/discharge: 0 = stop
                        ModbusBatteryModeWrite.Constant(47100, 0, ModbusWriteFunction.WriteSingleRegister),
                    },
                    HoldWrites = new()
                    {
                        //Forced discharge with 0 W blocks discharging
                        ModbusBatteryModeWrite.Constant(47100, 2, ModbusWriteFunction.WriteSingleRegister),
                        ModbusBatteryModeWrite.Constant(47246, 0, ModbusWriteFunction.WriteSingleRegister),
                        ModbusBatteryModeWrite.Constant(47083, 1, ModbusWriteFunction.WriteSingleRegister),
                        ModbusBatteryModeWrite.Constant(47249, 0, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.UInt),
                    },
                    ChargeWrites = new()
                    {
                        ModbusBatteryModeWrite.Constant(47100, 1, ModbusWriteFunction.WriteSingleRegister),
                        ModbusBatteryModeWrite.Constant(47246, 0, ModbusWriteFunction.WriteSingleRegister),
                        ModbusBatteryModeWrite.Constant(47083, 1, ModbusWriteFunction.WriteSingleRegister),
                        ModbusBatteryModeWrite.Dynamic(47247, BatteryModeWriteValueSource.MaxChargePowerW, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.UInt),
                        //47087 charge from grid: enabled
                        ModbusBatteryModeWrite.Constant(47087, 1, ModbusWriteFunction.WriteSingleRegister),
                    },
                },
            }
        },
        //evcc template huawei-emma: all values are read from unit 0
        {
            TemplateValueGatherType.HuaweiEmmaModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ReadTimeoutMilliseconds = 15000,
                ConnectDelayMilliseconds = 1000,
                ValueRegisters = new()
                {
                    //31657 power of built-in energy sensor: positive = import
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 31657, Length = 2, UsedFor = ValueUsage.GridPower, Operator = ValueOperator.Minus },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 30354, Length = 2, UsedFor = ValueUsage.InverterPower },
                    //30360 battery power: positive = charging
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 30360, Length = 2, UsedFor = ValueUsage.HomeBatteryPower, NotAvailableValue = Int32Nan },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UShort, Address = 30368, Length = 1, UsedFor = ValueUsage.HomeBatterySoc, CorrectionFactor = 0.01m, NotAvailableValue = Uint16Nan },
                },
            }
        },
        //evcc template deye-storage (Deye/Sunsynk single phase storage inverters)
        {
            TemplateValueGatherType.DeyeStorageModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ValueRegisters = new()
                {
                    //169 total grid power: positive = import
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Short, Address = 169, Length = 1, UsedFor = ValueUsage.GridPower, Operator = ValueOperator.Minus },
                    //186-189 PV1-4 input power
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UShort, Address = 186, Length = 1, UsedFor = ValueUsage.InverterPower },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UShort, Address = 187, Length = 1, UsedFor = ValueUsage.InverterPower },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UShort, Address = 188, Length = 1, UsedFor = ValueUsage.InverterPower },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UShort, Address = 189, Length = 1, UsedFor = ValueUsage.InverterPower },
                    //190 battery output power: positive = discharging
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Short, Address = 190, Length = 1, UsedFor = ValueUsage.HomeBatteryPower, Operator = ValueOperator.Minus },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UShort, Address = 184, Length = 1, UsedFor = ValueUsage.HomeBatterySoc },
                },
            }
        },
        //evcc templates deye-string and deye-mi (identical registers): word swapped 32 bit registers
        {
            TemplateValueGatherType.DeyeStringInverterModbus, new()
            {
                Endianess = ModbusEndianess.LittleEndian,
                ValueRegisters = new()
                {
                    //86 output active power, 0.1 W
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UInt, Address = 86, Length = 2, UsedFor = ValueUsage.InverterPower, CorrectionFactor = 0.1m },
                },
            }
        },
        //evcc template fox-ess-h1 (values via Modbus TCP register set)
        {
            TemplateValueGatherType.FoxEssH1Modbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ValueRegisters = new()
                {
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Short, Address = 31002, Length = 1, UsedFor = ValueUsage.InverterPower },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Short, Address = 31005, Length = 1, UsedFor = ValueUsage.InverterPower },
                    //31022 battery charge/discharge: positive = discharging
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Short, Address = 31022, Length = 1, UsedFor = ValueUsage.HomeBatteryPower, Operator = ValueOperator.Minus },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Short, Address = 31024, Length = 1, UsedFor = ValueUsage.HomeBatterySoc },
                },
            }
        },
        //evcc template fox-ess-h3-smart
        {
            TemplateValueGatherType.FoxEssH3SmartModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ValueRegisters = new()
                {
                    //38816/38818/38820 meter power R/S/T, 0.1 W: positive = export
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 38816, Length = 2, UsedFor = ValueUsage.GridPower, CorrectionFactor = 0.1m },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 38818, Length = 2, UsedFor = ValueUsage.GridPower, CorrectionFactor = 0.1m },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 38820, Length = 2, UsedFor = ValueUsage.GridPower, CorrectionFactor = 0.1m },
                    //39279-39289 PV1-6
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 39279, Length = 2, UsedFor = ValueUsage.InverterPower },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 39281, Length = 2, UsedFor = ValueUsage.InverterPower },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 39283, Length = 2, UsedFor = ValueUsage.InverterPower },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 39285, Length = 2, UsedFor = ValueUsage.InverterPower },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 39287, Length = 2, UsedFor = ValueUsage.InverterPower },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 39289, Length = 2, UsedFor = ValueUsage.InverterPower },
                    //39237 battery charge/discharge: positive = discharging
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 39237, Length = 2, UsedFor = ValueUsage.HomeBatteryPower, Operator = ValueOperator.Minus },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Short, Address = 37612, Length = 1, UsedFor = ValueUsage.HomeBatterySoc },
                },
                //Reserve based control via the min soc on grid register
                BatteryControl = new()
                {
                    NormalWrites = new()
                    {
                        ModbusBatteryModeWrite.Dynamic(46611, BatteryModeWriteValueSource.MinSoc, ModbusWriteFunction.WriteSingleRegister),
                    },
                    HoldWrites = new()
                    {
                        ModbusBatteryModeWrite.Dynamic(46611, BatteryModeWriteValueSource.CurrentSoc, ModbusWriteFunction.WriteSingleRegister),
                    },
                    ChargeWrites = new()
                    {
                        ModbusBatteryModeWrite.Dynamic(46611, BatteryModeWriteValueSource.MaxChargeSoc, ModbusWriteFunction.WriteSingleRegister),
                    },
                },
            }
        },
        //evcc template fox-ess-avocado
        {
            TemplateValueGatherType.FoxEssAvocadoModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ValueRegisters = new()
                {
                    //39168 active power: positive = export
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 39168, Length = 2, UsedFor = ValueUsage.GridPower },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 39118, Length = 2, UsedFor = ValueUsage.InverterPower },
                    //39237 battery combined power: positive = charging
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 39237, Length = 2, UsedFor = ValueUsage.HomeBatteryPower },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Short, Address = 39424, Length = 1, UsedFor = ValueUsage.HomeBatterySoc },
                },
                BatteryControl = new()
                {
                    //Remote control times out on the device after 60 seconds
                    RewriteInterval = TimeSpan.FromSeconds(30),
                    NormalWrites = new()
                    {
                        //46001 remote control mode: 0 = disabled
                        ModbusBatteryModeWrite.Constant(46001, 0, ModbusWriteFunction.WriteSingleRegister),
                        ModbusBatteryModeWrite.Dynamic(46609, BatteryModeWriteValueSource.MinSoc, ModbusWriteFunction.WriteSingleRegister),
                        ModbusBatteryModeWrite.Dynamic(46610, BatteryModeWriteValueSource.MaxChargeSoc, ModbusWriteFunction.WriteSingleRegister),
                    },
                    HoldWrites = new()
                    {
                        //46001 remote control mode: 5 = battery discharge, with 0 W active power
                        ModbusBatteryModeWrite.Constant(46001, 5, ModbusWriteFunction.WriteSingleRegister),
                        ModbusBatteryModeWrite.Constant(46002, 60, ModbusWriteFunction.WriteSingleRegister),
                        ModbusBatteryModeWrite.Constant(46003, 0, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.Int),
                    },
                    ChargeWrites = new()
                    {
                        //46001 remote control mode: 7 = battery charging
                        ModbusBatteryModeWrite.Constant(46001, 7, ModbusWriteFunction.WriteSingleRegister),
                        ModbusBatteryModeWrite.Constant(46002, 60, ModbusWriteFunction.WriteSingleRegister),
                        ModbusBatteryModeWrite.Dynamic(46003, BatteryModeWriteValueSource.MaxChargePowerW, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.Int),
                    },
                },
            }
        },
        //evcc template sofarsolar (ME3000SP and older HYD models)
        {
            TemplateValueGatherType.SofarSolarModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ValueRegisters = new()
                {
                    //0x0212 feed in/out power: positive = export
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 0x0212, Length = 2, UsedFor = ValueUsage.GridPower, CorrectionFactor = 0.01m },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UShort, Address = 0x0215, Length = 1, UsedFor = ValueUsage.InverterPower, CorrectionFactor = 0.01m },
                    //0x020D battery power, 10 W units: positive = charging
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Short, Address = 0x020D, Length = 1, UsedFor = ValueUsage.HomeBatteryPower, CorrectionFactor = 10 },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UShort, Address = 0x0210, Length = 1, UsedFor = ValueUsage.HomeBatterySoc },
                },
            }
        },
        //evcc template sofarsolar-g3 (HYD G3). Only battery 1 is supported, a third PV input is not supported.
        //Battery control requires raw multi register writes and is not supported yet.
        {
            TemplateValueGatherType.SofarSolarG3HybridModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ValueRegisters = new()
                {
                    //0x0488 active power at PCC, 10 W units: positive = export
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Short, Address = 0x0488, Length = 1, UsedFor = ValueUsage.GridPower, CorrectionFactor = 10 },
                    //PV1-4 power, 10 W units
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UShort, Address = 0x586, Length = 1, UsedFor = ValueUsage.InverterPower, CorrectionFactor = 10 },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UShort, Address = 0x589, Length = 1, UsedFor = ValueUsage.InverterPower, CorrectionFactor = 10 },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UShort, Address = 0x58C, Length = 1, UsedFor = ValueUsage.InverterPower, CorrectionFactor = 10 },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UShort, Address = 0x58E, Length = 1, UsedFor = ValueUsage.InverterPower, CorrectionFactor = 10 },
                    //0x0606 battery 1 power, 10 W units: positive = charging
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Short, Address = 0x0606, Length = 1, UsedFor = ValueUsage.HomeBatteryPower, CorrectionFactor = 10 },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UShort, Address = 0x0608, Length = 1, UsedFor = ValueUsage.HomeBatterySoc },
                },
            }
        },
        //evcc template varta (element, pulse, one)
        {
            TemplateValueGatherType.VartaModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ValueRegisters = new()
                {
                    //1078 grid power: positive = export
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Short, Address = 1078, Length = 1, UsedFor = ValueUsage.GridPower },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UShort, Address = 1102, Length = 1, UsedFor = ValueUsage.InverterPower },
                    //1066 active power: positive = charging
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Short, Address = 1066, Length = 1, UsedFor = ValueUsage.HomeBatteryPower },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Short, Address = 1068, Length = 1, UsedFor = ValueUsage.HomeBatterySoc },
                },
                BatteryControl = new()
                {
                    RewriteInterval = TimeSpan.FromSeconds(120),
                    NormalWrites = new()
                    {
                        //1074 max discharge power, needs to be written as negative value
                        ModbusBatteryModeWrite.Dynamic(1074, BatteryModeWriteValueSource.MaxDischargePowerW, ModbusWriteFunction.WriteSingleRegister, ModbusValueType.Short, factor: -1),
                    },
                    HoldWrites = new()
                    {
                        ModbusBatteryModeWrite.Constant(1074, 0, ModbusWriteFunction.WriteSingleRegister, ModbusValueType.Short),
                    },
                    //Forced charging is not supported by the device
                    ChargeWrites = new(),
                },
            }
        },
        //evcc template sax (SAX Power Home)
        {
            TemplateValueGatherType.SaxPowerModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ValueRegisters = new()
                {
                    //48 smart meter power, offset encoded: positive = import
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UShort, Address = 48, Length = 1, UsedFor = ValueUsage.GridPower, Operator = ValueOperator.Minus, Offset = -16384 },
                    //47 battery power, offset encoded: positive = discharging
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UShort, Address = 47, Length = 1, UsedFor = ValueUsage.HomeBatteryPower, Operator = ValueOperator.Minus, Offset = -16384 },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UShort, Address = 46, Length = 1, UsedFor = ValueUsage.HomeBatterySoc },
                },
                BatteryControl = new()
                {
                    RewriteInterval = TimeSpan.FromSeconds(240),
                    NormalWrites = new()
                    {
                        //43 battery discharging power
                        ModbusBatteryModeWrite.Dynamic(43, BatteryModeWriteValueSource.MaxDischargePowerW, ModbusWriteFunction.WriteMultipleRegisters),
                    },
                    HoldWrites = new()
                    {
                        ModbusBatteryModeWrite.Constant(43, 0, ModbusWriteFunction.WriteMultipleRegisters),
                    },
                    //Forced charging is not supported by the device
                    ChargeWrites = new(),
                },
            }
        },
        //evcc template mtec-eb-gen3 (M-TEC Energy Butler Gen3)
        {
            TemplateValueGatherType.MtecEbGen3Modbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ValueRegisters = new()
                {
                    //11000 meter power: positive = export
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 11000, Length = 2, UsedFor = ValueUsage.GridPower },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UInt, Address = 11028, Length = 2, UsedFor = ValueUsage.InverterPower },
                    //30258 battery power: positive = discharging
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 30258, Length = 2, UsedFor = ValueUsage.HomeBatteryPower, Operator = ValueOperator.Minus },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UShort, Address = 33000, Length = 1, UsedFor = ValueUsage.HomeBatterySoc, CorrectionFactor = 0.01m },
                },
                BatteryControl = new()
                {
                    NormalWrites = new()
                    {
                        //50000 operating mode: 257 = normal
                        ModbusBatteryModeWrite.Constant(50000, 257, ModbusWriteFunction.WriteSingleRegister),
                    },
                    HoldWrites = new()
                    {
                        //50000 operating mode: 258 = eco
                        ModbusBatteryModeWrite.Constant(50000, 258, ModbusWriteFunction.WriteSingleRegister),
                    },
                    ChargeWrites = new()
                    {
                        //50000 operating mode: 259 = usp (charge)
                        ModbusBatteryModeWrite.Constant(50000, 259, ModbusWriteFunction.WriteSingleRegister),
                    },
                },
            }
        },
        //evcc template solarmax-maxstorage: word swapped 32 bit registers
        {
            TemplateValueGatherType.SolarmaxMaxStorageModbus, new()
            {
                Endianess = ModbusEndianess.LittleEndian,
                ValueRegisters = new()
                {
                    //118 feed in power: positive = export
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Int, Address = 118, Length = 2, UsedFor = ValueUsage.GridPower },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Int, Address = 110, Length = 2, UsedFor = ValueUsage.InverterPower },
                    //114 battery power: positive = charging
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Int, Address = 114, Length = 2, UsedFor = ValueUsage.HomeBatteryPower },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Short, Address = 122, Length = 1, UsedFor = ValueUsage.HomeBatterySoc },
                },
                BatteryControl = new()
                {
                    RewriteInterval = TimeSpan.FromSeconds(60),
                    //Register 142 requires a changing value so the setpoints in 140/141 are applied
                    NormalWrites = new()
                    {
                        ModbusBatteryModeWrite.Dynamic(140, BatteryModeWriteValueSource.MaxChargePowerW, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.Short),
                        ModbusBatteryModeWrite.Dynamic(141, BatteryModeWriteValueSource.MaxDischargePowerW, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.Short),
                        ModbusBatteryModeWrite.Constant(142, 0, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.Short),
                    },
                    HoldWrites = new()
                    {
                        ModbusBatteryModeWrite.Constant(140, 0, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.Short),
                        ModbusBatteryModeWrite.Constant(141, 0, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.Short),
                        ModbusBatteryModeWrite.Dynamic(142, BatteryModeWriteValueSource.Random, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.Short),
                    },
                    ChargeWrites = new()
                    {
                        ModbusBatteryModeWrite.Dynamic(140, BatteryModeWriteValueSource.MaxChargePowerW, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.Short),
                        ModbusBatteryModeWrite.Constant(141, 0, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.Short),
                        ModbusBatteryModeWrite.Dynamic(142, BatteryModeWriteValueSource.Random, ModbusWriteFunction.WriteMultipleRegisters, ModbusValueType.Short),
                    },
                },
            }
        },
        //evcc template solarmax-inverter-smt
        {
            TemplateValueGatherType.SolarmaxSmtInverterModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ValueRegisters = new()
                {
                    //4151 PAC, 0.1 W
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UInt, Address = 4151, Length = 2, UsedFor = ValueUsage.InverterPower, CorrectionFactor = 0.1m },
                },
            }
        },
        //evcc template victron-energy (GX devices): all values are read from unit 100 (com.victronenergy.system)
        {
            TemplateValueGatherType.VictronGxModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ValueRegisters = new()
                {
                    //820-822 grid power L1-L3: positive = import
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Short, Address = 820, Length = 1, UsedFor = ValueUsage.GridPower, Operator = ValueOperator.Minus },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Short, Address = 821, Length = 1, UsedFor = ValueUsage.GridPower, Operator = ValueOperator.Minus },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Short, Address = 822, Length = 1, UsedFor = ValueUsage.GridPower, Operator = ValueOperator.Minus },
                    //808-813 AC out/in PV power L1-L3 + 850 DC PV power
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UShort, Address = 808, Length = 1, UsedFor = ValueUsage.InverterPower },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UShort, Address = 809, Length = 1, UsedFor = ValueUsage.InverterPower },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UShort, Address = 810, Length = 1, UsedFor = ValueUsage.InverterPower },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UShort, Address = 811, Length = 1, UsedFor = ValueUsage.InverterPower },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UShort, Address = 812, Length = 1, UsedFor = ValueUsage.InverterPower },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UShort, Address = 813, Length = 1, UsedFor = ValueUsage.InverterPower },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UShort, Address = 850, Length = 1, UsedFor = ValueUsage.InverterPower },
                    //842 battery power: positive = charging
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Short, Address = 842, Length = 1, UsedFor = ValueUsage.HomeBatteryPower },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UShort, Address = 843, Length = 1, UsedFor = ValueUsage.HomeBatterySoc },
                },
                //Reserve based control via the ESS min soc register (0.1 % units)
                BatteryControl = new()
                {
                    NormalWrites = new()
                    {
                        ModbusBatteryModeWrite.Dynamic(2901, BatteryModeWriteValueSource.MinSoc, ModbusWriteFunction.WriteSingleRegister, factor: 10),
                    },
                    HoldWrites = new()
                    {
                        ModbusBatteryModeWrite.Dynamic(2901, BatteryModeWriteValueSource.CurrentSoc, ModbusWriteFunction.WriteSingleRegister, factor: 10),
                    },
                    ChargeWrites = new()
                    {
                        ModbusBatteryModeWrite.Dynamic(2901, BatteryModeWriteValueSource.MaxChargeSoc, ModbusWriteFunction.WriteSingleRegister, factor: 10),
                    },
                },
            }
        },
        //evcc template intilion-scalebloc (battery with integrated grid meter)
        {
            TemplateValueGatherType.IntilionScaleblocModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ValueRegisters = new()
                {
                    //6010/6012/6014 active power L1-L3, 100 W units: positive = import
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Short, Address = 6010, Length = 1, UsedFor = ValueUsage.GridPower, Operator = ValueOperator.Minus, CorrectionFactor = 100 },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Short, Address = 6012, Length = 1, UsedFor = ValueUsage.GridPower, Operator = ValueOperator.Minus, CorrectionFactor = 100 },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Short, Address = 6014, Length = 1, UsedFor = ValueUsage.GridPower, Operator = ValueOperator.Minus, CorrectionFactor = 100 },
                    //5040 system active power, 100 W units: positive = discharging
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Short, Address = 5040, Length = 1, UsedFor = ValueUsage.HomeBatteryPower, Operator = ValueOperator.Minus, CorrectionFactor = 100 },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UShort, Address = 5002, Length = 1, UsedFor = ValueUsage.HomeBatterySoc, CorrectionFactor = 0.1m },
                },
            }
        },
        //evcc template siemens-junelight
        {
            TemplateValueGatherType.SiemensJunelightModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ReadTimeoutMilliseconds = 5000,
                ValueRegisters = new()
                {
                    //14 grid power: positive = import
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 14, Length = 2, UsedFor = ValueUsage.GridPower, Operator = ValueOperator.Minus },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 16, Length = 2, UsedFor = ValueUsage.InverterPower },
                    //6 battery output power: positive = discharging
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 6, Length = 2, UsedFor = ValueUsage.HomeBatteryPower, Operator = ValueOperator.Minus },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 8, Length = 2, UsedFor = ValueUsage.HomeBatterySoc },
                },
            }
        },
        //evcc template storaxe (Ads-tec StoraXe, battery only device)
        {
            TemplateValueGatherType.StoraxeModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ValueRegisters = new()
                {
                    //1 real power, 100 W units: positive = discharging
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Short, Address = 1, Length = 1, UsedFor = ValueUsage.HomeBatteryPower, Operator = ValueOperator.Minus, CorrectionFactor = 100 },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Short, Address = 125, Length = 1, UsedFor = ValueUsage.HomeBatterySoc },
                },
            }
        },
        //evcc template solinteg (also sold as M-TEC, Wattsonic OEM platform with LAN port)
        {
            TemplateValueGatherType.SolintegModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ValueRegisters = new()
                {
                    //11000 total power on meter: positive = export
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 11000, Length = 2, UsedFor = ValueUsage.GridPower },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UInt, Address = 11028, Length = 2, UsedFor = ValueUsage.InverterPower },
                    //30258 battery power: positive = discharging
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 30258, Length = 2, UsedFor = ValueUsage.HomeBatteryPower, Operator = ValueOperator.Minus },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UShort, Address = 33000, Length = 1, UsedFor = ValueUsage.HomeBatterySoc, CorrectionFactor = 0.01m },
                },
                //Reserve based control via the min soc register (0.1 % units)
                BatteryControl = new()
                {
                    NormalWrites = new()
                    {
                        ModbusBatteryModeWrite.Dynamic(52503, BatteryModeWriteValueSource.MinSoc, ModbusWriteFunction.WriteSingleRegister, factor: 10),
                    },
                    HoldWrites = new()
                    {
                        ModbusBatteryModeWrite.Dynamic(52503, BatteryModeWriteValueSource.CurrentSoc, ModbusWriteFunction.WriteSingleRegister, factor: 10),
                    },
                    ChargeWrites = new()
                    {
                        ModbusBatteryModeWrite.Dynamic(52503, BatteryModeWriteValueSource.MaxChargeSoc, ModbusWriteFunction.WriteSingleRegister, factor: 10),
                    },
                },
            }
        },
        //evcc template afore-hybrid (battery values only)
        {
            TemplateValueGatherType.AforeHybridModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ValueRegisters = new()
                {
                    //2007 battery total power: positive = discharging
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Int, Address = 2007, Length = 2, UsedFor = ValueUsage.HomeBatteryPower, Operator = ValueOperator.Minus },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UShort, Address = 2002, Length = 1, UsedFor = ValueUsage.HomeBatterySoc },
                },
            }
        },
        //evcc template anker-solix-x1: word swapped 32 bit registers
        {
            TemplateValueGatherType.AnkerSolixX1Modbus, new()
            {
                Endianess = ModbusEndianess.LittleEndian,
                ValueRegisters = new()
                {
                    //10644 meter total active power: positive = import
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Int, Address = 10644, Length = 2, UsedFor = ValueUsage.GridPower, Operator = ValueOperator.Minus },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Int, Address = 10183, Length = 2, UsedFor = ValueUsage.InverterPower },
                    //10008 battery power: positive = discharging
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Int, Address = 10008, Length = 2, UsedFor = ValueUsage.HomeBatteryPower, Operator = ValueOperator.Minus },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UShort, Address = 10014, Length = 1, UsedFor = ValueUsage.HomeBatterySoc },
                },
            }
        },
        //evcc template ibc-homeone
        {
            TemplateValueGatherType.IbcHomeOneModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ValueRegisters = new()
                {
                    //60038/60040/60042 external meter power L1-L3: positive = import
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Int, Address = 60038, Length = 2, UsedFor = ValueUsage.GridPower, Operator = ValueOperator.Minus },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Int, Address = 60040, Length = 2, UsedFor = ValueUsage.GridPower, Operator = ValueOperator.Minus },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Int, Address = 60042, Length = 2, UsedFor = ValueUsage.GridPower, Operator = ValueOperator.Minus },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UInt, Address = 1600, Length = 2, UsedFor = ValueUsage.InverterPower },
                    //1618 battery power: positive = discharging
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Int, Address = 1618, Length = 2, UsedFor = ValueUsage.HomeBatteryPower, Operator = ValueOperator.Minus },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UShort, Address = 1621, Length = 1, UsedFor = ValueUsage.HomeBatterySoc, CorrectionFactor = 0.01m },
                },
            }
        },
        //evcc template ecoflow-powerocean-modbus: word swapped float registers
        {
            TemplateValueGatherType.EcoflowPowerOceanModbus, new()
            {
                Endianess = ModbusEndianess.LittleEndian,
                ValueRegisters = new()
                {
                    //40521 grid power: positive = import
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Float, Address = 40521, Length = 2, UsedFor = ValueUsage.GridPower, Operator = ValueOperator.Minus },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Float, Address = 40523, Length = 2, UsedFor = ValueUsage.InverterPower },
                    //40525 battery power: positive = charging
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Float, Address = 40525, Length = 2, UsedFor = ValueUsage.HomeBatteryPower },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UShort, Address = 40527, Length = 1, UsedFor = ValueUsage.HomeBatterySoc },
                },
            }
        },
        //evcc template senergy
        {
            TemplateValueGatherType.SenergyInverterModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ValueRegisters = new()
                {
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.UInt, Address = 4151, Length = 2, UsedFor = ValueUsage.InverterPower },
                },
            }
        },
        //evcc template kostal-ksem-inverter (inverter power via Kostal Smart Energy Meter)
        {
            TemplateValueGatherType.KostalKsemInverterModbus, new()
            {
                Endianess = ModbusEndianess.LittleEndian,
                ValueRegisters = new()
                {
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Float, Address = 252, Length = 2, UsedFor = ValueUsage.InverterPower },
                },
            }
        },
        //evcc template solarlog: word swapped 32 bit registers
        {
            TemplateValueGatherType.SolarlogModbus, new()
            {
                Endianess = ModbusEndianess.LittleEndian,
                ValueRegisters = new()
                {
                    //Grid = production (3502) - consumption (3518)
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UInt, Address = 3502, Length = 2, UsedFor = ValueUsage.GridPower },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UInt, Address = 3518, Length = 2, UsedFor = ValueUsage.GridPower, Operator = ValueOperator.Minus },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UInt, Address = 3502, Length = 2, UsedFor = ValueUsage.InverterPower },
                },
            }
        },
        //evcc template plexlog
        {
            TemplateValueGatherType.PlexlogModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ReadTimeoutMilliseconds = 30000,
                ValueRegisters = new()
                {
                    //Grid = production (0) - consumption (2) + battery discharge (37)
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Int, Address = 0, Length = 2, UsedFor = ValueUsage.GridPower },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Int, Address = 2, Length = 2, UsedFor = ValueUsage.GridPower, Operator = ValueOperator.Minus },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Int, Address = 37, Length = 2, UsedFor = ValueUsage.GridPower },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Int, Address = 0, Length = 2, UsedFor = ValueUsage.InverterPower },
                    //37 battery power: positive = discharging
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.Int, Address = 37, Length = 2, UsedFor = ValueUsage.HomeBatteryPower, Operator = ValueOperator.Minus },
                    new() { RegisterType = ModbusRegisterType.InputRegister, ValueType = ModbusValueType.UShort, Address = 36, Length = 1, UsedFor = ValueUsage.HomeBatterySoc },
                },
            }
        },
        //evcc template powerdog
        {
            TemplateValueGatherType.PowerdogModbus, new()
            {
                Endianess = ModbusEndianess.BigEndian,
                ValueRegisters = new()
                {
                    //Grid = pv (40002) - consumption (40026)
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 40002, Length = 2, UsedFor = ValueUsage.GridPower },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 40026, Length = 2, UsedFor = ValueUsage.GridPower, Operator = ValueOperator.Minus },
                    new() { RegisterType = ModbusRegisterType.HoldingRegister, ValueType = ModbusValueType.Int, Address = 40002, Length = 2, UsedFor = ValueUsage.InverterPower },
                },
            }
        },
    };
}
