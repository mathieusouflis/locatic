# Docker

## Pourquoi un Dockerfile

Pour l'instant, Locatic ne tourne que si le SDK .NET est installé et qu'on lance `dotnet run`. Le Dockerfile packe l'application dans une image qu'on peut démarrer sur n'importe quelle machine avec juste `docker run`, sans installer .NET. C'est ce qui permet ensuite de déployer sur Kubernetes (minikube).

Fichier dans `app/Dockerfile`, contexte de build : `app/` (les `COPY` sont relatifs à ce dossier, pas à la racine).

## Multi-stage build

Un Dockerfile classique installerait le SDK .NET (environ 800 Mo), compilerait, et garderait tout ça dans l'image finale, alors que le SDK n'est utile que pendant la compilation. Le multi-stage build découpe en plusieurs étapes (`FROM ... AS <nom>`) : on compile avec le SDK complet, puis on ne récupère que le résultat (quelques `.dll`) dans une étape finale basée sur le runtime seul (~220 Mo, sans compilateur).

Trois étapes.

### 1. `test`

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS test
WORKDIR /src
COPY locatic/locatic.csproj locatic/
COPY Locatic.Tests/Locatic.Tests.csproj Locatic.Tests/
RUN dotnet restore Locatic.Tests/Locatic.Tests.csproj
COPY locatic/ locatic/
COPY Locatic.Tests/ Locatic.Tests/
RUN dotnet test Locatic.Tests/Locatic.Tests.csproj --configuration Release
```

Si un test échoue, le `RUN` échoue et le build Docker s'arrête. Piège découvert en pratique : aucune étape ne fait `COPY --from=test`, donc Docker (qui ne construit que les étapes dont dépend la cible finale) ignore complètement cette étape sur un simple `docker build .`. Pour l'exécuter il faut cibler explicitement :

```bash
docker build --target test -t locatic:test-stage app
```

Pas grave dans notre cas : la CI fait déjà tourner `dotnet test` hors Docker, et le job qui construit l'image dépend de ce test. L'étage `test` reste utile pour vérifier manuellement en local.

### 2. `build`

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY locatic/locatic.csproj locatic/
RUN dotnet restore locatic/locatic.csproj
COPY locatic/ locatic/
RUN dotnet publish locatic/locatic.csproj -c Release -o /out /p:UseAppHost=false
```

`dotnet publish` produit une version prête à l'exécution dans `/out`.

Deux détails : copier le `.csproj` avant le reste du code permet à Docker de réutiliser le cache de `dotnet restore` (l'étape la plus longue) tant que le fichier ne change pas. `/p:UseAppHost=false` évite de générer l'exécutable natif ("apphost") inutile puisqu'on lance l'app avec `dotnet Locatic.dll`.

### 3. `production`

```dockerfile
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
```

C'est l'image utilisée en prod : runtime .NET seul (`aspnet:8.0`), résultat compilé récupéré via `COPY --from=build /out .`.

- `ASPNETCORE_HTTP_PORTS=8080` : port non privilégié, important puisqu'on tourne en non-root. Le HTTPS est géré par Nginx en frontal.
- `DB_PATH=/data/locatic.db` : lu par `Program.cs` pour la connexion SQLite. `/data` deviendra un volume Docker ou un PVC Kubernetes.
- `USER app` : l'image `aspnet:8.0` fournit un utilisateur non-root (`app`, UID/GID 1654), bonne pratique de sécurité de base.
- `RUN mkdir /data && chown app:app /data` avant `USER app` : bug rencontré en testant l'image, pas une précaution théorique. Au premier montage, un volume Docker hérite des permissions du dossier dans l'image ; sans ce chown, `/data` appartient à `root:root` et l'app (non-root) ne peut pas écrire dedans (`unable to open database file`).
- `HEALTHCHECK` : Docker et Kubernetes s'en servent pour vérifier que l'app répond, via `/health`.
- `ENTRYPOINT ["dotnet", "Locatic.dll"]` : nécessaire puisque l'apphost natif n'a pas été généré.

`app/.dockerignore` :

```
bin/
obj/
*.db
*.db-shm
*.db-wal
.git
.env
```

## Vérifier en local

```bash
# 1. Étape test (facultatif, la CI le refait)
docker build --target test -t locatic:test-stage app

# 2. Image finale
docker build -t locatic:dev app

# 3. Lancer avec un volume, pour simuler le PVC Kubernetes
docker volume create locatic-data
docker run -d --name locatic -p 8080:8080 -v locatic-data:/data locatic:dev

# 4. Vérifier
curl http://localhost:8080/health        # doit répondre "Healthy"
open http://localhost:8080
docker logs locatic                      # migrations EF appliquées au démarrage
```

Vérifier que les données survivent à un redémarrage :

```bash
# Créer un client dans l'UI, puis supprimer et relancer le conteneur
docker rm -f locatic
docker run -d --name locatic -p 8080:8080 -v locatic-data:/data locatic:dev
# le client créé est toujours là, et les logs ne montrent plus de migration
# (la base existait déjà dans le volume)
```

Captures dans `docs/preuves/`.

## Publier l'image à la main

En temps normal, la CI publie automatiquement à chaque merge sur `main` (voir [ci-cd.md](ci-cd.md)). Pour tester manuellement :

```bash
echo $GH_TOKEN | docker login ghcr.io -u <username> --password-stdin
docker tag locatic:dev ghcr.io/<username>/locatic:manual
docker push ghcr.io/<username>/locatic:manual
```
