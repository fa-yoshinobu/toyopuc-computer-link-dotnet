# API Unification Policy

This document defines the planned public API rules for the TOYOPUC .NET library.
It is a design policy document. It does not claim that every rule is implemented yet.

## Purpose

- Keep the high-level API stable and easy to compare with sibling PLC libraries.
- Preserve low-level protocol-oriented access for advanced users.
- Add asynchronous support without creating a second naming system.

## Public API Layers

The library must keep two explicit layers.

1. `ToyopucClient`
   Low-level API for numeric addresses, raw payloads, relay frames, FR commit details, and protocol-oriented operations.
2. `ToyopucDeviceClient`
   High-level API for string device addresses such as `P1-D0000` or `FR000000`.

High-level examples, README quick start, and UI code must prefer `ToyopucDeviceClient`.
Low-level examples may use `ToyopucClient`.

## Naming Rules

High-level generic device access must use these names.

- `Read`
- `Write`
- `ReadMany`
- `WriteMany`
- `ReadDWord`
- `WriteDWord`
- `ReadDWords`
- `WriteDWords`
- `ReadFloat32`
- `WriteFloat32`
- `ReadFloat32s`
- `WriteFloat32s`
- `ReadFr`
- `WriteFr`
- `CommitFr`
- `ResolveDevice`
- `RelayRead`
- `RelayWrite`
- `RelayReadMany`
- `RelayWriteMany`

Low-level typed access must use explicit protocol-oriented names.

- `ReadWords`
- `WriteWords`
- `ReadBytes`
- `WriteBytes`
- `ReadBit`
- `WriteBit`
- `ReadDWords`
- `WriteDWords`
- `ReadFloat32s`
- `WriteFloat32s`
- `ReadExtWords`
- `WriteExtWords`
- `Pc10BlockRead`
- `Pc10BlockWrite`
- `ReadClock`
- `WriteClock`
- `ReadCpuStatus`

Do not add high-level convenience names that duplicate the generic layer.

Forbidden examples for the high-level layer:

- `ReadWordAsync(string device)`
- `WriteBitAsync(string device, bool value)`
- `ReadDeviceAsync(...)`

If the caller already has a string device address, the canonical high-level entry points are `Read`, `Write`, `ReadMany`, and `WriteMany`.

## 32-Bit Value Rules

The library should distinguish raw 32-bit integers from IEEE 754 floating-point values.

- `DWord` means a raw 32-bit unsigned value stored across two PLC words.
- Signed 32-bit helpers, if added later, should be named `ReadInt32` and `WriteInt32`.
- Floating-point helpers should use `Float32` in the name, not plain `Float`.

Default 32-bit word-pair interpretation:

- The default contract is protocol-native low-word-first ordering.
- If alternate word order must be supported, use an explicit option such as `wordOrder`.
- Do not encode word order in vague names such as `ReadFloatSwap`.

## Async Rules

Async support must use the same base names as the sync API with the .NET `Async` suffix.

Examples:

- `ConnectAsync`
- `SendRawAsync`
- `ReadWordsAsync`
- `WriteWordsAsync`
- `ReadClockAsync`
- `ReadCpuStatusAsync`
- `WaitFrWriteCompleteAsync`
- `ReadAsync`
- `WriteAsync`
- `ReadManyAsync`
- `WriteManyAsync`
- `ReadFrAsync`
- `WriteFrAsync`
- `CommitFrAsync`
- `RelayReadAsync`
- `RelayWriteAsync`

Async methods must follow these rules.

- Return the same logical result shape as the sync method.
- Keep parameter order identical to the sync method.
- Accept an optional `CancellationToken` on new async methods.
- Avoid creating a parallel API vocabulary just for async.

## Internal Naming Rules

Private and helper method names must describe the object or grouping they operate on.
Short names are acceptable only when the scope is trivially local.

Avoid vague private names such as:

- `ReadOne`
- `WriteOne`
- `RelayReadOne`
- `RelayWriteOne`
- `Offset`
- `ResolveDeviceObject`

Prefer names such as:

- `ReadResolvedDevice`
- `WriteResolvedDevice`
- `RelayReadResolvedDevice`
- `RelayWriteResolvedDevice`
- `OffsetResolvedDevice`
- `NormalizeResolvedDeviceInput`
- `NormalizeResolvedWriteItems`
- `PackUInt32LowWordFirst`
- `UnpackUInt32LowWordFirst`
- `PackFloat32LowWordFirst`
- `UnpackFloat32LowWordFirst`

Batch helpers should include the grouping concept in the name.

- `ReadBatch`
- `WriteBatch`
- `ReadResolvedBatch`
- `WriteResolvedBatch`
- `ReadPc10WordBatch`
- `WriteExtBitBatch`

## Documentation Rules

README and sample code must use the planned public surface.

- High-level quick start must show `ToyopucDeviceClient`.
- High-level async quick start must show `ReadAsync` and `WriteAsync`.
- README must not advertise provisional names that are not part of this policy.

## Stability Rules

- Existing sync names remain the source of truth for base naming.
- Async additions should be additive.
- Do not keep legacy public class aliases once the canonical class name is published.
