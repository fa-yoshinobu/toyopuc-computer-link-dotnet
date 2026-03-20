# Validation Plan

This document records the hardware validation flow used in this repository for:

- direct `TOYOPUC-Plus`
- direct `PC10G`
- relay target `Nano 10GX`

## Preconditions

- Use [Toyopuc.SmokeTest](examples/Toyopuc.SmokeTest/Program.cs) for general validation runs.
- Use [Toyopuc.BitPatternProbe](examples/Toyopuc.BitPatternProbe/Program.cs) for sampled `BIT -> W/L/H` readback validation.
- Add `--restore-after-write` to write checks so the original value is restored after verification.
- When `--profile` is specified, basic families `P/K/V/T/C/L/X/Y/M/S/N/R/D` must be addressed as `P1-*`, `P2-*`, or `P3-*`.
- Pass relay hops as one PowerShell string.
  Example: `--hops "P1-L2:N4,P1-L2:N6,P1-L2:N2"`

## 1. Direct TOYOPUC-Plus

### 1-1. Read-only Suite

Purpose:

- prefixed basic word / bit / byte
- prefixed program word
- lower `U`
- unsupported area skip behavior

Command:

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "TOYOPUC-Plus:Plus Extended mode" --suite "TOYOPUC-Plus:Plus Extended mode" --verbose --log logs\plus_suite.log
```

Expected result:

- `summary : suite=TOYOPUC-Plus:Plus Extended mode ok=6 skip=3 ng=0`
- `U08000`, `EB00000`, and `FR000000` show `SKIP`

### 1-2. Safe Word Write / Readback / Restore

Purpose:

- direct write path
- readback
- restore

Command:

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "TOYOPUC-Plus:Plus Extended mode" --device P1-D0000 --write-value 0x1234 --restore-after-write --verbose --log logs\plus_word_restore.log
```

Expected result:

- `write`
- `verify`
- `restore`
- `recheck`

### 1-3. Safe Bit Write / Readback / Restore

Purpose:

- basic bit write path
- restore

Command:

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "TOYOPUC-Plus:Plus Extended mode" --device P1-M0000 --write-value 1 --restore-after-write --verbose --log logs\plus_bit_restore.log
```

### 1-4. Sampled Bit-to-packed Readback Probe (supplemental)

Purpose:

- sample first / middle / last aligned starts for bit-device families
- write `16` bits with recognizable patterns
- verify packed `WORD`, `L`, and `H` reads
- restore the original state after every case

Command:

```powershell
dotnet run --project examples\Toyopuc.BitPatternProbe -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "TOYOPUC-Plus:Plus Extended mode" --retries 3 --retry-delay 0.5 --csv logs\bit_pattern_plus_extended.csv --summary-json logs\bit_pattern_plus_extended.json
```

Acceptance notes for this profile:

- `X/Y` low-base points and some `V/EV` points are treated as target-specific packed readback behavior when restore succeeds.
- accepted target-specific mismatches:
  - `P1-V0000`, `P1-V0010`, `P1-V00D0`, `P1-V00F0`
  - `P2-V0000`, `P2-V0010`
  - `P3-V0000`, `P3-V0010`
  - `P1-X0000`, `P2-X0000`, `P3-X0000`
  - `P1-Y0000`, `P2-Y0000`, `P3-Y0000`
  - `EV0E20`

### 1-5. SocketError Follow-up (stability)

Purpose:

- reproduce intermittent socket errors under sustained polling
- confirm reconnect and retry behavior on the direct `TOYOPUC-Plus` path

Command:

```powershell
dotnet run --project examples\Toyopuc.SoakMonitor -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "TOYOPUC-Plus:Plus Extended mode" --devices P1-D0000,P1-M0000,P1-V0000,EV0E20 --interval 1s --duration 30m --retries 3 --success-log-interval 10 --log logs\plus_soak_30m.log --poll-csv logs\plus_soak_30m.csv --summary-json logs\plus_soak_30m.json
```

Expected result:

- for a stable line: summary shows `ng=0` and `reconnects=0`
- if `ng > 0`, keep the log and classify each failure (`Socket error`, timeout, or peer-reset)
- rerun once with `--duration 2h` before closing direct `TOYOPUC-Plus` communication stability

## 2. Relay Nano 10GX

### 2-1. Relay Read-only Suite

Purpose:

- prefixed basic word / bit / byte
- prefixed program word
- lower / upper `U`
- `EB`
- `FR`
- relay unwrap / nested ACK confirmation

Command:

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "Nano 10GX:Compatible mode" --hops "P1-L2:N4,P1-L2:N6,P1-L2:N2" --suite "Nano 10GX:Compatible mode" --verbose --log logs\relay_suite_10gx.log
```

Expected result:

- `summary : suite=Nano 10GX:Compatible mode ok=9 skip=0 ng=0`

### 2-2. Relay Word Write / Readback / Restore

Purpose:

- relay write
- relay readback
- restore

Command:

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "Nano 10GX:Compatible mode" --hops "P1-L2:N4,P1-L2:N6,P1-L2:N2" --device P1-D0000 --write-value 0x1357 --restore-after-write --verbose --log logs\relay_word_restore.log
```

Expected result:

- example: `0x2468 -> 0x1357 -> 0x2468`

### 2-3. Relay Bit Write / Readback / Restore

Purpose:

- relay bit write
- relay bit restore

Command:

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "Nano 10GX:Compatible mode" --hops "P1-L2:N4,P1-L2:N6,P1-L2:N2" --device P1-M0000 --write-value 1 --restore-after-write --verbose --log logs\relay_bit_restore.log
```

### 2-4. Relay FR Write + Commit + Restore

Purpose:

- `C3` write
- `CA` commit
- commit completion wait
- restore

Command:

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "Nano 10GX:Compatible mode" --hops "P1-L2:N4,P1-L2:N6,P1-L2:N2" --fr-device FR000000 --fr-write-value 0x55AB --fr-commit --restore-after-write --verbose --log logs\relay_fr_commit_restore.log
```

Expected result:

- `fr-verify: FR000000 = 0x55AB`
- `fr-recheck: FR000000 = 0x55AA`
- verbose trace shows `C3`, `CA`, and completion-wait polling

## 3. Direct PC10G

### 3-1. Sampled Bit-to-packed Readback Probe

Purpose:

- sample first / middle / last aligned starts for bit-device families
- write `16` bits with recognizable patterns
- verify packed `WORD`, `L`, and `H` reads
- restore the original state after every case

Command:

```powershell
dotnet run --project examples\Toyopuc.BitPatternProbe -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "PC10G:PC10 mode" --csv logs\bit_pattern_pc10g_direct.csv --summary-json logs\bit_pattern_pc10g_direct.json
```

Expected result:

- summary format: `ok=<strict-ok> expected=<expected-mismatch> total=<accepted>/<all> failed=0`
- verified direct `PC10G` run: `ok=385 expected=5 total=390/390 failed=0`
- every case must finish with successful restore
- `V` / `EV` expected mismatches are recorded as facts for this target and are not treated as `NG`

Scope:

- `P/K/V/T/C/L/X/Y/M` for `P1`, `P2`, `P3`
- `EP/EK/EV/ET/EC/EL/EX/EY/EM/GM/GX/GY`

Current direct `PC10G` expected mismatches:

- `P1-V0000`
- `P1-V00F0`
- `P2-V0000`
- `P3-V0000`
- `EV0E20`

## 4. Direct PC3JX Plus Expansion

### 4-1. Read-only Suite

Purpose:

- generated read-only coverage for extended and prefixed areas

Command:

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "PC3JX:Plus expansion mode" --suite "PC3JX:Plus expansion mode" --verbose --log logs\pc3jx_plus_expansion_suite_20260312.log
```

Expected result:

- `summary : suite=PC3JX:Plus expansion mode ok=158 skip=0 ng=0`

### 4-2. Prefix-expanded Safe Write / Readback / Restore

Purpose:

- confirm prefixed `D` / `M` write path for `P2` and `P3`

Command:

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "PC3JX:Plus expansion mode" --device P2-D0000 --write-value 0x1234 --restore-after-write --verbose --log logs\pc3jx_plus_expansion_p2_d0000_restore_20260312.log
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "PC3JX:Plus expansion mode" --device P3-D0000 --write-value 0x1234 --restore-after-write --verbose --log logs\pc3jx_plus_expansion_p3_d0000_restore_20260312.log
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "PC3JX:Plus expansion mode" --device P2-M0000 --write-value 1 --restore-after-write --verbose --log logs\pc3jx_plus_expansion_p2_m0000_restore_20260312.log
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "PC3JX:Plus expansion mode" --device P3-M0000 --write-value 1 --restore-after-write --verbose --log logs\pc3jx_plus_expansion_p3_m0000_restore_20260312.log
```

Expected result:

- all four runs complete with verify/recheck `OK`

### 4-3. Byte / Packed-word Safe Write / Readback / Restore

Purpose:

- confirm byte and packed-word write/restore on representative points

Command:

```powershell
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "PC3JX:Plus expansion mode" --device P1-D0000L --write-value 0x34 --restore-after-write --verbose --log logs\pc3jx_plus_expansion_p1_d0000l_restore_20260312.log
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "PC3JX:Plus expansion mode" --device P1-D0000H --write-value 0x12 --restore-after-write --verbose --log logs\pc3jx_plus_expansion_p1_d0000h_restore_20260312.log
dotnet run --project examples\Toyopuc.SmokeTest -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "PC3JX:Plus expansion mode" --device P1-M000W --write-value 0xA55A --restore-after-write --verbose --log logs\pc3jx_plus_expansion_p1_m000w_restore_20260312.log
```

Expected result:

- all three runs complete with verify/recheck `OK`

### 4-4. Sampled Bit-to-packed Readback Probe (`V/X/Y/EV`)

Purpose:

- sample `V/X/Y/EV` for `BIT -> W/L/H` consistency and restore safety

Command:

```powershell
dotnet run --project examples\Toyopuc.BitPatternProbe -- --host 192.168.250.101 --port 1025 --protocol tcp --profile "PC3JX:Plus expansion mode" --areas V,X,Y,EV --retries 3 --retry-delay 0.5 --csv logs\bit_pattern_pc3jx_plus_expansion_vxyev_20260312.csv --summary-json logs\bit_pattern_pc3jx_plus_expansion_vxyev_20260312.json
```

Expected result:

- communication error: `0`
- restore mismatch: `0`
- if strict mismatches are observed on `V/EV`, record the exact points in `TESTRESULTS.md`

## Result Criteria

- `OK`
  - communication succeeded and the observed value matched the expectation
  - for [Toyopuc.BitPatternProbe](examples/Toyopuc.BitPatternProbe/Program.cs) on direct `PC10G`, documented `V` / `EV` target-specific mismatches are also accepted when restore succeeds
  - for [Toyopuc.BitPatternProbe](examples/Toyopuc.BitPatternProbe/Program.cs) on direct `TOYOPUC-Plus:Plus Extended mode`, documented `V` / `EV` and low-base `X` / `Y` target-specific mismatches are also accepted when restore succeeds
- `SKIP`
  - the area is confirmed unsupported for that machine profile
- `NG`
  - communication error
  - readback mismatch
  - restore mismatch
  - suite summary reports `ng > 0`

## Notes

- The direct `TOYOPUC-Plus` target and relay `Nano 10GX` target use different profiles.
- Use `--profile "Nano 10GX:Compatible mode"` on the relay target.
- Relay FR completion wait automatically falls back to CPU-status polling if `A0 01 10` is not accepted.
