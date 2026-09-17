# Stage 1: Build the Go binaries (tesla-control for pairing, tesla-bled as the long living BLE worker)
FROM golang:1.23 AS go
WORKDIR /app

# Pinned so an upstream change can only ever break the image build, never a user's runtime. Bump deliberately after
# reviewing the upstream changes; the go test gate below re-validates the injected code against the new version.
ARG VEHICLE_COMMAND_REF=724d8c85e3c5c948dbf04ca6b6022ad4d5f4be02

RUN apt-get update \
    && apt-get install -y curl

RUN curl -L https://api.github.com/repos/teslamotors/vehicle-command/tarball/${VEHICLE_COMMAND_REF} | tar -xz --strip=1
# The worker compiles inside the vehicle-command module (it uses internal/log for the -debug tracing), and the
# multi-VIN beacon scan needs the package private adapter singleton of pkg/connector/ble.
COPY ["TeslaSolarCharger.BleApi/go/tesla-bled/", "cmd/tesla-bled/"]
COPY ["TeslaSolarCharger.BleApi/go/vehicle-command-ext/", "pkg/connector/ble/"]
RUN go get ./...
RUN go build ./...
# Guards the worker protocol and the outcome classification: a mismatch there is invisible until a car stops
# answering or a present car is reported as away, so it must fail the image build instead.
RUN go vet ./cmd/tesla-bled/ ./pkg/connector/ble/
# The background presence scan, the radio arbiter and the request loop are genuinely concurrent, and a handover bug
# there would look like a command hanging behind a scan - invisible until it happens at a user's site. So the tests
# run with -race, but only on amd64: the race detector does not exist on 32 bit arm at all, and on the emulated arm64
# leg it would run under QEMU, where it is slow and unreliable. amd64 is the native leg of the release build, so the
# race check still runs once per build.
ARG TARGETARCH
RUN if [ "$TARGETARCH" = "amd64" ]; then \
        go test -race ./cmd/tesla-bled/ ./pkg/connector/ble/; \
    else \
        go test ./cmd/tesla-bled/ ./pkg/connector/ble/; \
    fi
RUN go install ./...

# Stage 2: Build the .NET application
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble-amd64 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["TeslaSolarCharger.BleApi/TeslaSolarCharger.BleApi.csproj", "TeslaSolarCharger.BleApi/"]
COPY ["PkSoftwareService.Custom.Backend/PkSoftwareService.Custom.Backend.csproj", "PkSoftwareService.Custom.Backend/"]
RUN dotnet restore "./TeslaSolarCharger.BleApi/TeslaSolarCharger.BleApi.csproj"
COPY . .
WORKDIR "/src/TeslaSolarCharger.BleApi"
RUN dotnet build "./TeslaSolarCharger.BleApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Stage 3: Publish the .NET application
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./TeslaSolarCharger.BleApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Stage 4: Final image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
#Do not change app directory as it is used inside the code
WORKDIR /app
EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80

# Copy Go application
COPY --from=go /go/bin /app/go

# Copy .NET application
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "TeslaSolarCharger.BleApi.dll"]
