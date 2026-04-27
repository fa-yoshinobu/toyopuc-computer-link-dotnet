# Library Device Profile Specification

This document describes the machine presets and range rules implemented by the
.NET library.

This is an internal implementation document.
For protocol background and upstream model research, see:

- [`plc-comm-computerlink-python/docsrc/user/MODEL_RANGES.md`](https://github.com/fa-yoshinobu/plc-comm-computerlink-python/blob/main/docsrc/user/MODEL_RANGES.md)
- [`plc-comm-computerlink-python/internal_docs/maintainer/TESTING_GUIDE.md`](https://github.com/fa-yoshinobu/plc-comm-computerlink-python/blob/main/internal_docs/maintainer/TESTING_GUIDE.md)
- [`PYTHON_PORTING_NOTES.md`](PYTHON_PORTING_NOTES.md)

## Source Of Truth

The source of truth is code, not UI logic.

| Concern | Source |
| --- | --- |
| profile definitions | [`../../src/Toyopuc/ToyopucDeviceProfiles.cs`](../../src/Toyopuc/ToyopucDeviceProfiles.cs) |
| profile data types | [`../../src/Toyopuc/ToyopucDeviceProfile.cs`](../../src/Toyopuc/ToyopucDeviceProfile.cs) |
| lookup API | [`../../src/Toyopuc/ToyopucDeviceCatalog.cs`](../../src/Toyopuc/ToyopucDeviceCatalog.cs) |
| addressing option switches | [`../../src/Toyopuc/ToyopucAddressingOptions.cs`](../../src/Toyopuc/ToyopucAddressingOptions.cs) |
| reviewed range matrix | [`device_profile_matrix_r2.csv`](./device_profile_matrix_r2.csv) |

Applications built on `PlcComm.Toyopuc` must not maintain their own area
tables or model-specific upper bounds.

## Exposed Profiles

The library currently exposes these canonical profile names:

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

The exact per-area matrix is maintained in
[`device_profile_matrix_r2.csv`](./device_profile_matrix_r2.csv).

## Data Model

`ToyopucAreaDescriptor` supports disjoint ranges.

| Field | Meaning |
| --- | --- |
| `Area` | area name such as `M`, `D`, `U`, `FR` |
| `DirectRanges` | supported direct ranges |
| `PrefixedRanges` | supported `P1/P2/P3` ranges |
| `DirectRange` | convenience property when the direct side has exactly one range |
| `PrefixedRange` | convenience property when the prefixed side has exactly one range |
| `SupportsPackedWord` | whether packed `W` rows such as `M000W` are allowed |
| `AddressWidth` | displayed address width |
| `SuggestedStartStep` | dropdown generation step for UI start addresses |

Interpretation rules:

- `DirectRanges.Count == 0` means direct access is not supported.
- `PrefixedRanges.Count == 0` means `P1/P2/P3` access is not supported.
- If `DirectRanges.Count > 1` or `PrefixedRanges.Count > 1`, the area has disjoint segments.
- In the current profile set, basic families `P/K/V/T/C/L/X/Y/M/S/N/R/D` are modeled as `P1/P2/P3` only. High-level device resolution requires the `P1-`, `P2-`, or `P3-` prefix for these families.

## Library API Surface

Applications are expected to query model information through
`ToyopucDeviceCatalog`.

| API | Purpose |
| --- | --- |
| `ToyopucDeviceProfiles.GetNames()` | list exposed profile names |
| `ToyopucDeviceProfiles.FromName(profile)` | resolve a canonical profile name |
| `ToyopucDeviceCatalog.GetAreas(prefixed, profile)` | list usable areas for direct or prefixed access |
| `ToyopucDeviceCatalog.GetAreaDescriptor(area, profile)` | get metadata for one area |
| `ToyopucDeviceCatalog.GetSupportedRanges(area, prefixed, profile)` | get all implemented ranges for one area |
| `ToyopucDeviceCatalog.GetSupportedRange(area, prefixed, profile)` | get one range only when the area is not disjoint |
| `ToyopucDeviceCatalog.IsSupportedIndex(area, index, prefixed, profile)` | validate one numeric index |
| `ToyopucDeviceCatalog.GetSuggestedStartAddresses(area, prefix, profile)` | generate UI-friendly start candidates |

`GetSuggestedStartAddresses(...)` iterates every supported segment and appends
segment-end candidates when needed, so disjoint ranges such as
`0000-01FF,1000-17FF` remain reachable from the UI.

## Addressing Options

`ToyopucAddressingOptions.FromProfile(...)` maps the profiles to switching flags
used by the resolver and high-level client.

| Profile | `UseUpperUPc10` | `UseEbPc10` | `UseFrPc10` | `UseUpperBitPc10` | `UseUpperMBitPc10` |
| --- | --- | --- | --- | --- | --- |
| `Generic` | `true` | `true` | `true` | `true` | `true` |
| `TOYOPUC-Plus:Plus Standard mode` | `false` | `false` | `false` | `false` | `false` |
| `TOYOPUC-Plus:Plus Extended mode` | `false` | `false` | `false` | `false` | `false` |
| `Nano 10GX:Nano 10GX mode` | `true` | `true` | `true` | `true` | `true` |
| `Nano 10GX:Compatible mode` | `true` | `true` | `true` | `true` | `true` |
| `PC10G:PC10 standard/PC3JG mode` | `false` | `true` | `false` | `false` | `false` |
| `PC10G:PC10 mode` | `true` | `true` | `true` | `true` | `true` |
| `PC3JX:PC3 separate mode` | `false` | `false` | `false` | `false` | `false` |
| `PC3JX:Plus expansion mode` | `false` | `false` | `false` | `false` | `false` |
| `PC3JG:PC3JG mode` | `false` | `true` | `false` | `false` | `false` |
| `PC3JG:PC3 separate mode` | `false` | `false` | `false` | `false` | `false` |

## Area Metadata

| Area family | Areas | Address width | Packed `W` | Suggested step |
| --- | --- | --- | --- | --- |
| basic bit | `P K V T C L X Y M` | 4 | yes | `0x10` |
| basic word | `S N R D B` | 4 | no | `0x10` |
| ext bit | `EP EK EV ET EC EL EX EY EM GM GX GY` | 4 | yes | `0x10` |
| ext word | `ES EN H U EB` | 5 | no | `0x100` |
| FR word | `FR` | 6 | no | `0x1000` |

## Notes On Current Profile Intent

These are short reminders only. For exact ranges, use the CSV or code.

- `Generic` is the library superset. Basic families `P/K/V/T/C/L/X/Y/M/S/N/R/D` are prefixed-only, while `B` and extended families stay direct.
- `TOYOPUC-Plus:Plus Standard mode` keeps prefixed basic families and lower extended families, but does not expose `B`, `U`, `GM/GX/GY`, `EB`, or `FR`.
- `TOYOPUC-Plus:Plus Extended mode` keeps prefixed basic families and adds `GM/GX/GY` and lower `U`.
- `Nano 10GX:Nano 10GX mode` and `Nano 10GX:Compatible mode` currently expose the same matrix in the library.
- On the verified `Nano 10GX` relay target, packed-word access aliases `ET` with `EC`, `EX` with `EY`, and `GX` with `GY`.
- `PC10G:PC10 mode` keeps upper `P/V/T/C/L/M/S/N` segments.
- `PC3JX:PC3 separate mode` keeps `B` but does not expose `GM/GX/GY`.
- `PC3JG:PC3 separate mode` exposes `EB` without exposing `U`.

## Monitor Behavior Rules

Applications using the high-level API rely on these profile definitions for:

| UI behavior | Depends on profile spec |
| --- | --- |
| device dropdown contents | yes |
| address dropdown contents | yes |
| selected range validation | yes |
| scroll window stop conditions | yes |

That means:

- If a profile does not expose an area, the monitor must not offer it.
- If a profile has disjoint ranges, the monitor must stay inside the segment
  that contains the selected start address.
- The application must not duplicate upper bounds locally. All range queries
  must come from `ToyopucDeviceCatalog`.

## Change Rules

When changing supported ranges:

| Step | Action |
| --- | --- |
| 1 | update `device_profile_matrix_r2.csv` if the reviewed matrix changed |
| 2 | update `ToyopucDeviceProfiles` |
| 3 | verify `ToyopucAddressingOptions` still matches the profile intent |
| 4 | update [`../../tests/Toyopuc.Tests/AddressAndResolverTests.cs`](../../tests/Toyopuc.Tests/AddressAndResolverTests.cs) |
| 5 | update this document |
| 6 | if behavior is externally visible, update [`../../README.md`](../../README.md) |

