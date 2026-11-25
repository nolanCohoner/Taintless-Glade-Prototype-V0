#!/usr/bin/env bash
# Affiche la section du CHANGELOG.md correspondant a un tag (ex. v0.2.0 -> "## [0.2.0]").
# Une pre-release (v0.2.0-rc1) sans section propre retombe sur "## [Unreleased]".
# Une release stable sans section echoue : il faut ecrire le changelog avant de taguer.
set -euo pipefail

tag="${1:?usage: changelog_notes.sh vX.Y.Z}"
version="${tag#v}"
file="CHANGELOG.md"

extract() {
  awk -v header="## [$1]" '
    index($0, header) == 1 { found = 1; next }
    found && /^## \[/ { exit }
    found { print }
  ' "$file"
}

notes="$(extract "$version")"
if [ -z "${notes//[[:space:]]/}" ] && [[ "$version" == *-* ]]; then
  notes="$(extract "Unreleased")"
fi

if [ -z "${notes//[[:space:]]/}" ]; then
  echo "::error::Aucune section \"## [$version]\" (ou contenu vide) dans $file : ecris le changelog avant de creer le tag." >&2
  exit 1
fi

printf '%s\n' "$notes"
