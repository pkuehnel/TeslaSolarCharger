# TeslaSolarCharger

[![Docker version](https://img.shields.io/docker/v/pkuehnel/teslasolarcharger/latest)](https://hub.docker.com/r/pkuehnel/teslasolarcharger)
[![Docker size](https://img.shields.io/docker/image-size/pkuehnel/teslasolarcharger/latest)](https://hub.docker.com/r/pkuehnel/teslasolarcharger)
[![Docker pulls](https://img.shields.io/docker/pulls/pkuehnel/teslasolarcharger)](https://hub.docker.com/r/pkuehnel/teslasolarcharger)
[![](https://img.shields.io/badge/Donate-PayPal-ff69b4.svg)](https://www.paypal.com/donate/?hosted_button_id=S3CK8Q9KV3JUL)
[![edgeRelease](https://github.com/pkuehnel/TeslaSolarCharger/actions/workflows/edgeRelease.yml/badge.svg)](https://github.com/pkuehnel/TeslaSolarCharger/actions/workflows/edgeRelease.yml)


TeslaSolarCharger is a service to set one or multiple Teslas' charging current.

## Table of Contents

- [How to install](#how-to-install)
  - [Docker compose](#docker-compose)
  - [Setting up TeslaSolarCharger](#Setting-up-TeslaSolarCharger)
    - [docker-compose.yml content](#docker-composeyml-content)
    - [First startup of the application](#first-startup-of-the-application)
    - [Install and setup BLE API](#install-and-setup-ble-api)
- [Often used optional settings](#often-used-optional-settings)
  - [Power Buffer](#power-buffer)
  - [Home Battery](#home-battery)
  - [Telegram integration](#telegram-integration)
- [How to use](#how-to-use)
  - [Charge Modes](#charge-modes)
- [Generate logfiles](#generate-logfiles)
- [Privacy notes](#privacy-notes)

## How to install

You can either install the software in a Docker container or download the binaries and deploy it on any server. In June 2024, Tesla implemented rate limits to their API, so there is a BLE (Bluetooth Low Energy, implemented since Bluetooth Version 4.0) capable device needed near the car. You can find details on how to set up BLE [here](#install-and-setup-ble-api).

## How to migrate to subscription version
Check out pricing on [Solar4Car.com](https://solar4car.com/) and the migration guide [here](https://www.youtube.com/watch?v=nVP0sEyPUL0).

### Docker compose

The easiest way to use TeslaSolarCharger is with Docker.

Depending on your system, you have to install Docker first. To do this on a Raspberry Pi (should be the same on standard Linux systems), you need to execute the following commands in your Terminal window:
1. Install Docker
    ```
    curl -sSL https://get.docker.com | sh
    ```
1. Add permissions to the `pi` user. If you have another username, update the command accordingly
    ```
    sudo usermod -aG docker pi
    ```
1. Reboot your Raspberry Pi
1. Test the Docker installation
    ```
    docker run hello-world
    ```
If any issues occur, try to identify them using [this more detailed instruction](https://www.simplilearn.com/tutorials/docker-tutorial/raspberry-pi-docker).

If you are using a Windows host, install the Software from [here](https://docs.docker.com/desktop/install/windows-install/). Windows 11 is highly recommended. Select Linux Containers in the installation process. Note: The SMA plugin is not supported on Docker on Windows.

### Setting up TeslaSolarCharger

To set up TeslaSolarCharger, you must create a `docker-compose.yml` (name is important!) file in a new directory.

#### docker-compose.yml content

The required content of your `docker-compose.yml` depends on your inverter. By default, TeslaSolarCharger can consume JSON/XML REST APIs, MQTT messages or Modbus TCP. To get the software running on [SMA](https://www.sma.de/), [SolarEdge](https://www.solaredge.com/) or Solax based inverters, you can use specific plugins which create the required JSON API.

##### Content without using a plugin

Below you can see the content for your `docker-compose.yml` if you are not using any plugin. Note: I recommend changing as few things as possible on this file as this will increase the effort to set everything up but feel free to change the Timezone.

```yaml
version: '3.3'

services:
  teslasolarcharger:
    image: pkuehnel/teslasolarcharger:latest
    container_name: teslasolarcharger
    logging:
        driver: "json-file"
        options:
            max-file: "10"
            max-size: "100m"
    restart: always
    environment:
#      - Serilog__MinimumLevel__Default=Verbose #uncomment this line and recreate container with docker compose up -d for more detailed logs
      - TZ=Europe/Berlin ##You can change your Timezone here
    ports:
      - 7190:80
    volumes:
      - teslasolarcharger-configs:/app/configs

volumes:
  teslasolarcharger-configs:

```

##### Content using SMA plugin

[![Docker version](https://img.shields.io/docker/v/pkuehnel/teslasolarchargersmaplugin/latest)](https://hub.docker.com/r/pkuehnel/teslasolarchargersmaplugin)
[![Docker size](https://img.shields.io/docker/image-size/pkuehnel/teslasolarchargersmaplugin/latest)](https://hub.docker.com/r/pkuehnel/teslasolarchargersmaplugin)
[![Docker pulls](https://img.shields.io/docker/pulls/pkuehnel/teslasolarchargersmaplugin)](https://hub.docker.com/r/pkuehnel/teslasolarchargersmaplugin)

The SMA plugin is used to access your EnergyMeter (or Sunny Home Manager 2.0) values.
To use the plugin, add these lines before the volumes section of your `docker-compose.yml`. Note: The SMA plugin is not supported on Docker on Windows.

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
  teslasolarcharger:
    image: pkuehnel/teslasolarcharger:latest
    container_name: teslasolarcharger
    logging:
        driver: "json-file"
        options:
            max-file: "10"
            max-size: "100m"
    restart: always
    environment:
#      - Serilog__MinimumLevel__Default=Verbose #uncomment this line and recreate container with docker compose up -d for more detailed logs
      - TZ=Europe/Berlin ##You can change your Timezone here
    ports:
      - 7190:80
    volumes:
      - teslasolarcharger-configs:/app/configs
  
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

volumes:
  teslasolarcharger-configs:

```
  
</details>

##### Content using SolarEdge plugin

[![Docker version](https://img.shields.io/docker/v/pkuehnel/teslasolarchargersolaredgeplugin/latest)](https://hub.docker.com/r/pkuehnel/teslasolarchargersolaredgeplugin)
[![Docker size](https://img.shields.io/docker/image-size/pkuehnel/teslasolarchargersolaredgeplugin/latest)](https://hub.docker.com/r/pkuehnel/teslasolarchargersolaredgeplugin)
[![Docker pulls](https://img.shields.io/docker/pulls/pkuehnel/teslasolarchargersolaredgeplugin)](https://hub.docker.com/r/pkuehnel/teslasolarchargersolaredgeplugin)

The SolarEdge Plugin uses the cloud API, which is limited to 300 which is reset after 15 minutes. When the limit is reached the SolarEdge API does not gather any new values. This results in TSC displaying 0 grid and home battery power until 15 minutes are over.

To use the plugin, just add these lines before the volumes section of your `docker-compose.yml`. Note: You have to change your site ID and your API key in the `CloudUrl` environment variable

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
    ports:
      - 7193:80

```

You can also copy the complete content from here:
<details>
  <summary>Complete file using SolarEdge plugin</summary>

```yaml
version: '3.3'
services:      
  teslasolarcharger:
    image: pkuehnel/teslasolarcharger:latest
    container_name: teslasolarcharger
    logging:
        driver: "json-file"
        options:
            max-file: "10"
            max-size: "100m"
    restart: always
    environment:
#      - Serilog__MinimumLevel__Default=Verbose #uncomment this line and recreate container with docker compose up -d for more detailed logs
      - TZ=Europe/Berlin ##You can change your Timezone here
    ports:
      - 7190:80
    volumes:
      - teslasolarcharger-configs:/app/configs
  
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
    ports:
      - 7193:80

volumes:
  teslasolarcharger-configs:

```
  
</details>

##### Content using Solax plugin

[![Docker version](https://img.shields.io/docker/v/pkuehnel/teslasolarchargersolaxplugin/latest)](https://hub.docker.com/r/pkuehnel/teslasolarchargersolaxplugin)
[![Docker size](https://img.shields.io/docker/image-size/pkuehnel/teslasolarchargersolaxplugin/latest)](https://hub.docker.com/r/pkuehnel/teslasolarchargersolaxplugin)
[![Docker pulls](https://img.shields.io/docker/pulls/pkuehnel/teslasolarchargersolaxplugin)](https://hub.docker.com/r/pkuehnel/teslasolarchargersolaxplugin)

To use the Solax plugin, just add these lines before the volumes section of your `docker-compose.yml`. Note: You have to specify your solar system's IP address and password.

```yaml
  solaxplugin:
    image: pkuehnel/teslasolarchargersolaxplugin:latest
    container_name: teslasolarcharger_solaxplugin
    logging:
        driver: "json-file"
        options:
            max-file: "5"
            max-size: "10m"
    restart: always
    environment:
      - SolarSystemBaseUrl=http://192.168.1.50 ##Change IP Address to your solar system
      - SolarSystemPassword=AD5TSVGR51 ##Change this to the password of your solar system (wifi dongle serial number)
    ports:
      - 7194:80

```

You can also copy the complete content from here:
<details>
  <summary>Complete file using Solax plugin</summary>

```yaml
version: '3.3'
services:
  teslasolarcharger:
    image: pkuehnel/teslasolarcharger:latest
    container_name: teslasolarcharger
    logging:
        driver: "json-file"
        options:
            max-file: "10"
            max-size: "100m"
    restart: always
    environment:
#      - Serilog__MinimumLevel__Default=Verbose #uncomment this line and recreate container with docker compose up -d for more detailed logs
      - TZ=Europe/Berlin ##You can change your Timezone here
    ports:
      - 7190:80
    volumes:
      - teslasolarcharger-configs:/app/configs
  
  solaxplugin:
    image: pkuehnel/teslasolarchargersolaxplugin:latest
    container_name: teslasolarcharger_solaxplugin
    logging:
        driver: "json-file"
        options:
            max-file: "5"
            max-size: "10m"
    restart: always
    environment:
      - SolarSystemBaseUrl=http://192.168.1.50 ##Change IP Address to your solar system
      - SolarSystemPassword=AD5TSVGR51 ##Change this to the password of your solar system (wifi dongle serial number)
    ports:
      - 7194:80

volumes:
  teslasolarcharger-configs:
```
  
</details>

#### First startup of the application

1. Move to your above created directory with your `docker-compose.yml`.
1. Start all containers using the command `docker compose up -d`.
1. Open `http://your-ip-address:7190`
1. Go to `Base Configuration` (if you are on a mobile device, it is behind the menu button).
1. Generate a Fleet API token
1. Again go to `Base Configuration`
1. Use the map to select your home area. This is the area where TSC will start charging your car based on solar power.
1. Click on `Save` at the bottom of the page.
1. Go to `Car Settings` and reload the page until the message `Restart TSC to add new cars` is displayed.
1. Restart the container with `docker compose restart teslasolarcharger` (you need to be in the directory of your `docker-compose.yml`).
1. Wake up the car by opening a car door. Now the SoC values, car name,... should be displayed on the `Overview` page:
![image](https://github.com/user-attachments/assets/8ba58c08-f66f-4b4a-897b-439b83a8b04a)
1. If there are any messages displayed below your car name, just follow the instructions.

If you only want to charge based on Spot Price, you are done now.

##### Setting up solar power values

To let the TeslaSolarCharger know how much power there is to charge the car, you need to set TSC up to gather the solar values

###### REST values including vendor specific plugins
To set up a REST API click on `Add new REST source` and fill out the fields.

If you are using a plugin, you need to use the following values:
- SMA Plugin:
  - Url: `http://<IP of your Docker host>:7192/api/CurrentPower/GetAllValues` (Note: the serial number in the screenshot is optional in case you have multiple SMA EnergyMeter/Homa Managers)
  - Node Pattern Type: JSON
  - Add a result with the configuration seen in the screenshot
![SMA plugin](https://github.com/user-attachments/assets/2785c050-e51e-404c-8c61-898f72177374)

- SolarEdge Plugin:
  - Url: `http://solaredgeplugin/api/CurrentValues/GetCurrentPvValues`
  - Set Result types to json and use the following json patterns:
    - Grid Power: `$.gridPower`
    - Inverter Power: `$.inverterPower`
    - Home Battery SoC: `$.homeBatterySoc`
    - Home Battery Power: `$.homeBatteryPower`
- Solax Plugin:
  - Url: `http://solaxplugin/api/CurrentValues/GetCurrentPvValues`
  - Set Result types to json and use the following json patterns:
    - Grid Power: `$.gridPower`
    - Inverter Power: `$.inverterPower`
    - Home Battery SoC: `$.homeBatterySoc`
    - Home Battery Power: `$.homeBatteryPower`
  - The result should look like this:
![Solax Plugin](https://github.com/user-attachments/assets/d8d3324b-2988-4532-bb56-2ef4a8b4f52e)



###### Modbus values

Fill out the values according to the documentation of your inverter:

- `unitIdentifier`: Internal ID of your inverter (in most cases, 3 or 1)
- `Host`: IP Address or hostname of your inverter
- `port`: Modbus TCP Port of your inverter (default: 502)
- `Connect Delay Milliseconds`: Delay before communicating the first time (you should use 1000)
- `Read Timeout Milliseconds`: Timeout until returning an error if the inverter is not responding (you should use 1000)
- `Address`: Register address of the value you want to extract.
- `Length`: Number of registers to read from (for integer values should be 2)
- `Correction Factor`: Factor to multiply the resulting value with. The result should be Watt, so if your inverter returns Watt, you can leave 1. If your inverter returns 0.1W, you have to use 10.


###### JSON Path

If you have the following JSON result:

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

You can use `$.data.value` as `Json Pattern`.

###### XML Path

If your energy monitoring device or inverter has no JSON, but an XML API, use the following instructions: Given an API endpoint `http://192.168.xxx.xxx/measurements.xml` which returns the following XML:

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
Assuming the `Measurement` node with `Type` `GridPower` is the power your house feeds to the grid, you need the following values in your Base configuration:

```yaml
- Url=http://192.168.xxx.xxx/measurements.xml
- CurrentPowerToGridXmlPattern=Device/Measurements/Measurement
- CurrentPowerToGridXmlAttributeHeaderName=Type
- CurrentPowerToGridXmlAttributeHeaderValue=GridPower
- CurrentPowerToGridXmlAttributeValueName=Value
```

Inverter Power:
Assuming the `Measurement` node with `Type` `AC_Power` is the power your inverter is currently feeding, you can use the following  values in your Base configuration:

```yaml
- Url=http://192.168.xxx.xxx/measurements.xml
- CurrentInverterPowerXmlPattern=Device/Measurements/Measurement
- CurrentInverterPowerAttributeHeaderName=Type
- CurrentInverterPowerAttributeHeaderValue=AC_Power
- CurrentInverterPowerAttributeValueName=Value
```

#### Correction Factors
The correction factor is used to *multiply* the input value so that the results correspond with what TeslaSolarCharger expects.

|Input|Expected Value|
|-----|--------------|
|Inverter Power|A measurement of solar power generated as a positive number in Watts (W)|
|Grid Power| A measurement of grid power export/ import in Watts (W). A export to the grid should be positive and an import from the grid should be negative.|
|Home Battery Power|A measurement of home battery power charge/ discharge in Watts (W). If the battery is charging, this should be positive and if it is discharging this should be negative.|
|Home Battery SoC|A measurement of the percentage charge of your home battery from 0-100%|

You can use the correction factors to scale/ correct these values as appropriate. For example:

- Grid Power input expresses a positive integer as an import and a negative as an export: Select `Minus` as operator and `1` for the correction factor (this multiplies by -1).
- Inverter Power is expressed as kW instead of W: Select `Plus` for the operator and 1000 for the correction factor (this multiplies by 1000).
- Home Battery expresses its state of charge as an absolute value in kWh: Select `Plus` for the operator and a correction factor of  1/(Full Charge Capacity) e.g. if the battery has a full charge capacity of 100kWh the correction factor is 1/100 or 0.01

#### Install and setup BLE API
To go around Teslas API limitations, you can use Bluetooth (BLE) to control your car. You can do this either by using the same device as your TSC is running on, or by using a separate device. Note: The device needs to be placed near the car. Even if it is working when being a few meters away or in different rooms, I can guarantee you, that you will have issues sooner or later. The device needs to be in one room with the car without any walls between them.

Confirmed working hardware:
* Raspberry Pi Zero 2W (only capable when used as separate device)
* Raspberry Pi 3 Model B
* Raspberry Pi 4 Model B
* Raspberry Pi 5

##### Install BLE API on the same device as TSC
To set up the BLE API on the same device as your TSC is running on, you need to add the following lines to your docker-compose.yml:

```yaml
services:
#here are all the other services like TeslaMate, TSC, etc.
  bleapi:
    image: ghcr.io/pkuehnel/teslasolarchargerbleapi:latest
    container_name: TeslaSolarChargerBleApi
    privileged: true
    restart: unless-stopped
    network_mode: host
    environment:
      - ASPNETCORE_URLS=http://+:7210
    volumes:
      - tscbleapi:/externalFiles
      - /var/run/dbus:/var/run/dbus

volumes:
  #here are all the other volumes like teslamate-db, teslamate-grafana-data, etc.
  tscbleapi:
```

You can also copy the complete content from here:
<details>
  <summary>Complete file including BLE API</summary>

```yaml
version: '3.3'

services:
  teslasolarcharger:
    image: pkuehnel/teslasolarcharger:latest
    container_name: teslasolarcharger
    logging:
        driver: "json-file"
        options:
            max-file: "10"
            max-size: "100m"
    restart: always
    environment:
#      - Serilog__MinimumLevel__Default=Verbose #uncomment this line and recreate container with docker compose up -d for more detailed logs
      - TZ=Europe/Berlin ##You can change your Timezone here
    ports:
      - 7190:80
    volumes:
      - teslasolarcharger-configs:/app/configs
  
  bleapi:
    image: ghcr.io/pkuehnel/teslasolarchargerbleapi:latest
    container_name: TeslaSolarChargerBleApi
    privileged: true
    restart: unless-stopped
    network_mode: host
    environment:
      - ASPNETCORE_URLS=http://+:7210
    volumes:
      - tscbleapi:/externalFiles
      - /var/run/dbus:/var/run/dbus

volumes:
  teslasolarcharger-configs:
  tscbleapi:
```
  
</details>

##### Install BLE API on a separate device
To set up a separate device for the BLE API, you need to install Docker on the device, like described [here](#docker-compose). Thereafter, you can use the following docker-compose.yml and start the container with `docker compose up -d`:

```yaml
services:
  bleapi:
    image: ghcr.io/pkuehnel/teslasolarchargerbleapi:latest
    container_name: TeslaSolarChargerBleApi
    privileged: true
    restart: unless-stopped
    network_mode: host
    environment:
      - ASPNETCORE_URLS=http://+:7210
    volumes:
      - tscbleapi:/externalFiles
      - /var/run/dbus:/var/run/dbus

volumes:
  tscbleapi:
```

##### Setup BLE (same device and separate device)
After starting the BLE API, you need to add the BLE API Base URL to your TeslaSolarCharger configuration. The URL is `http://<IP of device with BLE API running>:7210/`

Now you can pair each car by going to the `Car Settings` enable "Use BLE", click Save and then click on Pair Car. Note: It could take up to three tries to pair the car. After you get a message that pairing succeeded, you can test the API by clicking on the `Set to 7A`. Note: The car needs to be awake during the pairing and test process.



## Often used optional settings

When you are at this point, your car connected to any charging cable in your set home area should start charging based on solar power. But there are a few additional settings that are maybe helpful for your environment:

### Power Buffer

If you set `PowerBuffer` to a value different from `0`, the system uses the value as an offset. E.g., If you set `1000`, the car's current is reduced as long as less than 1000 Watt power goes to the grid.

### Home Battery

To configure your home battery, you need to add the following settings:

- Home Battery Minimum SoC
- Home Battery Charging Power

As long as your home battery's SoC is below the set value, the configured charging power is reserved for the home battery. E.g. if you set Home Battery Minimum SoC to 80% and Home Battery Charging Power to 5000W TSC lets the home battery charge with 5000W as long as its SoC is below 80%.

**Note:** If your battery is discharging, the power should be displayed in red. If the battery is charging, the power should be displayed in green. If this is the other way around, you must update the `Operator` to be `Minus`.

### Telegram integration
In this section, you learn how to create the Telegram Bot Key and where you get the Telegram ChannelID from:

- Create a bot by chatting with `BotFather`

![Botfather](https://user-images.githubusercontent.com/35361981/233467207-918b7871-54dd-4ea0-bf9e-b828f2da3509.jpg)

- Ask `BotFather` to reate a new bot with the `/newbot` command and follow the instructions

![newbot](https://user-images.githubusercontent.com/35361981/233468050-b996475a-fe3a-4131-805e-0fe4c60ce603.jpg)

- Click on the link starting with `t.me/` (second line of `BotFarther`'s answer in the chat) and send any message to your newly created bot. The reason for that is, that a chat exists, where TSC can send messages to.
- Copy the Bot token as Telegram Bot Key to your TSC

![BotToken](https://user-images.githubusercontent.com/35361981/233468177-620b0c2f-d9fa-46de-9f87-2eb7b6562553.jpg)

- To get a chat ID, you have to chat with the `userinfobot`

![userinfobot](https://user-images.githubusercontent.com/35361981/233468260-41bada84-1ab1-4cb5-92bc-828ab65177b9.jpg)

- Ask the bot for your `UserID` with the command `/start`

![UserIDVonBot](https://user-images.githubusercontent.com/35361981/233468350-f56ac7b9-8609-4d4b-98e0-277f3b82f356.jpg)

- Copy the user ID as Telegram Channel ID to your TSC


## How to use

After setting everything up, you can use the software via `http://your-ip-address:7190`.

### Charge Modes

Currently, there are four different charge modes available:

1. **PV only**: Only solar energy is used to charge. You can set a SOC level that should be reached at the specified date and time (if charge every day is enabled, the car charges to that SoC every day, not only once). If solar power is insufficient to reach the set soc level in time, the car starts charging at full speed. Note: To let this work, specify `usable kWh` in the car settings section.
1. **Maximum Power**: The car charges with the maximum available power
1. **Min SoC + PV**: If plugged in, the car starts charging with maximum power until the set Min SoC is reached. Thereafter, only PV Power is used to charge the car.
1. **Spot Price + PV**: You can set a Min Soc, which should be reached at a specific date and time (if charge every day is enabled, the car charges to that SoC every day, not only once). The charge times are then planned to charge at the cheapest possible time. This is especially useful if you have hourly electricity prices, like with [Tibber](https://tibber.com/) or [aWATTar](https://www.awattar.de/). Note: The car will still charge based on Solar energy if available, and you need to enable `Use Spot Price` in the Charge Prices settings for correct charge price calculation.
1. **TSC Disabled**: TSC leaves this car as is and does not update the charging speed etc.

## Generate logfiles

To generate logfiles, you must write each container's logs to a separate logfile. 
**Note:** To create a more detailed logfile, you must add `- Serilog__MinimumLevel__Default=Verbose` as environment variable.
The commands if you used the docker-compose.yml files from above:<br />
For the main **TeslaSolarCharger** container:

```bash
docker logs teslasolarcharger > teslasolarcharger.log
```

For the **SmaPlugin**:

```bash
docker logs teslasolarcharger_smaplugin > teslasolarcharger_smaplugin.log
```

For the **SolaredgePlugin**:

```bash
docker logs teslasolarcharger_solaredgeplugin > teslasolarcharger_solaredgeplugin.log
```

For the **ModbusPlugin**:

```bash
docker logs teslasolarcharger_modbusplugin > teslasolarcharger_modbusplugin.log
```

If you get an error like `Error: No such container:` you can look up the containernames with

```bash
docker ps
```

## Privacy notes
As the new Tesla Fleet API requires a domain and external Token creation from version 2.23.0 onwards, TSC transfers some data to the owner of this repository. By using this software, you accept the transfer of this data. As this is open source, you can see which data is transferred. For now (4th July 2024), the following data is transferred:
- Your access code is used to get the access token from Tesla (Note: the token itself is only stored locally in your TSC installation. It is only transferred via my server, but the token only exists in memory on the server itself. It is not stored in a database or log file)
- Your installation ID (GUID) is at the bottom of the page. Do not post this GUID in public forums, as it is used to deliver the Tesla access token to your installation. Note: There is only a five-minute time window between requesting and providing the token using the installation ID. After these 5 minutes, all requests are blocked.)
- Your installed version.
- Error and warning logs
- Your VIN and if using the Fleet API the data for each request (e.g. change-charging-amp to 7A)
- A statistic of your Fleet API and BLE API usage (e.g. changed car amps 58 times including Timestamps of the request)
- Your configuration regarding using BLE API, the configured Fleet API Refresh Interval, if getting Data from TeslaMate is enabled
