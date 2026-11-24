## Quoi et pourquoi

<!-- Decris le changement en une ou deux phrases, et le contexte (issue, ticket, discussion). -->

## Verification

- [ ] Capture ou video du changement (obligatoire pour tout changement visible dans le jeu)
- [ ] Teste en lancant la scene concernee dans l'editeur Unity
- [ ] Aucune erreur ni nouvel avertissement dans la console Unity

## Fichiers Unity

- [ ] Aucune scene (`.unity`) modifiee, **ou** les modifications sont voulues et decrites ci-dessus
- [ ] Aucun prefab modifie par accident (ouvert puis enregistre sans changement)
- [ ] `ProjectSettings/` non touche, **ou** modifie volontairement (precise lesquels)
- [ ] `Packages/manifest.json` non touche, **ou** nouveau package justifie (pas de version `-pre` ou `-exp` sans accord)
- [ ] Les fichiers `.meta` des assets ajoutes ou deplaces sont dans la PR

## Qualite

- [ ] La PR est en brouillon (Draft) tant qu'elle n'est pas prete a etre relue
- [ ] Le titre de la PR suit le format `type: description` (`feat`, `fix`, `chore`, `docs`, `refactor`, `style`, `perf`, `ci`, `build`, `revert`)
- [ ] Les verifications automatiques de la CI sont vertes
- [ ] Pas de `TODO` sans issue (`TODO(#12)`)
