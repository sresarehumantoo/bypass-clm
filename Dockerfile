FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG RID=win-x64
WORKDIR /src
COPY bypass-clm.sln .
COPY bypass-clm/bypass-clm.csproj bypass-clm/
RUN dotnet restore bypass-clm/bypass-clm.csproj -r $RID
COPY bypass-clm/ bypass-clm/
RUN dotnet publish bypass-clm/bypass-clm.csproj -c Release -r $RID --no-restore -o /out

FROM scratch
COPY --from=build /out/bypass-clm.exe /
