# Contribuer à Taintless Glade

## Mise en place (une fois par poste)

1. Installe **Unity 6000.5.9f1** (la version exacte, via Unity Hub), **Git**, **Git LFS**, le **SDK .NET** et **Python 3**.
2. Clone le dépôt, puis lance dans un terminal Git Bash :
   ```
   bash scripts/setup-dev.sh
   ```
   Le script active les hooks de commit, Git LFS et l'outil de fusion Unity (`UnityYAMLMerge`) pour les scènes et les prefabs.
3. Ouvre le projet avec Unity Hub.

## Branches et pull requests

- Ne travaille jamais directement sur `main` : crée une branche `type/sujet-court` (ex. `feat/hud-coeurs`, `fix/dash-collision`).
- Ouvre ta PR en **brouillon (Draft)** tant qu'elle n'est pas prête : la CI tourne, mais aucun relecteur n'est sollicité. Clique sur « Ready for review » quand c'est bon.
- Le **titre de la PR** suit le format `type: description` ou `type(scope): description`. Types : `feat`, `fix`, `chore`, `docs`, `refactor`, `style`, `perf`, `ci`, `build`, `revert`.
- Garde les PR **petites** : au-delà de 500 lignes de code modifiées, une alerte et le label `size/large` apparaissent.
- La fusion se fait en **squash** : le titre de la PR devient le message du commit sur `main`.
- Remplis la checklist du modèle de PR, et joins une capture ou une vidéo pour tout changement visible.

## Relecture

- Les relecteurs sont demandés automatiquement selon les dossiers modifiés (`.github/CODEOWNERS`).
- Un commentaire « Résumé automatique » est posté sur chaque PR : catégories de fichiers, poids ajouté, alertes.
- Des labels sont ajoutés automatiquement : `code`, `tests`, `scene`, `art`, `third-party`, `settings`, `ci`, ainsi que `enhancement` (`feat:`) et `bug` (`fix:`).
- Le label **`needs-lead-review`** signale que `ProjectSettings/`, `Packages/manifest.json` ou `packages-lock.json` sont modifiés. Vérifie que ce n'est pas un effet de bord d'Unity, et précise la raison dans la PR.
- Le label **`unity-version`** signale un changement de `ProjectVersion.txt` : toute l'équipe doit installer la nouvelle version avant la fusion.
- Le label **`scene-conflict`** signale qu'une autre PR ouverte modifie la même scène ou le même prefab : les deux auteurs sont prévenus par commentaire.
- Une PR sans relecture depuis 48 h reçoit un rappel. Une PR inactive est marquée `stale` après 14 jours, puis fermée 7 jours plus tard. Le label `keep-open` l'en protège.
- L'auteur résout les conversations avant la fusion.

## Unity en équipe

- **N'enregistre pas une scène ou un prefab sans raison.** Unity modifie facilement `ProjectSettings/`, les scènes et les assets de données à l'ouverture. Avant chaque commit, regarde `git status` et restaure ce qui est involontaire : `git restore <fichier>`.
- **Scènes et prefabs** : une seule personne à la fois sur une même scène. Vérifie l'alerte de conflit sur ta PR et préfère découper en prefabs.
- **Commite toujours les `.meta`** des assets ajoutés, déplacés ou supprimés (la CI vérifie les `.meta` manquants et les GUID dupliqués).
- **Fichiers binaires sources** (`.psd`, `.ai`, `.kra`, `.ase`, `.aseprite`, `.fbx`, `.blend`, `.ma`, `.mb`, `.max`, `.wav`) : ils ne se fusionnent pas. Verrouille-les avant de les modifier.
  ```
  git lfs lock chemin/du/fichier.psd
  git lfs locks
  git lfs unlock chemin/du/fichier.psd
  ```
  Ces fichiers restent en lecture seule tant que tu ne les as pas verrouillés.
- **Version d'Unity** : elle ne change que dans une PR dédiée, annoncée à l'équipe.
- **Packages** : pas de version `-pre`, `-exp` ou `-preview` sans l'ajouter à `.github/config/prerelease-packages.txt` dans la même PR.
- **Packs tiers** (`Assets/` hors `_Project`) : ne les modifie pas.

## Conventions de code (C#)

- Le code du jeu est dans `Assets/_Project/Scripts/Runtime` (assembly `TaintlessGlade.Runtime`, namespace `TaintlessGlade`). Les outils d'éditeur sont dans `Scripts/Editor`.
- Types, méthodes, propriétés et constantes en `PascalCase`.
- Champs publics sérialisés par Unity en `camelCase`, champs privés en `_camelCase`.
- Groupe les champs de l'inspecteur avec `[Header("…")]`.
- Commentaires XML `/// <summary>` en français pour les classes et les membres publics.
- Accolades sur la ligne suivante, indentation de 4 espaces (voir `.editorconfig`).
- Interdits dans le code du jeu : `Debug.Log` (`LogWarning` et `LogError` sont permis), `FindObjectOfType` et `FindObjectsOfType` (utilise `FindFirstObjectByType` et `FindObjectsByType`), les blocs vides, les `TODO` sans issue (écris `TODO(#12)`).
- Un fichier de code de jeu dépasse 400 lignes : avertissement. Au-dessus de 600 : erreur, découpe-le.

## Vérifications automatiques

Chaque PR et chaque push sur `main` lancent la CI :

| Contrôle | Ce qu'il détecte |
| --- | --- |
| Project checks | `.meta` manquants ou orphelins, GUID dupliqués, références cassées, scripts manquants, paquets expérimentaux, textures de plus de 2048 px, fichiers de plus de 25 Mo, règles de code |
| Code format | Formatage C# (`dotnet format whitespace`) |
| Secret scan | Mots de passe, tokens et clés dans le code ou l'historique |
| Workflow lint | Erreurs dans les workflows GitHub Actions |
| Conventional PR title | Format du titre de la PR |

Les mêmes contrôles tournent chez toi avant chaque commit grâce au hook Git installé par `scripts/setup-dev.sh`. Pour un commit exceptionnel : `git commit --no-verify`.

## Releases

Crée un tag `vX.Y.Z` et pousse-le : une release GitHub est créée avec des notes générées à partir des labels des PR. Un tag contenant un tiret (`v0.2.0-rc1`) est marqué comme pré-release. Le label `ignore-for-release` exclut une PR des notes.

```
git tag v0.2.0
git push origin v0.2.0
```

## Notifications

Pour recevoir des messages dans Discord ou Slack, ajoute le secret `NOTIFY_WEBHOOK_URL` (Settings → Secrets and variables → Actions) avec l'URL d'un webhook. Sans ce secret, les notifications sont simplement ignorées.

Un message est envoyé quand :

- une PR est ouverte (pas en brouillon) ou passe de brouillon à « prête pour relecture » ;
- une PR est fusionnée ;
- la CI de `main` échoue ;
- une release est publiée (tag `v*`).

## Issues

Utilise les formulaires **Bug** et **Fonctionnalité** : ils demandent la version d'Unity, la scène concernée et les étapes pour reproduire.
