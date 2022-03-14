#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0-bullseye-slim-arm32v7 AS build
WORKDIR /src
COPY ["Plugins.SmaEnergymeter/Plugins.SmaEnergymeter.csproj", "Plugins.SmaEnergymeter/"]
RUN dotnet restore "Plugins.SmaEnergymeter/Plugins.SmaEnergymeter.csproj"
COPY . .
WORKDIR "/src/Plugins.SmaEnergymeter"
RUN dotnet build "Plugins.SmaEnergymeter.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Plugins.SmaEnergymeter.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Plugins.SmaEnergymeter.dll"]