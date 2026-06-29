# Stage 1: Build the Go application
FROM golang:1.23 AS go
WORKDIR /app

RUN apt-get update \
    && apt-get install -y curl

RUN curl -L https://api.github.com/repos/teslamotors/vehicle-command/tarball/main | tar -xz --strip=1
RUN go get ./...
RUN go build ./...
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
