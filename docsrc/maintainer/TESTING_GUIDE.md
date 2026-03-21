# Testing Guide

This document describes the test structure and verification approach for `Toyopuc.Net`.

Related documents:

- [ARCHITECTURE.md](ARCHITECTURE.md)
- [PROTOCOL_SPEC.md](PROTOCOL_SPEC.md)
- [VALIDATION.md](VALIDATION.md)

## Unit / Integration Tests

The automated test suite is under `tests/Toyopuc.Tests/`.

Run with:

```powershell
dotnet test Toyopuc.sln -v normal
```

Expected result: all tests pass, 0 warnings.

## Simulator

`scripts/sim_server.py` in `plc-comm-computerlink-python` is a development helper.

Currently supported commands in the simulator:

- basic single-point: `CMD=1C/1D/1E/1F/20/21`
- basic multi-point: `CMD=22/23/24/25`
- extended single/contiguous: `CMD=94/95/96/97`
- extended multi-point: `CMD=98/99`
- PC10 block/multi: `CMD=C2/C3/C4/C5`
- relay: `CMD=60`
- clock: `CMD=32 70 00` / `CMD=32 71 00`
- CPU status: `CMD=32 11 00`

Not modeled accurately enough to treat as hardware-equivalent:

- FR commit behavior
- Hardware-specific NAK / error responses

## Hardware Verification

Verified hardware targets:

- `TOYOPUC-Plus CPU (TCC-6740)` + `Plus EX2 (TCU-6858)` via UDP
- Single-hop, two-hop, and three-hop relay paths verified

### Relay Verification

Verified relay configurations (from `plc-comm-computerlink-python` testing):

- `P1-L2:N2` (single-hop)
- `P1-L2:N2 -> P1-L2:N4` (two-hop)
- `P1-L2:N2 -> P1-L2:N4 -> P1-L2:N6` (three-hop)

Verified inner commands over relay:

- `CMD=32 / 11 00` CPU status read
- `CMD=32 / 70 00` clock read
- `CMD=1C` word read
- `CMD=C2` / `CMD=C3` FR read/write
- `CMD=CA` FR commit with completion wait

## Cross-Library Parity

The .NET library is kept in sync with `plc-comm-computerlink-python`.

When adding or changing a method, verify:

1. The equivalent Python method exists and has the same semantics.
2. The `.Async` counterpart exists in `ToyopucClient.Async.cs`.
3. The relay variant exists where applicable.

## CI

CI runs on every push via `.github/workflows/ci.yml`:

```powershell
dotnet build Toyopuc.sln
dotnet test Toyopuc.sln --no-build
```
