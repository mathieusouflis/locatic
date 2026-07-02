# Docker — conteneurisation de Locatic

## Dockerfile attendu

À créer dans `app/Dockerfile` (contexte de build = `app/`) :

```dockerfile
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

# Utilisateur non privilégié fourni par l'image de base (.NET 8) : UID 1654.
# Le conteneur n'a jamais besoin de root.
USER app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DB_PATH=/data/locatic.db

COPY --from=build /out .
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=10s \
  CMD wget -qO- http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Locatic.dll"]
```

### Pourquoi ces choix

- **Multi-stage** : le SDK (~800 Mo) ne sert qu'au build ; l'image finale repose sur `aspnet:8.0` (~220 Mo), sans compilateur ni sources. Image plus légère et surface d'attaque réduite.
- **`COPY` du csproj avant le reste** : Docker met en cache la couche `dotnet restore` ; un changement de code ne retélécharge pas les dépendances.
- **`USER app`** : les images .NET 8 embarquent un utilisateur non-root prêt à l'emploi. Kubernetes pourra imposer `runAsNonRoot: true`.
- **`ASPNETCORE_HTTP_PORTS=8080`** : port non privilégié (< 1024 interdit sans root). Le TLS n'est pas géré ici : c'est le rôle de Nginx.
- **`DB_PATH=/data/locatic.db`** : le code lit cette variable pour construire la chaîne de connexion SQLite. `/data` sera un volume (Docker) ou un PVC (Kubernetes). Valeur surchargeable au `docker run` / dans le Deployment.
- **`HEALTHCHECK`** : utilise l'endpoint `/health` (qui teste aussi l'accès à la base).

Ajouter aussi `app/.dockerignore` :

```
bin/
obj/
*.db
*.db-shm
*.db-wal
Locatic.Tests/
```

## Vérification locale

```bash
# Build (depuis la racine du dépôt)
docker build -t locatic:dev app

# Lancer avec un volume pour la base (les données survivent au conteneur)
docker volume create locatic-data
docker run -d --name locatic -p 8080:8080 -v locatic-data:/data locatic:dev

# Vérifier
curl http://localhost:8080/health        # → Healthy
open http://localhost:8080               # l'application
docker logs locatic                      # logs de démarrage (migrations)

# Prouver la persistance : créer un client dans l'UI, puis
docker rm -f locatic
docker run -d --name locatic -p 8080:8080 -v locatic-data:/data locatic:dev
# → le client créé est toujours là
```

Garder une capture de ces vérifications dans `docs/preuves/`.

## Publication manuelle (avant que la CI le fasse)

```bash
echo $GH_TOKEN | docker login ghcr.io -u <username> --password-stdin
docker tag locatic:dev ghcr.io/<username>/locatic:manual
docker push ghcr.io/<username>/locatic:manual
```

En régime de croisière, c'est le job `publish` de la CI qui publie (voir [ci-cd.md](ci-cd.md)) — la publication manuelle ne sert qu'à valider l'accès registry.
