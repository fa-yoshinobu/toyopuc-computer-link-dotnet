# TOYOPUC Computer Link for .NET

TOYOPUC computer-link communication library for .NET.

This repository is a .NET port of the Python library
[`pytoyopuc-computerlink`](https://github.com/fa-yoshinobu/toyopuc-computer-link-python).
For protocol background, address details, and other fine-grained reference
material, see the Python repository first.

This repository contains a reusable .NET library, a validation CLI, a minimal sample, and recorded hardware test results.

Repository:
[`fa-yoshinobu/toyopuc-computer-link-dotnet`](https://github.com/fa-yoshinobu/toyopuc-computer-link-dotnet)

## Status

- Library name: `Toyopuc`
- NuGet package name: `Toyopuc.Net`
- NuGet publication: planned for the first public release
- License: `MIT`
- Verified direct target: `TOYOPUC-Plus`
- Verified relay target: `Nano 10GX`
- Verified on real hardware:
  - `TOYOPUC-Plus CPU (TCC-6740) + Plus EX2 (TCU-6858)`
  - `Nano 10GX (TUC-1157)`
  - `PC3JX-D (TCC-6902)`
  - `PC10G CPU (TCC-6353)`

## Requirements

- .NET SDK 9.0 to build the solution, examples, and tests
- `net9.0` in the consuming application
- a reachable PLC or the upstream simulator if you want to run the examples

## Quick Navigation

- Beginner (first successful read in about 5 minutes):
  - [5-Minute Setup (Beginner)](#5-minute-setup-beginner)
  - [Start Here](#start-here)
  - [Example Programs](#example-programs)
- Advanced validation and release work:
  - [Hardware Validation Commands (Advanced)](#hardware-validation-commands-advanced)
  - [Validation Runner](#validation-runner)
  - [Verified Hardware Behavior](#verified-hardware-behavior)
  - [RELEASE.md](RELEASE.md)

## 5-Minute Setup (Beginner)

If this is your first time, do only these steps:

1. Build once

```powershell
git clone https://github.com/fa-yoshinobu/toyopuc-computer-link-dotnet
cd toyopuc-computer-link-dotnet
dotnet build Toyopuc.sln
```

2. Run the smallest read sample

```powershell
dotnet run --project examples\Toyopuc.MinimalRead -- 192.168.250.101 1025 tcp P1-D0000 "TOYOPUC-Plus:Plus Extended mode"
```

3. If you need only a quick communication check, stop here.
4. If you need wider checks, continue with `SmokeTest` in [Example Programs](#example-programs).

## Start Here

If you are not sure which profile to use, start with this:

- Direct `TOYOPUC-Plus`: `TOYOPUC-Plus:Plus Extended mode`

Most users can stop there. Profile selection mainly affects `U`, `EB`, `FR`,
some upper `1000`-series areas, and whether basic families must be written as `P1/P2/P3-*`.

If you need relay access, use `Nano 10GX:Compatible mode`. Relay-specific notes
and verification are summarized in the later "Hardware Validation Commands (Advanced)" and
"Verified Hardware Behavior" sections.

## Install

The package is not published on NuGet yet.

Until the first public release, build from source:

```powershell
git clone https://github.com/fa-yoshinobu/toyopuc-computer-link-dotnet
cd toyopuc-computer-link-dotnet
dotnet build Toyopuc.sln
```

After publication, install it with:

```powershell
dotnet add package Toyopuc.Net
```

## Upstream Reference

- Original Python library:
  [`fa-yoshinobu/toyopuc-computer-link-python`](https://github.com/fa-yoshinobu/toyopuc-computer-link-python)
- .NET repository:
  [`fa-yoshinobu/toyopuc-computer-link-dotnet`](https://github.com/fa-yoshinobu/toyopuc-computer-link-dotnet)
- This repository focuses on the .NET port, packaging, validation, and verified
  hardware behavior.
- Detailed protocol notes and fine-grained reference material should be checked
  against the upstream Python documentation and source.

## Features

- TCP and UDP communication
- Low-level client: `ToyopucClient`
- High-level string address API: `ToyopucHighLevelClient`
- Basic, program, extended, PC10, relay, and FR access
- CPU status and clock read
- Windows table-style device monitor sample
- File logging and verbose TX/RX frame dump
- Safe write validation with readback and automatic restore
- Addressing profiles for model-specific behavior
- Library-side machine presets and device-range catalog

## Repository Layout

- [`src/Toyopuc`](src/Toyopuc): library source
- [`examples/Toyopuc.SmokeTest`](examples/Toyopuc.SmokeTest): validation and troubleshooting CLI
- [`examples/Toyopuc.MinimalRead`](examples/Toyopuc.MinimalRead): smallest read-only sample
- [`examples/Toyopuc.DeviceMonitor`](examples/Toyopuc.DeviceMonitor): Windows monitor sample with a 16-point register table, connection settings dialog, separate CPU status / clock windows, and PLC clock set support
- [`screenshot/DeviceMonitor.png`](screenshot/DeviceMonitor.png): DeviceMonitor UI screenshot (reference)
- [`examples/run_validation.ps1`](examples/run_validation.ps1): scripted validation runner
- [`release.bat`](release.bat): local release build, test, and pack helper
- [`CHANGELOG.md`](CHANGELOG.md): public release notes and pending changes
- [`RELEASE.md`](RELEASE.md): release checklist
- [`LICENSE`](LICENSE): repository license
- [`examples/README.md`](examples/README.md): example commands
- [`docs/internal/VALIDATION.md`](docs/internal/VALIDATION.md): validation checklist
- [`docs/internal/LIBRARY_PROFILE_SPEC.md`](docs/internal/LIBRARY_PROFILE_SPEC.md): internal machine preset and address-range specification
- [`docs/internal/PYTHON_PORTING_NOTES.md`](docs/internal/PYTHON_PORTING_NOTES.md): mapping and differences from the original Python implementation
- [`docs/internal/TESTRESULTS.md`](docs/internal/TESTRESULTS.md): recorded hardware results
- [`fa-yoshinobu/toyopuc-computer-link-python`](https://github.com/fa-yoshinobu/toyopuc-computer-link-python): original Python repository and reference materials

## Build From Source

If you are working from this repository rather than consuming a package:

```powershell
dotnet build Toyopuc.sln
```

## Screenshots

DeviceMonitor reference screenshot:

![Toyopuc DeviceMonitor screenshot](screenshot/DeviceMonitor.png)

## Test From Source

```powershell
dotnet test Toyopuc.sln --no-build
```

## Pack

Create the NuGet package:

```powershell
dotnet pack src\Toyopuc\Toyopuc.csproj -c Release
```

Generated package:

- `src\Toyopuc\bin\Release\Toyopuc.Net.1.0.0.nupkg`
- `src\Toyopuc\bin\Release\Toyopuc.Net.1.0.0.snupkg`

Use the batch helper if you also want the release zip under `artifacts\release`:

```bat
release.bat
```

- `artifacts\release\1.0.0\Toyopuc.Net.1.0.0-dll.zip`
- `artifacts\release\1.0.0\Toyopuc.Net.1.0.0.nupkg`
- `artifacts\release\1.0.0\Toyopuc.Net.1.0.0.snupkg`
- `artifacts\release\1.0.0\Toyopuc.DeviceMonitor.exe`

## Quick Start

### Direct Read/Write

```csharp
using Toyopuc;

var profile = ToyopucAddressingOptions.FromProfile("TOYOPUC-Plus:Plus Extended mode");

using var plc = new ToyopucHighLevelClient(
    "192.168.250.101",
    1025,
    protocol: "tcp",
    addressingOptions: profile,
    deviceProfile: "TOYOPUC-Plus:Plus Extended mode");

var word = (int)plc.Read("P1-D0000");
var bit = (bool)plc.Read("P1-M0000");
var status = plc.ReadCpuStatus();
var clock = plc.ReadClock().AsDateTime();

plc.Write("P1-D0000", 0x1234);
```

### FR Read/Write

```csharp
using Toyopuc;

var profile = ToyopucAddressingOptions.FromProfile("Nano 10GX:Compatible mode");

using var plc = new ToyopucHighLevelClient(
    "192.168.250.101",
    1025,
    protocol: "tcp",
    addressingOptions: profile,
    deviceProfile: "Nano 10GX:Compatible mode");

var current = (int)plc.ReadFr("FR000000");
plc.WriteFr("FR000000", 0x55AB, commit: true);
```

For optional relay API usage, see "Optional Relay API Example" near the end of this document.

## Choosing a Profile

For normal use, start with this:

- direct `TOYOPUC-Plus` -> `TOYOPUC-Plus:Plus Extended mode`

Use relay profiles only when you actually need relay routing.
The verified relay path in this repository is:

- relay `Nano 10GX` -> `Nano 10GX:Compatible mode`

Use `Generic` only when you intentionally want the library-side superset or you
are comparing behavior across models.

When a profile is enforced through `deviceProfile` or `--profile`, basic families `P/K/V/T/C/L/X/Y/M/S/N/R/D` are resolved as `P1/P2/P3` program access only.

All available profile names:

- `Generic`
- `TOYOPUC-Plus:Plus Standard mode`
- `TOYOPUC-Plus:Plus Extended mode`
- `Nano 10GX:Nano 10GX mode`
- `Nano 10GX:Compatible mode`
- `PC10G:PC10 standard/PC3JG mode`
- `PC10G:PC10 mode`
- `PC3JX:PC3 separate mode`
- `PC3JX:Plus expansion mode`
- `PC3JG:PC3JG mode`
- `PC3JG:PC3 separate mode`

These names are exposed through:

- `ToyopucDeviceProfiles`
- `ToyopucDeviceCatalog`

For the exact area/range matrix, see:

- [`docs/internal/device_profile_matrix_r2.csv`](docs/internal/device_profile_matrix_r2.csv)
- [`docs/internal/LIBRARY_PROFILE_SPEC.md`](docs/internal/LIBRARY_PROFILE_SPEC.md)

Example:

```csharp
var profile = ToyopucAddressingOptions.FromProfile("TOYOPUC-Plus:Plus Extended mode");
```

## Example Programs

Beginner recommendation:

1. `MinimalRead` (single read)
2. `SmokeTest` (basic read-only check)
3. `DeviceMonitor` (manual interactive check)

### Minimal Read

```powershell
dotnet run --project examples\Toyopuc.MinimalRead -- 192.168.250.101 1025 tcp P1-D0000 "TOYOPUC-Plus:Plus Extended mode"
```

### Smoke Test

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 127.0.0.1 --port 15000 --protocol tcp
```

### Device Monitor

```powershell
dotnet run --project examples\Toyopuc.DeviceMonitor
```

Publish a single-file Windows executable:

```powershell
dotnet publish examples\Toyopuc.DeviceMonitor -c Release -p:PublishProfile=win-x64-single-file -o artifacts\publish\Toyopuc.DeviceMonitor
```

Or use the helper batch:

```bat
device_monitor_release.bat
```

This Windows sample provides:

- connection and profile switching
- relay hop input
- a 16-point register table from the selected start address
- program/device/start selection
- single-point read/write
- CPU clock and status read

### Soak Monitor

```powershell
dotnet run --project examples\Toyopuc.SoakMonitor -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "Nano 10GX:Compatible mode" --hops "P1-L2:N4,P1-L2:N6,P1-L2:N2" --devices P1-D0000,P1-M0000,U08000 --interval 2s --duration 30m --retries 3 --log logs\soak.log --poll-csv logs\soak.csv --summary-json logs\soak_summary.json
```

This dedicated CLI is for long-duration observation, not one-shot validation. It keeps a fixed device list, logs per-poll outcomes, can auto-reconnect after failures, and writes a final JSON summary.

Publish a single-file Windows executable:

```powershell
dotnet publish examples\Toyopuc.SoakMonitor -c Release -p:PublishProfile=win-x64-single-file -o artifacts\publish\Toyopuc.SoakMonitor
```

Or use the helper batch:

```bat
soak_monitor_release.bat
```

For the recommended Nano 10GX relay core device set, use:

```bat
soak_monitor_10gx_core.bat
```

This batch uses:

- profile: `Nano 10GX:Compatible mode`
- hops: `P1-L2:N4,P1-L2:N6,P1-L2:N2`
- devices: `P1-D0000,P1-M0000,U08000,EB00000,FR000000`
- interval: `5s`
- duration: `30m`

Output files:

- `logs\soak_10gx_core.log`
- `logs\soak_10gx_core.csv`
- `logs\soak_10gx_core.json`

Useful variants:

- `soak_monitor_10gx_core.bat --2h`
- `soak_monitor_10gx_core.bat --duration 45m --interval 2s`
- `soak_monitor_10gx_core.bat --skip-build --verbose`

Supported batch options:

- `--skip-build`
- `--duration <time>`
- `--interval <time>`
- `--log-dir <path>`
- `--prefix <name>`
- `--2h`

The CLI also supports direct use when you want a different device set or a different relay path:

```powershell
dotnet run --project examples\Toyopuc.SoakMonitor -- --help
```

### Verbose Smoke Test With Log File

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 127.0.0.1 --port 15000 --protocol tcp --verbose --log logs\smoke.log
```

Most library users can stop at this point. The remaining sections focus on
hardware validation, troubleshooting, and recorded verified behavior.

## Hardware Validation Commands (Advanced)

### Direct TOYOPUC-Plus Read-Only Suite

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "TOYOPUC-Plus:Plus Extended mode" --suite "TOYOPUC-Plus:Plus Extended mode" --verbose --log logs\plus_suite.log
```

Expected result:

- `summary : suite=TOYOPUC-Plus:Plus Extended mode ok=6 skip=3 ng=0`

Expected unsupported addresses on the verified direct path:

- `U08000`
- `EB00000`
- `FR000000`

### Direct TOYOPUC-Plus Safe Write/Restore

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "TOYOPUC-Plus:Plus Extended mode" --device P1-D0000 --write-value 0x1234 --restore-after-write --verbose --log logs\plus_word_restore.log
```

### Relay Nano 10GX Read-Only Suite

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "Nano 10GX:Compatible mode" --hops "P1-L2:N4,P1-L2:N6,P1-L2:N2" --suite "Nano 10GX:Compatible mode" --verbose --log logs\relay_suite_10gx.log
```

Expected result:

- `summary : suite=Nano 10GX:Compatible mode ok=9 skip=0 ng=0`

For an exhaustive read-only sweep generated from the profile catalog:

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "Nano 10GX:Compatible mode" --hops "P1-L2:N4,P1-L2:N6,P1-L2:N2" --suite "full:Nano 10GX:Compatible mode" --verbose --log logs\relay_suite_10gx_full.log
```

### Relay Nano 10GX Safe Write/Restore

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "Nano 10GX:Compatible mode" --hops "P1-L2:N4,P1-L2:N6,P1-L2:N2" --device P1-D0000 --write-value 0x1357 --restore-after-write --verbose --log logs\relay_word_restore.log
```

On the verified Nano 10GX relay target, packed-word access for `ET/EC`, `EX/EY`, and `GX/GY` behaves as shared underlying storage. Treat each pair as aliases and do not expect independent simultaneous values in a single multi-write.

### Relay Nano 10GX FR Write/Commit/Restore

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "Nano 10GX:Compatible mode" --hops "P1-L2:N4,P1-L2:N6,P1-L2:N2" --fr-device FR000000 --fr-write-value 0x55AB --fr-commit --restore-after-write --verbose --log logs\relay_fr_commit_restore.log
```

## Validation Runner

Run the predefined validation sets:

```powershell
powershell -ExecutionPolicy Bypass -File examples\run_validation.ps1 -Target plus
powershell -ExecutionPolicy Bypass -File examples\run_validation.ps1 -Target relay-10gx
```

Show commands without executing:

```powershell
powershell -ExecutionPolicy Bypass -File examples\run_validation.ps1 -Target plus -DryRun
```

## Simulator

The upstream Python repository includes a simulator for local testing:
[`fa-yoshinobu/toyopuc-computer-link-python`](https://github.com/fa-yoshinobu/toyopuc-computer-link-python)

Start it in another terminal:

```powershell
git clone https://github.com/fa-yoshinobu/toyopuc-computer-link-python
cd toyopuc-computer-link-python
python -m tools.sim_server --host 127.0.0.1 --port 15000
```

Then run:

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 127.0.0.1 --port 15000 --protocol tcp
```

## Logging and Trace

`Toyopuc.SmokeTest` supports:

- `--verbose`
  - prints TX/RX frames
  - prints internal multi-frame trace for FR write/commit flows
- `--log <path>`
  - writes the same output to a log file
- `--restore-after-write`
  - reads the original value
  - writes the test value
  - verifies it
  - restores the original value

## Verified Hardware Behavior

From the recorded tests in this repository:

- Direct `TOYOPUC-Plus`
  - `P1-D0000`, `P1-M0000`, `P1-D0000L`, `P1-S0000`, `U0000`, `U07FFF` verified
  - `U08000`, `EB00000`, `FR000000` reported unsupported
- Relay `Nano 10GX`
  - `P1-D0000`, `P1-M0000`, `P1-D0000L`, `P1-S0000`, `U0000`, `U07FFF`, `U08000`, `EB00000`, `FR000000` verified
  - relay write/readback/restore and FR commit/restore verified

See:

- [`docs/internal/PYTHON_PORTING_NOTES.md`](docs/internal/PYTHON_PORTING_NOTES.md)
- [`docs/internal/VALIDATION.md`](docs/internal/VALIDATION.md)
- [`docs/internal/TESTRESULTS.md`](docs/internal/TESTRESULTS.md)

## Optional Relay API Example

```csharp
using Toyopuc;

var profile = ToyopucAddressingOptions.FromProfile("Nano 10GX:Compatible mode");
var hops = "P1-L2:N4,P1-L2:N6,P1-L2:N2";

using var plc = new ToyopucHighLevelClient(
    "192.168.250.101",
    1025,
    protocol: "tcp",
    addressingOptions: profile,
    deviceProfile: "Nano 10GX:Compatible mode");

var value = (int)plc.RelayRead(hops, "U08000");
```

## Notes

- Use `TOYOPUC-Plus:Plus Extended mode` for the verified direct `TOYOPUC-Plus` path.
- Relay is optional in most setups. If needed, use `Nano 10GX:Compatible mode` for the verified relay `Nano 10GX` path.
- `logs`, `bin`, and `obj` are ignored build/runtime artifacts.
