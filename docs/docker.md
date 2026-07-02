# Docker — conteneuriser Locatic

## Pourquoi un Dockerfile

Pour l'instant, Locatic ne tourne que si on a le SDK .NET installé sur sa machine et qu'on lance `dotnet run` à la main. Le but du Dockerfile est de packager l'application (le code compilé + tout ce dont il a besoin pour tourner) dans une image Docker : une sorte de "boîte" autonome qu'on peut démarrer sur n'importe quelle machine avec juste `docker run`, sans installer .NET. C'est ce qui nous permettra ensuite de déployer l'app sur Kubernetes (minikube).

Le fichier se trouve dans `app/Dockerfile`, et le contexte de build est le dossier `app/` (c'est-à-dire que toutes les commandes `COPY` du Dockerfile sont relatives à `app/`, pas à la racine du repo).

## Le principe du multi-stage build

Un Dockerfile classique ferait tout dans une seule image : installer le SDK .NET, copier le code, compiler, et garder tout ça dans l'image finale. Le problème, c'est que le SDK .NET pèse environ 800 Mo (compilateur, outils de build...), alors qu'on n'en a besoin que pendant la compilation — pas quand l'app tourne en prod.

Le multi-stage build résout ça en découpant le Dockerfile en plusieurs étapes (`FROM ... AS <nom>`), où chaque étape peut partir d'une image de base différente. On compile dans une étape avec le SDK complet, puis on ne récupère que le résultat de la compilation (quelques fichiers .dll) dans une étape finale qui, elle, part d'une image beaucoup plus légère (juste le runtime .NET, ~220 Mo, sans compilateur). Le SDK et tout le code source disparaissent de l'image finale : elle ne contient que ce qui est nécessaire pour exécuter l'app.

Notre Dockerfile a trois étapes :

### Étape 1 — `test`

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

Cette étape restaure les dépendances puis lance `dotnet test`. Si un test échoue, la commande `RUN` renvoie une erreur et **le build Docker s'arrête** — impossible de construire une image si les tests ne passent pas.

Petit piège que j'ai découvert en faisant ça : cette étape n'est jamais utilisée par la suite du Dockerfile (aucune étape ne fait `COPY --from=test`). Or Docker ne construit **que** les étapes dont dépend la cible finale — donc un simple `docker build .` **ignore complètement** cette étape `test` et ne la lance jamais ! Pour vraiment l'exécuter, il faut la cibler explicitement :

```bash
docker build --target test -t locatic:test-stage app
```

Dans notre cas ce n'est pas grave : dans la CI, le job `test` fait déjà tourner `dotnet test` en dehors de Docker, et le job qui construit l'image (`needs: test`) ne démarre que si les tests sont verts. L'étape `test` du Dockerfile reste quand même utile pour vérifier manuellement en local, ou si un jour on branche l'image Docker elle-même sur un autre pipeline.

### Étape 2 — `build`

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY locatic/locatic.csproj locatic/
RUN dotnet restore locatic/locatic.csproj
COPY locatic/ locatic/
RUN dotnet publish locatic/locatic.csproj -c Release -o /out /p:UseAppHost=false
```

Ici on compile réellement l'application avec `dotnet publish`, qui produit une version prête à être exécutée dans le dossier `/out`.

Deux détails à expliquer :

- **Copier le `.csproj` avant le reste du code** (`COPY locatic/locatic.csproj locatic/` puis `RUN dotnet restore`, et *seulement après* `COPY locatic/ locatic/`) : ça a l'air bizarre de copier en deux fois, mais c'est fait exprès. Docker met en cache chaque instruction du Dockerfile : tant que le `.csproj` ne change pas, Docker réutilise le cache de `dotnet restore` (qui télécharge tous les packages NuGet) au lieu de le refaire. Si je modifiais juste un fichier `.cs`, seule la deuxième `COPY` changerait, et le `restore` — l'étape la plus longue — ne serait pas relancée à chaque build.

- **`/p:UseAppHost=false`** : par défaut, `dotnet publish` génère deux choses : le `.dll` de l'app, et un exécutable natif (l'"apphost") qui permet de lancer l'app directement sans taper `dotnet` devant. On n'en a pas besoin ici puisqu'on va lancer l'app avec `dotnet Locatic.dll` (voir l'étape 3) — cette option évite de générer ce fichier en plus, pour rien.

### Étape 3 — `production`

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

C'est l'image qu'on utilise vraiment en prod. Elle part de `aspnet:8.0` (le runtime .NET tout seul, sans le SDK) et récupère juste le résultat compilé de l'étape `build` grâce à `COPY --from=build /out .`.

Point par point :

- **`ASPNETCORE_HTTP_PORTS=8080`** : dit à l'app d'écouter sur le port 8080 à l'intérieur du conteneur. Ce n'est pas un port "privilégié" (les ports < 1024 demandent des droits root), ce qui est important vu qu'on tourne en non-root (voir plus bas). Le HTTPS n'est pas géré ici : ce sera le rôle de Nginx en frontal, plus tard.
- **`DB_PATH=/data/locatic.db`** : cette variable est lue par `Program.cs` pour construire la chaîne de connexion SQLite. `/data` est pensé pour devenir un volume monté (Docker) ou un PersistentVolumeClaim (Kubernetes), donc les données de la base survivent même si le conteneur est supprimé et recréé.
- **`USER app`** : l'image `aspnet:8.0` fournit déjà un utilisateur non-root appelé `app` (UID/GID 1654). Faire tourner le conteneur avec cet utilisateur plutôt qu'en root, c'est une bonne pratique de sécurité de base : si jamais quelqu'un arrive à exécuter du code dans le conteneur, il n'aura pas les pleins pouvoirs dessus.
- **`RUN mkdir /data && chown app:app /data` avant `USER app`** : c'est un bug que j'ai vraiment rencontré en testant l'image en local, pas une précaution théorique. Quand on monte un volume Docker pour la première fois sur un dossier, ce dossier récupère les permissions telles qu'elles étaient dans l'image *au moment du montage*. Si je ne fais rien, `/data` appartient à `root:root` par défaut, et comme le conteneur tourne avec l'utilisateur `app` (non-root), il n'a pas le droit d'écrire dedans — l'app crashe au démarrage avec `unable to open database file`. En créant `/data` et en donnant sa propriété à `app` *avant* de passer en `USER app`, le volume hérite des bonnes permissions dès le premier montage.
- **`HEALTHCHECK`** : Docker (et Kubernetes plus tard) peuvent utiliser cette instruction pour vérifier régulièrement que l'app répond toujours, en interrogeant l'endpoint `/health` de l'application.
- **`ENTRYPOINT ["dotnet", "Locatic.dll"]`** : c'est la commande qui s'exécute quand on démarre le conteneur. Comme on a désactivé la génération de l'apphost natif à l'étape précédente (`UseAppHost=false`), on ne peut pas lancer l'app directement — il faut passer par `dotnet`, le runtime qui sait exécuter un `.dll` .NET.

`app/.dockerignore` évite de copier des fichiers inutiles (ou dangereux) dans le contexte de build :

```
bin/
obj/
*.db
*.db-shm
*.db-wal
.git
.env
```

## Vérifier que ça marche en local

Avant de faire confiance à ce Dockerfile, je l'ai testé moi-même étape par étape :

```bash
# 1. Vérifier que l'étape test passe (facultatif, la CI le refait de toute façon)
docker build --target test -t locatic:test-stage app

# 2. Construire l'image finale
docker build -t locatic:dev app

# 3. La lancer avec un volume, pour simuler ce qui se passera avec un PVC Kubernetes
docker volume create locatic-data
docker run -d --name locatic -p 8080:8080 -v locatic-data:/data locatic:dev

# 4. Vérifier que ça répond
curl http://localhost:8080/health        # doit répondre "Healthy"
open http://localhost:8080               # l'application dans le navigateur
docker logs locatic                      # voir les logs (migrations EF appliquées au démarrage)
```

Le test le plus important, c'est de vérifier que les données survivent bien à un redémarrage du conteneur — sinon tout l'intérêt du volume ne sert à rien :

```bash
# Créer un client dans l'UI, puis supprimer et relancer le conteneur
docker rm -f locatic
docker run -d --name locatic -p 8080:8080 -v locatic-data:/data locatic:dev
# → le client créé est toujours là, et les logs ne montrent plus de migration
#   appliquée (la base existait déjà dans le volume)
```

Je garde des captures de ces vérifications dans `docs/preuves/`.

## Publier l'image à la main (pour tester)

En temps normal, c'est la CI qui publie l'image sur la registry automatiquement à chaque merge sur `main` (voir [ci-cd.md](ci-cd.md)). Mais avant d'avoir la CI en place, ou pour vérifier que les accès à la registry fonctionnent, je peux le faire à la main :

```bash
echo $GH_TOKEN | docker login ghcr.io -u <username> --password-stdin
docker tag locatic:dev ghcr.io/<username>/locatic:manual
docker push ghcr.io/<username>/locatic:manual
```
