# Changelog

Toutes les modifications notables de Taintless Glade sont listées ici.
Le format suit [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/) et les versions suivent [SemVer](https://semver.org/lang/fr/).

Catégories : **Added** (nouveautés), **Changed** (modifications), **Fixed** (corrections), **Deprecated**, **Removed**, **Security**.

Avant de créer un tag `vX.Y.Z`, déplace le contenu de `[Unreleased]` dans une nouvelle section `## [X.Y.Z] - AAAA-MM-JJ`.
Le workflow de release publie cette section dans les notes de la release GitHub.

## [Unreleased]

### Added
- Menu de démarrage (scène `MainMenu`) : décor 3D, feuillage 2D généré par script, animations nerveuses et particules.
- Sélection du personnage en roulette 3D, avec stats, atout et arme. Seul l'Axolotl est jouable pour l'instant.
- Choix de la salle (grille 3 x 2 avec pages). Seule la salle de test existe.
- Paramètres : contrôles réaffectables (clavier, souris, manette), affichage, audio, jeu, avec enregistrement et réinitialisation.
- Curseurs de souris Kenney selon le contexte (menu, survol, viseur en jeu, chargement).
- Pipeline CI/CD : tests Unity, build Windows et release GitHub avec archive `TaintlessGlade-windows-x64.zip`.

### Changed
- Police globale : Fredoka remplace Blackadder.
- Le mode Play démarre toujours sur le menu de démarrage.
- Le HUD de jeu est construit à chaque chargement de salle, plus dans le menu.
- Deux branches principales : `dev` (instable) et `main` (stable).

## [0.1.0] - 2026-11-25

### Added
- Salle de test jouable : Axolotl (déplacement, dash, crachat, tir chargé), mannequin d'entraînement, HUD de vie.
- Intégration continue : contrôles du projet Unity, format du code, détection de secrets, notifications.
