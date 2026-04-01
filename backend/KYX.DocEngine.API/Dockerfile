# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY KYX.DocEngine.API.csproj .
RUN dotnet restore KYX.DocEngine.API.csproj
COPY . .
RUN dotnet publish KYX.DocEngine.API.csproj -c Release -o /app/publish --no-restore

# Run (HTML→PDF via Chromium)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends \
    chromium \
    fontconfig \
    fonts-liberation \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*
# Pacote `chromium` no Ubuntu expõe normalmente `chromium-browser` (snap wrapper em imagens recentes).
ENV CHROME_EXECUTABLE_PATH=/usr/bin/chromium-browser
ENV ASPNETCORE_URLS=http://+:3000
EXPOSE 3000
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "KYX.DocEngine.API.dll"]
