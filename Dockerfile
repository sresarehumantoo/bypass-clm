FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG RID=win-x64
ARG MODES=standard
WORKDIR /src
COPY bypass-clm.sln .
COPY bypass-clm/bypass-clm.csproj bypass-clm/
RUN dotnet restore bypass-clm/bypass-clm.csproj -r $RID
COPY bypass-clm/ bypass-clm/

RUN <<EOF
#!/bin/sh
set -e
mkdir -p /out
for mode in $(echo "$MODES" | tr ',' ' '); do
  case $mode in
    standard)
      dotnet publish bypass-clm/bypass-clm.csproj \
        -c Release -r "$RID" --no-restore -o /tmp/standard
      cp /tmp/standard/bypass-clm.exe /out/ 2>/dev/null \
        || cp /tmp/standard/bypass-clm /out/
      ;;
    slim)
      dotnet publish bypass-clm/bypass-clm.csproj \
        -c Release -r "$RID" --no-restore -o /tmp/slim \
        -p:DefineConstants=SLIM -p:AssemblyName=bypass-clm-slim
      cp /tmp/slim/bypass-clm-slim.exe /out/ 2>/dev/null \
        || cp /tmp/slim/bypass-clm-slim /out/
      ;;
    dll)
      dotnet build bypass-clm/bypass-clm.csproj \
        -c Release -o /tmp/dll \
        -p:DefineConstants=DLL -p:OutputType=Library
      cp /tmp/dll/bypass-clm.dll /out/
      ;;
  esac
done
EOF

FROM scratch
COPY --from=build /out/ /
