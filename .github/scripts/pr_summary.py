#!/usr/bin/env python3
import os
import re
import subprocess
import sys

sys.stdout.reconfigure(encoding="utf-8")

BASE = os.environ["BASE_SHA"]
HEAD = os.environ["HEAD_SHA"]

CATEGORIES = [
    ("Code du jeu", lambda p: p.startswith("Assets/_Project/Scripts/") and p.endswith(".cs")),
    ("Tests", lambda p: p.startswith("Assets/_Project/Tests/")),
    ("Scenes et prefabs", lambda p: p.endswith((".unity", ".prefab"))),
    ("Art et donnees", lambda p: p.startswith("Assets/_Project/")),
    ("Packs tiers", lambda p: p.startswith("Assets/")),
    ("Reglages Unity", lambda p: p.startswith(("ProjectSettings/", "Packages/"))),
    ("CI et outils", lambda p: p.startswith((".github/", ".githooks/")) or p in (".editorconfig", ".gitleaks.toml", ".gitignore", ".gitattributes")),
]
SENSITIVE_PREFIXES = ("ProjectSettings/", "Packages/manifest.json", "Packages/packages-lock.json")
LFS_SIZE = re.compile(r"^size (\d+)$", re.M)


def git(*args):
    return subprocess.run(["git", "-c", "core.quotepath=false", *args], capture_output=True, text=True, check=True).stdout


def category(path):
    for name, test in CATEGORIES:
        if test(path):
            return name
    return "Autres"


def blob_size(path):
    try:
        size = int(git("cat-file", "-s", f"{HEAD}:{path}").strip())
    except subprocess.CalledProcessError:
        return 0
    if size < 512:
        content = git("cat-file", "-p", f"{HEAD}:{path}")
        if content.startswith("version https://git-lfs"):
            match = LFS_SIZE.search(content)
            if match:
                return int(match.group(1))
    return size


def main():
    stats = {}
    for line in git("diff", "--numstat", "--no-renames", f"{BASE}...{HEAD}").splitlines():
        added, deleted, path = line.split("\t", 2)
        entry = stats.setdefault(category(path), {"files": 0, "added": 0, "deleted": 0, "binary": 0})
        entry["files"] += 1
        if added == "-":
            entry["binary"] += 1
        else:
            entry["added"] += int(added)
            entry["deleted"] += int(deleted)

    changed = []
    weight = 0
    for line in git("diff", "--name-status", "--no-renames", f"{BASE}...{HEAD}").splitlines():
        status, path = line.split("\t", 1)
        changed.append(path)
        if status in ("A", "M") and not path.endswith(".meta"):
            weight += blob_size(path)

    sensitive = [p for p in changed if p.startswith(SENSITIVE_PREFIXES) or p.endswith("EditorBuildSettings.asset")]

    print("### Resume automatique")
    print()
    if not changed:
        print("Aucun fichier modifie.")
    else:
        print("| Categorie | Fichiers | Lignes ajoutees | Lignes supprimees | Binaires |")
        print("| --- | ---: | ---: | ---: | ---: |")
        for name, entry in sorted(stats.items(), key=lambda item: -item[1]["files"]):
            print(f"| {name} | {entry['files']} | +{entry['added']} | -{entry['deleted']} | {entry['binary']} |")
        print()
        print(f"Poids des fichiers ajoutes ou modifies (hors .meta) : **{weight / (1024 * 1024):.1f} Mo**")
        total_lines = sum(e["added"] + e["deleted"] for e in stats.values())
        if total_lines > 500:
            print()
            print(f"Cette PR modifie {total_lines} lignes : pense a la decouper pour faciliter la relecture.")
    if sensitive:
        print()
        print("**Fichiers sensibles modifies** (relecture du responsable du projet conseillee) :")
        for path in sensitive:
            print(f"- `{path}`")
    print()
    print(f"_Mis a jour automatiquement pour le commit `{HEAD[:7]}`._")


if __name__ == "__main__":
    main()
