#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:9.0-bookworm-slim-amd64 AS build
WORKDIR /src
COPY ["Plugins.Solax/Plugins.Solax.csproj", "Plugins.Solax/"]
RUN dotnet restore "Plugins.Solax/Plugins.Solax.csproj"
COPY . .
WORKDIR "/src/Plugins.Solax"
RUN dotnet build "Plugins.Solax.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Plugins.Solax.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:80
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Plugins.Solax.dll"]