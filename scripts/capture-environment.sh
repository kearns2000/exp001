#!/usr/bin/env bash
set -euo pipefail
OUT="${1:-environment.txt}"
{
  echo "captured_utc=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo
  echo "## dotnet"
  dotnet --info || true
  echo
  echo "## git"
  git --version || true
  echo
  echo "## codeql"
  codeql version || true
  echo
  echo "## os"
  uname -a || true
} > "$OUT"
echo "Wrote $OUT"
