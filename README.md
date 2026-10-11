# PasteFlow

Outil Windows de remplissage de formulaires sans souris : on se place dans un formulaire, on appuie sur **Ctrl+Alt+V**, et l'outil remplit les champs avec les informations affichées à l'écran.

> « PasteFlow » est un nom de travail, il pourra changer.

## Auteur

- Antoine Barthélémy

## État d'avancement

Prototype complet (étapes 1 à 12) : l'application tourne en arrière-plan (icône près de l'horloge). Ctrl+Alt+V lit les champs du formulaire de la fenêtre active et le texte des fenêtres visibles (active et 3 juste derrière). Il y reconnaît les valeurs utiles (e-mails, téléphones, dates, montants, IBAN, références, noms, villes, pays, entreprises), puis remplit les champs dont la valeur est sûre, au format attendu par le champ. Les champs incertains sont encadrés en orange, avec des propositions. Les champs remplis d'un formulaire ouvert derrière sont repris sous la même étiquette (copie de formulaire à formulaire). Les champs déjà remplis sont laissés intacts, sauf si l'on appuie une 2e fois sur Ctrl+Alt+V dans les 8 secondes. Les mots de passe ne sont jamais touchés, et Ctrl+Alt+Z annule le remplissage.

Feuille de route :

1. [x] Squelette : icône, raccourci global, notification
2. [x] Lire le formulaire de la fenêtre active (UI Automation)
3. [x] Écrire une valeur dans un champ
4. [x] Lire le texte des fenêtres visibles et des fenêtres juste derrière
5. [x] Découper le texte en unités de sens : e-mails, téléphones, dates, montants, IBAN, références, avec leur libellé (règles)
6. [x] Associer champs et candidats, remplir le formulaire
7. [x] Interface : annulation globale, surlignage des champs à vérifier, récapitulatif, propositions au focus d'un champ à vérifier
8. [x] Reconnaître noms, prénoms, villes, pays et entreprises grâce aux listes officielles (INSEE, La Poste), vérifier code postal et ville
9. [x] Historique chiffré (DPAPI) et apprentissage : une valeur utilisée deux fois dans un même type de champ est reprise quand l'écran ne dit rien
10. [x] Finitions : valeurs écrites au format du champ (dates, téléphones, IBAN, montants), paramètres (prudence, apprentissage, démarrage avec Windows)
11. [x] OCR de secours : une fenêtre qui ne donne pas son texte (image, PDF scanné, bureau à distance) est lue à l'image, ses valeurs sont seulement proposées
12. [x] Copie de formulaire à formulaire (champs remplis des fenêtres derrière, avec leur étiquette) et 2e appui qui remplace les valeurs déjà présentes
13. [ ] Plus tard : extension Chrome pour lire les autres onglets, envoi optionnel vers l'historique Win+V, choix des raccourcis

Les informations à coller viennent de **sources** interchangeables (interface commune posée à l'étape 4). Sources prévues :

| Source | Quand |
|---|---|
| Texte affiché à l'écran | Étape 4 |
| Profil de l'utilisateur (identité, adresse, liens, études) | Après l'étape 6 |
| Historique des remplissages (chiffré, effaçable depuis le menu de l'icône) | Étape 9 |
| Gestionnaire de mots de passe (Bitwarden, via son CLI) | Après la V1, avec le volet confidentialité |

L'auto-remplissage de Chrome n'est pas une source envisagée : ses données sont chiffrées pour qu'aucun logiciel extérieur ne puisse les lire.

### Données de référence

L'outil reconnaît les noms, villes et pays grâce à des listes officielles, intégrées à l'application (270 Ko compressés) :

| Liste | Source | Licence |
|---|---|---|
| Prénoms donnés au moins 500 fois depuis 1900 | [Fichier des prénoms, INSEE](https://www.insee.fr/fr/statistiques/8595130) | Licence Ouverte 2.0 |
| Codes postaux et communes | [Base officielle des codes postaux, La Poste](https://www.data.gouv.fr/fr/datasets/base-officielle-des-codes-postaux/) | Licence Ouverte 2.0 |
| Pays en français et en anglais | [umpirsky/country-list](https://github.com/umpirsky/country-list) | MIT |

Pour les mettre à jour (une fois par an) : lancer le workflow « Reference data » dans l'onglet Actions de GitHub. Il pousse les nouvelles listes sur la branche `reference-data`, à relire puis à reporter dans `main`.

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
| Ctrl+Alt+V (2e appui en moins de 8 s) | Remplace aussi les champs déjà remplis, quand la valeur trouvée est sûre |
| Ctrl+Alt+Z | Annule le dernier remplissage : vide les champs remplis par l'outil et remet les valeurs remplacées, sauf les champs modifiés depuis |
| Ctrl+Maj+1, 2, 3 | Dans un champ encadré en orange, choisit une des valeurs proposées (actif seulement quand la bulle est affichée) |

Clic droit sur l'icône près de l'horloge : « Paramètres… » (prudence, apprentissage, démarrage avec Windows), « Effacer l'historique », « Quitter ».

## Structure

| Dossier | Rôle |
|---|---|
| `src/PasteFlow.Core` | Logique métier, sans dépendance à Windows, entièrement testable |
| `src/PasteFlow.App` | Application Windows : icône, raccourci global (API Win32), notifications |
| `tests/PasteFlow.Core.Tests` | Tests unitaires (xUnit) de la logique métier |
| `tools/PasteFlow.DataBuilder` | Outil qui construit les listes de référence à partir des données officielles |
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
