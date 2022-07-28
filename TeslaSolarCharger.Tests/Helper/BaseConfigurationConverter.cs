using System;
using Newtonsoft.Json;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration.OldVersions.V0._1;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Helper;

public class BaseConfigurationConverter : TestBase
{
    public BaseConfigurationConverter(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Fact]
    public void Can_Detect_Missing_Version()
    {
        var converter = Mock.Create<Server.Helper.BaseConfigurationConverter>();
        const string baseConfigJsonString = "{\"LastEditDateTime\":\"2022-07-21T13:44:22.9562369Z\",\"CurrentPowerToGridUrl\":\"http://192.168.1.50:5007/api/ChargingLog/GetCurrentGridPower\",\"CurrentInverterPowerUrl\":\"http://modbusplugin/api/Modbus/GetValue?unitIdentifier=3&startingAddress=30775&quantity=2&ipAddress=192.168.1.28&port=502&factor=1&connectDelaySeconds=0&timeoutSeconds=1&minimumResult=0\",\"TeslaMateApiBaseUrl\":\"http://teslamateapi:8080\",\"UpdateIntervalSeconds\":30,\"PvValueUpdateIntervalSeconds\":1,\"CarPriorities\":\"1|2\",\"GeoFence\":\"Zu Hause\",\"MinutesUntilSwitchOn\":5,\"MinutesUntilSwitchOff\":5,\"PowerBuffer\":0,\"CurrentPowerToGridJsonPattern\":null,\"CurrentPowerToGridInvertValue\":false,\"CurrentInverterPowerJsonPattern\":null,\"TelegramBotKey\":\"\",\"TelegramChannelId\":\"\",\"TeslaMateDbServer\":\"database\",\"TeslaMateDbPort\":5432,\"TeslaMateDbDatabaseName\":\"teslamate\",\"TeslaMateDbUser\":\"teslamate\",\"TeslaMateDbPassword\":\"secret\",\"MqqtClientId\":\"TeslaSolarCharger\",\"MosquitoServer\":\"mosquitto\",\"CurrentPowerToGridXmlPattern\":null,\"CurrentPowerToGridXmlAttributeHeaderName\":null,\"CurrentPowerToGridXmlAttributeHeaderValue\":null,\"CurrentPowerToGridXmlAttributeValueName\":null,\"CurrentInverterPowerXmlPattern\":null,\"CurrentInverterPowerXmlAttributeHeaderName\":null,\"CurrentInverterPowerXmlAttributeHeaderValue\":null,\"CurrentInverterPowerXmlAttributeValueName\":null}";

        var version = converter.GetVersionFromBaseConfigurationJsonString(baseConfigJsonString);

        Assert.Equal(new Version(0, 1), version);
    }

    [Fact]
    public void Can_Move_BaseConfig_ValuesV0_1ToV1_0()
    {
        var converter = Mock.Create<Server.Helper.BaseConfigurationConverter>();
        const string baseConfigJsonString = "{\"LastEditDateTime\":\"2022-07-21T13:44:22.9562369Z\",\"CurrentPowerToGridUrl\":\"http://192.168.1.50:5007/api/ChargingLog/GetCurrentGridPower\",\"CurrentInverterPowerUrl\":\"http://modbusplugin/api/Modbus/GetValue?unitIdentifier=3&startingAddress=30775&quantity=2&ipAddress=192.168.1.28&port=502&factor=1&connectDelaySeconds=0&timeoutSeconds=1&minimumResult=0\",\"TeslaMateApiBaseUrl\":\"http://teslamateapi:8080\",\"UpdateIntervalSeconds\":30,\"PvValueUpdateIntervalSeconds\":1,\"CarPriorities\":\"1|2\",\"GeoFence\":\"Zu Hause\",\"MinutesUntilSwitchOn\":5,\"MinutesUntilSwitchOff\":5,\"PowerBuffer\":0,\"CurrentPowerToGridJsonPattern\":null,\"CurrentPowerToGridInvertValue\":false,\"CurrentInverterPowerJsonPattern\":null,\"TelegramBotKey\":\"\",\"TelegramChannelId\":\"\",\"TeslaMateDbServer\":\"database\",\"TeslaMateDbPort\":5432,\"TeslaMateDbDatabaseName\":\"teslamate\",\"TeslaMateDbUser\":\"teslamate\",\"TeslaMateDbPassword\":\"secret\",\"MqqtClientId\":\"TeslaSolarCharger\",\"MosquitoServer\":\"mosquitto\",\"CurrentPowerToGridXmlPattern\":null,\"CurrentPowerToGridXmlAttributeHeaderName\":null,\"CurrentPowerToGridXmlAttributeHeaderValue\":null,\"CurrentPowerToGridXmlAttributeValueName\":null,\"CurrentInverterPowerXmlPattern\":null,\"CurrentInverterPowerXmlAttributeHeaderName\":null,\"CurrentInverterPowerXmlAttributeHeaderValue\":null,\"CurrentInverterPowerXmlAttributeValueName\":null}";

        var oldBaseConfig = JsonConvert.DeserializeObject<BaseConfigurationJsonV0_1>(baseConfigJsonString) ?? throw new InvalidOperationException();

        var newBaseConfig = converter.ConvertV0_1ToV1_0(oldBaseConfig);

        Assert.Equal(oldBaseConfig.CarPriorities, newBaseConfig.CarPriorities);
        Assert.Equal(oldBaseConfig.CurrentInverterPowerUrl, newBaseConfig.CurrentInverterPowerUrl);
        Assert.Equal(oldBaseConfig.UpdateIntervalSeconds, newBaseConfig.UpdateIntervalSeconds);
        Assert.Equal(oldBaseConfig.CurrentPowerToGridJsonPattern, newBaseConfig.CurrentPowerToGridJsonPattern);
        Assert.Equal(1, newBaseConfig.CurrentPowerToGridCorrectionFactor);
    }

    [Fact]
    public void Can_Convert_BaseConfigValuesV0_1ToV1_0_InvertGridValueToFactor()
    {
        var converter = Mock.Create<Server.Helper.BaseConfigurationConverter>();
        const string baseConfigJsonString = "{\"LastEditDateTime\":\"2022-07-21T13:44:22.9562369Z\",\"CurrentPowerToGridUrl\":\"http://192.168.1.50:5007/api/ChargingLog/GetCurrentGridPower\",\"CurrentInverterPowerUrl\":\"http://modbusplugin/api/Modbus/GetValue?unitIdentifier=3&startingAddress=30775&quantity=2&ipAddress=192.168.1.28&port=502&factor=1&connectDelaySeconds=0&timeoutSeconds=1&minimumResult=0\",\"TeslaMateApiBaseUrl\":\"http://teslamateapi:8080\",\"UpdateIntervalSeconds\":30,\"PvValueUpdateIntervalSeconds\":1,\"CarPriorities\":\"1|2\",\"GeoFence\":\"Zu Hause\",\"MinutesUntilSwitchOn\":5,\"MinutesUntilSwitchOff\":5,\"PowerBuffer\":0,\"CurrentPowerToGridJsonPattern\":null,\"CurrentPowerToGridInvertValue\":true,\"CurrentInverterPowerJsonPattern\":null,\"TelegramBotKey\":\"\",\"TelegramChannelId\":\"\",\"TeslaMateDbServer\":\"database\",\"TeslaMateDbPort\":5432,\"TeslaMateDbDatabaseName\":\"teslamate\",\"TeslaMateDbUser\":\"teslamate\",\"TeslaMateDbPassword\":\"secret\",\"MqqtClientId\":\"TeslaSolarCharger\",\"MosquitoServer\":\"mosquitto\",\"CurrentPowerToGridXmlPattern\":null,\"CurrentPowerToGridXmlAttributeHeaderName\":null,\"CurrentPowerToGridXmlAttributeHeaderValue\":null,\"CurrentPowerToGridXmlAttributeValueName\":null,\"CurrentInverterPowerXmlPattern\":null,\"CurrentInverterPowerXmlAttributeHeaderName\":null,\"CurrentInverterPowerXmlAttributeHeaderValue\":null,\"CurrentInverterPowerXmlAttributeValueName\":null}";

        var oldBaseConfig = JsonConvert.DeserializeObject<BaseConfigurationJsonV0_1>(baseConfigJsonString) ?? throw new InvalidOperationException();

        var newBaseConfig = converter.ConvertV0_1ToV1_0(oldBaseConfig);

        Assert.Equal(-1, newBaseConfig.CurrentPowerToGridCorrectionFactor);
    }

    [Fact]
    public void Can_Convert_BaseConfigValuesV0_1ToV1_0_ModbusGridUrl()
    {
        var converter = Mock.Create<Server.Helper.BaseConfigurationConverter>();
        const string baseConfigJsonString = "{\"LastEditDateTime\":\"2022-07-21T13:44:22.9562369Z\",\"CurrentPowerToGridUrl\":\"http://192.168.1.50:5007/api/ChargingLog/GetCurrentGridPower\",\"CurrentInverterPowerUrl\":\"http://modbusplugin/api/Modbus/GetValue?unitIdentifier=3&startingAddress=30775&quantity=2&ipAddress=192.168.1.28&port=502&factor=1&connectDelaySeconds=0&timeoutSeconds=1&minimumResult=0\",\"TeslaMateApiBaseUrl\":\"http://teslamateapi:8080\",\"UpdateIntervalSeconds\":30,\"PvValueUpdateIntervalSeconds\":1,\"CarPriorities\":\"1|2\",\"GeoFence\":\"Zu Hause\",\"MinutesUntilSwitchOn\":5,\"MinutesUntilSwitchOff\":5,\"PowerBuffer\":0,\"CurrentPowerToGridJsonPattern\":null,\"CurrentPowerToGridInvertValue\":true,\"CurrentInverterPowerJsonPattern\":null,\"TelegramBotKey\":\"\",\"TelegramChannelId\":\"\",\"TeslaMateDbServer\":\"database\",\"TeslaMateDbPort\":5432,\"TeslaMateDbDatabaseName\":\"teslamate\",\"TeslaMateDbUser\":\"teslamate\",\"TeslaMateDbPassword\":\"secret\",\"MqqtClientId\":\"TeslaSolarCharger\",\"MosquitoServer\":\"mosquitto\",\"CurrentPowerToGridXmlPattern\":null,\"CurrentPowerToGridXmlAttributeHeaderName\":null,\"CurrentPowerToGridXmlAttributeHeaderValue\":null,\"CurrentPowerToGridXmlAttributeValueName\":null,\"CurrentInverterPowerXmlPattern\":null,\"CurrentInverterPowerXmlAttributeHeaderName\":null,\"CurrentInverterPowerXmlAttributeHeaderValue\":null,\"CurrentInverterPowerXmlAttributeValueName\":null}";

        var oldBaseConfig = JsonConvert.DeserializeObject<BaseConfigurationJsonV0_1>(baseConfigJsonString) ?? throw new InvalidOperationException();

        var newBaseConfig = converter.ConvertV0_1ToV1_0(oldBaseConfig);

        Assert.Equal(-1, newBaseConfig.CurrentPowerToGridCorrectionFactor);
    }

    [Fact]
    public void Can_Convert_BaseConfigValuesV0_1ToV1_0_DoesNotUpdateGridUrlWhenNotNeeded()
    {
        var converter = Mock.Create<Server.Helper.BaseConfigurationConverter>();
        const string baseConfigJsonString = "{\"LastEditDateTime\":\"2022-07-21T13:44:22.9562369Z\",\"CurrentPowerToGridUrl\":\"http://192.168.1.50:5007/api/ChargingLog/GetCurrentGridPower\",\"CurrentInverterPowerUrl\":\"http://modbusplugin/api/Modbus/GetValue?unitIdentifier=3&startingAddress=30775&quantity=2&ipAddress=192.168.1.28&port=502&factor=1&connectDelaySeconds=0&timeoutSeconds=1&minimumResult=0\",\"TeslaMateApiBaseUrl\":\"http://teslamateapi:8080\",\"UpdateIntervalSeconds\":30,\"PvValueUpdateIntervalSeconds\":1,\"CarPriorities\":\"1|2\",\"GeoFence\":\"Zu Hause\",\"MinutesUntilSwitchOn\":5,\"MinutesUntilSwitchOff\":5,\"PowerBuffer\":0,\"CurrentPowerToGridJsonPattern\":null,\"CurrentPowerToGridInvertValue\":true,\"CurrentInverterPowerJsonPattern\":null,\"TelegramBotKey\":\"\",\"TelegramChannelId\":\"\",\"TeslaMateDbServer\":\"database\",\"TeslaMateDbPort\":5432,\"TeslaMateDbDatabaseName\":\"teslamate\",\"TeslaMateDbUser\":\"teslamate\",\"TeslaMateDbPassword\":\"secret\",\"MqqtClientId\":\"TeslaSolarCharger\",\"MosquitoServer\":\"mosquitto\",\"CurrentPowerToGridXmlPattern\":null,\"CurrentPowerToGridXmlAttributeHeaderName\":null,\"CurrentPowerToGridXmlAttributeHeaderValue\":null,\"CurrentPowerToGridXmlAttributeValueName\":null,\"CurrentInverterPowerXmlPattern\":null,\"CurrentInverterPowerXmlAttributeHeaderName\":null,\"CurrentInverterPowerXmlAttributeHeaderValue\":null,\"CurrentInverterPowerXmlAttributeValueName\":null}";

        var oldBaseConfig = JsonConvert.DeserializeObject<BaseConfigurationJsonV0_1>(baseConfigJsonString) ?? throw new InvalidOperationException();

        var newBaseConfig = converter.ConvertV0_1ToV1_0(oldBaseConfig);

        Assert.Equal(oldBaseConfig.CurrentPowerToGridUrl, newBaseConfig.CurrentPowerToGridUrl);
        Assert.Equal(-1, newBaseConfig.CurrentPowerToGridCorrectionFactor);
    }

    [Fact]
    public void Can_Convert_BaseConfigValuesV0_1ToV1_0_UpdateGridUrlWhenNotNeeded()
    {
        var converter = Mock.Create<Server.Helper.BaseConfigurationConverter>();
        const string baseConfigJsonString = "{\"LastEditDateTime\":\"2022-07-21T13:44:22.9562369Z\",\"CurrentPowerToGridUrl\":\"http://modbusplugin/api/Modbus/GetValue?unitIdentifier=1&startingAddress=37113&quantity=2&ipAddress=inverterLocIP&port=502&factor=10&connectDelaySeconds=1&timeoutSeconds=10\",\"CurrentInverterPowerUrl\":\"http://modbusplugin/api/Modbus/GetValue?unitIdentifier=3&startingAddress=30775&quantity=2&ipAddress=192.168.1.28&port=502&factor=1&connectDelaySeconds=0&timeoutSeconds=1&minimumResult=0\",\"TeslaMateApiBaseUrl\":\"http://teslamateapi:8080\",\"UpdateIntervalSeconds\":30,\"PvValueUpdateIntervalSeconds\":1,\"CarPriorities\":\"1|2\",\"GeoFence\":\"Zu Hause\",\"MinutesUntilSwitchOn\":5,\"MinutesUntilSwitchOff\":5,\"PowerBuffer\":0,\"CurrentPowerToGridJsonPattern\":null,\"CurrentPowerToGridInvertValue\":true,\"CurrentInverterPowerJsonPattern\":null,\"TelegramBotKey\":\"\",\"TelegramChannelId\":\"\",\"TeslaMateDbServer\":\"database\",\"TeslaMateDbPort\":5432,\"TeslaMateDbDatabaseName\":\"teslamate\",\"TeslaMateDbUser\":\"teslamate\",\"TeslaMateDbPassword\":\"secret\",\"MqqtClientId\":\"TeslaSolarCharger\",\"MosquitoServer\":\"mosquitto\",\"CurrentPowerToGridXmlPattern\":null,\"CurrentPowerToGridXmlAttributeHeaderName\":null,\"CurrentPowerToGridXmlAttributeHeaderValue\":null,\"CurrentPowerToGridXmlAttributeValueName\":null,\"CurrentInverterPowerXmlPattern\":null,\"CurrentInverterPowerXmlAttributeHeaderName\":null,\"CurrentInverterPowerXmlAttributeHeaderValue\":null,\"CurrentInverterPowerXmlAttributeValueName\":null}";

        var oldBaseConfig = JsonConvert.DeserializeObject<BaseConfigurationJsonV0_1>(baseConfigJsonString) ?? throw new InvalidOperationException();

        var newBaseConfig = converter.ConvertV0_1ToV1_0(oldBaseConfig);

        Assert.Equal("http://modbusplugin/api/Modbus/GetValue?unitIdentifier=1&startingAddress=37113&quantity=2&ipAddress=inverterLocIP&port=502&connectDelaySeconds=1&timeoutSeconds=10",
            newBaseConfig.CurrentPowerToGridUrl);
        Assert.Equal(-10, newBaseConfig.CurrentPowerToGridCorrectionFactor);
    }
}