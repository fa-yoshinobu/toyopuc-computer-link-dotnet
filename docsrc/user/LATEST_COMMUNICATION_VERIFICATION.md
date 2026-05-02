# Latest Communication Verification

This page keeps the current public summary only. Older detailed notes are not kept in the public documentation set.

## Current Retained Summary

- last refreshed: `2026-05-02`
- verified profile groups: direct `TOYOPUC-Plus`, relay `Nano 10GX`, direct `PC10G`
- verified public surface: queued connection, plain reads/writes, typed reads/writes, mixed snapshots, FR helpers, relay helpers
- recommended first public test: `P1-D0000` and `P1-M0000`

## Latest Direct PC10G Result

The latest direct `PC10G:PC10 mode` refresh was run on `2026-05-02` against
`192.168.250.100:1025` over TCP.

- release build: `OK`
- full `SmokeTest` suite: `ok=212 skip=0 ng=0`
- restored word write: `P1-D0100 0x0000 -> 0x1234 -> 0x0000`
- restored bit write: `P1-M0000 0 -> 1 -> 0`
- 60-second `SoakMonitor`: `polls=60 ok=60 ng=0 reconnects=0 sessions=1`

## Practical Public Conclusions

- `TOYOPUC-Plus` is the cleanest first-run path for prefixed word and bit access
- relay `Nano 10GX` remains a supported public path, but keep relay hops out of the first beginner test
- direct `PC10G` remains supported, but profile-specific range limits matter more there than on the basic `TOYOPUC-Plus` path

## Current Cautions

- exact writable ranges depend on profile and hardware
- `FR` support is profile-dependent and should not be the first smoke test
- keep range-sweep and protocol-detail evidence out of the public user path

## Where Older Evidence Went

Public historical validation clutter was removed. Maintainer-only retained evidence now belongs under `internal_docs/`,
including the reproducible command log for the `2026-05-02` direct PC10G smoke/soak refresh.
