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
  - [Plugins](#plugins)
    - [SMA-EnergyMeter Plugin](#sma-energymeter-plugin)

## How to use

You can either use it in a Docker container or go download the code and deploy it yourself on any server.

### Docker-compose

If you run the simple Docker deployment of TeslaMate, then adding this will do the trick. You'll have the frontend available on port 7190 then.

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
      - CurrentPowerToGridUrl=http://192.168.1.50/api/CurrentPower
      - TeslaMateApiBaseUrl=http://teslamateapi:8080
      - UpdateIntervalSeconds=30
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
| **CurrentPowerToGridUrl** | string | URL to REST Endpoint of smart meter | http://192.168.1.50/api/CurrentPower |
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

### Plugins
If your SmartMeter does not have a REST Endpoint as needed you can use plugins:

#### SMA-EnergyMeter Plugin
[![Docker version](https://img.shields.io/docker/v/pkuehnel/smartteslaampsettersmaplugin/latest)](https://hub.docker.com/r/pkuehnel/smartteslaampsettersmaplugin)
[![Docker size](https://img.shields.io/docker/image-size/pkuehnel/smartteslaampsettersmaplugin/latest)](https://hub.docker.com/r/pkuehnel/smartteslaampsettersmaplugin)
[![Docker pulls](https://img.shields.io/docker/pulls/pkuehnel/smartteslaampsettersmaplugin)](https://hub.docker.com/r/pkuehnel/smartteslaampsettersmaplugin)

With the SMA Energymeter Plugin (note: Every SMA Home Manager 2.0 has an integrated EnergyMeter Interface, so this plugin is working with SMA Home Manager 2.0 as well) a new service is created, which receives the EnergyMeter values and averages them for the last x seconds. The URL of the endpoint is: http://ip-of-your-host:8453/api/CurrentPower?lastXSeconds=30
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
      - MaxValuesInLastValuesList=120
```
