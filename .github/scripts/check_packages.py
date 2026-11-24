#!/usr/bin/env python3
import json
import re
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")

MANIFEST = Path("Packages/manifest.json")
ALLOWLIST = Path(".github/config/prerelease-packages.txt")
PRERELEASE = re.compile(r"-(pre|exp|preview|alpha|beta|rc)", re.IGNORECASE)


def load_allowlist():
    if not ALLOWLIST.exists():
        return set()
    entries = set()
    for line in ALLOWLIST.read_text(encoding="utf-8").splitlines():
        entry = line.split("#", 1)[0].strip()
        if entry:
            entries.add(entry)
    return entries


def main():
    dependencies = json.loads(MANIFEST.read_text(encoding="utf-8")).get("dependencies", {})
    allowed = load_allowlist()
    failed = False
    for name, version in sorted(dependencies.items()):
        if PRERELEASE.search(version) and f"{name}@{version}" not in allowed:
            print(
                f"::error file={MANIFEST.as_posix()},title=Package pre-release::"
                f"{name}@{version} est une version experimentale. "
                f"Si c'est voulu, ajoute '{name}@{version}' dans {ALLOWLIST.as_posix()}."
            )
            failed = True
    if failed:
        return 1
    print(f"OK : {len(dependencies)} packages, aucune version experimentale non validee.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
