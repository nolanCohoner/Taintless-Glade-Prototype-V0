#!/usr/bin/env python3
import re
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")

RUNTIME = Path("Assets/_Project/Scripts/Runtime")
SCRIPTS = Path("Assets/_Project/Scripts")
LINES_WARN = 400
LINES_ERROR = 600

DEBUG_LOG = re.compile(r"\bDebug\.Log\(")
OBSOLETE_FIND = re.compile(r"\bFindObjects?OfType\b")
GAMEOBJECT_FIND = re.compile(r"\bGameObject\.Find\(")
TODO_WITHOUT_ISSUE = re.compile(r"\b(TODO|FIXME|HACK)\b(?!\(#\d+\))")
EMPTY_BLOCK = re.compile(r"(\b(?:if|while|for|foreach)\b.*|\belse\b|\bcatch\b.*)\{\s*\}\s*;?\s*(//.*)?$")


def annotate(level, path, number, title, message):
    print(f"::{level} file={path.as_posix()},line={number},title={title}::{message}")


def check_runtime_file(path):
    failed = False
    for number, line in enumerate(path.read_text(encoding="utf-8", errors="ignore").splitlines(), 1):
        code = line.split("//", 1)[0]
        if DEBUG_LOG.search(code):
            annotate("error", path, number, "Debug.Log", "Pas de Debug.Log dans le code du jeu (Debug.LogWarning / LogError autorises).")
            failed = True
        if OBSOLETE_FIND.search(code):
            annotate("error", path, number, "API obsolete", "FindObjectOfType / FindObjectsOfType sont obsoletes : utilise FindFirstObjectByType / FindObjectsByType.")
            failed = True
        if GAMEOBJECT_FIND.search(code):
            annotate("warning", path, number, "GameObject.Find", "GameObject.Find est lent : a eviter hors initialisation.")
        if EMPTY_BLOCK.search(code):
            annotate("error", path, number, "Bloc vide", "Bloc vide : inverse la condition ou supprime-le.")
            failed = True
    return failed


def check_todos(path):
    failed = False
    for number, line in enumerate(path.read_text(encoding="utf-8", errors="ignore").splitlines(), 1):
        if "//" not in line:
            continue
        comment = line.split("//", 1)[1]
        if TODO_WITHOUT_ISSUE.search(comment):
            annotate("error", path, number, "TODO sans issue", "Un TODO / FIXME / HACK doit referencer une issue : TODO(#12).")
            failed = True
    return failed


def check_length(path):
    count = len(path.read_text(encoding="utf-8", errors="ignore").splitlines())
    if count > LINES_ERROR:
        annotate("error", path, 1, "Fichier trop long", f"{count} lignes (maximum : {LINES_ERROR}). Decoupe le fichier.")
        return True
    if count > LINES_WARN:
        annotate("warning", path, 1, "Fichier long", f"{count} lignes (seuil d'alerte : {LINES_WARN}).")
    return False


def main():
    failed = False
    for path in sorted(RUNTIME.rglob("*.cs")):
        failed = check_runtime_file(path) or failed
        failed = check_length(path) or failed
    for path in sorted(SCRIPTS.rglob("*.cs")):
        failed = check_todos(path) or failed
    if failed:
        return 1
    print("OK : regles de code respectees.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
