# Build (restore/publish com menos paralelismo reduz RAM — exit 137 / CoreCLR 0x80070008 no CI = OOM comum)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    DOTNET_NOLOGO=1 \
    NUGET_XMLDOC_MODE=skip \
    DOTNET_gcServer=0
COPY KYX.DocEngine.API.csproj .
RUN dotnet restore KYX.DocEngine.API.csproj --verbosity minimal --disable-parallel
COPY . .
RUN dotnet publish KYX.DocEngine.API.csproj -c Release -o /app/publish --no-restore -m:1

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
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "KYX.DocEngine.API.dll"]
