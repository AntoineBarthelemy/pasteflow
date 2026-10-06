# PasteFlow

Outil Windows de remplissage de formulaires sans souris : on se place dans un formulaire, on appuie sur **Ctrl+Alt+V**, et l'outil remplit les champs avec les informations affichées à l'écran.

> « PasteFlow » est un nom de travail, il pourra changer.

## Auteur

- Antoine Barthélémy

## État d'avancement

Étapes 1 à 5 du prototype : l'application tourne en arrière-plan (icône près de l'horloge). Ctrl+Alt+V lit les champs du formulaire de la fenêtre active, lit le texte des fenêtres visibles (active et 3 juste derrière) en y reconnaissant les valeurs utiles, puis remplit les champs texte vides (pour l'instant avec le nom de chaque champ), sans jamais toucher aux champs déjà remplis ni aux mots de passe.

Feuille de route :

1. [x] Squelette : icône, raccourci global, notification
2. [x] Lire le formulaire de la fenêtre active (UI Automation)
3. [x] Écrire une valeur dans un champ
4. [x] Lire le texte des fenêtres visibles et des fenêtres juste derrière
5. [x] Découper le texte en unités de sens : e-mails, téléphones, dates, montants, IBAN, références, avec leur libellé (règles)
6. [ ] Associer champs et candidats, remplir le formulaire
7. [ ] Interface : annulation globale (fait), surlignage des champs à vérifier (fait), récapitulatif, mini fenêtre
8. [ ] Petits modèles d'IA locaux (ONNX Runtime)
9. [ ] Apprentissage des habitudes et historique (SQLite)
10. [ ] Finitions : OCR de secours, adaptation des formats, paramètres

Les informations à coller viennent de **sources** interchangeables (interface commune posée à l'étape 4). Sources prévues :

| Source | Quand |
|---|---|
| Texte affiché à l'écran | Étape 4 |
| Profil de l'utilisateur (identité, adresse, liens, études) | Après l'étape 6 |
| Historique des remplissages | Étape 9 |
| Gestionnaire de mots de passe (Bitwarden, via son CLI) | Après la V1, avec le volet confidentialité |

L'auto-remplissage de Chrome n'est pas une source envisagée : ses données sont chiffrées pour qu'aucun logiciel extérieur ne puisse les lire.

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

Raccourcis :

| Raccourci | Action |
|---|---|
| Ctrl+Alt+V | Remplit le formulaire de la fenêtre active |
| Ctrl+Alt+Z | Annule le dernier remplissage : vide les champs remplis par l'outil, sauf ceux modifiés depuis |

Pour quitter l'application : clic droit sur l'icône près de l'horloge, puis « Quitter ».

## Structure

| Dossier | Rôle |
|---|---|
| `src/PasteFlow.Core` | Logique métier, sans dépendance à Windows, entièrement testable |
| `src/PasteFlow.App` | Application Windows : icône, raccourci global (API Win32), notifications |
| `tests/PasteFlow.Core.Tests` | Tests unitaires (xUnit) de la logique métier |
| `tests/PasteFlow.App.Tests` | Tests d'intégration Windows : un vrai formulaire de test est affiché, puis lu et rempli via UI Automation |

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
| xUnit | Tests unitaires et d'intégration Windows | `tests/` |
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
- tests unitaires et d'intégration Windows avec couverture de code, nombre de tests affiché, résultats conservés en cas d'échec
- vérification des messages de commit des pull requests

Dependabot propose chaque semaine la mise à jour des paquets NuGet et des actions GitHub.
