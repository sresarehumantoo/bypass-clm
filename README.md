# bypass-clm

Bypass PowerShell Constrained Language Mode on modern .NET. Patches
`GetSystemLockdownPolicy` and `AmsiScanBuffer` in memory, then drops into a
Full Language Mode interactive PowerShell session, all from a single
self-contained executable.

Modernized fork of [calebstewart/bypass-clm](https://github.com/calebstewart/bypass-clm),
ported from .NET Framework 4.x to .NET 10 with the PowerShell 7.6 hosting API.

![demo](docs/demo.gif)

## How it works

1. Reflectively loads the `SystemPolicy` class from `System.Management.Automation`.
2. JIT-compiles `GetSystemLockdownPolicy`, then overwrites it with
   `xor rax,rax; ret` via `VirtualProtect` + `Marshal.Copy`, forcing it to
   return `SystemEnforcementMode.None`.
3. Patches `AmsiScanBuffer` (rasta-mouse technique) to return `E_INVALIDARG`,
   disabling AMSI scanning for the process.
4. Opens a PowerShell runspace via `InitialSessionState.CreateDefault2()` and
   runs an interactive REPL.

No `powershell.exe` process is spawned. The interpreter runs in-process from
the .NET libraries.

## Building

Requires .NET SDK 10.0+.

### Linux (cross-compile)

```
./build.sh            # default: win-x64
./build.sh win-arm64  # ARM64 target
```

### Windows

```
dotnet publish bypass-clm/bypass-clm.csproj -c Release -r win-x64 -o dist\win-x64
```

Output is a single self-contained `bypass-clm.exe` in `dist/`.

## Usage

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
- Removed `System.Configuration.Install` / `InstallUtil` loader class
  (does not exist on modern .NET).
- Patched `AmsiScanBuffer` with correct calling-convention stubs for both
  x64 and x86.
- Added `build.sh` for Linux cross-compilation.

## Credits

Original project by [Caleb Stewart](https://github.com/calebstewart).
AMSI bypass technique from [rasta-mouse](https://github.com/rasta-mouse/AmsiScanBufferBypass).
