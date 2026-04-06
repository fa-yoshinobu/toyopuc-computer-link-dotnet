# Automated Test Plan by Machine Profile

## Goal

Build a layered automated test strategy that verifies:

- profile definitions match the reviewed machine matrix
- address resolution stays correct for every profile, area, access mode, and boundary
- protocol-level read validation can be generated per machine without hand-written suites
- real hardware write validation stays safe by limiting writes to machine-specific allowlists

The target is not "write to every address on every PLC". The target is "exhaustive where it is safe and deterministic, controlled where hardware risk exists".

## Test Layers

### 1. Spec-Exhaustive Unit Tests

Run on every local build and CI run.

- Treat [`device_profile_matrix_r2.csv`](./device_profile_matrix_r2.csv) as the reviewed source of truth.
- Verify all canonical profile names match the matrix header.
- Verify every `area x access` row matches the runtime catalog:
  - supported / unsupported
  - disjoint ranges
  - packed-word support
  - address width
  - suggested step
- Generate resolver tests from catalog data instead of hand-written addresses only.
- Validate boundaries for each range:
  - start
  - end
  - just below
  - just above
  - disjoint segment transitions such as `0x01FF -> 0x1000`

### 2. Protocol / Integration Tests

Run against a simulator or controlled loopback target.

- Generate read-only probes from the catalog for every profile.
- Cover representative addresses for:
  - basic bit / word
  - byte access
  - prefixed access
  - extended areas
  - `U`, `EB`, `FR`
  - upper `1000` segments where applicable
- Store structured results per profile and area.

### 2.5. Application Regression Tests

Run on every local build and CI run.

- Lock the user-facing entry points to the reviewed rules:
  - no synthetic `GXY` naming in examples, scripts, or help text
  - profile-bound basic families use `P1/P2/P3-*`
  - derived `W/L/H` examples use the shorter manual notation
- Cover:
  - `README.md`
  - `examples/README.md`
  - `examples/run_validation.ps1`
  - `examples/PlcComm.Toyopuc.MinimalRead`
  - `examples/PlcComm.Toyopuc.SmokeTest`
  - `examples/PlcComm.Toyopuc.SoakMonitor`
  - `examples/PlcComm.Toyopuc.WriteLimitProbe`
- Add file-level regression tests first.
- Add process-level CLI contract tests second:
  - `--help`
  - default option text
  - validation of representative invalid input

### 3. Real Hardware Validation

Run manually or by explicit workflow dispatch.

- Read-only suites should be generated from the profile catalog.
- Write validation must use per-machine safe allowlists only.
- Every write case uses:
  - read
  - write
  - verify
  - restore
  - recheck
- `FR` write tests run only for machines that are confirmed to support them.

## Required Artifacts

- `tests/Toyopuc.Tests/ProfileMatrixCsvTests.cs`
  - CSV-to-catalog conformance
- `tests/Toyopuc.Tests/ResolverMatrixTests.cs`
  - generated resolver boundary coverage
- `examples/validation_targets.json` or similar
  - machine target manifest
- generated smoke suites in `examples/PlcComm.Toyopuc.SmokeTest`
  - no more hand-written two-profile-only suites
- machine-readable run reports
  - CSV or JSON per validation run

## Rollout Phases

### Phase 1

- Add CSV-to-catalog conformance tests.
- Make the reviewed matrix part of the test inputs.

### Phase 2

- Add generated resolver tests for all profiles and all supported ranges.
- Focus on boundary coverage and profile-specific switching behavior.

### Phase 3

- Replace hand-written smoke suites with generated read-only suites.
- Support every canonical machine profile.

### Phase 4

- Add machine target manifests.
- Add safe write validation per machine.

### Phase 5

- Add structured reports and GitHub Actions entry points for manual hardware runs.

### Phase 6

- Add a dedicated long-duration monitoring program for real hardware soak tests.
- Keep it separate from `PlcComm.Toyopuc.SmokeTest` so short validation and long-running observation do not share the same execution model.
- Current implementation:
  - `examples/PlcComm.Toyopuc.SoakMonitor`
- Cover at least:
  - repeated connect / disconnect
  - periodic read of a fixed device set
  - timeout / socket error logging
  - optional auto-reconnect
  - summary output for total reads, failures, reconnects, and elapsed time

## Deferred / Remaining Work

- Run the long-duration monitoring with `examples/PlcComm.Toyopuc.SoakMonitor`.
- Real hardware validation is deferred for the following profiles:
  - `TOYOPUC-Plus:Plus Standard mode`
  - `Nano 10GX:Nano 10GX mode`
  - `PC3JX:PC3 separate mode`
  - `PC3JG:PC3JG mode`
  - `PC3JG:PC3 separate mode`
- For each deferred profile, run a read-only suite first and compare it with a nearest already-verified profile before any write/restore checks.
- Before any write test in `Nano 10GX:Nano 10GX mode`, run the same read-only suite used for `Nano 10GX:Compatible mode` and compare the results.
- Relay continuous-length limit characterization is still open.
  - Current provisional observation on `Nano 10GX:Compatible mode` via relay:
    - `D0000` read succeeded up to `0x00F0` words (`240 words`) once
  - This is not a confirmed hard limit yet.
  - Follow-up is blocked by intermittent `rc=0x10 / error_code=0x73` relay-command-collision responses and subsequent socket errors on the same relay path.
  - Re-run when the relay path is confirmed idle, then measure:
    - `D0000` read/write
    - `U00000` read
    - `U08000` read
  - Current helper artifacts:
    - `examples/probe_relay_length_limits.ps1`
    - `examples/PlcComm.Toyopuc.SmokeTest --probe-counts`
    - `logs/relay_length_limit/relay_length_limit_summary.csv`
- Initial target:
  - Nano 10GX relay path
- Suggested first runs:
  - 30-minute soak
  - 2-hour soak
- Success criteria:
  - no silent data corruption
  - communication failures are logged with timestamps
  - reconnect behavior is explicit and measurable

## Acceptance Criteria

- A profile or range change that drifts from the reviewed matrix fails unit tests immediately.
- Adding a new profile requires updating the matrix and the generated tests, not hand-editing many suites.
- The smoke test runner can execute a read-only validation sweep for every canonical profile.
- Hardware write tests are explicit, safe, and auditable by machine.
