#!/usr/bin/env bash
set -euo pipefail

SCHEMA_PATH=${1:-proto/tienlen.proto}
OUT_DIR=${2:-Client/Assets/Scripts/Proto}

if [[ ! -f "$SCHEMA_PATH" ]]; then
  echo "Schema not found at $SCHEMA_PATH" >&2
  exit 1
fi

mkdir -p "$OUT_DIR"
protoc --csharp_out="$OUT_DIR" "$SCHEMA_PATH"
echo "C# stubs generated to $OUT_DIR"
