# Agent Guide: Toyopuc Computer Link .NET

This repository is part of the PLC Communication Workspace and follows the global standards defined in `D:\PLC_COMM_PROJ\AGENTS.md`.

## 1. Project-Specific Context
- Protocol: TOYOPUC Computer Link (JTEKT)
- Authoritative Source: JTEKT or Toyoda specifications
- Language: .NET 9.0 (C#)
- Role: Core communication library for TOYOPUC-Plus, Nano 10GX, and related models

## 2. Mandatory Rules
- Language: All code, comments, and documentation must be in English.
- Encoding: Use UTF-8 without BOM.
- Static analysis: Changes must pass `dotnet build` and formatting or analyzer checks.
- Documentation structure: Keep user guidance in `docsrc/user/` and maintainer guidance in `docsrc/maintainer/`.

## 3. Development Workflow
- Log remaining work in `TODO.md` when that file exists.
- Update `CHANGELOG.md` for externally visible changes.
- Keep protocol behavior aligned with the Python port when practical.

## 4. API Naming Policy

Detailed naming policy lives in `docsrc/maintainer/API_UNIFICATION_POLICY.md`.

Public API rules:

- Canonical client class names are `ToyopucClient` for the low-level API and `ToyopucDeviceClient` for the string-device API.
- High-level string-device access uses `Read`, `Write`, `ReadMany`, `WriteMany`, `ReadFr`, `WriteFr`, and `CommitFr`.
- Low-level protocol access uses explicit names such as `ReadWords`, `WriteWords`, `ReadBit`, `ReadClock`, and `ReadCpuStatus`.
- 32-bit helpers should use `ReadDWord`, `WriteDWord`, `ReadDWords`, `WriteDWords`, `ReadFloat32`, and `WriteFloat32` style names.
- Async names keep the same base word and add the `.NET` `Async` suffix.

Private or helper naming rules:

- Avoid vague names like `ReadOne`, `WriteOne`, or `Offset` when the helper operates on a resolved device model.
- Prefer names such as `ReadResolvedDevice`, `WriteResolvedDevice`, `RelayReadResolvedDevice`, and `OffsetResolvedDevice`.
- Batch helpers should include the grouping concept in the name, for example `ReadResolvedBatch` or `WritePc10WordBatch`.
- 32-bit codec helpers should include both type and word order, for example `PackUInt32LowWordFirst` or `UnpackFloat32LowWordFirst`.

