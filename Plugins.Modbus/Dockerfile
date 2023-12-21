#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim-amd64 AS build
WORKDIR /src
COPY ["Plugins.Modbus/Plugins.Modbus.csproj", "Plugins.Modbus/"]
RUN dotnet restore "Plugins.Modbus/Plugins.Modbus.csproj"
COPY . .
WORKDIR "/src/Plugins.Modbus"
RUN dotnet build "Plugins.Modbus.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Plugins.Modbus.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Plugins.Modbus.dll"]