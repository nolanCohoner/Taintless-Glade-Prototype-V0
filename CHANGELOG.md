# Changelog

Toutes les modifications notables de Taintless Glade sont listées ici.
Le format suit [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/) et les versions suivent [SemVer](https://semver.org/lang/fr/).

Catégories : **Added** (nouveautés), **Changed** (modifications), **Fixed** (corrections), **Deprecated**, **Removed**, **Security**.

Avant de créer un tag `vX.Y.Z`, déplace le contenu de `[Unreleased]` dans une nouvelle section `## [X.Y.Z] - AAAA-MM-JJ`.
Le workflow de release publie cette section dans les notes de la release GitHub.

## [Unreleased]

Première version jouable du prototype (candidate : `v0.1.0-rc1`).

### Added
- Salle de test jouable : Axolotl (déplacement, dash, crachat, tir chargé), mannequin d'entraînement, HUD de vie.
- Menu de démarrage (scène `MainMenu`) : décor 3D, feuillage 2D généré par script, animations nerveuses et particules.
- Sélection du personnage en roulette 3D, avec stats, atout et arme. Seul l'Axolotl est jouable pour l'instant ; les cinq autres personnages apparaissent en silhouette « Bientôt disponible ».
- Choix de la salle (grille 3 x 2 avec pages). Seule la salle de test existe.
- Paramètres : contrôles réaffectables (clavier, souris, manette), affichage, audio, jeu, avec enregistrement et réinitialisation.
- Curseurs de souris Kenney selon le contexte (menu, survol, viseur en jeu, chargement).
- Intégration continue : contrôles du projet Unity, format du code, détection de secrets, notifications.
- Tests Unity EditMode et PlayMode, exécutés sur un runner auto-hébergé après chaque push sur `dev` et `main`.
- Build Windows 64 bits (`Tools > Taintless Glade > Build > Windows 64 bits`, ou en CI) et release GitHub avec l'archive `TaintlessGlade-windows-x64.zip`.

### Changed
- Police globale : Fredoka remplace Blackadder.
- Le mode Play démarre toujours sur le menu de démarrage.
- Le HUD de jeu est construit à chaque chargement de salle, plus dans le menu.
- Deux branches principales : `dev` (instable, où l'on fusionne tout) et `main` (stable, validée en préproduction).

### Fixed
- Le test d'intégrité des stats accepte les personnages à 0 PV protégés par autre chose (le Poisson et son bocal).
