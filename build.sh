#!/usr/bin/env bash
set -euo pipefail

PROJ=bypass-clm/bypass-clm.csproj
RID="${1:-win-x64}"
OUT="dist/${RID}"

dotnet publish "$PROJ" -c Release -r "$RID" -o "$OUT"

exe="${OUT}/bypass-clm"
[ "$RID" != "${RID#win}" ] && exe="${exe}.exe"

echo ""
echo "Built: ${exe}"
ls -lh "$exe"
