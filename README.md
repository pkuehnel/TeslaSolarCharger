# SmartTeslaAmpSetter

[![Docker version](https://img.shields.io/docker/v/pkuehnel/smartteslaampsetter/latest)](https://hub.docker.com/r/pkuehnel/smartteslaampsetter)
[![Docker size](https://img.shields.io/docker/image-size/pkuehnel/smartteslaampsetter/latest)](https://hub.docker.com/r/pkuehnel/smartteslaampsetter)
[![Docker pulls](https://img.shields.io/docker/pulls/pkuehnel/smartteslaampsetter)](https://hub.docker.com/r/pkuehnel/smartteslaampsetter)
[![](https://img.shields.io/badge/Donate-PayPal-ff69b4.svg)](https://www.paypal.com/donate/?hosted_button_id=S3CK8Q9KV3JUL)

SmartTeslaAmpSetter is service to set one or multiple Teslas' charging current using **[TeslaMateApi](https://github.com/tobiasehlert/teslamateapi)** and any REST Endpoint which presents the Watt to increase or reduce charging power

Needs:
- A running **[TeslaMateApi](https://github.com/tobiasehlert/teslamateapi)** instance, which needs self-hosted data logger **[TeslaMate](https://github.com/adriankumpf/teslamate)**
- REST Endpoint from any Smart Meter which returns current power to grid (values > 0 --> power goes to grid, values < 0 power comes from grid)

### Table of Contents

- [How to use](#how-to-use)
  - [Docker-compose](#docker-compose)
  - [Environment variables](#environment-variables)
  - [Car Priorities](#car-priorities)
  - [Power Buffer](#power-buffer)
  - [UI](#UI)
  - [Charge Modes](#charge-modes)
  - [Telegram Notifications](#telegram-notifications)
  - [Getting Values from XML](#getting-values-from-xml)
    - [Grid-Power](#grid-power)
    - [Inverter-Power](#inverter-power)
  - [Plugins](#plugins)
    - [SMA-EnergyMeter Plugin](#sma-energymeter-plugin)
    - [Solaredge Plugin](#solaredge-plugin)

## How to use

You can either use it in a Docker container or go download the code and deploy it yourself on any server.

### Docker-compose

If you run the simple Docker deployment of TeslaMate, then adding this will do the trick. You'll have the frontend available on port 7190 then. Note: you have to change the CurrentPowerToGridUrl based on your environment. If you use the SMA Plugin you only have to update the IP address.

```yaml
services:
    smartteslaampsetter:
    image: pkuehnel/smartteslaampsetter:latest
    logging:
        driver: "json-file"
        options:
            max-file: "5"
            max-size: "10m"
    restart: always
    depends_on:
      - teslamateapi
    environment:
      - CurrentPowerToGridUrl=http://192.168.1.50/api/CurrentPower/GetPower
      - TeslaMateApiBaseUrl=http://teslamateapi:8080
      - UpdateIntervalSeconds=20
      - CarPriorities=1
      - GeoFence=Home
      - MinutesUntilSwitchOn=5
      - MinutesUntilSwitchOff=5
      - PowerBuffer=0
      - TZ=Europe/Berlin
    ports:
      - 7190:80
    volumes:
      - teslaampsetter-configs:/app/configs
    .
    .
    .
volumes:
  .
  .
  .
  teslaampsetter-configs:
```

Note: TeslaMateApi has to be configured to allow any command without authentication:
```yaml
  teslamateapi:
    image: tobiasehlert/teslamateapi:latest
    logging:
        driver: "json-file"
        options:
            max-file: "5"
            max-size: "10m"
    restart: always
    depends_on:
      - database
    environment:
      - DATABASE_USER=teslamate
      - DATABASE_PASS=secret
      - DATABASE_NAME=teslamate
      - DATABASE_HOST=database
      - MQTT_HOST=mosquitto
      - TZ=Europe/Berlin
      - ENABLE_COMMANDS=true
      - COMMANDS_ALL=true
      - API_TOKEN_DISABLE=true
    ports:
      - 8080:8080
```

### Environment variables

| Variable | Type | Explanation | Example |
|---|---|---|---|
| **CurrentPowerToGridUrl** | string | URL to REST Endpoint of smart meter | http://192.168.1.50/api/CurrentPower/GetPower |
| **CurrentInverterPowerUrl** | string | URL to REST Endpoint of inverter (optional) | http://192.168.1.50/api/CurrentInverterPower |
| **TeslaMateApiBaseUrl** | string | Base URL to TeslaMateApi instance | http://teslamateapi:8080 |
| **UpdateIntervalSeconds** | int | Intervall how often the charging amps should be set (Note: TeslaMateApi takes some time to get new current values, so do not set a value lower than 30) | 30 |
| **CarPriorities** | string | TeslaMate Car Ids separated by \| in the priority order. | 1\|2 |
| **GeoFence** | string | TeslaMate Geofence Name where amps should be set | Home |
| **MinutesUntilSwitchOn** | int | Minutes with more power to grid than minimum settable until charging starts | 5 |
| **MinutesUntilSwitchOff** | int | Minutes with power from grid until charging stops | 5 |
| **PowerBuffer** | int | Power Buffer in Watt | 0 |
| **CurrentPowerToGridJsonPattern** | string | If Power to grid is json formated use this to extract the correct value | $.data.overage |
| **CurrentPowerToGridInvertValue** | boolean | Set this to `true` if Power from grid has positive values and power to grid has negative values | true |
| **CurrentInverterPowerJsonPattern** | string | If Power from inverter is json formated use this to extract the correct value | $.data.overage |
| **TelegramBotKey** | string | Telegram Bot API key | 1234567890:ASDFuiauhwerlfvasedr |
| **TelegramChannelId** | string | ChannelId Telegram bot should send messages to | -156480125 |

### Car Priorities
If you set `CarPriorities` environment variable like the example above, the car with ID 2 will only start charing, if car 1 is charging at full speed and there is still power left, or if car 1 is not charging due to reached battery limit or not within specified geofence. Note: You always have to add the car Ids to this list separated by `|`. Even if you only have one car you need to ad the car's Id but then without `|`.

### Power Buffer
If you set `PowerBuffer` to a value different from `0` the system uses the value as an offset. Eg. If you set `1000` the current of the car is reduced as long as there is less than 1000 Watt power going to the grid.

### UI
The current UI can display the car's names including SOC and SOC Limit + one Button to switch between different charge modes. If you set the port like in the example above, you can access the UI via http://ip-to-host:7190/

### Charge Modes
Currently there are three different charge modes available:
1. **PV only**: Only solar energy is used to charge. You can set a SOC level which should be reached at a specific date and time. If solar energy is not enough to reach the set soc level in time, the car starts charging at full speed. Note: To let this work, you have to specify `usable kWh` in the car settings section.
1. **Maximum Power**: Car charges with maximum available power
1. **Min SoC + PV**: If plugged in the car starts charging with maximum power until set Min SoC is reached. After that only PV Power is used to charge the car.

### Telegram Notifications
If you set the environment variables `TelegramBotKey`and `TelegramChannelId`, you get messages, if a car can not be woken up, or any command could not be sent to a Tesla. Note: If your car takes longer than 30 seconds to wake up probably you will get an error notification, but as soon as the car is online charging starts.
You can check if your Key and Channel Id is working by restarting the container. If your configuration is working, on startup the application sends a demo message to the specified Telegram channel/chat.

### Getting Values from XML
If your energy monitoring device or inverter has no JSON but an XML API use the following instructions:
Given an API endpoint `http://192.168.xxx.xxx/measurements.xml` which returns the following XML:
```xml
<?xml version="1.0" encoding="UTF-8"?>
<Device Name="PIKO 4.6-2 MP plus" Type="Inverter" Platform="Net16" HmiPlatform="HMI17" NominalPower="4600" UserPowerLimit="nan" CountryPowerLimit="nan" Serial="XXXXXXXXXXXXXXXXXXXX" OEMSerial="XXXXXXXX" BusAddress="1" NetBiosName="XXXXXXXXXXXXXXX" WebPortal="PIKO Solar Portal" ManufacturerURL="kostal-solar-electric.com" IpAddress="192.168.XXX.XXX" DateTime="2022-06-08T19:33:25" MilliSeconds="806">
  <Measurements>
    <Measurement Value="231.3" Unit="V" Type="AC_Voltage"/>
    <Measurement Value="1.132" Unit="A" Type="AC_Current"/>
    <Measurement Value="256.1" Unit="W" Type="AC_Power"/>
    <Measurement Value="264.3" Unit="W" Type="AC_Power_fast"/>
    <Measurement Value="49.992" Unit="Hz" Type="AC_Frequency"/>
    <Measurement Value="474.2" Unit="V" Type="DC_Voltage"/>
    <Measurement Value="0.594" Unit="A" Type="DC_Current"/>
    <Measurement Value="473.5" Unit="V" Type="LINK_Voltage"/>
    <Measurement Value="18.7" Unit="W" Type="GridPower"/>
    <Measurement Value="0.0" Unit="W" Type="GridConsumedPower"/>
    <Measurement Value="18.7" Unit="W" Type="GridInjectedPower"/>
    <Measurement Value="237.3" Unit="W" Type="OwnConsumedPower"/>
    <Measurement Value="100.0" Unit="%" Type="Derating"/>
  </Measurements>
</Device>
```

#### Grid-Power
Assuming the `Measurement` node with `Type` `GridPower` is the power your house feeds to the grid you need the following environment variables:
```yaml
- CurrentPowerToGridUrl=http://192.168.xxx.xxx/measurements.xml
- CurrentPowerToGridXmlPattern=Device/Measurements/Measurement
- CurrentPowerToGridXmlAttributeHeaderName=Type
- CurrentPowerToGridXmlAttributeHeaderValue=GridPower
- CurrentPowerToGridXmlAttributeValueName=Value
```

#### Inverter-Power
Assuming the `Measurement` node with `Type` `AC_Power` is the power your inverter is currently feeding you can use the following environment variables:
```yaml
- CurrentInverterPowerUrl=http://192.168.xxx.xxx/measurements.xml
- CurrentInverterPowerXmlPattern=Device/Measurements/Measurement
- CurrentInverterPowerAttributeHeaderName=Type
- CurrentInverterPowerAttributeHeaderValue=AC_Power
- CurrentInverterPowerAttributeValueName=Value
```
Note: This values are not needed, they are just used to show additional information.

### Plugins
If your SmartMeter does not have a REST Endpoint as needed you can use plugins:

#### SMA-EnergyMeter Plugin
[![Docker version](https://img.shields.io/docker/v/pkuehnel/smartteslaampsettersmaplugin/latest)](https://hub.docker.com/r/pkuehnel/smartteslaampsettersmaplugin)
[![Docker size](https://img.shields.io/docker/image-size/pkuehnel/smartteslaampsettersmaplugin/latest)](https://hub.docker.com/r/pkuehnel/smartteslaampsettersmaplugin)
[![Docker pulls](https://img.shields.io/docker/pulls/pkuehnel/smartteslaampsettersmaplugin)](https://hub.docker.com/r/pkuehnel/smartteslaampsettersmaplugin)

With the SMA Energymeter Plugin (note: Every SMA Home Manager 2.0 has an integrated EnergyMeter Interface, so this plugin is working with SMA Home Manager 2.0 as well) a new service is created, which receives the EnergyMeter values and averages them for the last x seconds. The URL of the endpoint is: http://ip-of-your-host:8453/api/CurrentPower/GetPower
To use the plugin add the following to your `docker-compose.yml`:
```yaml
services:
    smaplugin:
    image: pkuehnel/smartteslaampsettersmaplugin:latest
    logging:
        driver: "json-file"
        options:
            max-file: "5"
            max-size: "10m"
    restart: always
    network_mode: host
    environment:
      - ASPNETCORE_URLS=http://+:8453
```

#### Solaredge Plugin
[![Docker version](https://img.shields.io/docker/v/pkuehnel/smartteslaampsettersolaredgeplugin/latest)](https://hub.docker.com/r/pkuehnel/smartteslaampsettersolaredgeplugin)
[![Docker size](https://img.shields.io/docker/image-size/pkuehnel/smartteslaampsettersolaredgeplugin/latest)](https://hub.docker.com/r/pkuehnel/smartteslaampsettersolaredgeplugin)
[![Docker pulls](https://img.shields.io/docker/pulls/pkuehnel/smartteslaampsettersolaredgeplugin)](https://hub.docker.com/r/pkuehnel/smartteslaampsettersolaredgeplugin)

Currently only the cloud API is supported. As there are not allowed more than 300 requests per plant, IP address and day, this integration is at this state very limited as you can only get the current power every few minutes. To use the solaredge plugin, you have to add another service to your `docker-compose.yml`:
```yaml
services:
    solaredgeplugin:
    image: pkuehnel/smartteslaampsettersolaredgeplugin:solaredge
    logging:
        driver: "json-file"
        options:
            max-file: "5"
            max-size: "10m"
    restart: always
    environment:
      - ASPNETCORE_URLS=http://+:8453
      - CloudUrl=https://monitoringapi.solaredge.com/site/1561056/currentPowerFlow.json?api_key=asdfasdfasdfasdfasdfasdf&
      - RefreshIntervallSeconds=360
     ports:
      - 8453:8453
```
Note: You have to change the cloud URL and also can change the refresh intervall. The default refresh intervall of 360 results in 240 of 300 allowed API calls per day.
To use the plugin in the `smartteslaampsetter` you have to add the following environmentvariables to the `smartteslaampsetter` service:
```yaml
- CurrentPowerToGridUrl=http://solaredgeplugin:8453/api/CurrentValues/GetPowerToGrid
- CurrentInverterPowerUrl=http://solaredgeplugin:8453/api/CurrentValues/GetInverterPower
```
