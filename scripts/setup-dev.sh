#!/usr/bin/env bash
set -euo pipefail

cd "$(git rev-parse --show-toplevel)"

git config core.hooksPath .githooks
echo "OK  hooks Git : .githooks"

if git lfs version >/dev/null 2>&1; then
  git lfs install --local >/dev/null
  echo "OK  Git LFS active"
else
  echo "!!  Git LFS est introuvable : installe-le depuis https://git-lfs.com puis relance ce script"
fi

version=$(sed -n 's/^m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt | tr -d '\r')
echo "    Version Unity du projet : $version"

candidates=()
if [ -n "${UNITY_EDITOR_PATH:-}" ]; then
  candidates+=("$UNITY_EDITOR_PATH/Data/Tools/UnityYAMLMerge.exe" "$UNITY_EDITOR_PATH/Data/Tools/UnityYAMLMerge")
fi
candidates+=(
  "/c/Program Files/Unity/Hub/Editor/$version/Editor/Data/Tools/UnityYAMLMerge.exe"
  "/Applications/Unity/Hub/Editor/$version/Unity.app/Contents/Tools/UnityYAMLMerge"
  "$HOME/Unity/Hub/Editor/$version/Editor/Data/Tools/UnityYAMLMerge"
)

merge_tool=""
for candidate in "${candidates[@]}"; do
  if [ -x "$candidate" ]; then
    merge_tool="$candidate"
    break
  fi
done

if [ -n "$merge_tool" ]; then
  git config merge.unityyamlmerge.name "Unity SmartMerge (UnityYAMLMerge)"
  git config merge.unityyamlmerge.driver "\"$merge_tool\" merge -h -p --force %O %B %A %P"
  git config merge.unityyamlmerge.recursive binary
  echo "OK  fusion des scenes et prefabs : $merge_tool"
else
  echo "!!  UnityYAMLMerge introuvable pour Unity $version."
  echo "    Installe cette version avec Unity Hub, ou definis UNITY_EDITOR_PATH (dossier 'Editor' de ton installation) puis relance ce script."
fi

echo "Termine. Detail des regles de l'equipe : CONTRIBUTING.md"
