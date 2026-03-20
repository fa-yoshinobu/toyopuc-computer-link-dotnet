
[![Documentation](https://img.shields.io/badge/docs-GitHub_Pages-blue.svg)](https://fa-yoshinobu.github.io/examples/)

Use `examples/Toyopuc.SmokeTest` for .NET validation.
Use `examples/Toyopuc.MinimalRead` as the smallest read-only integration sample.
Use `examples/Toyopuc.WriteLimitProbe` for safe write-limit confirmation with after-failure re-read and chunked restore.
Use `examples/Toyopuc.BitPatternProbe` for sampled 16-bit bit-write vs `W/L/H` readback validation with immediate restore.
Use `examples/Toyopuc.DeviceMonitor` for a Windows table-style monitor UI.
Use `examples/Toyopuc.SoakMonitor` for dedicated long-duration polling and reconnect observation.
Use [VALIDATION.md](d:/Github/toyopucdriver/docsrc/internal/VALIDATION.md) as the test checklist.

When `--profile` is supplied, basic families `P/K/V/T/C/L/X/Y/M/S/N/R/D` must be written as `P1-*`, `P2-*`, or `P3-*`.

Start the simulator from the upstream Python repository:
[`fa-yoshinobu/pytoyopuc-computerlink`](https://github.com/fa-yoshinobu/pytoyopuc-computerlink)

```powershell
git clone https://github.com/fa-yoshinobu/pytoyopuc-computerlink
cd pytoyopuc-computerlink
python -m tools.sim_server --host 127.0.0.1 --port 15000
```

Run the smoke test:

```powershell
cd d:\Github\toyopucdriver
dotnet run --project examples\Toyopuc.SmokeTest -- --host 127.0.0.1 --port 15000 --protocol tcp
```

Run the minimal read example:

```powershell
cd d:\Github\toyopucdriver
dotnet run --project examples\Toyopuc.MinimalRead -- 192.168.250.101 1025 tcp P1-D0000 "TOYOPUC-Plus:Plus Extended mode"
```

Run the direct write-limit probe:

```powershell
cd d:\Github\toyopucdriver
powershell -ExecutionPolicy Bypass -File examples\probe_direct_length_limits.ps1
```

Run the sampled bit-to-packed readback probe:

```powershell
cd d:\Github\toyopucdriver
dotnet run --project examples\Toyopuc.BitPatternProbe -- --profile "PC10G:PC10 mode" --csv logs\bit_pattern_pc10g_direct.csv --summary-json logs\bit_pattern_pc10g_direct.json
```

On the verified direct `PC10G` target, `V` / `EV` include known target-specific packed readback exceptions. The probe records those cases as expected mismatches and still requires restore to succeed.

Run the Windows device monitor:

```powershell
cd d:\Github\toyopucdriver
dotnet run --project examples\Toyopuc.DeviceMonitor
```

Reference screenshot:

![Toyopuc DeviceMonitor screenshot](../screenshot/DeviceMonitor.png)

Publish a single-file Windows executable:

```powershell
cd d:\Github\toyopucdriver
dotnet publish examples\Toyopuc.DeviceMonitor -c Release -p:PublishProfile=win-x64-single-file -o artifacts\publish\Toyopuc.DeviceMonitor
```

Or use:

```bat
cd d:\Github\toyopucdriver
device_monitor_release.bat
```

Run the dedicated soak monitor:

```powershell
cd d:\Github\toyopucdriver
dotnet run --project examples\Toyopuc.SoakMonitor -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "Nano 10GX:Compatible mode" --hops "P1-L2:N4,P1-L2:N6,P1-L2:N2" --devices P1-D0000,P1-M0000,U08000 --interval 2s --duration 30m --retries 3 --log logs\soak.log --poll-csv logs\soak.csv --summary-json logs\soak_summary.json
```

Publish a single-file Windows executable:

```powershell
cd d:\Github\toyopucdriver
dotnet publish examples\Toyopuc.SoakMonitor -c Release -p:PublishProfile=win-x64-single-file -o artifacts\publish\Toyopuc.SoakMonitor
```

Or use:

```bat
cd d:\Github\toyopucdriver
soak_monitor_release.bat
```

For the recommended Nano 10GX relay core watch set, use:

```bat
cd d:\Github\toyopucdriver
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

The monitor UI supports:

- `tcp` and `udp`
- `Generic`, `TOYOPUC-Plus:Plus Standard mode`, `TOYOPUC-Plus:Plus Extended mode`, `Nano 10GX:Nano 10GX mode`, `Nano 10GX:Compatible mode`, `PC10G:PC10 standard/PC3JG mode`, `PC10G:PC10 mode`, `PC3JX:PC3 separate mode`, `PC3JX:Plus expansion mode`, `PC3JG:PC3JG mode`, and `PC3JG:PC3 separate mode` profiles
- connection settings dialog for transport/profile/relay setup
- program/device/start selection
- fixed 16-point table from the selected start address
- table polling with auto refresh
- manual read and write
- separate clock window
- PLC clock set from the clock window
- separate CPU status window with all decoded fields

Write/readback test:

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 127.0.0.1 --port 15000 --protocol tcp --device P1-D0000 --write-value 0x1234
```

Verbose frame dump plus file log:

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 127.0.0.1 --port 15000 --protocol tcp --device P1-D0000 --write-value 0x1234 --verbose --log logs\smoke.log
```

TOYOPUC-Plus over TCP 1025:

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "TOYOPUC-Plus:Plus Extended mode" --device P1-D0000 --write-value 0x1234 --verbose --log logs\plus.log
```

TOYOPUC-Plus read-only suite:

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "TOYOPUC-Plus:Plus Extended mode" --suite "TOYOPUC-Plus:Plus Extended mode" --verbose --log logs\plus_suite.log
```

This suite reads representative addresses and reports `OK`, `SKIP`, or `NG`.
On the current TOYOPUC-Plus path, `U08000`, `EB00000`, and `FR000000` are expected to show `SKIP`.

Nano 10GX read-only suite:

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "Nano 10GX:Compatible mode" --suite "Nano 10GX:Compatible mode" --verbose --log logs\10gx_suite.log
```

Exhaustive Nano 10GX read-only suite generated from the profile catalog:

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "Nano 10GX:Compatible mode" --hops "P1-L2:N4,P1-L2:N6,P1-L2:N2" --suite "full:Nano 10GX:Compatible mode" --verbose --log logs\relay_suite_10gx_full.log
```

On a Nano 10GX target, `U08000`, `EB00000`, and `FR000000` are expected to be readable.

Relay write/readback with restore:

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "Nano 10GX:Compatible mode" --hops "P1-L2:N4,P1-L2:N6,P1-L2:N2" --device P1-D0000 --write-value 0x1357 --restore-after-write --verbose --log logs\relay_write_restore.log
```

On the verified Nano 10GX relay target, packed-word access aliases `ET/EC`, `EX/EY`, and `GX/GY`. Validate those pairs one at a time instead of assigning different values in a single `--devices` write.

Relay FR write + commit with restore:

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "Nano 10GX:Compatible mode" --hops "P1-L2:N4,P1-L2:N6,P1-L2:N2" --fr-device FR000000 --fr-write-value 0x55AB --fr-commit --restore-after-write --verbose --log logs\relay_fr_commit_restore.log
```

FR test:

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 127.0.0.1 --port 15000 --protocol tcp --fr-device FR000000 --fr-write-value 0x1234 --fr-commit
```

Validation runner:

```powershell
powershell -ExecutionPolicy Bypass -File examples\run_validation.ps1 -Target plus
powershell -ExecutionPolicy Bypass -File examples\run_validation.ps1 -Target relay-10gx
```



