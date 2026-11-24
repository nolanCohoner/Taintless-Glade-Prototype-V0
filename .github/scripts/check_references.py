#!/usr/bin/env python3
import re
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")

SCAN_ROOT = Path("Assets/_Project")
ASSETS_ROOT = Path("Assets")
BASELINE = Path(".github/config/external-guids.txt")
PACKAGE_CACHE = Path("Library/PackageCache")
BUILTIN_PREFIX = "0000000000000000"

GUID_META = re.compile(r"^guid: ([0-9a-f]{32})\s*$", re.M)
GUID_REF = re.compile(r"guid: ([0-9a-f]{32})")
MISSING_SCRIPT = re.compile(r"m_Script: \{fileID: 0\}")
SCENE_LIKE = {".unity", ".prefab"}


def read_guid(meta):
    try:
        match = GUID_META.search(meta.read_text(encoding="utf-8", errors="ignore"))
    except OSError:
        return None
    return match.group(1) if match else None


def collect_guids(root):
    guids = {}
    if not root.exists():
        return guids
    for meta in root.rglob("*.meta"):
        guid = read_guid(meta)
        if guid:
            guids[guid] = str(meta.relative_to(root)).replace("\\", "/")
    return guids


def is_yaml(path):
    try:
        with path.open("rb") as handle:
            return handle.read(5) == b"%YAML"
    except OSError:
        return False


def scan_references():
    refs = {}
    missing_scripts = []
    for path in sorted(SCAN_ROOT.rglob("*")):
        if not path.is_file() or path.suffix == ".meta" or not is_yaml(path):
            continue
        text = path.read_text(encoding="utf-8", errors="ignore")
        for number, line in enumerate(text.splitlines(), 1):
            if path.suffix in SCENE_LIKE and MISSING_SCRIPT.search(line):
                missing_scripts.append((path.as_posix(), number))
            for guid in GUID_REF.findall(line):
                refs.setdefault(guid, (path.as_posix(), number))
    return refs, missing_scripts


def load_baseline():
    if not BASELINE.exists():
        return set()
    found = set()
    for line in BASELINE.read_text(encoding="utf-8").splitlines():
        match = re.match(r"^([0-9a-f]{32})\b", line.strip())
        if match:
            found.add(match.group(1))
    return found


def update_baseline(unresolved):
    cache = collect_guids(PACKAGE_CACHE)
    if not cache:
        print("Library/PackageCache introuvable : ouvre le projet dans Unity avant de mettre a jour la baseline.")
        return 1
    external, broken = {}, {}
    for guid, location in unresolved.items():
        if guid in cache:
            external[guid] = cache[guid]
        else:
            broken[guid] = location
    BASELINE.parent.mkdir(parents=True, exist_ok=True)
    lines = [f"{guid}  # {path}" for guid, path in sorted(external.items(), key=lambda item: item[1])]
    BASELINE.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")
    print(f"Baseline ecrite : {len(external)} GUID de packages Unity.")
    for guid, (file, line) in broken.items():
        print(f"REFERENCE CASSEE (non ajoutee a la baseline) : {guid} dans {file}:{line}")
    return 1 if broken else 0


def main():
    known = collect_guids(ASSETS_ROOT)
    refs, missing_scripts = scan_references()
    baseline = load_baseline()
    unresolved = {
        guid: location
        for guid, location in refs.items()
        if guid not in known and not guid.startswith(BUILTIN_PREFIX)
    }

    if "--update-baseline" in sys.argv:
        return update_baseline(unresolved)

    failed = False
    for path, number in missing_scripts:
        print(f"::error file={path},line={number},title=Script manquant::Un composant n'a plus de script (m_Script: {{fileID: 0}}).")
        failed = True
    for guid, (path, number) in unresolved.items():
        if guid in baseline:
            continue
        print(f"::error file={path},line={number},title=Reference cassee::Le GUID {guid} ne correspond a aucun asset du projet ni a un package connu.")
        failed = True

    if failed:
        print("Echec : references cassees ou scripts manquants.")
        return 1
    print(f"OK : {len(refs)} GUID references, {len(known)} assets connus, aucune reference cassee.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
