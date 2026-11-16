#!/usr/bin/env bash
set -euo pipefail

PROJ=bypass-clm/bypass-clm.csproj
OUTDIR=dist
MODES=standard
RID=win-x64
DOCKER=false

usage() {
    cat <<'USAGE'
Usage: build.sh [OPTIONS]

Options:
  -m, --mode MODES   Comma-separated list: standard,slim,dll,all (default: standard)
  -r, --rid RID      .NET runtime identifier (default: win-x64)
  --docker           Build inside Docker (no local .NET SDK needed)
  -o, --output DIR   Output directory (default: dist)
  -h, --help         Show this help

Modes:
  standard   Self-contained exe; patches CLM + AMSI in memory (default)
  slim       Self-contained exe; no patches, just the runspace escape
  dll        Framework-dependent class library; call loader.MainClass.go()
  all        Build all three

Examples:
  ./build.sh                            # standard exe, local SDK
  ./build.sh --docker                   # standard exe via Docker
  ./build.sh --docker -m all            # all variants via Docker
  ./build.sh -m slim -r win-arm64       # slim ARM64 exe, local SDK
  ./build.sh --docker -m slim,standard  # two variants via Docker
USAGE
}

while [[ $# -gt 0 ]]; do
    case $1 in
        -m|--mode)  MODES=$2;  shift 2 ;;
        -r|--rid)   RID=$2;    shift 2 ;;
        -o|--output) OUTDIR=$2; shift 2 ;;
        --docker)   DOCKER=true; shift ;;
        -h|--help)  usage; exit 0 ;;
        *) echo "error: unknown option: $1" >&2; usage >&2; exit 1 ;;
    esac
done

[[ $MODES == all ]] && MODES=standard,slim,dll

build_local() {
    local mode=$1
    case $mode in
        standard)
            echo "[*] Building standard (patches + runspace)..."
            dotnet publish "$PROJ" -c Release -r "$RID" -o "$OUTDIR"
            ;;
        slim)
            echo "[*] Building slim (runspace only, no patches)..."
            dotnet publish "$PROJ" -c Release -r "$RID" -o "$OUTDIR" \
                -p:DefineConstants=SLIM -p:AssemblyName=bypass-clm-slim
            ;;
        dll)
            echo "[*] Building dll (framework-dependent class library)..."
            dotnet build "$PROJ" -c Release -o "$OUTDIR" \
                -p:DefineConstants=DLL -p:OutputType=Library
            ;;
        *)
            echo "error: unknown mode: $mode" >&2; exit 1
            ;;
    esac
}

if $DOCKER; then
    echo "[*] Building via Docker: modes=$MODES rid=$RID"
    docker build \
        --build-arg "RID=$RID" \
        --build-arg "MODES=$MODES" \
        --output "$OUTDIR" .
else
    IFS=',' read -ra modes <<< "$MODES"
    for mode in "${modes[@]}"; do
        build_local "$mode"
    done
fi

echo ""
echo "Output in $OUTDIR/:"
ls -lh "$OUTDIR"/bypass-clm* 2>/dev/null || echo "  (no matching files)"
