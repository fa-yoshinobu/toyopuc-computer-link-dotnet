# Test Results

Last updated: `2026-03-12`

This document records the latest checked results per target. Verification dates are section-specific.

Current profile rule: when `deviceProfile` / `--profile` is enforced, basic families `P/K/V/T/C/L/X/Y/M/S/N/R/D` are treated as prefixed-only. Raw log files are kept local and are not tracked in this repository.
In command examples, `--log` / `--csv` / `--summary-json` paths are illustrative local paths.

## Environment

- Repository: `d:\Github\toyopucdriver`
- .NET library: `Toyopuc`
- Tools:
  - [SmokeTest](d:/Github/toyopucdriver/examples/Toyopuc.SmokeTest/Program.cs)
  - [MinimalRead](d:/Github/toyopucdriver/examples/Toyopuc.MinimalRead/Program.cs)
  - [run_validation.ps1](d:/Github/toyopucdriver/examples/run_validation.ps1)
- Procedure:
  - [VALIDATION.md](d:/Github/toyopucdriver/docs/internal/VALIDATION.md)

## 1. Direct TOYOPUC-Plus

Verified: `2026-03-10`

Connection:

- host: `192.168.250.101`
- port: `1025`
- protocol: `tcp`
- profile: `TOYOPUC-Plus:Plus Extended mode`

### Read-only Suite

Command:

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "TOYOPUC-Plus:Plus Extended mode" --suite "TOYOPUC-Plus:Plus Extended mode" --verbose --log logs\plus_suite.log
```

Result:

- `summary : suite=TOYOPUC-Plus:Plus Extended mode ok=6 skip=3 ng=0`

Verified points:

- `D0000` word read: `OK`
- `M0000` bit read: `OK`
- `D0000L` byte read: `OK`
- `P1-D0000` program word read: `OK`
- `U0000` read: `OK`
- `U07FFF` read: `OK`

Confirmed unsupported:

- `U08000`: `SKIP`
- `EB00000`: `SKIP`
- `FR000000`: `SKIP`

### Direct Write / Restore

Representative results:

- `D0000` word write / verify / restore: `OK`
- `M0000` bit write / verify / restore: `OK`

### Bit-to-packed Readback Probe (supplemental)

Verified: `2026-03-12`

Command:

```powershell
dotnet run --project examples\Toyopuc.BitPatternProbe -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "TOYOPUC-Plus:Plus Extended mode" --retries 3 --retry-delay 0.5 --csv logs\bit_pattern_plus_extended.csv --summary-json logs\bit_pattern_plus_extended.json
```

Observed result:

- strict pass: `375/390`
- strict mismatch: `15/390`
- communication error: `0`
- restore mismatch: `0`

Accepted target-specific packed readback mismatches for this profile:

- `P1-V0000`, `P1-V0010`, `P1-V00D0`, `P1-V00F0`
- `P2-V0000`, `P2-V0010`
- `P3-V0000`, `P3-V0010`
- `P1-X0000`, `P2-X0000`, `P3-X0000`
- `P1-Y0000`, `P2-Y0000`, `P3-Y0000`
- `EV0E20`

Notes:

- For these 15 points, readback differences are treated as target-specific behavior when restore succeeds.
- The above 15 points are registered in `Toyopuc.BitPatternProbe` expected mismatch rules for `TOYOPUC-Plus:Plus Extended mode`.

### Bit-to-packed Readback Probe Retest (`V/X/Y/EV`) + SocketError Follow-up

Verified: `2026-03-12`

Command:

```powershell
dotnet run --project examples\Toyopuc.BitPatternProbe -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "TOYOPUC-Plus:Plus Extended mode" --areas V,X,Y,EV --retries 3 --retry-delay 0.5 --csv logs\bit_pattern_plus_extended_vxyev.csv --summary-json logs\bit_pattern_plus_extended_vxyev.json
```

Observed result:

- summary: `ok=85 expected=14 total=98/100 failed=2`
- communication error: `0`
- restore mismatch: `0`

Failed points:

- `P1-V0000`
  - strict pass (`word/low/high/restore = OK`)
  - current expected-mismatch rule requires mismatch observation; this run observed no mismatch
- `P1-V0030`
  - `word=OK`, `low=NG`, `high=NG`, `restore=OK`
  - not yet registered in expected mismatch rules

SocketError comparison:

- Earlier full-scope run (`logs/bit_pattern_plus_extended_20260312.json`) recorded `failed=144` with many `Socket error` / timeout failures.
- The sampled rerun above with `--retries 3 --retry-delay 0.5` showed no communication failures.
- Long-duration monitoring is still required to close stability (see `Toyopuc.SoakMonitor` item in validation plan).

### Final Regression Rerun (read-only + D/M write-restore)

Verified: `2026-03-12`

Commands:

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "TOYOPUC-Plus:Plus Extended mode" --suite "TOYOPUC-Plus:Plus Extended mode" --verbose --log logs\plus_suite_regression_20260312.log
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "TOYOPUC-Plus:Plus Extended mode" --device P1-D0000 --write-value 0x1234 --restore-after-write --verbose --log logs\plus_word_restore_regression_20260312.log
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "TOYOPUC-Plus:Plus Extended mode" --device P1-M0000 --write-value 1 --restore-after-write --verbose --log logs\plus_bit_restore_regression_20260312.log
```

Observed result:

- read-only suite: `summary : suite=TOYOPUC-Plus:Plus Extended mode ok=6 skip=3 ng=0`
- `P1-D0000` write / verify / restore / recheck: `0x0000 -> 0x1234 -> 0x0000` (`OK`)
- `P1-M0000` write / verify / restore / recheck: `0 -> 1 -> 0` (`OK`)
- all three runs exited with code `0`

## 2. Relay Nano 10GX

Verified: `2026-03-12`

Connection:

- host: `192.168.250.101`
- port: `1025`
- protocol: `tcp`
- profile: `Nano 10GX:Compatible mode`
- hops: `P1-L2:N4,P1-L2:N6,P1-L2:N2`

Canonical commands:

```powershell
powershell -ExecutionPolicy Bypass -File examples\run_validation.ps1 -Target relay-10gx
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "Nano 10GX:Compatible mode" --hops "P1-L2:N4,P1-L2:N6,P1-L2:N2" --suite "full:Nano 10GX:Compatible mode" --verbose --log logs\relay_suite_10gx_full_r2.log
```

### Summary

- `run_validation.ps1 -Target relay-10gx`: `OK`
- Standard suite: `summary : suite=Nano 10GX:Compatible mode ok=9 skip=0 ng=0`
- Full generated suite: `summary : suite=full:Nano 10GX:Compatible mode ok=221 skip=0 ng=0`
- Build + tests stayed green while fixing validation gaps:
  - `dotnet build Toyopuc.sln`: `OK`
  - `dotnet test Toyopuc.sln --no-build`: `OK` (latest local run: `138 passed`)

### Read-only Coverage

The full generated suite covered representative probes for the supported areas of the profile and completed with `221/221 OK`.

Examples confirmed by the full suite:

- direct base areas: `P`, `K`, `V`, `T`, `C`, `L`, `X`, `Y`, `M`, `S`, `N`, `R`
- upper and extended areas: `U`, `EB`, `FR`
- extended packed-word families: `EP`, `EK`, `EV`, `ET`, `EC`, `EL`, `EX`, `EY`, `EM`
- GM families: `GM`, `GX`, `GY`
- additional word areas: `ES`, `EN`, `H`
- prefixed probes: representative `P1-*` areas including `P1-S1000`, `P1-N1000`, `P1-R0000`



### Safe Write / Verify / Restore Coverage

Single-device and grouped write validation passed for the following categories.

- word devices:
  - `D0000`
  - `P1-D0000`, `P1-D2FFF`
  - `P2-D0000`
  - `P3-D0000`
  - `U00000`, `U07FFF`, `U08000`, `U1FFFF`
  - `EB00000`, `EB3FFFF`
  - `FR000000` with commit
- bit devices:
  - `M0000`
  - `P0000`, `P1000`
  - `K0000`
  - `V1000`, `V17FF`
  - `T0000`, `T1000`
  - `C0000`, `C1000`
  - `L0000`, `L1000`
  - `X0000`
  - `Y0000`
  - `M1000`
  - `P1-P0000`, `P1-P1000`, `P1-V1000`, `P1-V17FF`, `P1-L1000`, `P1-X0000`, `P1-Y0000`, `P1-M1000`
  - `P2-P0000`, `P2-P1000`, `P2-K0000`, `P2-V1000`, `P2-V17FF`, `P2-T0000`, `P2-T1000`, `P2-C0000`, `P2-C1000`, `P2-L0000`, `P2-L1000`, `P2-X0000`, `P2-Y0000`, `P2-M0000`, `P2-M1000`
  - `P3-P0000`, `P3-P1000`, `P3-K0000`, `P3-V1000`, `P3-V17FF`, `P3-T0000`, `P3-T1000`, `P3-C0000`, `P3-C1000`, `P3-L0000`, `P3-L1000`, `P3-X0000`, `P3-Y0000`, `P3-M0000`, `P3-M1000`
- byte and packed-word devices:
  - `D0000L`, `D0000H`
  - `M0000W`
  - `EP0000W`, `EK0000W`, `EV0000W`, `EL0000W`, `EM0000W`
  - `ET0000W`, `EC0000W`
  - `EX0000W`, `EY0000W`
  - `GM0000W`, `GX0000W`, `GY0000W`
- grouped word writes:
  - `P1-S0000`, `P1-S1000`, `P1-N0000`, `P1-N1000`, `P1-R0000`
  - `P2-D0000`, `P2-S0000`, `P2-S1000`, `P2-N0000`, `P2-N1000`, `P2-R0000`
  - `P3-D0000`, `P3-S0000`, `P3-S1000`, `P3-N0000`, `P3-N1000`, `P3-R0000`



### Multi-read / Multi-write and Boundary Coverage

Representative multi-device and boundary cases passed.

- contiguous words:
  - `D2FF0 count=0x10`
  - `P1-D2FF0 count=0x10`
  - `U07FF0 count=0x20`
  - `EB07FF0 count=0x20`
- byte sequence:
  - `D00F8L count=0x20`
- packed-word sequence:
  - `M07F0W count=0x10`
- mixed many-device writes:
  - word mix across `D`, `U`, `EB`, `P1-D`
  - mixed bit + word writes
  - boundary mix including `D2FFF`, `U07FFF`, `U08000`, `EB3FFFF`, `P1-D2FFE`



### FR Persistence and Full-range Validation

`FR` support was verified at three levels.

1. Single-device commit / restore:
   - `FR000000` write + commit + verify + restore: `OK`
2. Whole-range destructive write:
   - `FR000000` to `FR1FFFFF`
   - pattern: `ramp16`
   - seed: `0x55AA`
   - result: `64/64` blocks verified
3. Change proof and reset persistence:
   - `before.csv` vs `after.csv`: `2,097,152` changed rows, `0` unchanged rows
   - `after.csv` vs `after_reset.csv`: identical SHA-256
   - reset persistence therefore confirmed for the full `FR` range
4. Operational power-cycle retention:
   - the PLC is routinely checked after power-off as part of normal operation
   - based on those repeated manual checks, `FR` retention across power cycles is considered operationally confirmed for this target



### Negative and Robustness Checks

Expected failures were observed for invalid addresses.

- `U20000` -> `U PC10 range is 0x08000-0x1FFFF`
- `EB40000` -> `EB index out of range (0x00000-0x3FFFF)`
- `P1-D3000` -> `Program word address out of range: D3000`
- `FR200000` -> `FR index out of range (0x000000-0x1FFFFF)`

Short reconnect probes also showed that validation should remain serialized.

- sequential reconnect probes: `OK`
- overlapping attempts produced `Socket error`

### Notes

- `V0000-V00FF` is treated as a system area on this PLC and is excluded from safe write / restore validation.
- `V1000-V17FF` was verified with write / verify / restore for direct access and for `P1`, `P2`, and `P3`.
- On this verified relay target, the packed-word families `ET/EC`, `EX/EY`, and `GX/GY` behave as aliases. Validation does not write different values to both sides of the same alias family at the same time.

## 3. Direct PC10G

Verified: `2026-03-12`

Connection:

- host: `192.168.250.101`
- port: `1025`
- protocol: `tcp`
- profile: `PC10G:PC10 mode`

Canonical command:

```powershell
powershell -ExecutionPolicy Bypass -File examples\run_validation.ps1 -Target pc10g-direct
```

### Summary

- `run_validation.ps1 -Target pc10g-direct`: `OK`
- Read-only suite: `summary : suite=PC10G:PC10 mode ok=212 skip=0 ng=0`
- `device_profile_matrix_r2.csv` correction for `M` was confirmed in code and on hardware
  - `P1-M1000`, `P1-M17FF`, `P1-M100W`
  - `P2-M1000`, `P3-M1000`
- Direct sequential read count limit was measured on hardware
  - `P1-D0000`: `636` words `OK`, `637` words `NG`
  - `U00000`: `636` words `OK`, `637` words `NG`
  - `U08000`: `636` words `OK`, `637` words `NG`
  - `EB00000`: `636` words `OK`, `637` words `NG`
  - failure mode at `637`: `rc=0x10 / error_code=0x41 (Word/byte count is out of range)`
- Direct sequential write / verify / restore count limit was measured on hardware
  - `P1-D0000`: `621` words `OK`, `622` words `NG`
  - `U00000`: `621` words `OK`, `622` words `NG`
  - `U08000`: `621` words `OK`, `622` words `NG`
  - `EB00000`: `621` words `OK`, `622` words `NG`
  - failure mode at the first failing count: connection drop / `Socket error`
  - safe rerun on `2026-03-12` re-read the PLC after each failing write count and observed `0` changed words for all four targets
- Bit-to-packed readback probe across sampled starts
  - command: `dotnet run --project examples\Toyopuc.BitPatternProbe -- --profile "PC10G:PC10 mode"`
  - sampled scope: `P/K/V/T/C/L/X/Y/M` for `P1/P2/P3`, plus `EP/EK/EV/ET/EC/EL/EX/EY/EM/GM/GX/GY`
  - result: `385` strict `OK`, `5` expected mismatches, `0` unexpected failures
  - logs: `logs\bit_pattern_pc10g_direct_20260312_r2.csv`, `logs\bit_pattern_pc10g_direct_20260312_r2.json`

### Safe Write / Verify / Restore Coverage

- word devices:
  - `P1-D0000`
  - `B0000`
  - `P1-S1000`, `P1-N1000`, `P1-R0000`
  - `U00000`, `U07FFF`, `U08000`, `U1FFFF`
  - `EB00000`, `EB3FFFF`
  - `FR000000` with commit
- bit devices:
  - `P1-M0000`, `P1-M1000`
  - `P1-P1000`, `P1-V1000`, `P1-T1000`, `P1-C1000`, `P1-L1000`
  - `P2-V1000`, `P3-V1000`
  - `P2-M1000`, `P3-M1000`
- byte and packed-word devices:
  - `P1-D0000L`, `P1-D0000H`
  - `P1-M000W`
  - `EP0000W`, `ET0000W`, `EX0000W`, `GM0000W`

### Multi-read / Multi-write and Boundary Coverage

- contiguous words:
  - `P1-D2FF0 count=0x10`
- byte sequence:
  - `P1-D00F8L count=0x20`
- packed-word sequence:
  - `P1-M07FW count=0x10`
  - `EP0FF0W count=0x10`
  - `GM0000W count=0x10`
- mixed many-device writes:
  - `P1-D0000`, `U08000`, `EB00000`, `P1-D0002`
  - `P1-D2FFF`, `U07FFF`, `U08000`, `EB3FFFF`, `P1-D2FFE`
  - `P1-S1000`, `P2-S1000`, `P3-S1000`, `P1-N1000`

Additional boundary note:

- `GMFFF0W count=0x10` returned `error_code=0x40 (Address or address+count is out of range)`

Bit-to-packed readback spec note on this direct `PC10G` target:

- `V` / `EV` include known target-specific packed readback exceptions and are not treated as library defects
- observed expected mismatches:
  - `P1-V0000`
  - `P1-V00F0`
  - `P2-V0000`
  - `P3-V0000`
  - `EV0E20`
- for those cases, bit writes and restore completed successfully, but `WORD/L/H` did not form a strict identity round-trip
- rerun log: `logs\bit_pattern_pc10g_direct_v_ev_rerun_20260312.json`

Additional EP / GM / FR boundary checks on the direct `PC10G` target showed the following.

- `EP` packed-word:
  - `EP0FFFW`: `OK`
  - `EP1000W`: profile range error
  - `EP0FF0W count=0x10`: write / verify / restore `OK`
  - `EP0FF1W count=0x10`: read `OK`
- `GM` bit vs packed-word:
  - manual support must be treated as `GM0000-GMFFFF` for bit and `GM000W-GMFFFW` for packed-word
  - exploratory probes outside the manual packed-word range are not treated as supported, even if a read path happens to return data
  - acceptance criteria should therefore reject `GM1000W` and above on the direct `PC10G` target
- `FR` boundary:
  - `FR007FFF count=0x2`: read `OK` across the `0x8000` block boundary
  - `FR007FFF` single write / commit / restore: `OK`
  - `FR008000` single write / commit / restore: `OK`
  - `FR1FFFFE count=0x2`: read `OK`
  - `FR1FFFFF count=0x2`: rejected because the sequential second element resolves to `FR200000`

### FR Persistence and Full-range Validation

`FR` support was additionally verified at full-range and power-cycle level on the direct `PC10G` target.

1. Whole-range destructive write:
   - `FR000000` to `FR1FFFFF`
   - pattern: `ramp16`
   - seed: `0x55AB`
   - result: `64/64` blocks verified
2. Change proof:
   - `before.csv` vs `after.csv`: `2,097,120` changed rows, `32` unchanged rows
3. Power-cycle retention:
   - `after.csv` vs `after_reset.csv`: identical SHA-256
   - row-by-row comparison: `0` changed rows, `2,097,152` same rows
   - retention across power-off / power-on therefore confirmed for the full `FR` range on this target

### Reconnect and Robustness Checks

Short reconnect probes showed that direct validation is stable when serialized and collision-prone when overlapped.

- sequential reconnect probes: `5/5 OK`
  - latest rerun on `2026-03-12`: `logs\pc10g_reconnect_rerun\seq_01.log` to `logs\pc10g_reconnect_rerun\seq_05.log`
- overlapping attempts:
  - latest rerun on `2026-03-12`: one full-suite probe completed `OK`
  - latest rerun on `2026-03-12`: one full-suite probe failed with `Socket error`

Timeout and malformed-response handling were also checked against local stub servers.

- no-response stub -> `Send/receive timeout`
- invalid CMD stub -> `Unexpected CMD in response`

### Negative Checks

- `M1800` -> `Address out of range for profile 'PC10G:PC10 mode': M1800`
- `P1-M1800` -> `Address out of range for profile 'PC10G:PC10 mode': P1-M1800`
- `U20000` -> `Address out of range for profile 'PC10G:PC10 mode': U20000`
- `EB40000` -> `Address out of range for profile 'PC10G:PC10 mode': EB40000`
- `P1-D3000` -> `Address out of range for profile 'PC10G:PC10 mode': P1-D3000`
- `FR200000` -> `Address out of range for profile 'PC10G:PC10 mode': FR200000`



## 4. Direct PC10G Standard / PC3JG

Verified: `2026-03-12`

Connection:

- host: `192.168.250.101`
- port: `1025`
- protocol: `tcp`
- profile: `PC10G:PC10 standard/PC3JG mode`

Canonical command:

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "PC10G:PC10 standard/PC3JG mode" --suite "PC10G:PC10 standard/PC3JG mode" --verbose --log logs\pc10g_std_pc3jg_suite_20260312.log
```

### Summary

- Read-only suite: `summary : suite=PC10G:PC10 standard/PC3JG mode ok=166 skip=0 ng=0`
- safe write / verify / restore `OK` on stable word targets
  - `P1-D0000`
  - `P1-D0FF0 count=0x10`
  - `B0000`
  - `P1-N0000`
  - `P1-R0000`
  - `U00000`
  - `U07FFF`
  - `EB00000`
  - `EB1FFFF`
  - `P2-D0000`
  - `P3-D0000`
- safe write / verify / restore `OK` on stable bit targets
  - `P1-M0000`, `P1-P0000`, `P1-T0000`, `P1-C0000`, `P1-L0000`, `P1-X0000`, `P1-Y0000`
  - `P2-M0000`
  - `P3-M0000`
- safe write / verify / restore `OK` on byte and packed-word targets
  - `P1-D0000L`, `P1-D0000H`
  - `P1-M000W`
  - `EP000W`, `EK000W`, `ET000W`, `EC000W`, `EL000W`, `EX000W`, `EY000W`, `EM000W`
  - `GM000W`, `GX000W`, `GY000W`
- stable many-device write / verify / restore `OK`
  - `P1-D0000`, `U07FFF`, `EB00000`, `P2-D0002`
  - `P1-D0000`, `P2-D0000`, `P3-D0000`, `P1-N0000`
  - `P1-N0000`, `P1-R0000`, `P2-D0000`, `P3-D0000`
  - `P1-M0000`, `P2-M0000`, `P3-M0000`, `P1-P0000`, `P1-T0000`, `P1-C0000`, `P1-L0000`
- negative checks rejected as expected
  - `P1-D1000`
  - `P1-V1000`
  - `P1-M1000`
  - `U08000`
  - `EB20000`
  - `FR000000`

### Acceptance Notes

- `FR` is not supported in this profile. `FR000000` returns `Unknown area for profile 'PC10G:PC10 standard/PC3JG mode': FR`.
- `V` is not treated as a strict bit write / readback target on this PLC/profile.
  - `P1-V0000`, `P2-V0000`, and `P3-V0000` bit writes returned `verify mismatch`
  - factual packed reads at the same start were `P1-V000W = 0x0058`, `P2-V000W = 0x0058`, `P3-V000W = 0x0058`
- `S` is written by the running system and is excluded from strict acceptance.
  - single-point writes on `P1-S0000`, `P2-S0000`, and `P3-S0000` were observed once and restored
  - `many-write` / `many-read` on `P1/P2/P3-S0000` is not treated as a driver defect probe because concurrent system updates can change the values during validation

## 5. Direct PC3JX Plus Expansion

Verified: `2026-03-12`

Connection:

- host: `192.168.250.101`
- port: `1025`
- protocol: `tcp`
- profile: `PC3JX:Plus expansion mode`

Canonical commands:

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "PC3JX:Plus expansion mode" --suite "PC3JX:Plus expansion mode" --verbose --log logs\pc3jx_plus_expansion_suite_20260312.log
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "PC3JX:Plus expansion mode" --device P1-D0000 --write-value 0x1234 --restore-after-write --verbose --log logs\pc3jx_plus_expansion_word_restore_20260312_rerun.log
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "PC3JX:Plus expansion mode" --device P1-M0000 --write-value 1 --restore-after-write --verbose --log logs\pc3jx_plus_expansion_bit_restore_20260312.log
```

### Summary

- Read-only suite: `summary : suite=PC3JX:Plus expansion mode ok=158 skip=0 ng=0`
- word write / verify / restore `OK`: `P1-D0000` (`0x0000 -> 0x1234 -> 0x0000`)
- bit write / verify / restore `OK`: `P1-M0000` (`0 -> 1 -> 0`)

### Supplemental Checks

Bit-to-packed readback probe (`V/X/Y/EV`):

```powershell
dotnet run --project examples\Toyopuc.BitPatternProbe -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "PC3JX:Plus expansion mode" --areas V,X,Y,EV --retries 3 --retry-delay 0.5 --csv logs\bit_pattern_pc3jx_plus_expansion_vxyev_20260312.csv --summary-json logs\bit_pattern_pc3jx_plus_expansion_vxyev_20260312.json
```

- summary: `ok=90 expected=0 total=90/100 failed=10`
- communication error: `0`
- restore mismatch: `0`
- observed strict mismatches:
  - `P1-V0000`, `P1-V0010`, `P1-V0030`, `P1-V00D0`, `P1-V00F0`
  - `P2-V0000`, `P2-V0010`
  - `P3-V0000`, `P3-V0010`
  - `EV0E20`

Prefix expansion write / restore (`P2/P3`):

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "PC3JX:Plus expansion mode" --device P2-D0000 --write-value 0x1234 --restore-after-write --verbose --log logs\pc3jx_plus_expansion_p2_d0000_restore_20260312.log
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "PC3JX:Plus expansion mode" --device P3-D0000 --write-value 0x1234 --restore-after-write --verbose --log logs\pc3jx_plus_expansion_p3_d0000_restore_20260312.log
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "PC3JX:Plus expansion mode" --device P2-M0000 --write-value 1 --restore-after-write --verbose --log logs\pc3jx_plus_expansion_p2_m0000_restore_20260312.log
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "PC3JX:Plus expansion mode" --device P3-M0000 --write-value 1 --restore-after-write --verbose --log logs\pc3jx_plus_expansion_p3_m0000_restore_20260312.log
```

- `P2-D0000`, `P3-D0000`: verify/recheck `OK`
- `P2-M0000`, `P3-M0000`: verify/recheck `OK`

Byte / packed-word write / restore:

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "PC3JX:Plus expansion mode" --device P1-D0000L --write-value 0x34 --restore-after-write --verbose --log logs\pc3jx_plus_expansion_p1_d0000l_restore_20260312.log
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "PC3JX:Plus expansion mode" --device P1-D0000H --write-value 0x12 --restore-after-write --verbose --log logs\pc3jx_plus_expansion_p1_d0000h_restore_20260312.log
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "PC3JX:Plus expansion mode" --device P1-M000W --write-value 0xA55A --restore-after-write --verbose --log logs\pc3jx_plus_expansion_p1_m000w_restore_20260312.log
```

- `P1-D0000L`, `P1-D0000H`, `P1-M000W`: verify/recheck `OK`

### Acceptance Notes

- One early `P1-D0000` run logged `Socket error` during connection setup.
- Re-running the same command sequentially completed with verify/recheck `OK`.

## 6. Direct Nano 10GX Compatible (No Relay)

Verified: `2026-03-12`

Connection:

- host: `192.168.250.101`
- port: `1025`
- protocol: `tcp`
- profile: `Nano 10GX:Compatible mode`
- relay hops: none (direct)

Canonical commands:

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "Nano 10GX:Compatible mode" --suite "Nano 10GX:Compatible mode" --timeout 5 --retries 3 --verbose --log logs\nano10gx_compatible_direct_suite_20260312_r3.log
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "Nano 10GX:Compatible mode" --device P1-D0000 --write-value 0x1357 --restore-after-write --timeout 5 --retries 3 --verbose --log logs\nano10gx_compatible_direct_word_restore_20260312.log
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "Nano 10GX:Compatible mode" --device P1-M0000 --write-value 1 --restore-after-write --timeout 5 --retries 3 --verbose --log logs\nano10gx_compatible_direct_bit_restore_20260312.log
```

### Summary

- read-only suite: `summary : suite=Nano 10GX:Compatible mode ok=9 skip=0 ng=0`
- `P1-D0000` write / verify / restore / recheck: `0x0000 -> 0x1357 -> 0x0000` (`OK`)
- `P1-M0000` write / verify / restore / recheck: `0 -> 1 -> 0` (`OK`)
- prefix expansion checks:
  - `P2-D0000`, `P3-D0000`, `P2-M0000`, `P3-M0000`: verify/recheck `OK`
- byte / packed-word checks:
  - `P1-D0000L`, `P1-D0000H`, `P1-M000W`: verify/recheck `OK`
- FR write/commit/restore checks:
  - `FR000000` commit/write/readback/restore `OK`
  - explicit change run: `0x55AB -> 0x55AA -> 0x55AB` (`OK`)

### Bit-to-packed Readback Probe (`V/X/Y/EV`)

Command:

```powershell
dotnet run --project examples\Toyopuc.BitPatternProbe -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "Nano 10GX:Compatible mode" --areas V,X,Y,EV --retries 3 --retry-delay 0.5 --csv logs\bit_pattern_nano10gx_compatible_direct_vxyev_20260312.csv --summary-json logs\bit_pattern_nano10gx_compatible_direct_vxyev_20260312.json
```

Observed result:

- summary: `ok=95 expected=0 total=95/100 failed=5`
- communication error: `0`
- restore mismatch: `0`
- strict mismatches:
  - `P1-V0000`, `P1-V00F0`
  - `P2-V0000`
  - `P3-V0000`
  - `EV0E20`

### Direct Read-Length Probe (current status)

Scope:

- direct path only (no relay hops)
- read-only sequential probe via `--probe-counts`

Representative commands:

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --timeout 5 --retries 3 --profile "Nano 10GX:Compatible mode" --device P1-D0000 --probe-counts 620,621,622,623 --log logs\nano10gx_compatible_direct_probecounts_p1d_20260312.log
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --timeout 8 --retries 3 --profile "Nano 10GX:Compatible mode" --device P1-D0000 --probe-counts 624,630,640,700,800 --log logs\nano10gx_compatible_direct_probecounts_p1d_mid_20260312.log
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --timeout 8 --retries 3 --profile "Nano 10GX:Compatible mode" --device P1-D0000 --probe-counts 631,632,633,634,635,636,637,638,639 --log logs\nano10gx_compatible_direct_probecounts_p1d_edge_20260312.log
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --timeout 5 --retries 3 --profile "Nano 10GX:Compatible mode" --device U00000 --probe-counts 620,621,622,623 --log logs\nano10gx_compatible_direct_probecounts_u00000_20260312.log
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --timeout 5 --retries 3 --profile "Nano 10GX:Compatible mode" --device U08000 --probe-counts 620,621,622,623 --log logs\nano10gx_compatible_direct_probecounts_u08000_20260312.log
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --timeout 5 --retries 3 --profile "Nano 10GX:Compatible mode" --device EB00000 --probe-counts 620,621,622,623 --log logs\nano10gx_compatible_direct_probecounts_eb00000_20260312.log
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --timeout 5 --retries 3 --profile "Nano 10GX:Compatible mode" --device FR000000 --probe-counts 620,621,622,623 --log logs\nano10gx_compatible_direct_probecounts_fr000000_20260312.log
```

Observed result:

- `P1-D0000`:
  - `count=0x27A` (`634`) read succeeded
  - `count=0x280` (`640`) and above returned `rc=0x10 / error_code=0x41 (Word/byte count is out of range)`
  - `count=0x27B` (`635`) to `0x27F` (`639`) timed out (`Send/receive timeout`)
- `U00000`, `U08000`, `EB00000`, `FR000000`:
  - `count=0x26F` (`623`) read succeeded
- connection instability observed after boundary probing:
  - edge check retries for `U00000 count=634/635` ended with `Socket error`
  - subsequent single-device health checks also ended with `Send/receive timeout`

Current conclusion:

- direct `Nano 10GX:Compatible mode` supports at least `count=623` for all tested representative areas.
- `P1-D0000` showed a provisional edge at `634` success and `>=635` unstable/failing behavior in this run window.
- final maximum-length conclusion is pending a rerun after the direct line is stable.

### Acceptance Notes

- One early direct suite attempt failed with `Timed out while connecting`.
- Re-running with `--timeout 5 --retries 3` completed successfully.

## 7. Minimal Sample

Verified: `2026-03-10`

Command:

```powershell
dotnet run --project examples\Toyopuc.MinimalRead -- 192.168.250.101 1025 tcp P1-D0000 "TOYOPUC-Plus:Plus Extended mode"
```

Result:

- `cpu-status` read: `OK`
- `clock` read: `OK`
- `D0000` read: `OK`

## 8. Build / Test / Pack

Verified: `2026-03-12`

Commands:

```powershell
dotnet build Toyopuc.sln
dotnet test Toyopuc.sln --no-build
dotnet pack src\Toyopuc\Toyopuc.csproj -c Release
```

Result:

- build: `OK`
- tests: `138 passed`
- pack: `Toyopuc.Net.1.0.0.nupkg` and `Toyopuc.Net.1.0.0.snupkg` generated `OK`

## 9. Release Batch Outputs

Verified: `2026-03-12`

Commands:

```bat
release.bat
device_monitor_release.bat
soak_monitor_release.bat
```

Result:

- release package output: `artifacts\release\1.0.0`
- packaged files: `Toyopuc.Net.1.0.0.nupkg`, `Toyopuc.Net.1.0.0.snupkg`, `Toyopuc.Net.1.0.0-dll.zip`
- publish output: `artifacts\publish\Toyopuc.DeviceMonitor\Toyopuc.DeviceMonitor.exe`
- publish output: `artifacts\publish\Toyopuc.SoakMonitor\Toyopuc.SoakMonitor.exe`

## 10. Remaining Hardware Targets (Pending)

Profiles reserved for later validation:

- `TOYOPUC-Plus:Plus Standard mode`
- `Nano 10GX:Nano 10GX mode`
- `PC3JX:PC3 separate mode`
- `PC3JG:PC3JG mode`
- `PC3JG:PC3 separate mode`
