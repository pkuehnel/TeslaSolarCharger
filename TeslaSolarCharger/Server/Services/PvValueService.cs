using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Globalization;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Web;
using System.Xml;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.EntityFramework;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Server.SignalR.Notifiers.Contracts;
using TeslaSolarCharger.Services.Services.Modbus.Contracts;
using TeslaSolarCharger.Services.Services.Mqtt.Contracts;
using TeslaSolarCharger.Services.Services.Rest.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues;
using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;
using TeslaSolarCharger.Shared.Dtos.MqttConfiguration;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.Shared.SignalRClients;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services;

public class PvValueService(
    ILogger<PvValueService> logger,
    ISettings settings,
    IInMemoryValues inMemoryValues,
    IConfigurationWrapper configurationWrapper,
    ITelegramService telegramService,
    IDateTimeProvider dateTimeProvider,
    ITeslaSolarChargerContext context,
    IRestValueExecutionService restValueExecutionService,
    IRestValueConfigurationService restValueConfigurationService,
    IModbusValueConfigurationService modbusValueConfigurationService,
    IModbusValueExecutionService modbusValueExecutionService,
    IMqttClientHandlingService mqttClientHandlingService,
    IMqttConfigurationService mqttConfigurationService,
    IConstants constants,
    ILoadPointManagementService loadPointManagementService,
    IAppStateNotifier appStateNotifier,
    IChangeTrackingService changeTrackingService)
    : IPvValueService
{
    public async Task ConvertToNewConfiguration()
    {
        var solarConfigurationsConverted =
            await context.TscConfigurations.AnyAsync(c => c.Key == constants.SolarValuesConverted).ConfigureAwait(false);
        if (solarConfigurationsConverted)
        {
            return;
        }
        if (!await context.RestValueConfigurations.AnyAsync())
        {
            //Do not change order of the following methods
            try
            {
                await ConvertGridRestValueConfiguration();
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error while converting grid rest value configuration");
            }

            try
            {
                await ConvertInverterRestValueConfiguration();

            }
            catch (Exception e)
            {
                logger.LogError(e, "Error while converting inverter rest value configuration");
            }

            try
            {
                await ConvertHomeBatterySocRestConfiguration();
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error while converting home battery soc rest value configuration");
            }

            try
            {
                await ConvertHomeBatteryPowerRestConfiguration();
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error while converting home battery power rest value configuration");
            }
        }

        if (!await context.ModbusConfigurations.AnyAsync())
        {
            try
            {
                await ConvertGridModbusValueConfiguration();
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error while converting grid modbus value configuration");
            }
            try
            {
                await ConvertInverterModbusValueConfiguration();

            }
            catch (Exception e)
            {
                logger.LogError(e, "Error while converting inverter modbus value configuration");
            }

            try
            {
                await ConvertHomeBatteryPowerModbusValueConfiguration();
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error while converting home battery power modbus value configuration");
            }
            try
            {
                await ConvertHomeBatteryPowerInversionUrl();
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error while converting home battery power inversion modbus value configuration");
            }

            try
            {
                await ConvertHomeBatterySocModbusValueConfiguration();
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error while converting home battery soc modbus value configuration");
            }
        }

        if (!await context.MqttConfigurations.AnyAsync())
        {
            await ConvertMqttConfigurations().ConfigureAwait(false);
        }

        context.TscConfigurations.Add(new TscConfiguration()
        {
            Key = constants.SolarValuesConverted,
            Value = "true",
        });
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    private async Task ConvertMqttConfigurations()
    {
        var frontendConfiguration = configurationWrapper.FrontendConfiguration();
        if (frontendConfiguration == default ||
            (frontendConfiguration.GridValueSource != SolarValueSource.Mqtt
             && frontendConfiguration.HomeBatteryValuesSource != SolarValueSource.Mqtt
             && frontendConfiguration.InverterValueSource != SolarValueSource.Mqtt))
        {
            logger.LogDebug("Do not convert MQTT as no value source is on MQTT.");
            return;
        }
        var solarMqttServer = configurationWrapper.SolarMqttServer();
        var solarMqttUser = configurationWrapper.SolarMqttUsername();
        var solarMqttPassword = configurationWrapper.SolarMqttPassword();
        if (string.IsNullOrEmpty(solarMqttServer))
        {
            return;
        }
        var mqttServerAndPort = solarMqttServer.Split(":");
        var mqttHost = mqttServerAndPort.First();
        int? mqttServerPort = null;
        if (mqttServerAndPort.Length > 1)
        {
            mqttServerPort = Convert.ToInt32(mqttServerAndPort[1]);
        }

        var mqttConfiguration = new DtoMqttConfiguration()
        {
            Host = mqttHost,
            Port = mqttServerPort ?? 1883,
            Username = solarMqttUser,
            Password = solarMqttPassword,
        };
        var mqttConfigurationId = await mqttConfigurationService.SaveConfiguration(mqttConfiguration);
        try
        {
            await ConvertGridMqttConfiguration(mqttConfigurationId);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while converting mqtt grid value configuration");
        }

        try
        {
            await ConvertInverterMqttConfiguration(mqttConfigurationId);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while converting mqtt inverter value configuration");
        }

        try
        {
            await ConvertHomeBatteryPowerMqttConfiguration(mqttConfigurationId);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while converting mqtt home battery power value configuration");
        }

        try
        {
            await ConvertHomeBatterySocMqttConfiguration(mqttConfigurationId);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while converting mqtt home battery soc value configuration");
        }
    }

    private async Task ConvertHomeBatterySocMqttConfiguration(int mqttConfigurationId)
    {
        var frontendConfiguration = configurationWrapper.FrontendConfiguration();
        var homeBatterySocMqttTopic = configurationWrapper.HomeBatterySocMqttTopic();
        if (frontendConfiguration?.HomeBatteryValuesSource != SolarValueSource.Mqtt
            || string.IsNullOrEmpty(homeBatterySocMqttTopic))
        {
            return;
        }
        var resultConfiguration = new DtoMqttResultConfiguration()
        {
            Topic = homeBatterySocMqttTopic,
            CorrectionFactor = configurationWrapper.HomeBatterySocCorrectionFactor(),
            UsedFor = ValueUsage.HomeBatterySoc,
        };
        resultConfiguration.NodePatternType = frontendConfiguration.HomeBatterySocNodePatternType ?? NodePatternType.Direct;
        if (resultConfiguration.NodePatternType == NodePatternType.Xml)
        {
            resultConfiguration.NodePattern = configurationWrapper.HomeBatterySocXmlPattern();
            resultConfiguration.XmlAttributeHeaderName = configurationWrapper.HomeBatterySocXmlAttributeHeaderName();
            resultConfiguration.XmlAttributeHeaderValue = configurationWrapper.HomeBatterySocXmlAttributeHeaderValue();
            resultConfiguration.XmlAttributeValueName = configurationWrapper.HomeBatterySocXmlAttributeValueName();
        }
        else if (resultConfiguration.NodePatternType == NodePatternType.Json)
        {
            resultConfiguration.NodePattern = configurationWrapper.HomeBatterySocJsonPattern();
        }
        await mqttConfigurationService.SaveResultConfiguration(mqttConfigurationId, resultConfiguration);
    }

    private async Task ConvertHomeBatteryPowerMqttConfiguration(int mqttConfigurationId)
    {
        var frontendConfiguration = configurationWrapper.FrontendConfiguration();
        var homeBatteryPowerMqttTopic = configurationWrapper.HomeBatteryPowerMqttTopic();
        if (frontendConfiguration?.HomeBatteryValuesSource != SolarValueSource.Mqtt
            || string.IsNullOrEmpty(homeBatteryPowerMqttTopic))
        {
            return;
        }
        var resultConfiguration = new DtoMqttResultConfiguration()
        {
            Topic = homeBatteryPowerMqttTopic,
            CorrectionFactor = configurationWrapper.HomeBatteryPowerCorrectionFactor(),
            UsedFor = ValueUsage.HomeBatteryPower,
        };
        resultConfiguration.NodePatternType = frontendConfiguration.HomeBatteryPowerNodePatternType ?? NodePatternType.Direct;
        if (resultConfiguration.NodePatternType == NodePatternType.Xml)
        {
            resultConfiguration.NodePattern = configurationWrapper.HomeBatteryPowerXmlPattern();
            resultConfiguration.XmlAttributeHeaderName = configurationWrapper.HomeBatteryPowerXmlAttributeHeaderName();
            resultConfiguration.XmlAttributeHeaderValue = configurationWrapper.HomeBatteryPowerXmlAttributeHeaderValue();
            resultConfiguration.XmlAttributeValueName = configurationWrapper.HomeBatteryPowerXmlAttributeValueName();
        }
        else if (resultConfiguration.NodePatternType == NodePatternType.Json)
        {
            resultConfiguration.NodePattern = configurationWrapper.HomeBatteryPowerJsonPattern();
        }
        await mqttConfigurationService.SaveResultConfiguration(mqttConfigurationId, resultConfiguration);
    }

    private async Task ConvertInverterMqttConfiguration(int mqttConfigurationId)
    {
        var frontendConfiguration = configurationWrapper.FrontendConfiguration();
        var currentInverterPowerMqttTopic = configurationWrapper.CurrentInverterPowerMqttTopic();
        if (frontendConfiguration?.InverterValueSource != SolarValueSource.Mqtt
            || string.IsNullOrEmpty(currentInverterPowerMqttTopic))
        {
            return;
        }
        var resultConfiguration = new DtoMqttResultConfiguration()
        {
            Topic = currentInverterPowerMqttTopic,
            CorrectionFactor = configurationWrapper.CurrentInverterPowerCorrectionFactor(),
            UsedFor = ValueUsage.InverterPower,
        };
        resultConfiguration.NodePatternType = frontendConfiguration.InverterPowerNodePatternType ?? NodePatternType.Direct;
        if (resultConfiguration.NodePatternType == NodePatternType.Xml)
        {
            resultConfiguration.NodePattern = configurationWrapper.CurrentInverterPowerXmlPattern();
            resultConfiguration.XmlAttributeHeaderName = configurationWrapper.CurrentInverterPowerXmlAttributeHeaderName();
            resultConfiguration.XmlAttributeHeaderValue = configurationWrapper.CurrentInverterPowerXmlAttributeHeaderValue();
            resultConfiguration.XmlAttributeValueName = configurationWrapper.CurrentInverterPowerXmlAttributeValueName();
        }
        else if (resultConfiguration.NodePatternType == NodePatternType.Json)
        {
            resultConfiguration.NodePattern = configurationWrapper.CurrentInverterPowerJsonPattern();
        }
        await mqttConfigurationService.SaveResultConfiguration(mqttConfigurationId, resultConfiguration);
    }

    private async Task ConvertGridMqttConfiguration(int mqttConfigurationId)
    {
        var frontendConfiguration = configurationWrapper.FrontendConfiguration();
        var currentPowerToGridMqttTopic = configurationWrapper.CurrentPowerToGridMqttTopic();
        if (frontendConfiguration?.GridValueSource != SolarValueSource.Mqtt
            || string.IsNullOrEmpty(currentPowerToGridMqttTopic))
        {
            return;
        }
        var resultConfiguration = new DtoMqttResultConfiguration()
        {
            Topic = currentPowerToGridMqttTopic,
            CorrectionFactor = configurationWrapper.CurrentPowerToGridCorrectionFactor(),
            UsedFor = ValueUsage.GridPower,
        };
        resultConfiguration.NodePatternType = frontendConfiguration.GridPowerNodePatternType ?? NodePatternType.Direct;
        if (resultConfiguration.NodePatternType == NodePatternType.Xml)
        {
            resultConfiguration.NodePattern = configurationWrapper.CurrentPowerToGridXmlPattern();
            resultConfiguration.XmlAttributeHeaderName = configurationWrapper.CurrentPowerToGridXmlAttributeHeaderName();
            resultConfiguration.XmlAttributeHeaderValue = configurationWrapper.CurrentPowerToGridXmlAttributeHeaderValue();
            resultConfiguration.XmlAttributeValueName = configurationWrapper.CurrentPowerToGridXmlAttributeValueName();
        }
        else if (resultConfiguration.NodePatternType == NodePatternType.Json)
        {
            resultConfiguration.NodePattern = configurationWrapper.CurrentPowerToGridJsonPattern();
        }
        await mqttConfigurationService.SaveResultConfiguration(mqttConfigurationId, resultConfiguration);
    }

    private async Task ConvertGridModbusValueConfiguration()
    {
        var gridRequestUrl = configurationWrapper.CurrentPowerToGridUrl();
        var frontendConfiguration = configurationWrapper.FrontendConfiguration();
        var correctionFactor = configurationWrapper.CurrentPowerToGridCorrectionFactor();
        if (!string.IsNullOrWhiteSpace(gridRequestUrl) && frontendConfiguration is
            { GridValueSource: SolarValueSource.Modbus })
        {
            await ConvertGenericModbusValueConfiguration(gridRequestUrl, ValueUsage.GridPower, correctionFactor);
        }
    }

    private async Task ConvertInverterModbusValueConfiguration()
    {
        var requestUrl = configurationWrapper.CurrentInverterPowerUrl();
        var frontendConfiguration = configurationWrapper.FrontendConfiguration();
        var correctionFactor = configurationWrapper.CurrentInverterPowerCorrectionFactor();
        if (!string.IsNullOrWhiteSpace(requestUrl) && frontendConfiguration is
                { InverterValueSource: SolarValueSource.Modbus })
        {
            await ConvertGenericModbusValueConfiguration(requestUrl, ValueUsage.InverterPower, correctionFactor);
        }
    }

    private async Task ConvertHomeBatteryPowerModbusValueConfiguration()
    {
        var requestUrl = configurationWrapper.HomeBatteryPowerUrl();
        var frontendConfiguration = configurationWrapper.FrontendConfiguration();
        var correctionFactor = configurationWrapper.HomeBatteryPowerCorrectionFactor();
        if (!string.IsNullOrWhiteSpace(requestUrl) && frontendConfiguration is
                { HomeBatteryValuesSource: SolarValueSource.Modbus })
        {
            await ConvertGenericModbusValueConfiguration(requestUrl, ValueUsage.HomeBatteryPower, correctionFactor);
        }
    }

    private async Task ConvertHomeBatteryPowerInversionUrl()
    {
        var requestUrl = configurationWrapper.HomeBatteryPowerInversionUrl();
        if (string.IsNullOrEmpty(requestUrl))
        {
            return;
        }
        var homeBatteryPowerResultConfigurations =
            await modbusValueConfigurationService.GetModbusResultConfigurationsByPredicate(r => r.UsedFor == ValueUsage.HomeBatteryPower);
        var homeBatteryPowerResultConfiguration = homeBatteryPowerResultConfigurations.Single();
        var parentConfigs = await modbusValueConfigurationService.GetModbusConfigurationByPredicate(c =>
            c.ModbusResultConfigurations.Any(r => r.Id == homeBatteryPowerResultConfiguration.Id));
        var parentConfig = parentConfigs.Single();
        var inversionId = await ConvertGenericModbusValueConfiguration(requestUrl, ValueUsage.HomeBatteryPower, 1);
        homeBatteryPowerResultConfiguration.InvertedByModbusResultConfigurationId = inversionId;
        await modbusValueConfigurationService.SaveModbusResultConfiguration(parentConfig.Id, homeBatteryPowerResultConfiguration);
    }

    private async Task ConvertHomeBatterySocModbusValueConfiguration()
    {
        var requestUrl = configurationWrapper.HomeBatterySocUrl();
        var frontendConfiguration = configurationWrapper.FrontendConfiguration();
        var correctionFactor = configurationWrapper.HomeBatterySocCorrectionFactor();
        if (!string.IsNullOrWhiteSpace(requestUrl) && frontendConfiguration is
                { HomeBatteryValuesSource: SolarValueSource.Modbus })
        {
            await ConvertGenericModbusValueConfiguration(requestUrl, ValueUsage.HomeBatterySoc, correctionFactor);
        }
    }

    private async Task<int> ConvertGenericModbusValueConfiguration(string requestUrl, ValueUsage valueUsage, decimal correctionFactor)
    {
        var uri = new Uri(requestUrl);
        var modbusValueConfiguration = new DtoModbusConfiguration()
        {
            UnitIdentifier = int.Parse(GetQueryParameterValue(uri, "unitIdentifier", "1")),
            Host = GetQueryParameterValue(uri, "ipAddress"),
            Port = int.Parse(GetQueryParameterValue(uri, "port", "502")),
        };
        var registerSwapString = GetQueryParameterValue(uri, "registerSwap", "false");
        modbusValueConfiguration.Endianess = bool.Parse(registerSwapString) ? ModbusEndianess.LittleEndian : ModbusEndianess.BigEndian;
        var connectDelaySecondsString = GetQueryParameterValue(uri, "connectDelaySeconds", "0");
        modbusValueConfiguration.ConnectDelayMilliseconds = (int.Parse(connectDelaySecondsString)) * 1000;
        var timeoutSecondsString = GetQueryParameterValue(uri, "timeoutSeconds", "1");
        modbusValueConfiguration.ReadTimeoutMilliseconds = (int.Parse(timeoutSecondsString)) * 1000;
        int configurationId;
        var existingConfigurations = await modbusValueConfigurationService.GetModbusConfigurationByPredicate(c =>
            c.Host == modbusValueConfiguration.Host && c.Port == modbusValueConfiguration.Port);
        if (existingConfigurations.Any())
        {
            configurationId = existingConfigurations.First().Id;
        }
        else
        {
            configurationId = await modbusValueConfigurationService.SaveModbusConfiguration(modbusValueConfiguration);
        }
        var resultConfiguration = new DtoModbusValueResultConfiguration()
        {
            UsedFor = valueUsage,
            CorrectionFactor = correctionFactor,
        };
        SetRegisterType(uri, resultConfiguration);
        var startIndex = GetQueryParameterValue(uri, "startIndex", string.Empty);
        if (string.IsNullOrEmpty(startIndex))
        {
            SetValueType(uri, resultConfiguration);
        }
        else
        {
            var startIndexInt = int.Parse(startIndex);
            //needs correction as was reversed in old Modbus plugin
            startIndexInt = 15 - startIndexInt;
            resultConfiguration.BitStartIndex = startIndexInt;
            resultConfiguration.ValueType = ModbusValueType.Bool;
        }
        var addressString = GetQueryParameterValue(uri, "startingAddress");
        resultConfiguration.Address = int.Parse(addressString);
        var quantityString = GetQueryParameterValue(uri, "quantity");
        resultConfiguration.Length = int.Parse(quantityString);
        return await modbusValueConfigurationService.SaveModbusResultConfiguration(configurationId, resultConfiguration);
    }

    private void SetValueType(Uri uri, DtoModbusValueResultConfiguration resultConfiguration)
    {
        var modbusValueTypeString = GetQueryParameterValue(uri, "modbusValueType", string.Empty);
        ModbusValueType valueType;
        if (string.IsNullOrEmpty(modbusValueTypeString))
        {
            var methodName = uri.Segments.Last();
            if (methodName.Equals("GetValue", StringComparison.CurrentCultureIgnoreCase) || methodName.Equals("GetInt32Value", StringComparison.CurrentCultureIgnoreCase))
            {
                valueType = ModbusValueType.Int;
            }
            else if (methodName.Equals("GetInt16Value", StringComparison.CurrentCultureIgnoreCase))
            {
                valueType = ModbusValueType.Short;
            }
            else if (methodName.Equals("GetFloatValue", StringComparison.CurrentCultureIgnoreCase))
            {
                valueType = ModbusValueType.Float;
            }
            else
            {
                valueType = ModbusValueType.Int;
            }
        }
        else
        {
            valueType = (ModbusValueType)Enum.Parse(typeof(ModbusValueType), modbusValueTypeString);
        }
        resultConfiguration.ValueType = valueType;
    }

    private void SetRegisterType(Uri uri, DtoModbusValueResultConfiguration resultConfiguration)
    {
        var modbusRegisterTypeString = GetQueryParameterValue(uri, "modbusRegisterType", string.Empty);
        if (string.IsNullOrEmpty(modbusRegisterTypeString))
        {
            resultConfiguration.RegisterType = ModbusRegisterType.HoldingRegister;
        }
        else
        {
            resultConfiguration.RegisterType = (ModbusRegisterType)Enum.Parse(typeof(ModbusRegisterType), modbusRegisterTypeString);
        }
    }

    private string GetQueryParameterValue(Uri uri, string queryParameter, string? defaultValue = null)
    {
        return HttpUtility.ParseQueryString(uri.Query).Get(queryParameter) ?? (defaultValue ?? throw new InvalidOperationException());
    }

    private async Task ConvertHomeBatteryPowerRestConfiguration()
    {
        var homeBatteryPowerRequestUrl = configurationWrapper.HomeBatteryPowerUrl();
        var frontendConfiguration = configurationWrapper.FrontendConfiguration();
        if (!string.IsNullOrWhiteSpace(homeBatteryPowerRequestUrl) && frontendConfiguration is
                { HomeBatteryValuesSource: SolarValueSource.Rest })
        {
            var patternType = frontendConfiguration.HomeBatteryPowerNodePatternType ?? NodePatternType.Direct;
            var newHomeBatteryPowerConfiguration = await context.RestValueConfigurations
                .Where(r => r.Url == homeBatteryPowerRequestUrl)
                .FirstOrDefaultAsync();
            if (newHomeBatteryPowerConfiguration == default)
            {
                newHomeBatteryPowerConfiguration = new RestValueConfiguration()
                {
                    Url = homeBatteryPowerRequestUrl,
                    NodePatternType = patternType,
                    HttpMethod = HttpVerb.Get,
                };
                context.RestValueConfigurations.Add(newHomeBatteryPowerConfiguration);
                var homeBatteryPowerHeaders = configurationWrapper.HomeBatteryPowerHeaders();
                foreach (var homeBatteryPowerHeader in homeBatteryPowerHeaders)
                {
                    newHomeBatteryPowerConfiguration.Headers.Add(new RestValueConfigurationHeader()
                    {
                        Key = homeBatteryPowerHeader.Key,
                        Value = homeBatteryPowerHeader.Value,
                    });
                }
            }
            var resultConfiguration = new RestValueResultConfiguration()
            {
                CorrectionFactor = configurationWrapper.HomeBatteryPowerCorrectionFactor(),
                UsedFor = ValueUsage.HomeBatteryPower,
            };
            if (newHomeBatteryPowerConfiguration.NodePatternType == NodePatternType.Xml)
            {
                resultConfiguration.NodePattern = configurationWrapper.HomeBatteryPowerXmlPattern();
                resultConfiguration.XmlAttributeHeaderName = configurationWrapper.HomeBatteryPowerXmlAttributeHeaderName();
                resultConfiguration.XmlAttributeHeaderValue = configurationWrapper.HomeBatteryPowerXmlAttributeHeaderValue();
                resultConfiguration.XmlAttributeValueName = configurationWrapper.HomeBatteryPowerXmlAttributeValueName();
            }
            else if (newHomeBatteryPowerConfiguration.NodePatternType == NodePatternType.Json)
            {
                resultConfiguration.NodePattern = configurationWrapper.HomeBatteryPowerJsonPattern();
            }
            newHomeBatteryPowerConfiguration.RestValueResultConfigurations.Add(resultConfiguration);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }
    }

    private async Task ConvertHomeBatterySocRestConfiguration()
    {
        var homeBatterySocRequestUrl = configurationWrapper.HomeBatterySocUrl();
        var frontendConfiguration = configurationWrapper.FrontendConfiguration();
        if (!string.IsNullOrWhiteSpace(homeBatterySocRequestUrl) && frontendConfiguration is
                { HomeBatteryValuesSource: SolarValueSource.Rest })
        {
            var patternType = frontendConfiguration.HomeBatterySocNodePatternType ?? NodePatternType.Direct;
            var newHomeBatterySocConfiguration = await context.RestValueConfigurations
                .Where(r => r.Url == homeBatterySocRequestUrl)
                .FirstOrDefaultAsync();
            if (newHomeBatterySocConfiguration == default)
            {
                newHomeBatterySocConfiguration = new RestValueConfiguration()
                {
                    Url = homeBatterySocRequestUrl,
                    NodePatternType = patternType,
                    HttpMethod = HttpVerb.Get,
                };
                context.RestValueConfigurations.Add(newHomeBatterySocConfiguration);
                var hombatteryHeaders = configurationWrapper.HomeBatterySocHeaders();
                foreach (var homeBatteryHeader in hombatteryHeaders)
                {
                    newHomeBatterySocConfiguration.Headers.Add(new RestValueConfigurationHeader()
                    {
                        Key = homeBatteryHeader.Key,
                        Value = homeBatteryHeader.Value,
                    });
                }
            }
            var resultConfiguration = new RestValueResultConfiguration()
            {
                CorrectionFactor = configurationWrapper.HomeBatterySocCorrectionFactor(),
                UsedFor = ValueUsage.HomeBatterySoc,
            };
            if (newHomeBatterySocConfiguration.NodePatternType == NodePatternType.Xml)
            {
                resultConfiguration.NodePattern = configurationWrapper.HomeBatterySocXmlPattern();
                resultConfiguration.XmlAttributeHeaderName = configurationWrapper.HomeBatterySocXmlAttributeHeaderName();
                resultConfiguration.XmlAttributeHeaderValue = configurationWrapper.HomeBatterySocXmlAttributeHeaderValue();
                resultConfiguration.XmlAttributeValueName = configurationWrapper.HomeBatterySocXmlAttributeValueName();
            }
            else if (newHomeBatterySocConfiguration.NodePatternType == NodePatternType.Json)
            {
                resultConfiguration.NodePattern = configurationWrapper.HomeBatterySocJsonPattern();
            }
            newHomeBatterySocConfiguration.RestValueResultConfigurations.Add(resultConfiguration);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }
    }

    private async Task ConvertInverterRestValueConfiguration()
    {
        var inverterRequestUrl = configurationWrapper.CurrentInverterPowerUrl();
        var frontendConfiguration = configurationWrapper.FrontendConfiguration();
        if (!string.IsNullOrWhiteSpace(inverterRequestUrl) && frontendConfiguration is
                { InverterValueSource: SolarValueSource.Rest })
        {
            var patternType = frontendConfiguration.InverterPowerNodePatternType ?? NodePatternType.Direct;
            var newInverterConfiguration = await context.RestValueConfigurations
                .Where(r => r.Url == inverterRequestUrl)
                .FirstOrDefaultAsync();
            if (newInverterConfiguration == default)
            {
                newInverterConfiguration = new RestValueConfiguration()
                {
                    Url = inverterRequestUrl,
                    NodePatternType = patternType,
                    HttpMethod = HttpVerb.Get,
                };
                context.RestValueConfigurations.Add(newInverterConfiguration);
                var inverterRequestHeaders = configurationWrapper.CurrentInverterPowerHeaders();
                foreach (var inverterRequestHeader in inverterRequestHeaders)
                {
                    newInverterConfiguration.Headers.Add(new RestValueConfigurationHeader()
                    {
                        Key = inverterRequestHeader.Key,
                        Value = inverterRequestHeader.Value,
                    });
                }
            }
            var resultConfiguration = new RestValueResultConfiguration()
            {
                CorrectionFactor = configurationWrapper.CurrentInverterPowerCorrectionFactor(),
                UsedFor = ValueUsage.InverterPower,
            };
            if (newInverterConfiguration.NodePatternType == NodePatternType.Xml)
            {
                resultConfiguration.NodePattern = configurationWrapper.CurrentInverterPowerXmlPattern();
                resultConfiguration.XmlAttributeHeaderName = configurationWrapper.CurrentInverterPowerXmlAttributeHeaderName();
                resultConfiguration.XmlAttributeHeaderValue = configurationWrapper.CurrentInverterPowerXmlAttributeHeaderValue();
                resultConfiguration.XmlAttributeValueName = configurationWrapper.CurrentInverterPowerXmlAttributeValueName();
            }
            else if (newInverterConfiguration.NodePatternType == NodePatternType.Json)
            {
                resultConfiguration.NodePattern = configurationWrapper.CurrentInverterPowerJsonPattern();
            }
            newInverterConfiguration.RestValueResultConfigurations.Add(resultConfiguration);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }
    }

    private async Task ConvertGridRestValueConfiguration()
    {
        var gridRequestUrl = configurationWrapper.CurrentPowerToGridUrl();
        var frontendConfiguration = configurationWrapper.FrontendConfiguration();
        if (!string.IsNullOrWhiteSpace(gridRequestUrl) && frontendConfiguration is
                { GridValueSource: SolarValueSource.Rest })
        {
            var patternType = frontendConfiguration.GridPowerNodePatternType ?? NodePatternType.Direct;
            var newGridConfiguration = new RestValueConfiguration()
            {
                Url = gridRequestUrl,
                NodePatternType = patternType,
                HttpMethod = HttpVerb.Get,
            };
            context.RestValueConfigurations.Add(newGridConfiguration);
            var gridRequestHeaders = configurationWrapper.CurrentPowerToGridHeaders();
            foreach (var gridRequestHeader in gridRequestHeaders)
            {
                newGridConfiguration.Headers.Add(new RestValueConfigurationHeader()
                {
                    Key = gridRequestHeader.Key,
                    Value = gridRequestHeader.Value,
                });
            }
            var resultConfiguration = new RestValueResultConfiguration()
            {
                CorrectionFactor = configurationWrapper.CurrentPowerToGridCorrectionFactor(),
                UsedFor = ValueUsage.GridPower,
            };
            if (newGridConfiguration.NodePatternType == NodePatternType.Xml)
            {
                resultConfiguration.NodePattern = configurationWrapper.CurrentPowerToGridXmlPattern();
                resultConfiguration.XmlAttributeHeaderName = configurationWrapper.CurrentPowerToGridXmlAttributeHeaderName();
                resultConfiguration.XmlAttributeHeaderValue = configurationWrapper.CurrentPowerToGridXmlAttributeHeaderValue();
                resultConfiguration.XmlAttributeValueName = configurationWrapper.CurrentPowerToGridXmlAttributeValueName();
            }
            else if (newGridConfiguration.NodePatternType == NodePatternType.Json)
            {
                resultConfiguration.NodePattern = configurationWrapper.CurrentPowerToGridJsonPattern();
            }
            newGridConfiguration.RestValueResultConfigurations.Add(resultConfiguration);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }
    }

    public async Task UpdatePvValues()
    {
        logger.LogTrace("{method}()", nameof(UpdatePvValues));
        var errorWhileUpdatingPvValues = false;
        if (configurationWrapper.ShouldUseFakeSolarValues())
        {
            logger.LogWarning("Fake solar values are used.");
            if (true)
            {
                foreach (var car in settings.CarsToManage)
                {
                    car.ChargerActualCurrent = 1;
                    car.ChargerVoltage = 1;
                    car.ChargerPhases = 1;
                }
                if (((settings.LastPvDemoCase / 16) % 2) == 0)
                {
                    foreach (var dtoCar in settings.CarsToManage)
                    {
                        dtoCar.IsHomeGeofence = true;
                    }
                }
                else
                {
                    foreach (var dtoCar in settings.CarsToManage)
                    {
                        dtoCar.IsHomeGeofence = false;
                    }
                }
                switch ((settings.LastPvDemoCase++ % 16))
                {
                    case 0:
                        settings.InverterPower = null;
                        settings.Overage = null;
                        settings.HomeBatteryPower = null;
                        settings.HomeBatterySoc = null;
                        break;
                    case 1:
                        settings.InverterPower = null;
                        settings.Overage = 200;
                        settings.HomeBatteryPower = null;
                        settings.HomeBatterySoc = null;
                        break;
                    case 8:
                        settings.InverterPower = null;
                        settings.Overage = -200;
                        settings.HomeBatteryPower = null;
                        settings.HomeBatterySoc = null;
                        break;
                    case 9:
                        settings.InverterPower = null;
                        settings.Overage = 0;
                        settings.HomeBatteryPower = null;
                        settings.HomeBatterySoc = null;
                        break;
                    case 2:
                        settings.InverterPower = 500;
                        settings.Overage = null;
                        settings.HomeBatteryPower = null;
                        settings.HomeBatterySoc = null;
                        break;
                    case 5:
                        settings.InverterPower = 0;
                        settings.Overage = null;
                        settings.HomeBatteryPower = null;
                        settings.HomeBatterySoc = null;
                        break;
                    case 3:
                        settings.InverterPower = 500;
                        settings.Overage = 300;
                        settings.HomeBatteryPower = null;
                        settings.HomeBatterySoc = null;
                        break;
                    case 4:
                        settings.InverterPower = 500;
                        settings.Overage = -300;
                        settings.HomeBatteryPower = null;
                        settings.HomeBatterySoc = null;
                        break;
                    case 6:
                        settings.InverterPower = 0;
                        settings.Overage = -300;
                        settings.HomeBatteryPower = null;
                        settings.HomeBatterySoc = null;
                        break;
                    case 7:
                        settings.InverterPower = 0;
                        settings.Overage = -300;
                        settings.HomeBatteryPower = 0;
                        settings.HomeBatterySoc = 0;
                        break;
                    case 10:
                        settings.InverterPower = 0;
                        settings.Overage = -300;
                        settings.HomeBatteryPower = -500;
                        settings.HomeBatterySoc = 20;
                        break;
                    case 11:
                        settings.InverterPower = 0;
                        settings.Overage = 300;
                        settings.HomeBatteryPower = -500;
                        settings.HomeBatterySoc = 20;
                        break;
                    case 12:
                        settings.InverterPower = 1000;
                        settings.Overage = 300;
                        settings.HomeBatteryPower = 500;
                        settings.HomeBatterySoc = 20;
                        break;
                    case 13:
                        settings.InverterPower = 1000;
                        settings.Overage = -20;
                        settings.HomeBatteryPower = 500;
                        settings.HomeBatterySoc = 20;
                        break;
                    case 14:
                        settings.InverterPower = 10;
                        settings.Overage = -200;
                        settings.HomeBatteryPower = 100;
                        settings.HomeBatterySoc = 20;
                        break;
                    case 15:
                        settings.InverterPower = 10;
                        settings.Overage = -500;
                        settings.HomeBatteryPower = 100;
                        settings.HomeBatterySoc = 20;
                        break;

                }
            }
            else
            {
                var random = new Random();
                var fakeInverterPower = random.Next(0, 30000);
                var fakeHousePower = random.Next(500, 25000);
                var fakeOverage = fakeInverterPower - fakeHousePower;
                var fakeHomeBatteryPower = 0;
                if (Math.Abs(fakeOverage) < 7000)
                {
                    var deviation = random.Next(-150, 150);
                    fakeHomeBatteryPower = fakeOverage - deviation;
                    fakeOverage = -deviation;
                }
                else
                {
                    if (fakeOverage > 0)
                    {
                        fakeHomeBatteryPower = 7000;
                    }
                    else
                    {
                        fakeHomeBatteryPower = -7000;
                    }
                    fakeOverage -= fakeHomeBatteryPower;
                }
                settings.InverterPower = fakeInverterPower;
                settings.Overage = fakeOverage;
                settings.HomeBatteryPower = fakeHomeBatteryPower;
                settings.HomeBatterySoc = 82;
                settings.LastPvValueUpdate = dateTimeProvider.DateTimeOffSetUtcNow();
            }
            
            
            return;
        }

        var valueUsages = new HashSet<ValueUsage>
        {
            ValueUsage.InverterPower,
            ValueUsage.GridPower,
            ValueUsage.HomeBatteryPower,
            ValueUsage.HomeBatterySoc,
        };
        var resultSums = new Dictionary<ValueUsage, decimal>();
        //ToDo: Modbus and rest values can be requersted in parallel but dictionary needs to be thread save for that
        var restConfigurations = await restValueConfigurationService
            .GetFullRestValueConfigurationsByPredicate(c => c.RestValueResultConfigurations.Any(r => valueUsages.Contains(r.UsedFor))).ConfigureAwait(false);
        foreach (var restConfiguration in restConfigurations)
        {
            try
            {
                var responseString = await restValueExecutionService.GetResult(restConfiguration).ConfigureAwait(false);
                var resultConfigurations = await restValueConfigurationService.GetResultConfigurationsByConfigurationId(restConfiguration.Id).ConfigureAwait(false);
                var results = new Dictionary<int, decimal>();
                foreach (var resultConfiguration in resultConfigurations)
                {
                    results.Add(resultConfiguration.Id, restValueExecutionService.GetValue(responseString, restConfiguration.NodePatternType, resultConfiguration));
                }
                foreach (var result in results)
                {
                    var valueUsage = resultConfigurations.First(r => r.Id == result.Key).UsedFor;
                    if (!resultSums.ContainsKey(valueUsage))
                    {
                        resultSums[valueUsage] = 0;
                    }
                    resultSums[valueUsage] += result.Value;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while getting result for {restConfigurationId} with URL {url}", restConfiguration.Id, restConfiguration.Url);
                errorWhileUpdatingPvValues = true;
            }
        }

        var modbusConfigurations = await modbusValueConfigurationService.GetModbusConfigurationByPredicate(c => c.ModbusResultConfigurations.Any(r => valueUsages.Contains(r.UsedFor))).ConfigureAwait(false);
        foreach (var modbusConfiguration in modbusConfigurations)
        {
            logger.LogDebug("Get Modbus results for modbus Configuration {host}:{port}", modbusConfiguration.Host,
                modbusConfiguration.Port);
            var modbusResultConfigurations =
                await modbusValueConfigurationService.GetModbusResultConfigurationsByPredicate(r =>
                    r.ModbusConfigurationId == modbusConfiguration.Id);
            foreach (var resultConfiguration in modbusResultConfigurations)
            {
                logger.LogDebug("Get Modbus result for modbus Configuration {host}:{port}: Register: {register}", modbusConfiguration.Host,
                    modbusConfiguration.Port, resultConfiguration.Address);
                var byteArry = await modbusValueExecutionService.GetResult(modbusConfiguration, resultConfiguration);
                logger.LogDebug("Got Modbus result for modbus Configuration {host}:{port}: Register: {register}, Result: {bitResult}", modbusConfiguration.Host,
                                       modbusConfiguration.Port, resultConfiguration.Address, modbusValueExecutionService.GetBinaryString(byteArry));
                var value = await modbusValueExecutionService.GetValue(byteArry, resultConfiguration);
                var valueUsage = resultConfiguration.UsedFor;
                if (!resultSums.ContainsKey(valueUsage))
                {
                    resultSums[valueUsage] = 0;
                }
                resultSums[valueUsage] += value;
            }
        }

        var mqttValues = mqttClientHandlingService.GetMqttValues();
        foreach (var mqttValue in mqttValues)
        {
            if (valueUsages.Contains(mqttValue.UsedFor))
            {
                if (!resultSums.ContainsKey(mqttValue.UsedFor))
                {
                    resultSums[mqttValue.UsedFor] = 0;
                }
                resultSums[mqttValue.UsedFor] += mqttValue.Value;
            }
        }


        int? inverterValue = resultSums.TryGetValue(ValueUsage.InverterPower, out var inverterPower) ?
            SafeToInt(inverterPower) : null;
        settings.InverterPower = inverterValue < 0 ? 0 : inverterValue;
        settings.Overage = resultSums.TryGetValue(ValueUsage.GridPower, out var gridPower) ?
            SafeToInt(gridPower) : null;
        settings.HomeBatteryPower = resultSums.TryGetValue(ValueUsage.HomeBatteryPower, out var homeBatteryPower) ?
            SafeToInt(homeBatteryPower) : null;
        settings.HomeBatterySoc = resultSums.TryGetValue(ValueUsage.HomeBatterySoc, out var homeBatterySoc) ?
            SafeToInt(homeBatterySoc) : null;
        if (!errorWhileUpdatingPvValues)
        {
            settings.LastPvValueUpdate = dateTimeProvider.DateTimeOffSetUtcNow();
        }
        int? powerBuffer = configurationWrapper.PowerBuffer();
        if (settings.InverterPower == null && settings.Overage == null)
        {
            powerBuffer = null;
        }
        var loadPoints = await loadPointManagementService.GetLoadPointsWithChargingDetails().ConfigureAwait(false);
        var pvValues = new DtoPvValues()
        {
            GridPower = settings.Overage,
            InverterPower = settings.InverterPower,
            HomeBatteryPower = settings.HomeBatteryPower,
            HomeBatterySoc = settings.HomeBatterySoc,
            PowerBuffer = powerBuffer,
            CarCombinedChargingPowerAtHome = loadPoints.Select(l => l.ChargingPower).Sum(),
            LastUpdated = settings.LastPvValueUpdate,
        };
        var changes = changeTrackingService.DetectChanges(
            DataTypeConstants.PvValues,
            null, // No entity ID for singleton PV values
            pvValues);

        if (changes != null)
        {
            await appStateNotifier.NotifyStateUpdateAsync(changes).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Safely converts a decimal value to an integer, clamping the value within the range of int.MinValue to int.MaxValue.
    /// </summary>
    /// <param name="value">The decimal value to convert.</param>
    /// <returns>An integer value clamped within the legal range of an int.</returns>
    private static int SafeToInt(decimal value)
    {
        return (int)Math.Min(Math.Max(value, int.MinValue), int.MaxValue);
    }



    private async Task<int?> GetValueByHttpResponse(HttpResponseMessage? httpResponse, string? jsonPattern, string? xmlPattern,
        double correctionFactor, NodePatternType nodePatternType, string? xmlAttributeHeaderName, string? xmlAttributeHeaderValue, string? xmlAttributeValueName)
    {
        int? intValue;
        if (httpResponse == null)
        {
            logger.LogError("HttpResponse is null, extraction of value is not possible");
            return null;
        }
        if (!httpResponse.IsSuccessStatusCode)
        {
            intValue = null;
            logger.LogError("Could not get value. {statusCode}, {reasonPhrase}", httpResponse.StatusCode,
                httpResponse.ReasonPhrase);
            await telegramService.SendMessage(
                    $"Getting value did result in statuscode {httpResponse.StatusCode} with reason {httpResponse.ReasonPhrase}")
                .ConfigureAwait(false);
        }
        else
        {
            intValue = await GetIntegerValue(httpResponse, jsonPattern, xmlPattern, correctionFactor, nodePatternType, xmlAttributeHeaderName, xmlAttributeHeaderValue, xmlAttributeValueName).ConfigureAwait(false);
        }

        return intValue;
    }

    private async Task<HttpResponseMessage> GetHttpResponse(HttpRequestMessage request)
    {
        logger.LogTrace("{method}({request}) [called by {callingMethod}]", nameof(GetHttpResponse), request, new StackTrace().GetFrame(1)?.GetMethod()?.Name);
        var httpClientHandler = new HttpClientHandler();

        if (configurationWrapper.ShouldIgnoreSslErrors())
        {
            logger.LogWarning("PV Value SSL errors are ignored.");
            httpClientHandler.ServerCertificateCustomValidationCallback = MyRemoteCertificateValidationCallback;
        }

        using var httpClient = new HttpClient(httpClientHandler);
        var response = await httpClient.SendAsync(request).ConfigureAwait(false);
        return response;
    }

    private bool MyRemoteCertificateValidationCallback(HttpRequestMessage requestMessage, X509Certificate2? certificate, X509Chain? chain, SslPolicyErrors sslErrors)
    {
        return true; // Ignoriere alle Zertifikatfehler
    }

    private static HttpRequestMessage GenerateHttpRequestMessage(string? gridRequestUrl, Dictionary<string, string> requestHeaders)
    {
        if (string.IsNullOrEmpty(gridRequestUrl))
        {
            throw new ArgumentNullException(nameof(gridRequestUrl));
        }

        var request = new HttpRequestMessage(HttpMethod.Get, gridRequestUrl);
        request.Headers.Add("Accept", "*/*");
        foreach (var requestHeader in requestHeaders)
        {
            request.Headers.Add(requestHeader.Key, requestHeader.Value);
        }

        return request;
    }

    public void ClearOverageValues()
    {
        inMemoryValues.OverageValues.Clear();
    }

    public void AddOverageValueToInMemoryList(int overage)
    {
        logger.LogTrace("{method}({overage})", nameof(AddOverageValueToInMemoryList), overage);
        inMemoryValues.OverageValues.Add(overage);

        var valuesToSave = (int)(configurationWrapper.ChargingValueJobUpdateIntervall().TotalSeconds /
                            configurationWrapper.PvValueJobUpdateIntervall().TotalSeconds);

        if (inMemoryValues.OverageValues.Count > valuesToSave)
        {
            inMemoryValues.OverageValues.RemoveRange(0, inMemoryValues.OverageValues.Count - valuesToSave);
        }
    }

    internal bool IsSameRequest(HttpRequestMessage? httpRequestMessage1, HttpRequestMessage httpRequestMessage2)
    {
        logger.LogTrace("{method}({request1}, {request2})", nameof(IsSameRequest), httpRequestMessage1, httpRequestMessage2);
        if (httpRequestMessage1 == null)
        {
            logger.LogTrace("Not same request as first request is null.");
            return false;
        }
        if (httpRequestMessage1.Method != httpRequestMessage2.Method)
        {
            logger.LogDebug("not same request as request1 method is {request1} and request2 method is {request2}",
                httpRequestMessage1.Method, httpRequestMessage2.Method);
            return false;
        }

        if (httpRequestMessage1.RequestUri != httpRequestMessage2.RequestUri)
        {
            logger.LogDebug("not same request as request1 Uri is {request1} and request2 Uri is {request2}",
                httpRequestMessage1.RequestUri, httpRequestMessage2.RequestUri);
            return false;
        }

        if (httpRequestMessage1.Headers.Count() != httpRequestMessage2.Headers.Count())
        {
            logger.LogDebug("not same request as request1 header count is {request1} and request2 header count is {request2}",
                httpRequestMessage1.Headers.Count(), httpRequestMessage2.Headers.Count());
            return false;
        }

        foreach (var httpRequestHeader in httpRequestMessage1.Headers)
        {
            var message2Header = httpRequestMessage2.Headers.FirstOrDefault(h => h.Key.Equals(httpRequestHeader.Key));
            if (message2Header.Key == default)
            {
                return false;
            }
            var message2HeaderValue = message2Header.Value.ToList();
            foreach (var headerValue in httpRequestHeader.Value)
            {
                if (!message2HeaderValue.Any(v => string.Equals(v, headerValue, StringComparison.InvariantCulture)))
                {
                    return false;
                }
            }
        }


        return true;
    }



    private async Task<int?> GetIntegerValue(HttpResponseMessage response, string? jsonPattern, string? xmlPattern, double correctionFactor,
        NodePatternType nodePatternType, string? xmlAttributeHeaderName, string? xmlAttributeHeaderValue, string? xmlAttributeValueName)
    {
        logger.LogTrace("{method}({httpResonse}, {jsonPattern}, {xmlPattern}, {correctionFactor})",
            nameof(GetIntegerValue), response, jsonPattern, xmlPattern, correctionFactor);

        var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        return GetIntegerValueByString(result, jsonPattern, xmlPattern, correctionFactor, nodePatternType, xmlAttributeHeaderName, xmlAttributeHeaderValue, xmlAttributeValueName);
    }

    public int? GetIntegerValueByString(string valueString, string? jsonPattern, string? xmlPattern, double correctionFactor,
        NodePatternType nodePatternType, string? xmlAttributeHeaderName, string? xmlAttributeHeaderValue, string? xmlAttributeValueName)
    {
        logger.LogTrace("{method}({valueString}, {jsonPattern}, {xmlPattern}, {correctionFactor})",
            nameof(GetIntegerValueByString), valueString, jsonPattern, xmlPattern, correctionFactor);
        var pattern = string.Empty;

        if (nodePatternType == NodePatternType.Json)
        {
            pattern = jsonPattern;
        }
        else if (nodePatternType == NodePatternType.Xml)
        {
            pattern = xmlPattern;
        }

        var doubleValue = GetValueFromResult(pattern, valueString, nodePatternType, xmlAttributeHeaderName, xmlAttributeHeaderValue, xmlAttributeValueName);

        return (int?)(doubleValue * correctionFactor);
    }
    
    internal double GetValueFromResult(string? pattern, string result, NodePatternType patternType,
        string? xmlAttributeHeaderName, string? xmlAttributeHeaderValue, string? xmlAttributeValueName)
    {
        switch (patternType)
        {
            //allow JSON values to be null, as this is needed by SMA inverters: https://tff-forum.de/t/teslasolarcharger-laden-nach-pv-ueberschuss-mit-beliebiger-wallbox/170369/2728?u=mane123
            case NodePatternType.Json:
                logger.LogTrace("Extract overage value from json {result} with {pattern}", result, pattern);
                result = (JObject.Parse(result).SelectToken(pattern ?? throw new ArgumentNullException(nameof(pattern))) ??
                          throw new InvalidOperationException("Could not find token by pattern")).Value<string>() ?? "0";
                break;
            case NodePatternType.Xml:
                logger.LogTrace("Extract overage value from xml {result} with {pattern}", result, pattern);
                var xmlDocument = new XmlDocument();
                xmlDocument.LoadXml(result);
                var nodes = xmlDocument.SelectNodes(pattern ?? throw new ArgumentNullException(nameof(pattern))) ?? throw new InvalidOperationException("Could not find any nodes by pattern");
                switch (nodes.Count)
                {
                    case < 1:
                        throw new InvalidOperationException($"Could not find any nodes with pattern {pattern}");
                    case 1:
                        result = nodes[0]?.LastChild?.Value ?? "0";
                        break;
                    case > 2:
                        for (var i = 0; i < nodes.Count; i++)
                        {
                            if (nodes[i]?.Attributes?[xmlAttributeHeaderName ?? throw new ArgumentNullException(nameof(xmlAttributeHeaderName))]?.Value == xmlAttributeHeaderValue)
                            {
                                result = nodes[i]?.Attributes?[xmlAttributeValueName ?? throw new ArgumentNullException(nameof(xmlAttributeValueName))]?.Value ?? "0";
                                break;
                            }
                        }
                        break;
                }
                break;
        }

        return GetdoubleFromStringResult(result);
    }

    internal double GetdoubleFromStringResult(string? inputString)
    {
        logger.LogTrace("{method}({param})", nameof(GetdoubleFromStringResult), inputString);
        return double.Parse(inputString ?? throw new ArgumentNullException(nameof(inputString)), CultureInfo.InvariantCulture);
    }
}
