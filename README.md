# TeslaSolarCharger

[![Docker version](https://img.shields.io/docker/v/pkuehnel/teslasolarcharger/latest)](https://hub.docker.com/r/pkuehnel/teslasolarcharger)
[![Docker size](https://img.shields.io/docker/image-size/pkuehnel/teslasolarcharger/latest)](https://hub.docker.com/r/pkuehnel/teslasolarcharger)
[![Docker pulls new name](https://img.shields.io/docker/pulls/pkuehnel/teslasolarcharger)](https://hub.docker.com/r/pkuehnel/teslasolarcharger)
[![Docker pulls old name](https://img.shields.io/docker/pulls/pkuehnel/smartteslaampsetter)](https://hub.docker.com/r/pkuehnel/smartteslaampsetter)
[![](https://img.shields.io/badge/Donate-PayPal-ff69b4.svg)](https://www.paypal.com/donate/?hosted_button_id=S3CK8Q9KV3JUL)
[![edgeRelease](https://github.com/pkuehnel/TeslaSolarCharger/actions/workflows/edgeRelease.yml/badge.svg)](https://github.com/pkuehnel/TeslaSolarCharger/actions/workflows/edgeRelease.yml)

Time spent in project since 14<sup>th</sup> April 2023:<br />
[![wakatime](https://wakatime.com/badge/github/pkuehnel/TeslaSolarCharger.svg)](https://wakatime.com/badge/github/pkuehnel/TeslaSolarCharger)


TeslaSolarCharger is a service to set one or multiple Teslas' charging current using the datalogger **[TeslaMate](https://github.com/adriankumpf/teslamate)**.

## Table of Contents

- [How to install](#how-to-install)
  - [Docker compose](#docker-compose)
  - [Setting up TeslaMate including TeslaSolarCharger](#Setting-up-TeslaMate-including-TeslaSolarCharger)
    - [docker-compose.yml content](#docker-composeyml-content)
    - [First startup of the application](#first-startup-of-the-application)
- [Often used optional settings](#often-used-optional-settings)
  - [Power Buffer](#power-buffer)
  - [Home Battery](#home-battery)
  - [Telegram integration](#telegram-integration)
- [How to use](#how-to-use)
  - [Charge Modes](#charge-modes)
- [Generate logfiles](#generate-logfiles)

## How to install

You can either install the software in a Docker container or download the binaries and deploy it on any server.

### Docker compose

The easiest way to use TeslaSolarCharger is with Docker.

Depending on your system, you have to install Docker first. To do this on a RaspberryPi (should be the same on standard Linux systems), you need to execute the following commands in your Terminal window:
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

If you are using a Windows host, install the Software from [here](https://docs.docker.com/desktop/install/windows-install/). Windows 11 is highly recommended. Select Linux Containers in the installation process.

### Setting up TeslaMate including TeslaSolarCharger

To set up TeslaSolarCharger, you must create a `docker-compose.yml` (name is important!) file in a new directory. Note: During the setup, some additional data folders to persist data will be created in that folder, so it is recommended to use a new directory for your `docker-compose.yml`.

#### docker-compose.yml content

The needed content of your `docker-compose.yml` depends on your inverter. By default, TeslaSolarCharger can consume JSON/XML REST APIs. To get the software running on [SMA](https://www.sma.de/) or [SolarEdge](https://www.solaredge.com/), you can use specific plugins which create the needed JSON API. You can use the software with any ModbusTCP-capable inverter also.

##### Content without using a plugin

Below you can see the content for your `docker-compose.yml` if you are not using any plugin. Note: I recommend changing as few things as possible on this file as this will increase the effort to set everything up but feel free to change the database password, encryption key, and Timezone. Important: If you change the password or the encryption key, you need to use the same password and encryption key at all points in your `docker-compose.yml`

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
    image: postgres:15
    restart: always
    environment:
      - POSTGRES_USER=teslamate
      - POSTGRES_PASSWORD=secret ##You can change your password here
      - POSTGRES_DB=teslamate
    volumes:
      - teslamate-db:/var/lib/postgresql/data

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
      - teslamate-grafana-data:/var/lib/grafana

  mosquitto:
    image: eclipse-mosquitto:2
    restart: always
    command: mosquitto -c /mosquitto-no-auth.conf
    #ports:
    #  - 1883:1883
    volumes:
      - mosquitto-conf:/mosquitto/config
      - mosquitto-data:/mosquitto/data

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
#      - Serilog__MinimumLevel__Default=Verbose #uncomment this line and recreate container with docker compose up -d for more detailed logs
      - TZ=Europe/Berlin ##You can change your Timezone here
    ports:
      - 7190:80
    volumes:
      - teslasolarcharger-configs:/app/configs

volumes:
  teslamate-db:
  teslamate-grafana-data:
  mosquitto-conf:
  mosquitto-data:
  teslasolarcharger-configs:

```

##### Content using SMA plugin

[![Docker version](https://img.shields.io/docker/v/pkuehnel/teslasolarchargersmaplugin/latest)](https://hub.docker.com/r/pkuehnel/teslasolarchargersmaplugin)
[![Docker size](https://img.shields.io/docker/image-size/pkuehnel/teslasolarchargersmaplugin/latest)](https://hub.docker.com/r/pkuehnel/teslasolarchargersmaplugin)
[![Docker pulls new name](https://img.shields.io/docker/pulls/pkuehnel/teslasolarchargersmaplugin)](https://hub.docker.com/r/pkuehnel/teslasolarchargersmaplugin)
[![Docker pulls old name](https://img.shields.io/docker/pulls/pkuehnel/smartteslaampsettersmaplugin)](https://hub.docker.com/r/pkuehnel/smartteslaampsettersmaplugin)

The SMA plugin is used to access your EnergyMeter (or Sunny Home Manager 2.0) values.
To use the plugin, add these lines to the bottom of your `docker-compose.yml`.

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
    image: postgres:15
    restart: always
    environment:
      - POSTGRES_USER=teslamate
      - POSTGRES_PASSWORD=secret ##You can change your password here
      - POSTGRES_DB=teslamate
    volumes:
      - teslamate-db:/var/lib/postgresql/data

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
      - teslamate-grafana-data:/var/lib/grafana

  mosquitto:
    image: eclipse-mosquitto:2
    restart: always
    command: mosquitto -c /mosquitto-no-auth.conf
    #ports:
    #  - 1883:1883
    volumes:
      - mosquitto-conf:/mosquitto/config
      - mosquitto-data:/mosquitto/data

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
  teslamate-db:
  teslamate-grafana-data:
  mosquitto-conf:
  mosquitto-data:
  teslasolarcharger-configs:

```
  
</details>

##### Content using SolarEdge plugin

[![Docker version](https://img.shields.io/docker/v/pkuehnel/teslasolarchargersolaredgeplugin/latest)](https://hub.docker.com/r/pkuehnel/teslasolarchargersolaredgeplugin)
[![Docker size](https://img.shields.io/docker/image-size/pkuehnel/teslasolarchargersolaredgeplugin/latest)](https://hub.docker.com/r/pkuehnel/teslasolarchargersolaredgeplugin)
[![Docker pulls new name](https://img.shields.io/docker/pulls/pkuehnel/teslasolarchargersolaredgeplugin)](https://hub.docker.com/r/pkuehnel/teslasolarchargersolaredgeplugin)
[![Docker pulls old name](https://img.shields.io/docker/pulls/pkuehnel/smartteslaampsettersolaredgeplugin)](https://hub.docker.com/r/pkuehnel/smartteslaampsettersolaredgeplugin)

The SolarEdge Plugin uses the cloud API, which is limited to 300 which is reset after 15 minutes. When the limit is reached the solaredge API does not gather any new values. This results in TSC displaying 0 grid and home battery power until 15 minutes are over.

To use the plugin, just add these lines to the bottom of your `docker-compose.yml`. Note: You have to change your site ID and your API key in the `CloudUrl` environment variable

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
    image: postgres:15
    restart: always
    environment:
      - POSTGRES_USER=teslamate
      - POSTGRES_PASSWORD=secret ##You can change your password here
      - POSTGRES_DB=teslamate
    volumes:
      - teslamate-db:/var/lib/postgresql/data

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
      - teslamate-grafana-data:/var/lib/grafana

  mosquitto:
    image: eclipse-mosquitto:2
    restart: always
    command: mosquitto -c /mosquitto-no-auth.conf
    #ports:
    #  - 1883:1883
    volumes:
      - mosquitto-conf:/mosquitto/config
      - mosquitto-data:/mosquitto/data


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
  teslamate-db:
  teslamate-grafana-data:
  mosquitto-conf:
  mosquitto-data:
  teslasolarcharger-configs:

```
  
</details>

##### Content using Modbus plugin

[![Docker version](https://img.shields.io/docker/v/pkuehnel/teslasolarchargermodbusplugin/latest)](https://hub.docker.com/r/pkuehnel/teslasolarchargermodbusplugin)
[![Docker size](https://img.shields.io/docker/image-size/pkuehnel/teslasolarchargermodbusplugin/latest)](https://hub.docker.com/r/pkuehnel/teslasolarchargermodbusplugin)
[![Docker pulls](https://img.shields.io/docker/pulls/pkuehnel/teslasolarchargermodbusplugin)](https://hub.docker.com/r/pkuehnel/teslasolarchargermodbusplugin)

You can also use the Modbus plugin. This is a general plugin, so don't be surprised if it does not work as expected right after starting up. Feel free to share your configurations [here](https://github.com/pkuehnel/TeslaSolarCharger/discussions/174) so I can add templates for future users.

To use the plugin, just add these lines to the bottom of your `docker-compose.yml`. Note: As some inverters struggle with too many requests within a specific time, you can change the `RequestBlockMilliseconds` environment variable.

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
    image: postgres:15
    restart: always
    environment:
      - POSTGRES_USER=teslamate
      - POSTGRES_PASSWORD=secret ##You can change your password here
      - POSTGRES_DB=teslamate
    volumes:
      - teslamate-db:/var/lib/postgresql/data

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
      - teslamate-grafana-data:/var/lib/grafana

  mosquitto:
    image: eclipse-mosquitto:2
    restart: always
    command: mosquitto -c /mosquitto-no-auth.conf
    #ports:
    #  - 1883:1883
    volumes:
      - mosquitto-conf:/mosquitto/config
      - mosquitto-data:/mosquitto/data

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
#      - Serilog__MinimumLevel__Default=Verbose #uncomment this line and recreate container with docker compose up -d for more detailed logs
      - TZ=Europe/Berlin ##You can change your Timezone here
    ports:
      - 7190:80
    volumes:
      - teslasolarcharger-configs:/app/configs
  
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

volumes:
  teslamate-db:
  teslamate-grafana-data:
  mosquitto-conf:
  mosquitto-data:
  teslasolarcharger-configs:
```
  
</details>


##### Content using Solax plugin

[![Docker version](https://img.shields.io/docker/v/pkuehnel/teslasolarchargersolaxplugin/latest)](https://hub.docker.com/r/pkuehnel/teslasolarchargersolaxplugin)
[![Docker size](https://img.shields.io/docker/image-size/pkuehnel/teslasolarchargersolaxplugin/latest)](https://hub.docker.com/r/pkuehnel/teslasolarchargersolaxplugin)
[![Docker pulls](https://img.shields.io/docker/pulls/pkuehnel/teslasolarchargersolaxplugin)](https://hub.docker.com/r/pkuehnel/teslasolarchargersolaxplugin)

To use the Solax plugin, just add these lines to the bottom of your `docker-compose.yml`. Note: You have to specify your solar system's IP address and password.

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
    image: postgres:15
    restart: always
    environment:
      - POSTGRES_USER=teslamate
      - POSTGRES_PASSWORD=secret ##You can change your password here
      - POSTGRES_DB=teslamate
    volumes:
      - teslamate-db:/var/lib/postgresql/data

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
      - teslamate-grafana-data:/var/lib/grafana

  mosquitto:
    image: eclipse-mosquitto:2
    restart: always
    command: mosquitto -c /mosquitto-no-auth.conf
    #ports:
    #  - 1883:1883
    volumes:
      - mosquitto-conf:/mosquitto/config
      - mosquitto-data:/mosquitto/data

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
  teslamate-db:
  teslamate-grafana-data:
  mosquitto-conf:
  mosquitto-data:
  teslasolarcharger-configs:
```
  
</details>

#### First startup of the application

1. Move to your above created directory with your `docker-compose.yml`.
1. Start all containers using the command `docker compose up -d`.
1. Use a third-party app to create a new Tesla Token [[Android](https://play.google.com/store/apps/details?id=net.leveugle.teslatokens&hl=en_US&gl=US)] [[iOS](https://apps.apple.com/us/app/tesla-token/id1411393432)]
1. Open your browser, go to `http://your-ip-address:4000` and paste your token and refresh token into the form.
1. Go to `Geo-Fences` and add a Geo-Fence called `Home` at the location you want TeslaSolarCharger to be active.
1. Open `http://your-ip-address:7190`
1. Go to `Base Configuration` (if you are on a mobile device, it is behind the menu button).

##### Setting Up Urls to get grid power

To let the TeslaSolarCharger know how much power there is to charge the car, you need to add a value in `Grid Power Url`.

###### Using vendor specific plugins

**Note:** These values will be filled in automatically in a future release. Maybe it is already working, and I just forgot to remove this section ;-)
Depending on your used plugins, you have to paste one of the following URLs to the `Grid Power Url` field:

- SMA Plugin: `http://<IP of your Docker host>:7192/api/CurrentPower/GetPower`
Note: If you have more than one EnergyMeter/Home Manager 2.0 in your network you need to add the serial number of the correct device. A grid URL would look like this then: `http://<IP of your Docker host>:7192/api/CurrentPower/GetPower?serialNumber=3001231234`
- SolarEdge Plugin:
  - Grid Power, InverterPower, HomeBatterySoc, Home Battery Power Url: `http://solaredgeplugin/api/CurrentValues/GetCurrentPvValues`
  - Set Result types to json and use the following json patterns:
    - Grid Power: `$.gridPower`
    - Inverter Power: `$.inverterPower`
    - Home Battery SoC: `$.homeBatterySoc`
    - Home Battery Power: `$.homeBatteryPower`
- Solax Plugin:
  - Grid Power, InverterPower, HomeBatterySoc, Home Battery Power Url: `http://solaxplugin/api/CurrentValues/GetCurrentPvValues`
  - Set Result types to json and use the following json patterns:
    - Grid Power: `$.gridPower`
    - Inverter Power: `$.inverterPower`
    - Home Battery SoC: `$.homeBatterySoc`
    - Home Battery Power: `$.homeBatteryPower`
  - The result should look like this:
  ![image](https://user-images.githubusercontent.com/35361981/226210694-18e1af38-25e8-43d8-a13d-6671f0d65fbc.png)


###### Using the Modbus plugin

**Warning:** As this plugin keeps an open connection to your inverter, it is highly recommended not to kill this container but always shut it down gracefully.
To use the Modbus plugin, you must create the URL string yourself. The URL looks like this:

```text
http://modbusplugin/api/Modbus/GetInt32Value?unitIdentifier=3&startingAddress=<modbusregisterAddress>&quantity=<NumberOFModbusRegistersToRead>&ipAddress=<IPAdressOfModbusDevice>&port=502&factor=<conversionFactor>&connectDelaySeconds=1&timeoutSeconds=10
```

An example URL with all values filled could look like this:

```text
http://modbusplugin/api/Modbus/GetInt32Value?unitIdentifier=3&startingAddress=30775&quantity=2&ipAddress=192.168.1.28&port=502&factor=1&connectDelaySeconds=1&timeoutSeconds=10
```

You can test the result of the URL by pasting it into your browser and replacing `modbusplugin` with `ipOfYourDockerHost:7191`, e.g.:

```text
http://192.168.1.50:7191/api/Modbus/GetInt32Value?unitIdentifier=3&startingAddress=30775&quantity=2&ipAddress=192.168.1.28&port=502&factor=1&connectDelaySeconds=1&timeoutSeconds=10
```

What the values mean:

- `unitIdentifier`: Internal ID of your inverter (in most cases, 3)
- `startingAddress`: Register address of the value you want to extract. You will find this value in the documentation of your inverter.
- `quantity`: Number of registers to read from (for integer values should be 2)
- `ipAddress`: IP Address of your inverter
- `port`: Modbus TCP Port of your inverter (default: 502)
- `factor`: Factor to multiply the resulting value with. The result should be Watt, so if your inverter returns Watt, you can leave 1. If your inverter returns 0.1W, you have to use 10.
- `connectDelaySeconds`: Delay before communication the first time (you should use 1)
- `timeoutSeconds`: Timeout until returning an error if the inverter is not responding (you should use 10)

For more convenience, you can go to `http://your-ip-address:7191/swagger`. There you can try your values with a user interface.

###### Using no plugin

If you have your own API or your energymeter directly has a REST API, you can also use these to get the grid power. Just insert the `Grid Power Url` Url; if there is a plain integer value, it should work. If your API returns JSON or XML results, you must add the exact path to that specific value.

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

You can use `$.data.value` as `Grid Power Json Pattern`.

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
- CurrentPowerToGridUrl=http://192.168.xxx.xxx/measurements.xml
- CurrentPowerToGridXmlPattern=Device/Measurements/Measurement
- CurrentPowerToGridXmlAttributeHeaderName=Type
- CurrentPowerToGridXmlAttributeHeaderValue=GridPower
- CurrentPowerToGridXmlAttributeValueName=Value
```

Inverter Power:
Assuming the `Measurement` node with `Type` `AC_Power` is the power your inverter is currently feeding, you can use the following  values in your Base configuration:

```yaml
- CurrentInverterPowerUrl=http://192.168.xxx.xxx/measurements.xml
- CurrentInverterPowerXmlPattern=Device/Measurements/Measurement
- CurrentInverterPowerAttributeHeaderName=Type
- CurrentInverterPowerAttributeHeaderValue=AC_Power
- CurrentInverterPowerAttributeValueName=Value
```

**Note:** These values are not needed. They are just used to show additional information.

## Often used optional settings

When you are at this point, your car connected to any charging cable in your set home area should start charging based on solar power. But there are a few additional settings that are maybe helpful for your environment:

### Power Buffer

If you set `PowerBuffer` to a value different from `0`, the system uses the value as an offset. E.g., If you set `1000`, the car's current is reduced as long as less than 1000 Watt power goes to the grid.

### Home Battery

To configure your home battery, you need to add the following settings:

- URL for getting the state of charge
- URL for getting current charging/discharging power
- Home Battery Minimum SoC
- Home Battery Charging Power

After setting everything up, your overview page should look like this:

![image](https://user-images.githubusercontent.com/35361981/183434947-16d13372-09ff-45a7-94a2-8d4043f39f18.png)

**Note:** If your battery is discharging, the power should be displayed in red. If the battery is charging, the power should be displayed in green. If this is the other way around, you must update the `Correction Factor` below your `HomeBatteryPower Url` setting and invert it to a negative number, e.g. `-1.0`.

### Telegram integration
In this section you learn how to create the Telegram Bot Key and where you get the Telegram ChannelID from:

- Create a bot by chatting with `BotFather`

![Botfather](https://user-images.githubusercontent.com/35361981/233467207-918b7871-54dd-4ea0-bf9e-b828f2da3509.jpg)

- Ask `BotFather` to reate a new bot with the `/newbot` command and follow the instructions

![newbot](https://user-images.githubusercontent.com/35361981/233468050-b996475a-fe3a-4131-805e-0fe4c60ce603.jpg)

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
1. **Min SoC + PV**: If plugged in, the car starts charging with maximum power until the set Min SoC is reached. After that, only PV Power is used to charge the car.
1. **Spot Price + PV**: You can set a Min Soc, which should be reached at a specific date and time (if charge every day is enabled, the car charges to that SoC every day, not only once). The charge times are then planned to charge at the cheapest possible time. This is especially useful if you have hourly electricity prices like with [Tibber](https://tibber.com/) or [aWATTar](https://www.awattar.de/). Note: The car will still charge based on Solar energy if available, and you need to enable `Use Spot Price` in the Charge Prices settings for correct charge price calculation.
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
