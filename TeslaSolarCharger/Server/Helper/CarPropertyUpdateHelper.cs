using System.Globalization;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Helper.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Helper;

public class CarPropertyUpdateHelper : ICarPropertyUpdateHelper
{
    private readonly ILogger<CarPropertyUpdateHelper> _logger;

    public CarPropertyUpdateHelper(ILogger<CarPropertyUpdateHelper> logger)
    {
        _logger = logger;
    }

    public void UpdateDtoCarProperty(DtoCar car, CarValueLog carValueLog)
    {
        _logger.LogTrace("{method}({carId}, ***secret***)", nameof(UpdateDtoCarProperty), car.Id);

        // List of relevant property names
        var relevantPropertyNames = new List<string>
        {
            nameof(CarValueLog.DoubleValue),
            nameof(CarValueLog.IntValue),
            nameof(CarValueLog.StringValue),
            nameof(CarValueLog.UnknownValue),
            nameof(CarValueLog.BooleanValue),
        };

        // Filter properties to only the relevant ones
        var carValueProperties = typeof(CarValueLog)
            .GetProperties()
            .Where(p => relevantPropertyNames.Contains(p.Name));

        object? valueToConvert = null;

        // Find the first non-null property in CarValueLog among the relevant ones
        foreach (var prop in carValueProperties)
        {
            var value = prop.GetValue(carValueLog);
            if (value != null)
            {
                valueToConvert = value;
                break;
            }
        }

        if (valueToConvert != null)
        {
            var propertyName = GetPropertyNameByCarValueType(carValueLog.Type);
            if (propertyName == null)
            {
                return;
            }
            var dtoProperty = typeof(DtoCar).GetProperty(propertyName);
            if (dtoProperty != null)
            {
                // Get the DtoTimeStampedValue instance from the property
                var dtoTimeStampedValue = dtoProperty.GetValue(car);
                if (dtoTimeStampedValue == null)
                {
                    _logger.LogWarning("Property {propertyName} on car {carId} is null", propertyName, car.Id);
                    return;
                }

                // Get the generic type argument of DtoTimeStampedValue<T>
                var dtoPropertyType = dtoProperty.PropertyType;
                if (!dtoPropertyType.IsGenericType ||
                    !dtoPropertyType.GetGenericTypeDefinition().IsAssignableFrom(typeof(DtoTimeStampedValue<>)))
                {
                    _logger.LogError("Property {propertyName} is not a DtoTimeStampedValue type", propertyName);
                    return;
                }

                // Get the inner type T from DtoTimeStampedValue<T>
                var innerType = dtoPropertyType.GetGenericArguments()[0];

                // Handle nullable types
                var targetType = Nullable.GetUnderlyingType(innerType) ?? innerType;
                object? convertedValue = null;

                try
                {
                    // Directly handle numeric conversions without converting to string
                    if (targetType == typeof(int))
                    {
                        if (valueToConvert is int intValue)
                        {
                            convertedValue = intValue;
                        }
                        else if (valueToConvert is double doubleValue)
                        {
                            // Decide how to handle the fractional part
                            intValue = (int)Math.Round(doubleValue); // Or Math.Floor(doubleValue), Math.Ceiling(doubleValue)
                            convertedValue = intValue;
                        }
                        else if (valueToConvert is string valueString)
                        {
                            // Use InvariantCulture when parsing the string
                            if (int.TryParse(valueString, NumberStyles.Integer, CultureInfo.InvariantCulture, out intValue))
                            {
                                convertedValue = intValue;
                            }
                            else if (double.TryParse(valueString, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out doubleValue))
                            {
                                intValue = (int)Math.Round(doubleValue);
                                convertedValue = intValue;
                            }
                        }
                        else if (valueToConvert is IConvertible)
                        {
                            convertedValue = Convert.ToInt32(valueToConvert);
                        }
                    }
                    else if (targetType == typeof(double))
                    {
                        if (valueToConvert is double doubleValue)
                        {
                            convertedValue = doubleValue;
                        }
                        else if (valueToConvert is int intValue)
                        {
                            convertedValue = (double)intValue;
                        }
                        else if (valueToConvert is string valueString)
                        {
                            if (double.TryParse(valueString, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out doubleValue))
                            {
                                convertedValue = doubleValue;
                            }
                        }
                        else if (valueToConvert is IConvertible)
                        {
                            convertedValue = Convert.ToDouble(valueToConvert, CultureInfo.InvariantCulture);
                        }
                    }
                    else if (targetType == typeof(decimal))
                    {
                        if (valueToConvert is decimal decimalValue)
                        {
                            convertedValue = decimalValue;
                        }
                        else if (valueToConvert is double doubleValue)
                        {
                            convertedValue = (decimal)doubleValue;
                        }
                        else if (valueToConvert is int intValue)
                        {
                            convertedValue = (decimal)intValue;
                        }
                        else if (valueToConvert is string valueString)
                        {
                            if (decimal.TryParse(valueString, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out decimalValue))
                            {
                                convertedValue = decimalValue;
                            }
                        }
                        else if (valueToConvert is IConvertible)
                        {
                            convertedValue = Convert.ToDecimal(valueToConvert, CultureInfo.InvariantCulture);
                        }
                    }
                    else if (targetType == typeof(bool))
                    {
                        if (valueToConvert is bool boolValue)
                        {
                            convertedValue = boolValue;
                        }
                        else if (valueToConvert is string valueString)
                        {
                            if (bool.TryParse(valueString, out boolValue))
                            {
                                convertedValue = boolValue;
                            }
                        }
                        else if (valueToConvert is IConvertible)
                        {
                            convertedValue = Convert.ToBoolean(valueToConvert, CultureInfo.InvariantCulture);
                        }
                    }
                    else if (targetType == typeof(string))
                    {
                        // Use InvariantCulture to ensure consistent formatting
                        convertedValue = Convert.ToString(valueToConvert, CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        // For other types, attempt to convert using ChangeType
                        if (valueToConvert is IConvertible)
                        {
                            convertedValue = Convert.ChangeType(valueToConvert, targetType, CultureInfo.InvariantCulture);
                        }
                        else if (targetType.IsAssignableFrom(valueToConvert.GetType()))
                        {
                            convertedValue = valueToConvert;
                        }
                    }

                    // Update the DtoTimeStampedValue if conversion was successful
                    if (convertedValue != null || targetType.IsClass || Nullable.GetUnderlyingType(innerType) != null)
                    {
                        // Get the Update method
                        var updateMethod = dtoPropertyType.GetMethod("Update");
                        if (updateMethod != null)
                        {
                            // Convert DateTime to DateTimeOffset
                            var timestamp = new DateTimeOffset(carValueLog.Timestamp, TimeSpan.Zero);

                            // If the target is a nullable type and convertedValue is not null, 
                            // we need to wrap it in the nullable type
                            if (Nullable.GetUnderlyingType(innerType) != null && convertedValue != null)
                            {
                                // The value is already the underlying type, just pass it
                                updateMethod.Invoke(dtoTimeStampedValue, [timestamp, convertedValue]);
                            }
                            else
                            {
                                // For non-nullable types or null values
                                updateMethod.Invoke(dtoTimeStampedValue, [timestamp, convertedValue]);
                            }
                        }
                        else
                        {
                            _logger.LogError("Update method not found on DtoTimeStampedValue for property {propertyName}", propertyName);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("Do not update {propertyName} on car {carId} as converted value is null for non-nullable type", propertyName, car.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error converting {propertyName} on car {carId}", propertyName, car.Id);
                }
            }
        }
    }

    private string? GetPropertyNameByCarValueType(CarValueType type)
    {
        return type switch
        {
            CarValueType.ModuleTempMin => nameof(DtoCar.MinBatteryTemperature),
            CarValueType.ModuleTempMax => nameof(DtoCar.MaxBatteryTemperature),
            CarValueType.ChargeAmps => nameof(DtoCar.ChargerActualCurrent),
            CarValueType.ChargeCurrentRequest => nameof(DtoCar.ChargerRequestedCurrent),
            CarValueType.IsPluggedIn => nameof(DtoCar.PluggedIn),
            CarValueType.IsCharging => nameof(DtoCar.IsCharging),
            CarValueType.ChargerPilotCurrent => nameof(DtoCar.ChargerPilotCurrent),
            CarValueType.Longitude => nameof(DtoCar.Longitude),
            CarValueType.Latitude => nameof(DtoCar.Latitude),
            CarValueType.StateOfCharge => nameof(DtoCar.SoC),
            CarValueType.StateOfChargeLimit => nameof(DtoCar.SocLimit),
            CarValueType.ChargerPhases => nameof(DtoCar.ChargerPhases),
            CarValueType.ChargerVoltage => nameof(DtoCar.ChargerVoltage),
            _ => null,
        };
    }
}
