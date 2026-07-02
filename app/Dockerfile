# ── Étape 1 : build ──────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copier d'abord les fichiers projet seuls : le restore est mis en cache
# tant que les dépendances ne changent pas (build reproductible et rapide).
COPY locatic/locatic.csproj locatic/
RUN dotnet restore locatic/locatic.csproj

COPY locatic/ locatic/
RUN dotnet publish locatic/locatic.csproj -c Release -o /out /p:UseAppHost=false

# ── Étape 2 : runtime ────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DB_PATH=/data/locatic.db

# Créer /data et lui donner l'utilisateur "app" (UID/GID 1654) *avant* de
# passer en non-root : un volume monté pour la première fois hérite des
# permissions du répertoire de l'image, sinon il reste root:root et
# SQLite ne peut plus écrire ("unable to open database file").
RUN mkdir /data && chown app:app /data

# Utilisateur non privilégié fourni par l'image de base (.NET 8) : UID 1654.
# Le conteneur n'a jamais besoin de root.
USER app

COPY --from=build /out .
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=10s \
  CMD wget -qO- http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Locatic.dll"]
