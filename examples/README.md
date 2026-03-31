[![Documentation](https://img.shields.io/badge/docs-GitHub_Pages-blue.svg)](https://fa-yoshinobu.github.io/plc-comm-computerlink-dotnet/)

Use this page as the user-facing guide to the high-level .NET examples.

## Start Here

If you want the shortest path, start with one of these:

- `examples/PlcComm.Toyopuc.MinimalRead`
  Smallest read-only example. Reads CPU status, clock, and one device.
- `examples/PlcComm.Toyopuc.HighLevelSample`
  High-level cookbook that demonstrates single reads, writes, typed helpers, snapshots, contiguous block reads, FR access, and polling.
- `examples/PlcComm.Toyopuc.SoakMonitor`
  Long-duration polling with reconnect and CSV logging.

When `--profile` is supplied, basic families `P/K/V/T/C/L/X/Y/M/S/N/R/D` should use `P1-*`, `P2-*`, or `P3-*`.

The newer explicit APIs such as `ToyopucDeviceClientFactory.OpenAndConnectAsync`,
`ReadWordsSingleRequestAsync`, and `ReadWordsChunkedAsync` use the same device
syntax shown in these examples.

## Quick Commands

Minimal read:

```powershell
dotnet run --project examples\PlcComm.Toyopuc.MinimalRead -- 192.168.250.100 1025 tcp P1-D0000 "TOYOPUC-Plus:Plus Extended mode"
dotnet run --project examples\PlcComm.Toyopuc.MinimalRead -- 192.168.250.100 1027 udp P1-D0000 "TOYOPUC-Plus:Plus Extended mode"
```

High-level cookbook:

```powershell
dotnet run --project examples\PlcComm.Toyopuc.HighLevelSample -- 192.168.250.100 1025 tcp "TOYOPUC-Plus:Plus Extended mode"
dotnet run --project examples\PlcComm.Toyopuc.HighLevelSample -- 192.168.250.100 1025 tcp "PC10G:PC10 mode"
```

Dedicated soak monitor:

```powershell
dotnet run --project examples\PlcComm.Toyopuc.SoakMonitor -- --host 192.168.250.100 --port 1025 --protocol tcp --profile "Nano 10GX:Compatible mode" --devices P1-D0000,P1-M0000,U08000 --interval 2s --duration 30m --retries 3 --log logs\soak.log --poll-csv logs\soak.csv --summary-json logs\soak_summary.json
```

## Choose an Example by Task

- Read one device and confirm that communication works
  - `examples/PlcComm.Toyopuc.MinimalRead`
- Learn the main high-level APIs
  - `examples/PlcComm.Toyopuc.HighLevelSample`
- Observe reconnect behavior and log a watch list
  - `examples/PlcComm.Toyopuc.SoakMonitor`

## Simulator

Use the sibling Python repository in this workspace as the simulator source:

```powershell
cd D:\PLC_COMM_PROJ\plc-comm-computerlink-python
python scripts\sim_server.py --host 127.0.0.1 --port 15000
```

Then run the .NET smoke test from this repository:

```powershell
cd D:\PLC_COMM_PROJ\plc-comm-computerlink-dotnet
dotnet run --project examples\PlcComm.Toyopuc.SmokeTest -- --host 127.0.0.1 --port 15000 --protocol tcp
```

Write/readback simulator check:

```powershell
dotnet run --project examples\PlcComm.Toyopuc.SmokeTest -- --host 127.0.0.1 --port 15000 --protocol tcp --device P1-D0000 --write-value 0x1234
```

## Engineering Utilities

These projects are useful for validation and investigation, but they are not the main user tutorial:

- `examples/PlcComm.Toyopuc.SmokeTest`
- `examples/PlcComm.Toyopuc.WriteLimitProbe`
- `examples/PlcComm.Toyopuc.BitPatternProbe`

Write-limit probe:

```powershell
powershell -ExecutionPolicy Bypass -File examples\probe_direct_length_limits.ps1
```

Bit-pattern probe:

```powershell
dotnet run --project examples\PlcComm.Toyopuc.BitPatternProbe -- --profile "PC10G:PC10 mode" --csv logs\bit_pattern_pc10g_direct.csv --summary-json logs\bit_pattern_pc10g_direct.json
```

Profile-driven validation:

```powershell
powershell -ExecutionPolicy Bypass -File examples\run_validation.ps1 -Target plus
powershell -ExecutionPolicy Bypass -File examples\run_validation.ps1 -Target relay-10gx
```

Relay FR write + commit with restore:

```powershell
dotnet run --project examples\PlcComm.Toyopuc.SmokeTest -- --host 192.168.250.100 --port 1025 --protocol tcp --profile "Nano 10GX:Compatible mode" --hops "P1-L2:N4,P1-L2:N6,P1-L2:N2" --fr-device FR000000 --fr-write-value 0x55AB --fr-commit --restore-after-write --verbose --log logs\relay_fr_commit_restore.log
```

## Publish

High-level sample:

```powershell
dotnet publish examples\PlcComm.Toyopuc.HighLevelSample -c Release -p:PublishProfile=win-x64-single-file -o artifacts\publish\PlcComm.Toyopuc.HighLevelSample
```

Soak monitor:

```powershell
dotnet publish examples\PlcComm.Toyopuc.SoakMonitor -c Release -p:PublishProfile=win-x64-single-file -o artifacts\publish\PlcComm.Toyopuc.SoakMonitor
```
