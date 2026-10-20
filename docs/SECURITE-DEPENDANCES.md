# Sécurité des dépendances

## Ce qui a été trouvé

`dotnet list package --vulnerable --include-transitive` sur le projet a remonté des vulnérabilités de sévérité **haute
ou critique**, toutes dans des dépendances **transitives** (tirées par d'autres paquets, que personne ne choisit
explicitement) :

| Paquet | Version trouvée | Sévérité | Tiré par | Correction |
|---|---|---|---|---|
| `MessagePack` | 2.5.187 | haute (12 avis) | backplane SignalR Redis | 2.5.307 |
| `System.Net.Http` | 4.3.0 | haute | `Serilog.Sinks.ColoredConsole` → `NETStandard.Library` 1.6.1 (Core) | 4.3.4 |
| `System.Text.RegularExpressions` | 4.3.0 | haute | idem (`NETStandard.Library` 1.6.1) | 4.3.1 |
| `System.Text.Json` | 8.0.0 | haute (2 avis) | MassTransit 8.1.3 (Application, Notification) | 8.0.5 |
| `System.Text.Encodings.Web` | 4.5.0 | **critique** | `Microsoft.AspNetCore.Http.Abstractions` 2.2 (Application) | 8.0.0 |
| `Newtonsoft.Json` | 11.0.1 | haute | Hangfire (Notification, Newsletter) | 13.0.4 |
| `Npgsql` | 6.0.0 | haute (injection SQL) | Hangfire.PostgreSql (Notification, Newsletter) | 6.0.11 (même branche, sans rupture) |
| `SQLitePCLRaw.lib.e_sqlite3` | 2.1.10 | haute | EF Core Sqlite (**tests uniquement**) | 2.1.14 |
| `Microsoft.AspNetCore.SignalR.Common` | 8.0.22 | haute (déni de service) | client SignalR (**tests uniquement**) | client 8.0.32 |

Résultat après correction : **aucun paquet vulnérable** dans les 9 projets.

## Comment c'est corrigé

On ne peut pas « mettre à jour » un paquet transitif directement : on **ajoute une référence directe** à la version
corrigée dans le projet concerné (NuGet retient la version la plus élevée demandée). Ces lignes sont regroupées dans un
bloc commenté en bas de chaque `.csproj`. Choix volontairement prudents :

- `Npgsql` reste en **6.0.11** pour Notification/Newsletter : c'est la version corrigée *de la même branche*, donc
  compatible avec `Hangfire.PostgreSql` 1.20 (passer à Npgsql 8 pourrait casser cette bibliothèque).
- `MessagePack` reste en 2.5.x (SignalR 8 est compilé contre l'API 2.x ; la 3.x est une rupture).

## Comment ça reste vrai

| Mécanisme | Rôle |
|---|---|
| Étape CI « Vulnerable dependencies » | `dotnet list package --vulnerable --include-transitive` ; **la CI échoue** si un paquet vulnérable apparaît (nouvel avis publié, nouvelle dépendance) |
| `.github/dependabot.yml` | propose chaque semaine des *pull requests* de mise à jour NuGet et GitHub Actions |

Quand la CI échoue pour cette raison : lire le journal (paquet, version, lien vers l'avis), puis soit mettre à jour le paquet
parent, soit ajouter/relever la référence directe corrigée, et relancer.

## Limites

- Le contrôle ne connaît que les avis publiés dans la base NuGet/GitHub : un avis sortant demain fera échouer la CI demain,
  sans changement de code (c'est voulu, mais prévoyez le temps de réaction).
- Les images Docker de base (`mcr.microsoft.com/dotnet/aspnet` et `sdk`) ne sont pas scannées (piste : Trivy).
- Le client de test SignalR est en 8.0.32 alors que les paquets serveur sont en 8.0.25 : l'écart est sans effet (le serveur
  utilise le runtime ASP.NET installé), mais à aligner lors d'une prochaine montée de version.
