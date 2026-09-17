using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Localization.Contracts;
using TeslaSolarCharger.Shared.Localization.Registries;
using TeslaSolarCharger.Shared.Localization.Registries.Components;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Shared.Localization;

/// <summary>
/// Display names of the options offered in the value source configuration dialogs. Without these the dropdowns are
/// rendered from the enum member names, which are never translated and read like code (e.g. "Home Battery Soc" or
/// "Ulong"). Kept in one place because the same enums are offered in the REST, MQTT and Modbus dialogs.
/// </summary>
public static class ValueConfigurationEnumNames
{
    public static string Get(ITextLocalizationService localizer, ValueUsage valueUsage) => valueUsage switch
    {
        ValueUsage.InverterPower => T(localizer, TranslationKeys.ValueUsageInverterPower),
        ValueUsage.GridPower => T(localizer, TranslationKeys.ValueUsageGridPower),
        ValueUsage.HomeBatteryPower => T(localizer, TranslationKeys.ValueUsageHomeBatteryPower),
        ValueUsage.HomeBatterySoc => T(localizer, TranslationKeys.ValueUsageHomeBatterySoc),
        _ => valueUsage.ToString(),
    };

    public static string Get(ITextLocalizationService localizer, ValueOperator valueOperator) => valueOperator switch
    {
        ValueOperator.Plus => T(localizer, TranslationKeys.ValueOperatorPlus),
        ValueOperator.Minus => T(localizer, TranslationKeys.ValueOperatorMinus),
        _ => valueOperator.ToString(),
    };

    public static string Get(ITextLocalizationService localizer, NodePatternType nodePatternType) => nodePatternType switch
    {
        NodePatternType.Direct => T(localizer, TranslationKeys.ValueSourceConfigNodePatternTypeDirect),
        NodePatternType.Json => T(localizer, TranslationKeys.ValueSourceConfigNodePatternTypeJson),
        NodePatternType.Xml => T(localizer, TranslationKeys.ValueSourceConfigNodePatternTypeXml),
        _ => nodePatternType.ToString(),
    };

    public static string Get(ITextLocalizationService localizer, ModbusRegisterType registerType) => registerType switch
    {
        ModbusRegisterType.HoldingRegister => T(localizer, TranslationKeys.ModbusRegisterTypeHolding),
        ModbusRegisterType.InputRegister => T(localizer, TranslationKeys.ModbusRegisterTypeInput),
        _ => registerType.ToString(),
    };

    public static string Get(ITextLocalizationService localizer, ModbusEndianess endianess) => endianess switch
    {
        ModbusEndianess.BigEndian => T(localizer, TranslationKeys.ModbusEndianessBig),
        ModbusEndianess.LittleEndian => T(localizer, TranslationKeys.ModbusEndianessLittle),
        _ => endianess.ToString(),
    };

    /// <summary>
    /// Data type names are the same in every language, but the enum member names hide the width the user has to
    /// match against their device's register map, so they are spelled out.
    /// </summary>
    public static string Get(ModbusValueType valueType) => valueType switch
    {
        ModbusValueType.Int => "Int 32",
        ModbusValueType.UInt => "UInt 32",
        ModbusValueType.Short => "Int 16",
        ModbusValueType.UShort => "UInt 16",
        ModbusValueType.Ulong => "UInt 64",
        ModbusValueType.Float => "Float",
        ModbusValueType.Bool => "Bool",
        _ => valueType.ToString(),
    };

    /// <summary>HTTP method names are protocol tokens, so they are not translated but spelled as in the protocol.</summary>
    public static string Get(HttpVerb httpVerb) => httpVerb.ToString().ToUpperInvariant();

    private static string T(ITextLocalizationService localizer, string key) =>
        localizer.Get<ValueSourceConfigurationLocalizationRegistry>(key) ?? key;
}
