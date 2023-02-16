using System.Reflection;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared;

public static class Extensions
{
    public static T Next<T>(this T src) where T : struct
    {
        if (!typeof(T).IsEnum) throw new ArgumentException(String.Format("Argument {0} is not an Enum", typeof(T).FullName));

        var arr = (T[])Enum.GetValues(src.GetType());
        var j = Array.IndexOf(arr, src) + 1;
        return (arr.Length==j) ? arr[0] : arr[j];
    }

    public static string ToFriendlyString(this ChargeMode chargeMode)
    {
        switch (chargeMode)
        {
            case ChargeMode.PvOnly:
                return "PV Only";
            case ChargeMode.MaxPower:
                return "Maximum Power";
            case ChargeMode.PvAndMinSoc:
                return "Min SoC + PV";
            case ChargeMode.SpotPrice:
                return "Spot Price + PV";
            default:
                return chargeMode.ToString();
        }
    }

    public static string ToFriendlyString(this ModbusRegisterType modbusRegisterType)
    {
        switch (modbusRegisterType)
        {
            case ModbusRegisterType.HoldingRegister:
                return "Holding Register";
            case ModbusRegisterType.InputRegister:
                return "Input Register";
            default:
                return modbusRegisterType.ToString();
        }
    }

    public static string ToFriendlyString(this SolarValueSource solarValueSource)
    {
        switch (solarValueSource)
        {
            case SolarValueSource.Rest:
                return "REST";
            case SolarValueSource.Mqtt:
                return "MQTT";
            default:
                return solarValueSource.ToString();
        }
    }

    public static string ToFriendlyString(this NodePatternType nodePatternType)
    {
        switch (nodePatternType)
        {
            case NodePatternType.Json:
                return "JSON";
            case NodePatternType.Xml:
                return "XML";
            default:
                return nodePatternType.ToString();
        }
    }

    public static string ToFriendlyString(this ModbusValueType modbusValueType)
    {
        switch (modbusValueType)
        {
            case ModbusValueType.Int:
                return "Int 32";
            case ModbusValueType.Float:
                return "Float";
            case ModbusValueType.Short:
                return "Int 16";
            case ModbusValueType.UInt:
                return "Uint 32";
            case ModbusValueType.UShort:
                return "Uint 16";
            case ModbusValueType.Ulong:
                return "Uint 64";
            default:
                return modbusValueType.ToString();
        }
    }

    /// <summary>
    /// Extension for 'Object' that copies the properties to a destination object.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="destination">The destination.</param>
    public static void CopyProperties(this object source, object destination)
    {
        // If any this null throw an exception
        if (source == null || destination == null)
            throw new Exception("Source or/and Destination Objects are null");
        // Getting the Types of the objects
        var typeDest = destination.GetType();
        var typeSrc = source.GetType();

        // Iterate the Properties of the source instance and  
        // populate them from their desination counterparts  
        var srcProps = typeSrc.GetProperties();
        foreach (var srcProp in srcProps)
        {
            if (!srcProp.CanRead)
            {
                continue;
            }
            var targetProperty = typeDest.GetProperty(srcProp.Name);
            if (targetProperty == null)
            {
                continue;
            }
            if (!targetProperty.CanWrite)
            {
                continue;
            }
            var setMethod = targetProperty.GetSetMethod(true);
            if (setMethod != null && setMethod.IsPrivate)
            {
                continue;
            }
            if ((targetProperty.GetSetMethod()?.Attributes & MethodAttributes.Static) != 0)
            {
                continue;
            }
            if (!targetProperty.PropertyType.IsAssignableFrom(srcProp.PropertyType))
            {
                continue;
            }
            // Passed all tests, lets set the value
            targetProperty.SetValue(destination, srcProp.GetValue(source, null), null);
        }
    }
}
