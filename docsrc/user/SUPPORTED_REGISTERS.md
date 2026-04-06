# Supported PLC Registers

This page is the canonical public register table for the .NET high-level API.

Exact writable spans depend on the selected profile and hardware. Use this page for the public high-level families, then use the verification summary for model-specific cautions.

## Common Prefixed Families

| Family | Kind | Example | Notes |
| --- | --- | --- | --- |
| `D` | word | `P1-D0100` | data register |
| `S` | word | `P1-S0000` | special register |
| `N` | word | `P1-N0100` | file register |
| `R` | word | `P1-R0000` | register area |
| `M` | bit | `P1-M0000` | internal relay |
| `X` | bit | `P1-X0000` | input |
| `Y` | bit | `P1-Y0000` | output |
| `T` | word/bit family | `P1-T0000` | timer-related area |
| `C` | word/bit family | `P1-C0000` | counter-related area |
| `L` | word/bit family | `P1-L0000` | link/relay-related area |
| `P`, `K`, `V` | profile families | `P1-V0000` | availability depends on profile |

## Extension and Storage Families

| Family | Kind | Example | Notes |
| --- | --- | --- | --- |
| `ES` | word | `ES0000` | extended special register |
| `EN` | word | `EN0000` | extended file register |
| `FR` | storage | `FR000000` | file-register flash area |

## High-Level Views

| Form | Example | Meaning |
| --- | --- | --- |
| plain word | `P1-D0100` | unsigned 16-bit word |
| signed view | `P1-D0100:S` | signed 16-bit value |
| dword view | `P1-D0100:D` | unsigned 32-bit value |
| long view | `P1-D0100:L` | signed 32-bit value |
| float view | `P1-D0100:F` | float32 value |
| bit in word | `P1-D0100.3` | one bit inside a word |

## Addressing Notes

- Start with `P1-D0000` and `P1-M0000`.
- When a profile is in use, basic area families should use `P1-`, `P2-`, or `P3-`.
- `FR` is a separate storage area and should not be the first beginner test.
- Profile-specific range limits remain model-dependent.

## Model-Specific Reminder

Current retained verification covers:

- direct `TOYOPUC-Plus`
- relay `Nano 10GX`
- direct `PC10G`

Keep model-specific acceptance decisions in the latest verification summary, not in the beginner path.
