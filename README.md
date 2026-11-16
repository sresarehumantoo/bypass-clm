# bypass-clm

Bypass PowerShell Constrained Language Mode. Two variants: a modern .NET 10
self-contained executable that patches the lockdown policy in memory, and a
.NET Framework 4.x version that compiles on-target with csc.exe (no SDK
install needed).

Modernized fork of [calebstewart/bypass-clm](https://github.com/calebstewart/bypass-clm).

## Variants

### Modern (.NET 10)

Patches `GetSystemLockdownPolicy` and `AmsiScanBuffer` in memory, then drops
into a Full Language Mode interactive PowerShell 7.6 session. Works from any
path (no AppLocker bypass needed). Self-contained single-file publish.

**How it works:**

1. Reflectively loads `SystemPolicy` from `System.Management.Automation`.
2. JIT-compiles `GetSystemLockdownPolicy`, then overwrites it with
   `xor rax,rax; ret` via `VirtualProtect` + `Marshal.Copy`, forcing
   `SystemEnforcementMode.None`.
3. Patches `AmsiScanBuffer` (rasta-mouse technique) to return `E_INVALIDARG`.
4. Opens a PowerShell runspace via `InitialSessionState.CreateDefault2()`.

**Tested on:** Windows Server 2025 (10.0.26100) with .NET 10.0.15 runtime.

![demo](docs/demo.gif)

**Pinned versions:**

| Component | Version |
|---|---|
| Target framework | net10.0 |
| Microsoft.PowerShell.SDK | 7.6.10 |
| Tested .NET runtime | 10.0.15 |
| Tested OS | Windows Server 2025 (10.0.26100.32230) |

**Building** (requires .NET SDK 10.0+):

```
# Linux cross-compile
./build.sh            # default: win-x64
./build.sh win-arm64  # ARM64 target

# Windows
dotnet publish bypass-clm/bypass-clm.csproj -c Release -r win-x64 -o dist\win-x64
```

Output is a single self-contained `bypass-clm.exe` in `dist/`.

### Framework (.NET Framework 4.x)

Compiles on-target with `csc.exe` from the .NET Framework already on Windows
(no SDK install, no internet access). Creates a Windows PowerShell 5.1
runspace via the GAC copy of `System.Management.Automation.dll`. Does not
patch the lockdown policy; instead, the exe must run from an AppLocker-allowed
path (e.g. `C:\Windows\Tasks`).

Three build variants from a single source file via preprocessor directives:

| Output | Define | Description |
|---|---|---|
| `bypass-clm.exe` | `FULL` | Full WinPS 5.1 runspace with core modules pre-loaded |
| `bypass-clm-slim.exe` | (none) | Minimal runspace, smaller footprint, fewer signaturable strings |
| `bypass-clm.dll` | `FULL,DLL` | InstallUtil loader for DLL-based AppLocker bypass |

**Tested on:** Windows Server 2025 Core with AppLocker default rules enforced.

**Building** (on target, in PowerShell):

```powershell
cd C:\work   # or wherever you dropped the files
.\build.ps1
```

**Usage:**

```
# EXE: copy to a writable subdirectory under an allowed path
copy bypass-clm.exe C:\Windows\Tasks\
C:\Windows\Tasks\bypass-clm.exe

# DLL: load via InstallUtil from an allowed path
copy bypass-clm.dll C:\Windows\Tasks\
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe /logfile= /LogToConsole=false /U C:\Windows\Tasks\bypass-clm.dll
```

The writable AppLocker bypass paths on a default deployment:
`C:\Windows\Tasks`, `C:\Windows\Tracing`,
`C:\Windows\System32\spool\drivers\color`.

## Usage (modern variant)

Copy `bypass-clm.exe` to the target and run it. You get a `PS>` prompt in Full
Language Mode with AMSI disabled.

```
C:\work> bypass-clm.exe
PS> $ExecutionContext.SessionState.LanguageMode
FullLanguage
PS> exit
```

## Changes from upstream

- Ported from .NET Framework 4.7.2 to .NET 10 (self-contained single-file publish).
- Replaced deprecated `ConsoleShell.Start` / `RunspaceConfiguration` with
  `InitialSessionState.CreateDefault2()` and the modern PowerShell hosting API.
- Removed `System.Configuration.Install` / `InstallUtil` loader class from the
  modern variant (does not exist on modern .NET).
- Patched `AmsiScanBuffer` with correct calling-convention stubs for both
  x64 and x86.
- Added `build.sh` for Linux cross-compilation.
- Added `framework/` variant: compiles with csc.exe on-target, three build
  outputs (full exe, slim exe, InstallUtil DLL), WinPS 5.1 runspace.

## Credits

Original project by [Caleb Stewart](https://github.com/calebstewart).
AMSI bypass technique from [rasta-mouse](https://github.com/rasta-mouse/AmsiScanBufferBypass).
