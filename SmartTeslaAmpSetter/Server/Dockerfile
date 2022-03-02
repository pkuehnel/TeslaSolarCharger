#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["SmartTeslaAmpSetter/Server/SmartTeslaAmpSetter.Server.csproj", "SmartTeslaAmpSetter/Server/"]
COPY ["SmartTeslaAmpSetter/Client/SmartTeslaAmpSetter.Client.csproj", "SmartTeslaAmpSetter/Client/"]
COPY ["SmartTeslaAmpSetter/Shared/SmartTeslaAmpSetter.Shared.csproj", "SmartTeslaAmpSetter/Shared/"]
RUN dotnet restore "SmartTeslaAmpSetter/Server/SmartTeslaAmpSetter.Server.csproj"
COPY . .
WORKDIR "/src/SmartTeslaAmpSetter/Server"
RUN dotnet build "SmartTeslaAmpSetter.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SmartTeslaAmpSetter.Server.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SmartTeslaAmpSetter.Server.dll"]