#!/usr/bin/env python3
import json
import os
import re
import subprocess
import sys

sys.stdout.reconfigure(encoding="utf-8")

MARKER = "<!-- pr-summary -->"
LARGE_PR_CODE_LINES = 500
SCENE_EXTENSIONS = (".unity", ".prefab")
SENSITIVE_PREFIXES = ("ProjectSettings/", "Packages/manifest.json", "Packages/packages-lock.json")
VERSION_FILE = "ProjectSettings/ProjectVersion.txt"
LFS_SIZE = re.compile(r"^size (\d+)$", re.M)
EDITOR_VERSION = re.compile(r"^m_EditorVersion: (.+)$", re.M)
TITLE_TYPE = re.compile(r"^(\w+)(\([^)]*\))?!?: ")

LABELS = {
    "code": ("1d76db", "Code C# du jeu"),
    "tests": ("0e8a16", "Tests"),
    "scene": ("fbca04", "Scenes et prefabs Unity"),
    "art": ("e99695", "Art, donnees et ressources"),
    "third-party": ("c5def5", "Packs tiers"),
    "settings": ("d93f0b", "Reglages Unity et packages"),
    "ci": ("5319e7", "CI, scripts et outils"),
    "needs-lead-review": ("b60205", "Fichiers sensibles : relecture du responsable requise"),
    "unity-version": ("b60205", "La version de l'editeur Unity change"),
    "scene-conflict": ("d93f0b", "Scene ou prefab aussi modifie par une autre PR ouverte"),
    "size/large": ("fef2c0", "PR volumineuse : a decouper"),
    "enhancement": ("a2eeef", "Nouvelle fonctionnalite"),
    "bug": ("d73a4a", "Correction de bug"),
}
TITLE_LABELS = {"feat": "enhancement", "fix": "bug"}

CATEGORIES = [
    ("Code du jeu", lambda p: p.startswith("Assets/_Project/Scripts/") and p.endswith(".cs")),
    ("Tests", lambda p: p.startswith("Assets/_Project/Tests/")),
    ("Scenes et prefabs", lambda p: p.endswith(SCENE_EXTENSIONS)),
    ("Art et donnees", lambda p: p.startswith("Assets/_Project/")),
    ("Packs tiers", lambda p: p.startswith("Assets/")),
    ("Reglages Unity", lambda p: p.startswith(("ProjectSettings/", "Packages/"))),
    ("CI et outils", lambda p: p.startswith((".github/", ".githooks/", "scripts/")) or p in (".editorconfig", ".gitleaks.toml", ".gitignore", ".gitattributes", "CONTRIBUTING.md")),
]
CATEGORY_LABEL = {
    "Code du jeu": "code",
    "Tests": "tests",
    "Scenes et prefabs": "scene",
    "Art et donnees": "art",
    "Packs tiers": "third-party",
    "Reglages Unity": "settings",
    "CI et outils": "ci",
}


def log(message):
    print(message, file=sys.stderr)


def git(*args):
    return subprocess.run(["git", "-c", "core.quotepath=false", *args], capture_output=True, text=True, check=True).stdout


def gh(*args):
    return subprocess.run(["gh", *args], capture_output=True, text=True)


def category(path):
    for name, test in CATEGORIES:
        if test(path):
            return name
    return "Autres"


def is_sensitive(path):
    return path.startswith(SENSITIVE_PREFIXES) or path.endswith("EditorBuildSettings.asset") or path == VERSION_FILE


def changed_files(base, head):
    counts = {}
    for line in git("diff", "--numstat", "--no-renames", f"{base}...{head}").splitlines():
        added, deleted, path = line.split("\t", 2)
        counts[path] = (None, None) if added == "-" else (int(added), int(deleted))
    files = []
    for line in git("diff", "--name-status", "--no-renames", f"{base}...{head}").splitlines():
        status, path = line.split("\t", 1)
        added, deleted = counts.get(path, (None, None))
        files.append({"status": status, "path": path, "added": added, "deleted": deleted})
    return files


def blob_size(head, path):
    try:
        size = int(git("cat-file", "-s", f"{head}:{path}").strip())
    except subprocess.CalledProcessError:
        return 0
    if size < 512:
        content = git("cat-file", "-p", f"{head}:{path}")
        if content.startswith("version https://git-lfs"):
            match = LFS_SIZE.search(content)
            if match:
                return int(match.group(1))
    return size


def summarize(files):
    stats = {}
    for item in files:
        entry = stats.setdefault(category(item["path"]), {"files": 0, "added": 0, "deleted": 0, "binary": 0})
        entry["files"] += 1
        if item["added"] is None:
            entry["binary"] += 1
        else:
            entry["added"] += item["added"]
            entry["deleted"] += item["deleted"]
    return stats


def code_lines(files):
    total = 0
    for item in files:
        if item["path"].startswith("Assets/_Project/") and item["path"].endswith(".cs") and item["added"] is not None:
            total += item["added"] + item["deleted"]
    return total


def editor_version(text):
    match = EDITOR_VERSION.search(text or "")
    return match.group(1).strip() if match else None


def compute_labels(files, title, is_large, version_changed, has_conflict):
    labels = {CATEGORY_LABEL[category(item["path"])] for item in files if category(item["path"]) in CATEGORY_LABEL}
    if any(is_sensitive(item["path"]) for item in files):
        labels.add("needs-lead-review")
    if version_changed:
        labels.add("unity-version")
    if has_conflict:
        labels.add("scene-conflict")
    if is_large:
        labels.add("size/large")
    match = TITLE_TYPE.match(title or "")
    if match and match.group(1) in TITLE_LABELS:
        labels.add(TITLE_LABELS[match.group(1)])
    return labels


def find_overlaps(my_scenes, open_prs, my_number):
    overlaps = []
    for other in open_prs:
        if other["number"] == my_number:
            continue
        paths = {f["path"] for f in other.get("files", [])}
        shared = sorted(my_scenes & paths)
        if shared:
            author = (other.get("author") or {}).get("login", "")
            overlaps.append({"number": other["number"], "title": other["title"], "author": author, "files": shared})
    return overlaps


def mention(login):
    return f"@{login}" if login and not login.endswith("[bot]") else (login or "?")


def build_comment(files, stats, weight, large_lines, is_large, versions, overlaps, author):
    out = [MARKER, "### Resume automatique", ""]
    if not files:
        out.append("Aucun fichier modifie.")
    else:
        out.append("| Categorie | Fichiers | Lignes ajoutees | Lignes supprimees | Binaires |")
        out.append("| --- | ---: | ---: | ---: | ---: |")
        for name, entry in sorted(stats.items(), key=lambda item: -item[1]["files"]):
            out.append(f"| {name} | {entry['files']} | +{entry['added']} | -{entry['deleted']} | {entry['binary']} |")
        out.append("")
        out.append(f"Poids des fichiers ajoutes ou modifies (hors .meta) : **{weight / (1024 * 1024):.1f} Mo**")

    alerts = []
    sensitive = [item["path"] for item in files if is_sensitive(item["path"])]
    if sensitive:
        listing = "\n".join(f"  - `{path}`" for path in sensitive)
        alerts.append(f"**Fichiers sensibles modifies** (label `needs-lead-review`) : verifie que ces changements sont voulus et pas un effet de bord d'Unity.\n{listing}")
    if versions:
        old, new = versions
        alerts.append(f"**Version de l'editeur Unity** : `{old or '?'}` vers `{new or '?'}`. Toute l'equipe doit installer cette version et reouvrir le projet avant de fusionner.")
    for overlap in overlaps:
        shared = ", ".join(f"`{p}`" for p in overlap["files"])
        alerts.append(f"**Conflit probable de scene ou prefab** avec #{overlap['number']} ({overlap['title']}) de {mention(overlap['author'])} : {shared}. Coordonnez-vous pour fusionner l'une apres l'autre.")
    if is_large:
        alerts.append(f"**PR volumineuse** : {large_lines} lignes de code modifiees (seuil : {LARGE_PR_CODE_LINES}). Pense a la decouper pour faciliter la relecture.")

    if alerts:
        out += ["", "#### Alertes", ""] + [f"- {alert}" for alert in alerts]
    return "\n".join(out)


def open_prs(dry_run):
    if dry_run:
        return json.loads(os.environ.get("DRY_RUN_PRS", "[]"))
    result = gh("pr", "list", "--state", "open", "--limit", "100", "--json", "number,title,author,isDraft,files")
    if result.returncode != 0:
        log(f"::warning::Impossible de lister les PR ouvertes : {result.stderr.strip()}")
        return []
    return json.loads(result.stdout)


def apply_labels(number, desired, dry_run):
    if dry_run:
        log(f"[dry-run] labels souhaites : {sorted(desired)}")
        return
    for name in desired:
        color, description = LABELS[name]
        gh("label", "create", name, "--color", color, "--description", description, "--force")
    current_result = gh("pr", "view", str(number), "--json", "labels")
    if current_result.returncode != 0:
        log(f"::warning::Lecture des labels impossible : {current_result.stderr.strip()}")
        return
    current = {item["name"] for item in json.loads(current_result.stdout)["labels"]}
    to_add = sorted(desired - current)
    to_remove = sorted((current & set(LABELS)) - desired)
    args = ["pr", "edit", str(number)]
    if to_add:
        args += ["--add-label", ",".join(to_add)]
    if to_remove:
        args += ["--remove-label", ",".join(to_remove)]
    if to_add or to_remove:
        result = gh(*args)
        if result.returncode != 0:
            log(f"::warning::Mise a jour des labels impossible : {result.stderr.strip()}")


def notify_other(overlap, my_number, my_title, my_author, dry_run):
    marker = f"<!-- scene-conflict:{my_number} -->"
    body = (
        f"{marker}\n**Conflit probable de scene ou prefab** : la PR #{my_number} ({my_title}) de {mention(my_author)} "
        f"modifie aussi {', '.join('`' + p + '`' for p in overlap['files'])}. Coordonnez-vous pour fusionner l'une apres l'autre."
    )
    if dry_run:
        log(f"[dry-run] commentaire sur #{overlap['number']} :\n{body}")
        return
    existing = gh("pr", "view", str(overlap["number"]), "--json", "comments")
    if existing.returncode == 0 and marker in existing.stdout:
        return
    gh("pr", "comment", str(overlap["number"]), "--body", body)
    gh("label", "create", "scene-conflict", "--color", LABELS["scene-conflict"][0], "--description", LABELS["scene-conflict"][1], "--force")
    gh("pr", "edit", str(overlap["number"]), "--add-label", "scene-conflict")


def main():
    dry_run = os.environ.get("DRY_RUN") == "1"
    base = os.environ["BASE_SHA"]
    head = os.environ["HEAD_SHA"]
    number = int(os.environ.get("PR_NUMBER", "0"))
    title = os.environ.get("PR_TITLE", "")
    author = os.environ.get("PR_AUTHOR", "")

    files = changed_files(base, head)
    stats = summarize(files)
    weight = sum(blob_size(head, item["path"]) for item in files if item["status"] in ("A", "M") and not item["path"].endswith(".meta"))

    large_lines = code_lines(files)
    is_large = large_lines > LARGE_PR_CODE_LINES

    versions = None
    if any(item["path"] == VERSION_FILE for item in files):
        merge_base = git("merge-base", base, head).strip()
        try:
            old = editor_version(git("show", f"{merge_base}:{VERSION_FILE}"))
        except subprocess.CalledProcessError:
            old = None
        new = editor_version(git("show", f"{head}:{VERSION_FILE}"))
        if old != new:
            versions = (old, new)

    my_scenes = {item["path"] for item in files if item["path"].endswith(SCENE_EXTENSIONS)}
    overlaps = find_overlaps(my_scenes, open_prs(dry_run), number) if my_scenes else []

    labels = compute_labels(files, title, is_large, versions is not None, bool(overlaps))
    print(build_comment(files, stats, weight, large_lines, is_large, versions, overlaps, author))

    if number:
        apply_labels(number, labels, dry_run)
        for overlap in overlaps:
            notify_other(overlap, number, title, author, dry_run)


if __name__ == "__main__":
    main()
