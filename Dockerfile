#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["SmartTeslaAmpSetter/SmartTeslaAmpSetter.csproj", "SmartTeslaAmpSetter/"]
RUN dotnet restore "SmartTeslaAmpSetter/SmartTeslaAmpSetter.csproj"
COPY . .
WORKDIR "/src/SmartTeslaAmpSetter"
RUN dotnet build "SmartTeslaAmpSetter.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SmartTeslaAmpSetter.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SmartTeslaAmpSetter.dll"]