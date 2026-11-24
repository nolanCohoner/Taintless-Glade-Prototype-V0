#!/usr/bin/env python3
import re
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")

ROOT = Path("Assets/_Project")
MAX_TEXTURE_SIZE = 2048
FILE_WARN_BYTES = 10 * 1024 * 1024
FILE_ERROR_BYTES = 25 * 1024 * 1024

TEXTURE_SIZE = re.compile(r"maxTextureSize: (\d+)")
LFS_SIZE = re.compile(rb"^size (\d+)$", re.M)


def real_size(path):
    size = path.stat().st_size
    if size < 512:
        head = path.read_bytes()
        if head.startswith(b"version https://git-lfs"):
            match = LFS_SIZE.search(head)
            if match:
                return int(match.group(1))
    return size


def check_textures():
    failed = False
    for meta in sorted(ROOT.rglob("*.meta")):
        text = meta.read_text(encoding="utf-8", errors="ignore")
        if "TextureImporter:" not in text:
            continue
        for match in TEXTURE_SIZE.finditer(text):
            size = int(match.group(1))
            if size > MAX_TEXTURE_SIZE:
                print(
                    f"::error file={meta.as_posix()},title=Texture trop grande::"
                    f"maxTextureSize = {size} (maximum autorise : {MAX_TEXTURE_SIZE})."
                )
                failed = True
    return failed


def check_file_sizes():
    failed = False
    for path in sorted(ROOT.rglob("*")):
        if not path.is_file() or path.suffix == ".meta":
            continue
        size = real_size(path)
        megabytes = size / (1024 * 1024)
        if size > FILE_ERROR_BYTES:
            print(f"::error file={path.as_posix()},title=Fichier trop lourd::{megabytes:.1f} Mo (maximum : {FILE_ERROR_BYTES // (1024 * 1024)} Mo).")
            failed = True
        elif size > FILE_WARN_BYTES:
            print(f"::warning file={path.as_posix()},title=Fichier lourd::{megabytes:.1f} Mo (seuil d'alerte : {FILE_WARN_BYTES // (1024 * 1024)} Mo).")
    return failed


def main():
    failed = check_textures()
    failed = check_file_sizes() or failed
    if failed:
        return 1
    print("OK : budgets d'assets respectes.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
