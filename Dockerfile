# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY WatchParty.slnx ./
COPY src/WatchParty.Domain/*.csproj src/WatchParty.Domain/
COPY src/WatchParty.Application/*.csproj src/WatchParty.Application/
COPY src/WatchParty.Infrastructure/*.csproj src/WatchParty.Infrastructure/
COPY src/WatchParty.Api/*.csproj src/WatchParty.Api/
RUN dotnet restore src/WatchParty.Api
COPY src/ src/
RUN dotnet publish src/WatchParty.Api -c Release -o /app --no-restore

# Runtime (with FFmpeg to convert uploads)
FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    ConnectionStrings__Default="Data Source=/data/watchparty.db" \
    Storage__RootPath=/data/media \
    DataProtection__KeysPath=/data/keys

VOLUME /data
EXPOSE 8080
ENTRYPOINT ["dotnet", "WatchParty.Api.dll"]
