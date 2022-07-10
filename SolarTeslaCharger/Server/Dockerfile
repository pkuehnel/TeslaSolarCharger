#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0-bullseye-slim-amd64 AS build
WORKDIR /src
COPY ["SolarTeslaCharger/Server/SolarTeslaCharger.Server.csproj", "SolarTeslaCharger/Server/"]
COPY ["SolarTeslaCharger/Client/SolarTeslaCharger.Client.csproj", "SolarTeslaCharger/Client/"]
COPY ["SolarTeslaCharger/Shared/SolarTeslaCharger.Shared.csproj", "SolarTeslaCharger/Shared/"]
RUN dotnet restore "SolarTeslaCharger/Server/SolarTeslaCharger.Server.csproj"
COPY . .
WORKDIR "/src/SolarTeslaCharger/Server"
RUN dotnet build "SolarTeslaCharger.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SolarTeslaCharger.Server.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SolarTeslaCharger.Server.dll"]