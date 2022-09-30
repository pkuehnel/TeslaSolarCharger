# TeslaSolarCharger

[![Docker version](https://img.shields.io/docker/v/pkuehnel/teslasolarcharger/latest)](https://hub.docker.com/r/pkuehnel/teslasolarcharger)
[![Docker size](https://img.shields.io/docker/image-size/pkuehnel/teslasolarcharger/latest)](https://hub.docker.com/r/pkuehnel/teslasolarcharger)
[![Docker pulls new name](https://img.shields.io/docker/pulls/pkuehnel/teslasolarcharger)](https://hub.docker.com/r/pkuehnel/teslasolarcharger)
[![Docker pulls old name](https://img.shields.io/docker/pulls/pkuehnel/smartteslaampsetter)](https://hub.docker.com/r/pkuehnel/smartteslaampsetter)
[![](https://img.shields.io/badge/Donate-PayPal-ff69b4.svg)](https://www.paypal.com/donate/?hosted_button_id=S3CK8Q9KV3JUL)

TeslaSolarCharger is a service to set one or multiple Teslas' charging current using the datalogger **[TeslaMate](https://github.com/adriankumpf/teslamate)**.
### Table of Contents

- [How to install](#how-to-install)
  - [Docker-compose](#docker-compose)
  - [Setting up TeslaMate including TeslaSolarCharger](#Setting-up-TeslaMate-including-TeslaSolarCharger)
    - [docker-compose.yml content](#docker-composeyml-content)
    - [First startup of the application](#first-startup-of-the-application)
- [Often used optional settings](#often-used-optional-settings)
  - [Car Priorities](#car-priorities)
  - [Power Buffer](#power-buffer)
  - [Home Battery](#home-battery)
- [How to use](#how-to-use)
  - [Charge Modes](#charge-modes)
- [Generate logfiles](#generate-logfiles)

## How to install

You can either install the software in a Docker container or go download the code and deploy it yourself on any server.

### Docker-compose

The easiest way to use TeslaSolarCharger is with Docker. Depending on your System you need to [install Docker including Docker-Compose](https://dev.to/rohansawant/installing-docker-and-docker-compose-on-the-raspberry-pi-in-5-simple-steps-3mgl) first.

### Setting up TeslaMate including TeslaSolarCharger

To set up TeslaSolarCharger you have to create a `docker-compose.yml` (name is important!) file in a new directory. Note: During the setup some additional data folders to persist data will be created in that folder, so it is recommended to use a new directory for your `docker-compose.yml`.

#### docker-compose.yml content

The needed content of your `docker-compose.yml` depends on your inverter. By default TeslaSolarCharger can consume JSON/XML REST APIs. To get the software running on [SMA](https://www.sma.de/) or [SolarEdge](https://www.solaredge.com/) you can use specific plugins, which create the needed JSON API. You can use the software with any ModbusTCP capable inverter also.

##### Content without using a plugin

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
    container_name: teslasolarcharger
    logging:
        driver: "json-file"
        options:
            max-file: "10"
            max-size: "100m"
    restart: always
    depends_on:
      - teslamateapi
    environment:
#      - Serilog__MinimumLevel__Default=Verbose #uncomment this line and recreate container with docker-compose up -d for more detailed logs
      - TZ=Europe/Berlin ##You can change your Timezone here
    ports:
      - 7190:80
    volumes:
      - ./teslasolarcharger-configs:/app/configs

```

##### Content using SMA plugin
[![Docker version](https://img.shields.io/docker/v/pkuehnel/teslasolarchargersmaplugin/latest)](https://hub.docker.com/r/pkuehnel/teslasolarchargersmaplugin)
[![Docker size](https://img.shields.io/docker/image-size/pkuehnel/teslasolarchargersmaplugin/latest)](https://hub.docker.com/r/pkuehnel/teslasolarchargersmaplugin)
[![Docker pulls new name](https://img.shields.io/docker/pulls/pkuehnel/teslasolarchargersmaplugin)](https://hub.docker.com/r/pkuehnel/teslasolarchargersmaplugin)
[![Docker pulls old name](https://img.shields.io/docker/pulls/pkuehnel/smartteslaampsettersmaplugin)](https://hub.docker.com/r/pkuehnel/smartteslaampsettersmaplugin)

The SMA plugin is used to access the values from your EnergyMeter (or Sunny Home Manager 2.0).
To use the plugin just add these lines to the bottom of your `docker-compose.yml`.
```yaml
  smaplugin:
    image: pkuehnel/teslasolarchargersmaplugin:latest
    container_name: teslasolarcharger_smaplugin
    logging:
        driver: "json-file"
        options:
            max-file: "5"
            max-size: "10m"
    restart: always
    network_mode: host
    environment:
      - ASPNETCORE_URLS=http://+:7192

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
    container_name: teslasolarcharger
    logging:
        driver: "json-file"
        options:
            max-file: "10"
            max-size: "100m"
    restart: always
    depends_on:
      - teslamateapi
    environment:
#      - Serilog__MinimumLevel__Default=Verbose #uncomment this line and recreate container with docker-compose up -d for more detailed logs
      - TZ=Europe/Berlin ##You can change your Timezone here
    ports:
      - 7190:80
    volumes:
      - ./teslasolarcharger-configs:/app/configs
  
  smaplugin:
    image: pkuehnel/teslasolarchargersmaplugin:latest
    container_name: teslasolarcharger_smaplugin
    logging:
        driver: "json-file"
        options:
            max-file: "5"
            max-size: "10m"
    restart: always
    network_mode: host
    environment:
      - ASPNETCORE_URLS=http://+:7192
```
  
</details>

##### Content using SolarEdge plugin
[![Docker version](https://img.shields.io/docker/v/pkuehnel/teslasolarchargersolaredgeplugin/latest)](https://hub.docker.com/r/pkuehnel/teslasolarchargersolaredgeplugin)
[![Docker size](https://img.shields.io/docker/image-size/pkuehnel/teslasolarchargersolaredgeplugin/latest)](https://hub.docker.com/r/pkuehnel/teslasolarchargersolaredgeplugin)
[![Docker pulls new name](https://img.shields.io/docker/pulls/pkuehnel/teslasolarchargersolaredgeplugin)](https://hub.docker.com/r/pkuehnel/teslasolarchargersolaredgeplugin)
[![Docker pulls old name](https://img.shields.io/docker/pulls/pkuehnel/smartteslaampsettersolaredgeplugin)](https://hub.docker.com/r/pkuehnel/smartteslaampsettersolaredgeplugin)

The SolarEdge Plugin is using the cloud API which is limited to 300 calls per day. To not exceed this limit there is an environmentvariable which limits the refresh interval to 360 seconds. This results in a very low update frequency of your power values. That is why it is recommended to use the ModbusPlugin below.

To use the plugin just add these lines to the bottom of your `docker-compose.yml`. Note: You have to change your site ID and your API key in the `CloudUrl` environment variable

```yaml
  solaredgeplugin:
    image: pkuehnel/teslasolarchargersolaredgeplugin:latest
    container_name: teslasolarcharger_solaredgeplugin
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
    container_name: teslasolarcharger
    logging:
        driver: "json-file"
        options:
            max-file: "10"
            max-size: "100m"
    restart: always
    depends_on:
      - teslamateapi
    environment:
#      - Serilog__MinimumLevel__Default=Verbose #uncomment this line and recreate container with docker-compose up -d for more detailed logs
      - TZ=Europe/Berlin ##You can change your Timezone here
    ports:
      - 7190:80
    volumes:
      - ./teslasolarcharger-configs:/app/configs
  
  solaredgeplugin:
    image: pkuehnel/teslasolarchargersolaredgeplugin:latest
    container_name: teslasolarcharger_solaredgeplugin
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

##### Content using Modbus plugin
[![Docker version](https://img.shields.io/docker/v/pkuehnel/teslasolarchargermodbusplugin/latest)](https://hub.docker.com/r/pkuehnel/teslasolarchargermodbusplugin)
[![Docker size](https://img.shields.io/docker/image-size/pkuehnel/teslasolarchargermodbusplugin/latest)](https://hub.docker.com/r/pkuehnel/teslasolarchargermodbusplugin)
[![Docker pulls](https://img.shields.io/docker/pulls/pkuehnel/teslasolarchargermodbusplugin)](https://hub.docker.com/r/pkuehnel/teslasolarchargermodbusplugin)

You can also use the Modbus plugin. This is a general plugin so don't be surprised if it does not work as excpected right after starting up. Feel free to share your configurations [here](https://github.com/pkuehnel/TeslaSolarCharger/discussions/174), so I can add templates for future users.

To use the plugin just add these lines to the bottom of your `docker-compose.yml`. Note: As some inverters struggle with to many requests within a specific time you can change `RequestBlockMilliseconds` environment variable.

```yaml
  modbusplugin:
    image: pkuehnel/teslasolarchargermodbusplugin:latest
    container_name: teslasolarcharger_modbusplugin
    logging:
        driver: "json-file"
        options:
            max-file: "5"
            max-size: "10m"
    restart: always
    environment:
      - RequestBlockMilliseconds=0
    ports:
      - 7191:80

```
You can also copy the complete content from here:
<details>
  <summary>Complete file using Modbus plugin</summary>

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
    container_name: teslasolarcharger
    logging:
        driver: "json-file"
        options:
            max-file: "10"
            max-size: "100m"
    restart: always
    depends_on:
      - teslamateapi
    environment:
#      - Serilog__MinimumLevel__Default=Verbose #uncomment this line and recreate container with docker-compose up -d for more detailed logs
      - TZ=Europe/Berlin ##You can change your Timezone here
    ports:
      - 7190:80
    volumes:
      - ./teslasolarcharger-configs:/app/configs
  
  modbusplugin:
    image: pkuehnel/teslasolarchargermodbusplugin:latest
    container_name: teslasolarcharger_modbusplugin
    logging:
        driver: "json-file"
        options:
            max-file: "5"
            max-size: "10m"
    restart: always
    environment:
      - RequestBlockMilliseconds=0
    ports:
      - 7191:80

```
  
</details>

#### First startup of the application

1. Move to your above created directory with your `docker-compose.yml`. 
1. Start all containers using the command `docker-compose up -d`.
1. Use a third party app to create a new Tesla Token [[Android](https://play.google.com/store/apps/details?id=net.leveugle.teslatokens&hl=en_US&gl=US)] [[iOS](https://apps.apple.com/us/app/tesla-token/id1411393432)]
1. Open your browser, go to `http://your-ip-address:4000` and paste your token and your refresh token into the form.
1. Go to `Geo-Fences` and add a Geo-Fence called `Home` at the location you want TeslaSolarCharger to be active.
1. Open `http://your-ip-address:7190`
1. Go to `Base Configuration` (if you are on a mobile device it is behind the menu button).

##### Setting Up Urls to get grid power
To let the TeslaSolarCharger know how much power there is to charge the car you need to add a value in `Grid Power Url`.

###### Using vendor specific plugins
Note: In a future release these values will be filled in automatically, maybe it is already working and I just forgot to remove this section ;-)
Depending on your used pluging you habe to paste one of the following URLs to the `Grid Power Url` field:
* SMA Plugin: `http://<IP of your Docker host>:7192/api/CurrentPower/GetPower`
* SolarEdge Plugin:
  - Grid Power: `http://solaredgeplugin/api/CurrentValues/GetPowerToGrid` 
  - Inverter Power: `http://solaredgeplugin/api/CurrentValues/GetInverterPower`
  - Home Battery SoC: `http://solaredgeplugin/api/CurrentValues/GetHomeBatterySoc`
  - Home Battery Power: `http://solaredgeplugin/api/CurrentValues/GetHomeBatteryPower`

###### Using the modbus plugin
Warning: As this plugin keeps an open connection to your inverter it is highly recommended not to kill this container but always shut it down gracefully.
To use the modbus plugin you have to create the url string by yourself. The URL looks like this:
```
http://modbusplugin/api/Modbus/GetInt32Value?unitIdentifier=3&startingAddress=<modbusregisterAddress>&quantity=<NumberOFModbusRegistersToRead>&ipAddress=<IPAdressOfModbusDevice>&port=502&factor=<conversionFactor>&connectDelaySeconds=1&timeoutSeconds=10
```

An example URL with all values filled could look like this:
```
http://modbusplugin/api/Modbus/GetInt32Value?unitIdentifier=3&startingAddress=30775&quantity=2&ipAddress=192.168.1.28&port=502&factor=1&connectDelaySeconds=1&timeoutSeconds=10
```

You can test the result of the URL by pasting it into your browser and replace `modbusplugin` with `ipOfYourDockerHost:7091` e.g: 
```
http://192.168.1.50:7091/api/Modbus/GetInt32Value?unitIdentifier=3&startingAddress=30775&quantity=2&ipAddress=192.168.1.28&port=502&factor=1&connectDelaySeconds=1&timeoutSeconds=10
```

What the values mean:
* `unitIdentifier`: Internal ID of your inverter (in most cases 3)
* `startingAddress`: Register address of the value you want to extract. You find this value in the documenation of your inverter
* `quantity`: Number of registers to read from (for integer values should be 2)
* `ipAddress`: IP Address of your inverter
* `port`: Modbus TCP Port of your inverter (default: 502)
* `factor`: Factor to multiply the resulting value with. The result should be Watt, so if your inverter returns Watt you can leave 1, if your inverter returns 0.1W you have to use 10.
* `connectDelaySeconds`: Delay before communication the first time (you should use 1)
* `timeoutSeconds`: Timeout until returning an error if inverter is not responding (you should use 10)

For more convenience you can go to `http://your-ip-address:7091/swagger`. There you can try your values with a user interface.

###### Using no plugin
If you have your own api or your energymeter directly has a REST API you can also use these to get the grid power. Just insert the `Grid Power Url` and if there is a plain integer value it should work. If your API returns JSON or XML results you have to add the exact path to that specific value.

###### Json Path
If you have the following json result:
```json
{
  "request": {
    "method": "get",
    "key": "asdf"
  },
  "code": 0,
  "type": "call",
  "data": {
    "value": 14
  }
}
```
You can use `$.data.value` as `Grid Power Json Pattern`.

###### XML Path
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

Grid Power:
Assuming the `Measurement` node with `Type` `GridPower` is the power your house feeds to the grid you need the following values in your Base configuration:
```yaml
- CurrentPowerToGridUrl=http://192.168.xxx.xxx/measurements.xml
- CurrentPowerToGridXmlPattern=Device/Measurements/Measurement
- CurrentPowerToGridXmlAttributeHeaderName=Type
- CurrentPowerToGridXmlAttributeHeaderValue=GridPower
- CurrentPowerToGridXmlAttributeValueName=Value
```

Inverter Power:
Assuming the `Measurement` node with `Type` `AC_Power` is the power your inverter is currently feeding you can use the following  values in your Base configuration:
```yaml
- CurrentInverterPowerUrl=http://192.168.xxx.xxx/measurements.xml
- CurrentInverterPowerXmlPattern=Device/Measurements/Measurement
- CurrentInverterPowerAttributeHeaderName=Type
- CurrentInverterPowerAttributeHeaderValue=AC_Power
- CurrentInverterPowerAttributeValueName=Value
```
Note: This values are not needed, they are just used to show additional information.

## Often used optional Settings
When you are at this point your car connected to any charging cable in your set home area should start charging based on solar power. But there a few additional settings which are maybe helpful for your environment:

### Car Priorities
If you have more than one car (or your car does not have the ID 1), you can change this setting in the `Car Ids` form field separated by `|`. Note: The order of the IDs is the order of power distribution.

### Power Buffer
If you set `PowerBuffer` to a value different from `0` the system uses the value as an offset. Eg. If you set `1000` the current of the car is reduced as long as there is less than 1000 Watt power going to the grid.

### Home Battery
To configure your home battery, you need to add following settings:
* URL for getting the state of charge 
* URL for getting current charging/discharging power
* Home Battery Minimum Soc
* Home Battery Charging Power

After setting everything up, your overview page should look like this:

![image](https://user-images.githubusercontent.com/35361981/183434947-16d13372-09ff-45a7-94a2-8d4043f39f18.png)

Note: If your battery is discharging the power should be displayed in red, if the battery is charging, the power should be displayed in green. If this is the other way around you have to update the `Correction Factor` below your `HomeBatteryPower Url` setting.

If you use this feature in combination with the SolarEdge plugin the URLs are:
* `http://solaredgeplugin/api/CurrentValues/GetHomeBatterySoc`
* `http://solaredgeplugin/api/CurrentValues/GetHomeBatteryPower`

## How to use
After setting everything up, you can use the software via `http://your-ip-address:7190`.

### Charge Modes
Currently there are three different charge modes available:
1. **PV only**: Only solar energy is used to charge. You can set a SOC level which should be reached at a specific date and time. If solar energy is not enough to reach the set soc level in time, the car starts charging at full speed. Note: To let this work, you have to specify `usable kWh` in the car settings section.
1. **Maximum Power**: Car charges with maximum available power
1. **Min SoC + PV**: If plugged in the car starts charging with maximum power until set Min SoC is reached. After that only PV Power is used to charge the car.

## Generate logfiles
To generate logfiles you have to write the logs for each container to a separate logfile.
Note: To create a more detailed logfile you have to add `- Serilog__MinimumLevel__Default=Verbose` as environment variable.
The commands if you used the docker-compose.yml files from above:<br />
For the main **TeslaSolarCharger** container:
```
docker logs teslasolarcharger > teslasolarcharger.log
```
For the **SmaPlugin**:
```
docker logs teslasolarcharger_smaplugin > teslasolarcharger_smaplugin.log
```
For the **SolaredgePlugin**:
```
docker logs teslasolarcharger_solaredgeplugin > teslasolarcharger_solaredgeplugin.log
```
For the **ModbusPlugin**:
```
docker logs teslasolarcharger_modbusplugin > teslasolarcharger_modbusplugin.log
```

If you get an error like `Error: No such container:` you can look up the containernames with
```
docker ps
```
