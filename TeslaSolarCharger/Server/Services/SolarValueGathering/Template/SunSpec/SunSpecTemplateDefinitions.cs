using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Template.SunSpec;

/// <summary>
/// SunSpec value + control maps per vendor, transcribed from the evcc templates
/// (https://github.com/evcc-io/evcc/tree/master/templates/definition/meter).
/// Sign conventions in TSC: GridPower positive = export, HomeBatteryPower positive = charging. evcc uses the
/// opposite conventions, so operators are flipped accordingly.
/// </summary>
public static class SunSpecTemplateDefinitions
{
    private const decimal StorCtlModStop = 0;
    //StorCtl_Mod bit1 = discharge control active
    private const decimal StorCtlModDischargeControl = 2;
    private const decimal ChaGriSetGrid = 1;

    public static IReadOnlyDictionary<TemplateValueGatherType, SunSpecTemplateDefinition> Definitions { get; } = new Dictionary<TemplateValueGatherType, SunSpecTemplateDefinition>()
    {
        //Generic SunSpec inverter (hybrid): grid via meter model, pv via inverter model, battery soc + control via model 124.
        {
            TemplateValueGatherType.SunSpecInverter, new()
            {
                ValueReads = new()
                {
                    new()
                    {
                        UsedFor = ValueUsage.InverterPower,
                        Components = new() { new() { PointFallbacks = new() { "103:W", "113:W", "102:W", "112:W", "101:W", "111:W" } } },
                    },
                    new()
                    {
                        UsedFor = ValueUsage.GridPower,
                        //Meter W is import positive in the SunSpec generic convention
                        Components = new() { new() { PointFallbacks = new() { "203:W", "213:W", "201:W", "211:W" }, Operator = ValueOperator.Minus } },
                    },
                    new()
                    {
                        UsedFor = ValueUsage.HomeBatterySoc,
                        Components = new() { new() { PointFallbacks = new() { "124:ChaState", "802:SoC" } } },
                    },
                },
                BatteryControl = Model124BatteryControl(),
            }
        },
        //Generic SunSpec meter (grid only)
        {
            TemplateValueGatherType.SunSpecMeter, new()
            {
                ValueReads = new()
                {
                    new()
                    {
                        UsedFor = ValueUsage.GridPower,
                        Components = new() { new() { PointFallbacks = new() { "203:W", "213:W", "201:W", "211:W" }, Operator = ValueOperator.Minus } },
                    },
                },
            }
        },
        //evcc template fronius-gen24
        {
            TemplateValueGatherType.FroniusGen24, new()
            {
                ValueReads = new()
                {
                    new()
                    {
                        UsedFor = ValueUsage.InverterPower,
                        //MPPT strings 1 and 2 are the PV strings
                        Components = new()
                        {
                            new() { PointFallbacks = new() { "160:1:DCW" }, OptionalIfMissing = true },
                            new() { PointFallbacks = new() { "160:2:DCW" }, OptionalIfMissing = true },
                        },
                    },
                    new()
                    {
                        UsedFor = ValueUsage.GridPower,
                        Components = new() { new() { PointFallbacks = new() { "201:W", "211:W", "203:W", "213:W" }, Operator = ValueOperator.Minus } },
                    },
                    new()
                    {
                        UsedFor = ValueUsage.HomeBatteryPower,
                        //MPPT string 3 = charge, string 4 = discharge. TSC is charge positive.
                        Components = new()
                        {
                            new() { PointFallbacks = new() { "160:3:DCW" }, OptionalIfMissing = true },
                            new() { PointFallbacks = new() { "160:4:DCW" }, Operator = ValueOperator.Minus, OptionalIfMissing = true },
                        },
                    },
                    new()
                    {
                        UsedFor = ValueUsage.HomeBatterySoc,
                        Components = new() { new() { PointFallbacks = new() { "124:ChaState" } } },
                    },
                },
                BatteryControl = Model124BatteryControl(),
            }
        },
        //evcc template kostal-plenticore-gen2: SunSpec reads, plain register battery control (little endian float32).
        {
            TemplateValueGatherType.KostalPlenticoreGen2, new()
            {
                ValueReads = new()
                {
                    new()
                    {
                        UsedFor = ValueUsage.InverterPower,
                        Components = new()
                        {
                            new() { PointFallbacks = new() { "160:1:DCW" }, OptionalIfMissing = true },
                            new() { PointFallbacks = new() { "160:2:DCW" }, OptionalIfMissing = true },
                            new() { PointFallbacks = new() { "160:3:DCW" }, OptionalIfMissing = true },
                        },
                    },
                    new()
                    {
                        UsedFor = ValueUsage.HomeBatteryPower,
                        //802:W is discharge positive, TSC is charge positive
                        Components = new() { new() { PointFallbacks = new() { "802:W" }, Operator = ValueOperator.Minus } },
                    },
                    new()
                    {
                        UsedFor = ValueUsage.HomeBatterySoc,
                        Components = new() { new() { PointFallbacks = new() { "802:SoC" } } },
                    },
                },
                BatteryControl = new()
                {
                    //External battery control setpoints time out on the inverter when not refreshed
                    RewriteInterval = TimeSpan.FromSeconds(60),
                    NormalWrites = new()
                    {
                        //1034 battery charge power setpoint = 0 resets a previous forced charge
                        SunSpecBatteryModeWrite.PlainConstant(1034, ModbusValueType.Float, 0, ModbusEndianess.LittleEndian),
                    },
                    HoldWrites = new()
                    {
                        //1040 max discharge power limit = 0 blocks discharging
                        SunSpecBatteryModeWrite.PlainConstant(1040, ModbusValueType.Float, 0, ModbusEndianess.LittleEndian),
                    },
                    ChargeWrites = new()
                    {
                        //Negative charge power setpoint forces charging
                        SunSpecBatteryModeWrite.PlainNegativeChargePower(1034, ModbusValueType.Float, ModbusEndianess.LittleEndian),
                    },
                },
            }
        },
        //evcc template solaredge-inverter (grid + pv, no battery). Grid via SunSpec meter (subdevice 1), pv via
        //SunSpec inverter AC power. SolarEdge meter W is export positive (evcc applies -1 to reach import positive).
        {
            TemplateValueGatherType.SolarEdgeInverter, new()
            {
                ValueReads = new()
                {
                    new()
                    {
                        UsedFor = ValueUsage.GridPower,
                        Components = new() { SunSpecValueComponent.Point(pointFallbacks: new[] { "203:W", "201:W" }) },
                    },
                    new()
                    {
                        UsedFor = ValueUsage.InverterPower,
                        Components = new() { SunSpecValueComponent.Point(pointFallbacks: new[] { "101:W", "103:W" }) },
                    },
                },
            }
        },
        //evcc template solaredge-hybrid. Battery power/soc and control use SolarEdge proprietary registers
        //(little endian float32, NaN when not available). StorageConf_CtrlMode (0xE004) must be set to 4 "Remote".
        {
            TemplateValueGatherType.SolarEdgeHybrid, new()
            {
                ValueReads = new()
                {
                    new()
                    {
                        UsedFor = ValueUsage.GridPower,
                        Components = new() { SunSpecValueComponent.Point(pointFallbacks: new[] { "203:W", "201:W" }) },
                    },
                    new()
                    {
                        //PV = inverter DC power + battery power (SolarEdge specific formula)
                        UsedFor = ValueUsage.InverterPower,
                        Components = new()
                        {
                            SunSpecValueComponent.Point(pointFallbacks: new[] { "101:DCW", "103:DCW" }),
                            SunSpecValueComponent.PlainRegister(0xF574, ModbusValueType.Float, ModbusEndianess.LittleEndian, optionalIfMissing: true),
                        },
                    },
                    new()
                    {
                        //0xE174 battery power is charge positive (evcc applies -1 to reach discharge positive)
                        UsedFor = ValueUsage.HomeBatteryPower,
                        Components = new() { SunSpecValueComponent.PlainRegister(0xE174, ModbusValueType.Float, ModbusEndianess.LittleEndian) },
                    },
                    new()
                    {
                        //0xE184 battery state of energy (soc %)
                        UsedFor = ValueUsage.HomeBatterySoc,
                        Components = new() { SunSpecValueComponent.PlainRegister(0xE184, ModbusValueType.Float, ModbusEndianess.LittleEndian) },
                    },
                },
                BatteryControl = new()
                {
                    RewriteInterval = TimeSpan.FromSeconds(60),
                    NormalWrites = new()
                    {
                        //0xE00D command mode 7 = maximize self consumption
                        SunSpecBatteryModeWrite.PlainConstant(0xE00D, ModbusValueType.UShort, 7, ModbusEndianess.BigEndian, ModbusWriteFunction.WriteSingleRegister),
                        //0xE010 discharge limit restored
                        SunSpecBatteryModeWrite.PlainSource(0xE010, ModbusValueType.Float, SunSpecWriteValueSource.MaxDischargePowerW, ModbusEndianess.LittleEndian),
                    },
                    HoldWrites = new()
                    {
                        SunSpecBatteryModeWrite.PlainConstant(0xE00D, ModbusValueType.UShort, 7, ModbusEndianess.BigEndian, ModbusWriteFunction.WriteSingleRegister),
                        //Discharge limit 0 blocks discharging
                        SunSpecBatteryModeWrite.PlainConstant(0xE010, ModbusValueType.Float, 0, ModbusEndianess.LittleEndian),
                    },
                    ChargeWrites = new()
                    {
                        //0xE00D command mode 3 = charge from PV + AC at the inverter's max battery power
                        SunSpecBatteryModeWrite.PlainConstant(0xE00D, ModbusValueType.UShort, 3, ModbusEndianess.BigEndian, ModbusWriteFunction.WriteSingleRegister),
                        SunSpecBatteryModeWrite.PlainConstant(0xE010, ModbusValueType.Float, 0, ModbusEndianess.LittleEndian),
                    },
                },
            }
        },
    };

    private static SunSpecBatteryControlDefinition Model124BatteryControl()
    {
        return new()
        {
            //Model 124 uses a revert timeout so the mode needs to be refreshed periodically
            RewriteInterval = TimeSpan.FromSeconds(60),
            NormalWrites = new()
            {
                SunSpecBatteryModeWrite.Point("124:0:StorCtl_Mod", StorCtlModStop),
                //OutWRte 100 % = full discharge allowed
                SunSpecBatteryModeWrite.Point("124:0:OutWRte", 100),
            },
            HoldWrites = new()
            {
                SunSpecBatteryModeWrite.Point("124:0:StorCtl_Mod", StorCtlModDischargeControl),
                //OutWRte 0 % = no discharge
                SunSpecBatteryModeWrite.Point("124:0:OutWRte", 0),
                SunSpecBatteryModeWrite.Point("124:0:InOutWRte_RvrtTms", 0),
            },
            ChargeWrites = new()
            {
                //Allow charging from grid
                SunSpecBatteryModeWrite.Point("124:0:ChaGriSet", ChaGriSetGrid),
                SunSpecBatteryModeWrite.Point("124:0:StorCtl_Mod", StorCtlModDischargeControl),
                //Negative OutWRte forces charging at the configured rate
                SunSpecBatteryModeWrite.PointNegativeChargeRate("124:0:OutWRte"),
                SunSpecBatteryModeWrite.Point("124:0:InOutWRte_RvrtTms", 0),
            },
        };
    }
}
