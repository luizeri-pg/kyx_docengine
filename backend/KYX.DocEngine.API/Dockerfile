# Etapa de build: restaura e publica a API (padrão NotifyHub; DocEngine = .NET 10 Alpine).
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY KYX.DocEngine.API.csproj .
RUN dotnet restore "KYX.DocEngine.API.csproj"

COPY . .
RUN dotnet publish "KYX.DocEngine.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Etapa final: runtime enxuto + Chromium (HTML→PDF / PuppeteerSharp)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

RUN apk add --no-cache \
    chromium \
    fontconfig \
    freetype \
    ttf-freefont \
    ca-certificates

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
ENV CHROME_EXECUTABLE_PATH=/usr/bin/chromium
ENV DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false
ENV DOTNET_USE_POLLING_FILE_WATCHER=1

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "KYX.DocEngine.API.dll"]
