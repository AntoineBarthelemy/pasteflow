# PasteFlow

Outil Windows de remplissage de formulaires sans souris : on se place dans un formulaire, on appuie sur **Ctrl+Alt+V**, et l'outil remplit les champs avec les informations affichées à l'écran.

> « PasteFlow » est un nom de travail, il pourra changer.

## Auteur

- Antoine Barthélémy

## État d'avancement

Étape 1 du prototype : l'application tourne en arrière-plan (icône près de l'horloge) et affiche une notification quand on appuie sur Ctrl+Alt+V, depuis n'importe quelle application.

Feuille de route :

1. [x] Squelette : icône, raccourci global, notification
2. [ ] Lire le formulaire de la fenêtre active (UI Automation)
3. [ ] Écrire une valeur dans un champ
4. [ ] Lire le texte des fenêtres visibles et des fenêtres juste derrière
5. [ ] Découper le texte en candidats (règles)
6. [ ] Associer champs et candidats, remplir le formulaire
7. [ ] Interface : surlignage, récapitulatif, mini fenêtre, annulation globale
8. [ ] Petits modèles d'IA locaux (ONNX Runtime)
9. [ ] Apprentissage des habitudes et historique (SQLite)
10. [ ] Finitions : OCR de secours, adaptation des formats, paramètres

## Installation / Configuration

### Prérequis

- Windows 10 ou 11
- [SDK .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0) (version fixée dans `global.json`)
- Git
- Un IDE : JetBrains Rider ou Visual Studio 2026 (support des solutions `.slnx`)

### Récupérer et lancer le projet

```shell
git clone https://github.com/AntoineBarthelemy/pasteflow.git
cd pasteflow
dotnet build
dotnet run --project src/PasteFlow.App
```

Le premier `dotnet build` restaure les outils .NET locaux et installe automatiquement les hooks Git (Husky.Net). Rien d'autre à configurer.

Pour quitter l'application : clic droit sur l'icône près de l'horloge, puis « Quitter ».

## Structure

| Dossier | Rôle |
|---|---|
| `src/PasteFlow.Core` | Logique métier, sans dépendance à Windows, entièrement testable |
| `src/PasteFlow.App` | Application Windows : icône, raccourci global (API Win32), notifications |
| `tests/PasteFlow.Core.Tests` | Tests unitaires (xUnit) de la logique métier |

Le cœur ne connaît que des interfaces (`IHotkeyListener`, `IUserNotifier`). Les implémentations Windows sont dans `PasteFlow.App`, ce qui permet de tester le cœur avec de faux objets (dossier `Fakes`).

## Qualité de code

### Conventions

- Code, commentaires et messages de commit **en anglais**. Interface utilisateur et `README.md` en français.
- Champs privés en `_camelCase`, constantes en `PascalCase`, une classe par fichier.
- Messages de commit au format [Commits Conventionnels](https://www.conventionalcommits.org/fr/v1.0.0/) : `type(portée): sujet`, sans point final.
  Types autorisés : `build`, `ci`, `chore`, `docs`, `feat`, `fix`, `perf`, `refactor`, `revert`, `style`, `test`. La portée est libre (`core`, `app`, ...).

### Outils

| Outil | Rôle | Configuration |
|---|---|---|
| `dotnet format` | Style et mise en forme du code | `.editorconfig` |
| Analyseurs .NET (mode *recommended*) | Analyse statique : bugs, performances, sécurité | `Directory.Build.props` |
| StyleCop.Analyzers | Règles de style, ordre des membres, documentation XML | `stylecop.json`, `.editorconfig` |
| Compilateur | Nullabilité activée, **tout avertissement fait échouer la compilation** | `Directory.Build.props` |
| xUnit | Tests unitaires | `tests/` |
| Husky.Net | Contrôles automatiques avant chaque commit | `.husky/` |

Les versions des paquets NuGet sont centralisées dans `Directory.Packages.props`.

### Commandes

| Commande | Rôle |
|---|---|
| `dotnet build` | Compile avec tous les analyseurs |
| `dotnet test` | Lance les tests |
| `dotnet format --verify-no-changes` | Vérifie la mise en forme |
| `dotnet format` | Corrige la mise en forme |
| `dotnet husky run --group pre-commit` | Lance tous les contrôles du pré-commit |
| `dotnet husky run --name format-fix` | Corrige la mise en forme via Husky |

### Contrôles automatiques avant commit

À chaque `git commit`, Husky.Net lance :

1. **format-check** : mise en forme des fichiers C# indexés
2. **forbidden-patterns** : refuse les restes de débogage (`Console.WriteLine(`, `Debug.WriteLine(`, `Debugger.Break(`, ...)
3. **build** : compilation avec analyseurs, avertissements bloquants
4. **test** : tests unitaires
5. **commit-message-linter** : format du message de commit

Si un contrôle échoue, le commit est refusé.

## Intégration continue

GitHub Actions (`.github/workflows/ci.yml`), à chaque push sur `main` et à chaque pull request, sur Windows :

- vérification de la mise en forme
- compilation Release avec analyseurs
- tests avec couverture de code, résultats conservés en cas d'échec
- vérification des messages de commit des pull requests

Dependabot propose chaque semaine la mise à jour des paquets NuGet et des actions GitHub.
