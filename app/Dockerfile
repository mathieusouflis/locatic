# Test Stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS test
WORKDIR /src
COPY locatic/locatic.csproj locatic/
COPY Locatic.Tests/Locatic.Tests.csproj Locatic.Tests/
RUN dotnet restore Locatic.Tests/Locatic.Tests.csproj
COPY locatic/ locatic/
COPY Locatic.Tests/ Locatic.Tests/
RUN dotnet test Locatic.Tests/Locatic.Tests.csproj --configuration Release

# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY locatic/locatic.csproj locatic/
RUN dotnet restore locatic/locatic.csproj

COPY locatic/ locatic/
RUN dotnet publish locatic/locatic.csproj -c Release -o /out /p:UseAppHost=false

# Production Stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS production
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DB_PATH=/data/locatic.db

RUN mkdir /data && chown app:app /data

USER app

COPY --from=build /out .
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --retries=3 \
  CMD wget -qO- http://127.0.0.1:8080/health >/dev/null || exit 1

ENTRYPOINT ["dotnet", "Locatic.dll"]
