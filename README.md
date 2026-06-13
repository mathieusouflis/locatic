# Locatic

[![CI](https://github.com/your-org/locatic/actions/workflows/ci.yml/badge.svg)](https://github.com/your-org/locatic/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Application ASP.NET Core MVC de gestion d'une agence de location de voitures. Permet de gérer le parc (marques, modèles, voitures), les clients et les réservations, avec persistance SQLite via Entity Framework Core.

---

## Prérequis

| Outil | Version |
|-------|---------|
| .NET SDK | >= 8.0 |

---

## Démarrage rapide

```bash
git clone <url-du-repo>
cd locatic

# Premier lancement : applique la migration et charge les données de départ
dotnet run
```

L'application est accessible sur `http://localhost:5118`.

---

## Développement

```bash
# Lancer en mode développement
dotnet run

# Ajouter une migration après modification d'une entité
dotnet ef migrations add NomDeLaMigration

# Compiler sans lancer
dotnet build
```

---

## Architecture

```
locatic/
├── locatic.csproj          # Fichier projet .NET
├── Program.cs              # Point d'entrée, configuration DI
├── appsettings.json
├── Migrations/             # Migrations EF Core
├── Views/                  # Vues Razor (Tailwind CDN)
├── wwwroot/                # Fichiers statiques
└── src/
    ├── Domain/
    │   ├── Entities/       # Brand, CarModel, Car, Client, Reservation
    │   └── Enums/          # FuelType
    ├── Infrastructure/
    │   ├── Data/           # AppDbContext + seed
    │   └── Repositories/   # Interfaces + implémentations
    ├── Application/
    │   ├── Services/       # Logique métier (interfaces + implémentations)
    │   ├── ViewModels/     # ViewModels par opération
    │   └── Validation/     # DateAnnotations custom
    └── Controllers/        # Controllers MVC (orchestration uniquement)
```

**Règles métier :**
- Date de fin postérieure à la date de début (DataAnnotation custom)
- Vérification des chevauchements de réservations (service layer)

---

## License

Distributed under the [MIT License](LICENSE).
