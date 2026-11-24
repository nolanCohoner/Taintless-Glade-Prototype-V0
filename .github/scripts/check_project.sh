#!/usr/bin/env bash
set -u

fail=0
error() {
  echo "::error::$1"
  fail=1
}

echo "== .meta files =="
while IFS= read -r -d '' path; do
  case "$path" in
    *.meta) [ -e "${path%.meta}" ] || error "Orphan .meta (asset deleted): $path" ;;
    *) [ -e "$path.meta" ] || error "Missing .meta for: $path" ;;
  esac
done < <(find Assets -mindepth 1 \( -name '.*' -o -name '*~' \) -prune -o -print0)

echo "== Duplicate GUIDs =="
duplicates=$(find Assets -name '*.meta' -print0 | xargs -0 grep -h '^guid:' | sort | uniq -d)
if [ -n "$duplicates" ]; then
  error "Duplicate GUIDs found: $duplicates"
fi

echo "== Merge conflict markers =="
if git grep -nE '^(<<<<<<<|>>>>>>>) ' -- '*.cs' '*.unity' '*.prefab' '*.asset' '*.mat' '*.meta' '*.asmdef' '*.yaml' '*.yml'; then
  error "Merge conflict markers found"
fi

echo "== Files that should never be committed =="
if git ls-files | grep -E '^(Library|Temp|obj|Logs|UserSettings|Builds?)/'; then
  error "Generated Unity folders are tracked by git"
fi

echo "== Large files outside Git LFS =="
large=$(git ls-tree -r -l HEAD | awk '$4 ~ /^[0-9]+$/ && $4 > 52428800 {print $5 " (" $4 " bytes)"}')
if [ -n "$large" ]; then
  error "Files above 50 MB must use Git LFS: $large"
fi

echo "== Git LFS pointers =="
if git lfs ls-files >/dev/null 2>&1; then
  git lfs fsck --pointers || error "Invalid Git LFS pointers"
fi

if [ "$fail" -eq 0 ]; then
  echo "All project checks passed"
fi
exit "$fail"
