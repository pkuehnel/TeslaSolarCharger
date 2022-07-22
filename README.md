# TeslaSolarCharger

[![Docker version](https://img.shields.io/docker/v/pkuehnel/teslasolarcharger/latest)](https://hub.docker.com/r/pkuehnel/teslasolarcharger)
[![Docker size](https://img.shields.io/docker/image-size/pkuehnel/teslasolarcharger/latest)](https://hub.docker.com/r/pkuehnel/teslasolarcharger)
[![Docker pulls](https://img.shields.io/docker/pulls/pkuehnel/teslasolarcharger)](https://hub.docker.com/r/pkuehnel/teslasolarcharger)
[![Docker pulls](https://img.shields.io/docker/pulls/pkuehnel/smartteslaampsetter)](https://hub.docker.com/r/pkuehnel/smartteslaampsetter)
[![](https://img.shields.io/badge/Donate-PayPal-ff69b4.svg)](https://www.paypal.com/donate/?hosted_button_id=S3CK8Q9KV3JUL)

TeslaSolarCharger is a service to set one or multiple Teslas' charging current using the datalogger **[TeslaMate](https://github.com/adriankumpf/teslamate)**.
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

The easiest way to use TeslaSolarCharger is with Docker. Depending on your System you need to [install Docker including Docker-Compose](https://dev.to/rohansawant/installing-docker-and-docker-compose-on-the-raspberry-pi-in-5-simple-steps-3mgl) first.

### Setting up TeslaMate including TeslaSolarCharger

So set up TeslaSolarCharger you have to create a `docker-compose.yml`(name is important!) file in a new directory. Note: During the setup some additional data folders to persist data will be created in that folder, so it is recommended to use a new directory for your `docker-compose.yml`.

### docker-compose.yml content

The needed content of your `docker-compose.yml` depends on your inverter. By default TeslaSolarCharger can consume JSON/XML REST APIs. To get the software running on [SMA](https://www.sma.de/) or [SolarEdge](https://www.solaredge.com/) you can use specific plugins, which create the needed JSON API. You can use the software with any ModbusTCP capable inverter also.

#### Content without using a plugin

Below you can see the content for your `docker-compose.yml` if you are not using any plugin. Note: It is recommended to change as few things as possible on this file as this will increase the effort to set everything up but feel free to change the database password, encryption key and Timezone. Important: If you change the password or the encryption key you need to use the same password and encyption key at all points in your `docker-compose.yml`

```yaml
version: '3.3'

services:
  teslamate:
    image: teslamate/teslamate:latest
    restart: always
    environment:
      - DATABASE_USER=teslamate
      - DATABASE_PASS=secret ##You can change your password here
      - DATABASE_NAME=teslamate
      - DATABASE_HOST=database
      - MQTT_HOST=mosquitto
      - ENCRYPTION_KEY=supersecret ##You can change your encryption key here
      - TZ=Europe/Berlin ##You can change your Timezone here
    ports:
      - 4000:4000
    volumes:
      - ./import:/opt/app/import
    cap_drop:
      - all

  database:
    image: postgres:13
    restart: always
    environment:
      - POSTGRES_USER=teslamate
      - POSTGRES_PASSWORD=secret ##You can change your password here
      - POSTGRES_DB=teslamate
    volumes:
      - ./teslamate-db:/var/lib/postgresql/data

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
      - DATABASE_PASS=secret ##You can change your password here
      - DATABASE_NAME=teslamate
      - DATABASE_HOST=database
      - MQTT_HOST=mosquitto
      - TZ=Europe/Berlin ##You can change your Timezone here
      - ENABLE_COMMANDS=true
      - COMMANDS_ALL=true
      - API_TOKEN_DISABLE=true
      - ENCRYPTION_KEY=supersecret ##You can change your encryption key here
    #ports:
    #  - 8080:8080

  grafana:
    image: teslamate/grafana:latest
    restart: always
    environment:
      - DATABASE_USER=teslamate
      - DATABASE_PASS=secret ##You can change your password here
      - DATABASE_NAME=teslamate
      - DATABASE_HOST=database
    ports:
      - 3100:3000
    volumes:
      - ./teslamate-grafana-data:/var/lib/grafana

  mosquitto:
    image: eclipse-mosquitto:2
    restart: always
    command: mosquitto -c /mosquitto-no-auth.conf
    #ports:
    #  - 1883:1883
    volumes:
      - ./mosquitto-conf:/mosquitto/config
      - ./mosquitto-data:/mosquitto/data
      
  teslasolarcharger:
    image: pkuehnel/teslasolarcharger:latest
    logging:
        driver: "json-file"
        options:
            max-file: "10"
            max-size: "100m"
    restart: always
    depends_on:
      - teslamateapi
    environment:
      - TZ=Europe/Berlin ##You can change your Timezone here
    ports:
      - 7190:80
    volumes:
      - ./teslasolarcharger-configs:/app/configs

```

#### Content using SMA plugin

The SMA plugin is used to access the values from your EnergyMeter (or Sunny Home Manager 2.0).
To use the plugin just add these lines to the bottom of your `docker-compose.yml`.
```yaml
  smaplugin:
    image: pkuehnel/teslasolarchargersmaplugin:latest
    logging:
        driver: "json-file"
        options:
            max-file: "5"
            max-size: "10m"
    restart: always
    network_mode: host
    ports:
      - 7192:80

```

You can also copy the complete content from here:
<details>
  <summary>Complete file using SMA plugin</summary>

```yaml
version: '3.3'

services:
  teslamate:
    image: teslamate/teslamate:latest
    restart: always
    environment:
      - DATABASE_USER=teslamate
      - DATABASE_PASS=secret ##You can change your password here
      - DATABASE_NAME=teslamate
      - DATABASE_HOST=database
      - MQTT_HOST=mosquitto
      - ENCRYPTION_KEY=supersecret ##You can change your encryption key here
      - TZ=Europe/Berlin ##You can change your Timezone here
    ports:
      - 4000:4000
    volumes:
      - ./import:/opt/app/import
    cap_drop:
      - all

  database:
    image: postgres:13
    restart: always
    environment:
      - POSTGRES_USER=teslamate
      - POSTGRES_PASSWORD=secret ##You can change your password here
      - POSTGRES_DB=teslamate
    volumes:
      - ./teslamate-db:/var/lib/postgresql/data

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
      - DATABASE_PASS=secret ##You can change your password here
      - DATABASE_NAME=teslamate
      - DATABASE_HOST=database
      - MQTT_HOST=mosquitto
      - TZ=Europe/Berlin ##You can change your Timezone here
      - ENABLE_COMMANDS=true
      - COMMANDS_ALL=true
      - API_TOKEN_DISABLE=true
      - ENCRYPTION_KEY=supersecret ##You can change your encryption key here
    #ports:
    #  - 8080:8080

  grafana:
    image: teslamate/grafana:latest
    restart: always
    environment:
      - DATABASE_USER=teslamate
      - DATABASE_PASS=secret ##You can change your password here
      - DATABASE_NAME=teslamate
      - DATABASE_HOST=database
    ports:
      - 3100:3000
    volumes:
      - ./teslamate-grafana-data:/var/lib/grafana

  mosquitto:
    image: eclipse-mosquitto:2
    restart: always
    command: mosquitto -c /mosquitto-no-auth.conf
    #ports:
    #  - 1883:1883
    volumes:
      - ./mosquitto-conf:/mosquitto/config
      - ./mosquitto-data:/mosquitto/data
      
  teslasolarcharger:
    image: pkuehnel/teslasolarcharger:latest
    logging:
        driver: "json-file"
        options:
            max-file: "10"
            max-size: "100m"
    restart: always
    depends_on:
      - teslamateapi
    environment:
      - TZ=Europe/Berlin ##You can change your Timezone here
    ports:
      - 7190:80
    volumes:
      - ./teslasolarcharger-configs:/app/configs
  
  smaplugin:
    image: pkuehnel/teslasolarchargersmaplugin:latest
    logging:
        driver: "json-file"
        options:
            max-file: "5"
            max-size: "10m"
    restart: always
    network_mode: host
    ports:
      - 7192:80
```
  
</details>

#### Content using SolarEdge plugin

The SolarEdge Plugin is using the cloud API which is limited to 300 calls per day. To not exceed this limit there is an environmentvariable which limits the refresh interval to 360 seconds. This results in a very low update frequency of your power values. That is why it is recommended to use the ModbusPlugin below.

To use the plugin just add these lines to the bottom of your `docker-compose.yml`. Note: You have to change your site ID and your API key in the `CloudUrl` environment variable

```yaml
  solaredgeplugin:
    image: pkuehnel/teslasolarchargersolaredgeplugin:solaredge
    logging:
        driver: "json-file"
        options:
            max-file: "5"
            max-size: "10m"
    restart: always
    environment:
      - CloudUrl=https://monitoringapi.solaredge.com/site/1561056/currentPowerFlow.json?api_key=asdfasdfasdfasdfasdfasdf& ##Change your site ID and API Key here
      - RefreshIntervalSeconds=360
    ports:
      - 7193:80

```

You can also copy the complete content from here:
<details>
  <summary>Complete file using SolarEdge plugin</summary>

```yaml
version: '3.3'

services:
  teslamate:
    image: teslamate/teslamate:latest
    restart: always
    environment:
      - DATABASE_USER=teslamate
      - DATABASE_PASS=secret ##You can change your password here
      - DATABASE_NAME=teslamate
      - DATABASE_HOST=database
      - MQTT_HOST=mosquitto
      - ENCRYPTION_KEY=supersecret ##You can change your encryption key here
      - TZ=Europe/Berlin ##You can change your Timezone here
    ports:
      - 4000:4000
    volumes:
      - ./import:/opt/app/import
    cap_drop:
      - all

  database:
    image: postgres:13
    restart: always
    environment:
      - POSTGRES_USER=teslamate
      - POSTGRES_PASSWORD=secret ##You can change your password here
      - POSTGRES_DB=teslamate
    volumes:
      - ./teslamate-db:/var/lib/postgresql/data

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
      - DATABASE_PASS=secret ##You can change your password here
      - DATABASE_NAME=teslamate
      - DATABASE_HOST=database
      - MQTT_HOST=mosquitto
      - TZ=Europe/Berlin ##You can change your Timezone here
      - ENABLE_COMMANDS=true
      - COMMANDS_ALL=true
      - API_TOKEN_DISABLE=true
      - ENCRYPTION_KEY=supersecret ##You can change your encryption key here
    #ports:
    #  - 8080:8080

  grafana:
    image: teslamate/grafana:latest
    restart: always
    environment:
      - DATABASE_USER=teslamate
      - DATABASE_PASS=secret ##You can change your password here
      - DATABASE_NAME=teslamate
      - DATABASE_HOST=database
    ports:
      - 3100:3000
    volumes:
      - ./teslamate-grafana-data:/var/lib/grafana

  mosquitto:
    image: eclipse-mosquitto:2
    restart: always
    command: mosquitto -c /mosquitto-no-auth.conf
    #ports:
    #  - 1883:1883
    volumes:
      - ./mosquitto-conf:/mosquitto/config
      - ./mosquitto-data:/mosquitto/data
      
  teslasolarcharger:
    image: pkuehnel/teslasolarcharger:latest
    logging:
        driver: "json-file"
        options:
            max-file: "10"
            max-size: "100m"
    restart: always
    depends_on:
      - teslamateapi
    environment:
      - TZ=Europe/Berlin ##You can change your Timezone here
    ports:
      - 7190:80
    volumes:
      - ./teslasolarcharger-configs:/app/configs
  
  solaredgeplugin:
    image: pkuehnel/teslasolarchargersolaredgeplugin:solaredge
    logging:
        driver: "json-file"
        options:
            max-file: "5"
            max-size: "10m"
    restart: always
    environment:
      - CloudUrl=https://monitoringapi.solaredge.com/site/1561056/currentPowerFlow.json?api_key=asdfasdfasdfasdfasdfasdf& ##Change your site ID and API Key here
      - RefreshIntervalSeconds=360
    ports:
      - 7193:80

```
  
</details>

#### Content using Modbus plugin

You can also use the Modbus plugin. This is a general plugin so don't be surprised if it does not work as excpected right after starting up. Feel free to share your configurations [here](https://github.com/pkuehnel/TeslaSolarCharger/discussions/174), so I can add templates for future users.

To use the plugin just add these lines to the bottom of your `docker-compose.yml`.

```yaml
  modbusplugin:
    image: pkuehnel/teslasolarchargermodbusplugin:latest
    logging:
        driver: "json-file"
        options:
            max-file: "5"
            max-size: "10m"
    restart: always
    ports:
      - 7191:80

```
You can also copy the complete content from here:
<details>
  <summary>Complete file using SolarEdge plugin</summary>

```yaml
version: '3.3'

services:
  teslamate:
    image: teslamate/teslamate:latest
    restart: always
    environment:
      - DATABASE_USER=teslamate
      - DATABASE_PASS=secret ##You can change your password here
      - DATABASE_NAME=teslamate
      - DATABASE_HOST=database
      - MQTT_HOST=mosquitto
      - ENCRYPTION_KEY=supersecret ##You can change your encryption key here
      - TZ=Europe/Berlin ##You can change your Timezone here
    ports:
      - 4000:4000
    volumes:
      - ./import:/opt/app/import
    cap_drop:
      - all

  database:
    image: postgres:13
    restart: always
    environment:
      - POSTGRES_USER=teslamate
      - POSTGRES_PASSWORD=secret ##You can change your password here
      - POSTGRES_DB=teslamate
    volumes:
      - ./teslamate-db:/var/lib/postgresql/data

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
      - DATABASE_PASS=secret ##You can change your password here
      - DATABASE_NAME=teslamate
      - DATABASE_HOST=database
      - MQTT_HOST=mosquitto
      - TZ=Europe/Berlin ##You can change your Timezone here
      - ENABLE_COMMANDS=true
      - COMMANDS_ALL=true
      - API_TOKEN_DISABLE=true
      - ENCRYPTION_KEY=supersecret ##You can change your encryption key here
    #ports:
    #  - 8080:8080

  grafana:
    image: teslamate/grafana:latest
    restart: always
    environment:
      - DATABASE_USER=teslamate
      - DATABASE_PASS=secret ##You can change your password here
      - DATABASE_NAME=teslamate
      - DATABASE_HOST=database
    ports:
      - 3100:3000
    volumes:
      - ./teslamate-grafana-data:/var/lib/grafana

  mosquitto:
    image: eclipse-mosquitto:2
    restart: always
    command: mosquitto -c /mosquitto-no-auth.conf
    #ports:
    #  - 1883:1883
    volumes:
      - ./mosquitto-conf:/mosquitto/config
      - ./mosquitto-data:/mosquitto/data
      
  teslasolarcharger:
    image: pkuehnel/teslasolarcharger:latest
    logging:
        driver: "json-file"
        options:
            max-file: "10"
            max-size: "100m"
    restart: always
    depends_on:
      - teslamateapi
    environment:
      - TZ=Europe/Berlin ##You can change your Timezone here
    ports:
      - 7190:80
    volumes:
      - ./teslasolarcharger-configs:/app/configs
  
  modbusplugin:
    image: pkuehnel/teslasolarchargermodbusplugin:latest
    logging:
        driver: "json-file"
        options:
            max-file: "5"
            max-size: "10m"
    restart: always
    ports:
      - 7191:80

```
  
</details>

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
[![Docker version](https://img.shields.io/docker/v/pkuehnel/teslasolarchargersmaplugin/latest)](https://hub.docker.com/r/pkuehnel/teslasolarchargersmaplugin)
[![Docker size](https://img.shields.io/docker/image-size/pkuehnel/teslasolarchargersmaplugin/latest)](https://hub.docker.com/r/pkuehnel/teslasolarchargersmaplugin)
[![Docker pulls](https://img.shields.io/docker/pulls/pkuehnel/teslasolarchargersmaplugin)](https://hub.docker.com/r/pkuehnel/teslasolarchargersmaplugin)

With the SMA Energymeter Plugin (note: Every SMA Home Manager 2.0 has an integrated EnergyMeter Interface, so this plugin is working with SMA Home Manager 2.0 as well) a new service is created, which receives the EnergyMeter values and averages them for the last x seconds. The URL of the endpoint is: http://ip-of-your-host:8453/api/CurrentPower/GetPower
To use the plugin add the following to your `docker-compose.yml`:
```yaml
services:
    smaplugin:
    image: pkuehnel/teslasolarchargersmaplugin:latest
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
[![Docker version](https://img.shields.io/docker/v/pkuehnel/teslasolarchargersolaredgeplugin/latest)](https://hub.docker.com/r/pkuehnel/teslasolarchargersolaredgeplugin)
[![Docker size](https://img.shields.io/docker/image-size/pkuehnel/teslasolarchargersolaredgeplugin/latest)](https://hub.docker.com/r/pkuehnel/teslasolarchargersolaredgeplugin)
[![Docker pulls](https://img.shields.io/docker/pulls/pkuehnel/teslasolarchargersolaredgeplugin)](https://hub.docker.com/r/pkuehnel/teslasolarchargersolaredgeplugin)

Currently only the cloud API is supported. As there are not allowed more than 300 requests per plant, IP address and day, this integration is at this state very limited as you can only get the current power every few minutes. To use the solaredge plugin, you have to add another service to your `docker-compose.yml`:
```yaml
services:
    solaredgeplugin:
    image: pkuehnel/teslasolarchargersolaredgeplugin:solaredge
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
To use the plugin in the `teslasolarcharger` you have to add the following environmentvariables to the `teslasolarcharger` service:
```yaml
- CurrentPowerToGridUrl=http://solaredgeplugin:8453/api/CurrentValues/GetPowerToGrid
- CurrentInverterPowerUrl=http://solaredgeplugin:8453/api/CurrentValues/GetInverterPower
```
