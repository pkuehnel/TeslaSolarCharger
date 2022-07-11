#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0-bullseye-slim-amd64 AS build
WORKDIR /src
COPY ["TeslaSolarCharger/Server/TeslaSolarCharger.Server.csproj", "TeslaSolarCharger/Server/"]
COPY ["TeslaSolarCharger/Client/TeslaSolarCharger.Client.csproj", "TeslaSolarCharger/Client/"]
COPY ["TeslaSolarCharger/Shared/TeslaSolarCharger.Shared.csproj", "TeslaSolarCharger/Shared/"]
RUN dotnet restore "TeslaSolarCharger/Server/TeslaSolarCharger.Server.csproj"
COPY . .
WORKDIR "/src/TeslaSolarCharger/Server"
RUN dotnet build "TeslaSolarCharger.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TeslaSolarCharger.Server.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TeslaSolarCharger.Server.dll"]