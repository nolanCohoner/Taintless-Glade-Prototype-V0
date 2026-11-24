#!/usr/bin/env bash
set -euo pipefail

cd "$(git rev-parse --show-toplevel)"

dotnet format whitespace . --folder \
  --include Assets/_Project/Scripts/ Assets/_Project/Tests/ \
  --verify-no-changes
