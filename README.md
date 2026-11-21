# bypass-clm

Bypass PowerShell Constrained Language Mode. Two variants: a modern .NET 10
self-contained executable that patches the lockdown policy in memory, and a
.NET Framework 4.x version that compiles on-target with csc.exe (no SDK
install needed).

Modernized fork of [calebstewart/bypass-clm](https://github.com/calebstewart/bypass-clm).

## Building

`build.sh` builds both the modern (.NET 10) and framework variants, locally
or via Docker.

```
./build.sh                              # standard exe, local .NET SDK
./build.sh --docker                     # standard exe via Docker (no SDK needed)
./build.sh --docker -m all              # all four variants via Docker
./build.sh --docker -m lite,standard    # pick specific variants
./build.sh -m lite -r win-arm64         # lite ARM64, local SDK
./build.sh -h                           # full usage
```

**Modes:**

| Mode | Output | Size | Description |
|---|---|---|---|
| `standard` | `ConfigHost.exe` | 123 MB | Self-contained exe; patches CLM + AMSI + ETW in memory |
| `slim` | `ConfigHost-slim.exe` | 123 MB | Self-contained exe; no patches, just the runspace escape |
| `lite` | `ConfigHost-lite.exe` + `.dll` + `.deps.json` + `.runtimeconfig.json` | 230 KB | Framework-dependent exe; patches, requires .NET 10 + PS 7.6 on target |
| `dll` | `ConfigHost.dll` | 8 KB | Framework-dependent class library; call `[ConfigHost.Program]::Run()` from a .NET 10+ host |
| `all` | all four | | Builds standard + slim + lite + dll |

**standard** and **slim** are self-contained: drop and run, no runtime needed
on the target. `slim` omits the memory patches (fewer signaturable strings)
and relies on being in an AppLocker-allowed path instead.

**lite** has the same patches as `standard` but is framework-dependent: the
target needs .NET 10 and PowerShell 7.6+ installed. The tradeoff is size
(230 KB vs 123 MB), which matters for exfil-constrained or slow transfers.
Copy all four output files together.

**dll** is a class library for injection into an existing .NET 10+ process
(e.g. `Add-Type -Path ConfigHost.dll; [ConfigHost.Program]::Run()` from a
PowerShell 7.6+ session that already has Full Language).

## Variants

### Modern (.NET 10)

Patches `GetSystemLockdownPolicy`, `AmsiScanBuffer`, and `EtwEventWrite` in
memory, opens a Full Language Mode interactive PowerShell 7.6 session, then
restores the original bytes so the patches are not visible if memory is
inspected afterward. Also disables ScriptBlock logging. Works from any path
(no AppLocker bypass needed). Self-contained single-file publish.

All sensitive strings (DLL names, export names, type names) and patch byte
arrays are XOR-encoded at compile time and decoded at runtime.

**How it works:**

1. Patches `EtwEventWrite` in ntdll with `xor eax,eax; ret` to suppress ETW
   tracing.
2. Reflectively loads `SystemPolicy` from `System.Management.Automation`,
   JIT-compiles `GetSystemLockdownPolicy`, then overwrites it with
   `sub eax,eax; ret` via `VirtualProtect` + `Marshal.Copy`, forcing
   `SystemEnforcementMode.None`.
3. Patches `AmsiScanBuffer` to write `AMSI_RESULT_CLEAN` to the result
   pointer and return `S_OK`.
4. Opens a PowerShell runspace via `InitialSessionState.CreateDefault2()`.
5. Restores the original bytes of all three patched functions.
6. Disables ScriptBlock logging by zeroing `_enableScriptBlockLogging` (or
   `enableScriptBlockLogging` on builds where the underscore-prefixed field
   does not exist, such as Win11 25H2 PS 5.1.26100) and setting
   `scriptBlockLoggingExplicitlyDisabled` to true.

**Tested on:** Windows Server 2025 (10.0.26100) with .NET 10.0.15 runtime.

![demo](docs/demo.gif)

**Pinned versions:**

| Component | Version |
|---|---|
| Target framework | net10.0 |
| Microsoft.PowerShell.SDK | 7.6.10 |
| Tested .NET runtime | 10.0.15 |
| Tested OS | Windows Server 2025 (10.0.26100.32230) |

### Framework (.NET Framework 4.x)

Compiles on-target with `csc.exe` from the .NET Framework already on Windows
(no SDK install, no internet access). Creates a Windows PowerShell 5.1
runspace via the GAC copy of `System.Management.Automation.dll`. Does not
patch the lockdown policy; instead, the exe must run from an AppLocker-allowed
path (e.g. `C:\Windows\Tasks`).

Three build variants from a single source file via preprocessor directives:

| Output | Define | Description |
|---|---|---|
| `ConfigHost.exe` | `FULL` | Full WinPS 5.1 runspace with core modules pre-loaded |
| `ConfigHost-slim.exe` | (none) | Minimal runspace, smaller footprint, fewer signaturable strings |
| `ConfigHost.dll` | `FULL,DLL` | Class library; load via reflection from an allowed path |

**Tested on:** Windows Server 2025 Core with AppLocker default rules enforced.

**Building** (on target, in PowerShell):

```powershell
cd C:\work
.\build.ps1
```

**Usage:**

```
# EXE: copy to a writable subdirectory under an allowed path
copy ConfigHost.exe C:\Windows\Tasks\
C:\Windows\Tasks\ConfigHost.exe

# DLL: load via reflection from an allowed path
copy ConfigHost.dll C:\Windows\Tasks\
powershell -c "[Reflection.Assembly]::LoadFrom('C:\Windows\Tasks\ConfigHost.dll'); [ConfigHost.Session]::Run()"
```

The writable AppLocker bypass paths on a default deployment:
`C:\Windows\Tasks`, `C:\Windows\Tracing`,
`C:\Windows\System32\spool\drivers\color`.

## Usage (modern variant)

Copy `ConfigHost.exe` to the target and run it. You get a `PS>` prompt in Full
Language Mode with AMSI, ETW, and ScriptBlock logging disabled.

```
C:\work> ConfigHost.exe
PS> $ExecutionContext.SessionState.LanguageMode
FullLanguage
PS> exit
```

## Changes from upstream

- Ported from .NET Framework 4.7.2 to .NET 10 (self-contained single-file publish).
- Replaced deprecated `ConsoleShell.Start` / `RunspaceConfiguration` with
  `InitialSessionState.CreateDefault2()` and the modern PowerShell hosting API.
- Replaced P/Invoke `DllImport` with `NativeLibrary.Load`/`GetExport`.
- XOR-encoded all sensitive strings and patch byte arrays.
- Added `EtwEventWrite` patching and ScriptBlock logging disable.
- Patches are restored after the runspace opens (clean memory on inspection).
- Changed AMSI patch from returning `E_INVALIDARG` to writing
  `AMSI_RESULT_CLEAN` and returning `S_OK`.
- Renamed output binaries to `ConfigHost` to avoid obvious naming.
- Added `build.sh` with `--docker` and `-m standard,slim,lite,dll,all` modes.
- Added `framework/` variant: compiles with csc.exe on-target, three build
  outputs (full exe, slim exe, reflection-loaded DLL), WinPS 5.1 runspace.

## Credits

Original project by [Caleb Stewart](https://github.com/calebstewart).
AMSI bypass technique from [rasta-mouse](https://github.com/rasta-mouse/AmsiScanBufferBypass).
