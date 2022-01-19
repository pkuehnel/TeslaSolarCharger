# SmartTeslaAmpSetter

[![Docker version](https://img.shields.io/docker/v/pkuehnel/smartteslaampsetter/latest)](https://hub.docker.com/r/pkuehnel/smartteslaampsetter)
[![Docker size](https://img.shields.io/docker/image-size/pkuehnel/smartteslaampsetter/latest)](https://hub.docker.com/r/pkuehnel/smartteslaampsetter)
[![Docker pulls](https://img.shields.io/docker/pulls/pkuehnel/smartteslaampsetter)](https://hub.docker.com/r/pkuehnel/smartteslaampsetter)

SmartTeslaAmpSetter is service to set one or multiple Teslas' charging current using **[TeslaMateApi](https://github.com/tobiasehlert/teslamateapi)** and any REST Endpoint which presents the Watt to increase or reduce charging power

Needs:
- A running **[TeslaMateApi](https://github.com/tobiasehlert/teslamateapi)** instance, which needs self-hosted data logger **[TeslaMate](https://github.com/adriankumpf/teslamate)**
- REST Endpoint from any Smart Meter which returns current power to grid (values > 0 --> power goes to grid, values < 0 power comes from grid)

### Table of Contents

- [How to use](#how-to-use)
  - [Docker-compose](#docker-compose)
  - [Environment variables](#environment-variables)
  - [Car Priorities](#car-priorities)

## How to use

You can either use it in a Docker container or go download the code and deploy it yourself on any server.

### Docker-compose

If you run the simple Docker deployment of TeslaMate, then adding this will do the trick. You'll have TeslaMateApi exposed at port 8080 locally then.

```yaml
services:
    smartteslaampsetter:
    image: localhost:4567/smartteslaampsetter
    logging:
        driver: "json-file"
        options:
            max-file: "5"
            max-size: "10m"
    restart: always
    depends_on:
      - teslamateapi
    environment:
      - CurrentPowerToGridUrl=http://192.168.1.50:5007/api/ChargingLog/GetAverageGridPowerOfLastXseconds
      - TeslaMateApiBaseUrl=http://teslamateapi:8080
      - UpdateIntervalSeconds=30
      - CarPriorities=1|2
      - GeoFence=Zu Hause
      - MaxAmpPerCar=16
      - MinAmpPerCar=1
      - MinutesUntilSwitchOn=5
      - MinutesUntilSwitchOff=5
    ports:
      - 7190:80
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
| **TeslaMateApiBaseUrl** | string | Base URL to TeslaMateApi instance | http://teslamateapi:8080 |
| **UpdateIntervalSeconds** | int | Intervall how often the charging amps should be set (Note: TeslaMateApi takes some time to get new current values, so do not set a value lower than 30) | 30 |
| **CarPriorities** | string | TeslaMate Car Ids separated by \| in the priority order. | 1\|2 |
| **GeoFence** | string | TeslaMate Geofence Name where amps should be set | Home |
| **MaxAmpPerCar** | int | Maximum current that can be set to a single car | 16 |
| **MinAmpPerCar** | int | Minimum current that can be set to a single car | 1 |
| **MinutesUntilSwitchOn** | int | Minutes with more power to grid than minimum settable until charging starts | 5 |
| **MinutesUntilSwitchOff** | int | Minutes with power from grid until charging stops | 5 |

### Car Priorities
If you set `CarPriorities` environment variable like the example above, car with ID 2 will only start charing, if car 1 is charging at full speed and there is still power left, or if car 1 is not charging due to reached battery limit or not within specified geofence. Note: You always have to add the car Ids to this list separated by `|`. Even if you only have one car you need to ad the car's Id.
