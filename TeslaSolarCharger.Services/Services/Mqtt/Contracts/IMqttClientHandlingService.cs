using MQTTnet;
using System.Collections.ObjectModel;
using TeslaSolarCharger.Services.Services.ValueRefresh;
using TeslaSolarCharger.Shared.Dtos.MqttConfiguration;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Services.Services.Mqtt.Contracts;

public interface IMqttClientHandlingService
{
    Task ConnectClient(DtoMqttConfiguration mqttConfiguration, List<DtoMqttResultConfiguration> resultConfigurations,
        bool forceReconnection);
    void RemoveClient(string host, int port, string? userName);
    IReadOnlyDictionary<ValueUsage, List<DtoHistoricValue<decimal>>> GetSolarValues();
    string CreateMqttClientKey(string host, int port, string? userName);
    IMqttClient? GetClientByKey(string key);
    ReadOnlyDictionary<string, AutoRefreshingValue<decimal>> GetRawValues();
}
