# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.6] - 2026-04-27

### Fixed
- Fixed TOYOPUC address parsing so single-letter areas such as `D` and `U` are not misread as unknown two-letter areas when the address starts with a hexadecimal `A-F` digit.
- Kept unsupported areas as hard errors instead of falling back to another device interpretation.

## [0.1.5] - 2026-04-14

### Changed
- Rebuilt the public Toyopuc docs around a beginner-first user flow and moved maintainer-only material under `internal_docs`.
- Separated local and publish docs build steps and fixed the example/doc regression checks for the new documentation layout.

## [0.1.4] - 2026-04-01

### Changed
- Refreshed the README, user docs, examples, and generated DocFX output after the unified `SingleRequest` and `Chunked` helper split.
- Added regression coverage for atomic single-request writes on program devices so the documented high-level contract stays verified.

## [0.1.3] - 2026-03-28

### Changed
- `ToyopucDeviceClient` high-level 32-bit and float helpers now accept numeric low-level word addresses in addition to string device addresses, matching the Python implementation.
- Transport and high-level layers now cache relay hops, resolved devices, and compiled run plans to reduce repeated parsing and dispatch overhead.
- Async wrappers now run on a per-client exclusive scheduler instead of dispatching every call through plain `Task.Run`.
- TCP receive and trace hot paths now avoid extra allocations during repeated polling and frame capture.
- Documentation and TODO notes were refreshed to match the current `PlcComm.Toyopuc.*` example set and the current CI/analyzer status.
- Added `release_check.bat` to run CI and DocFX generation as one pre-release entry point.
- Added example/doc regression tests so stale sample names and removed helper assets are caught in CI.

## [0.1.2] - 2026-03-22

### Changed
- Renamed NuGet package from legacy `Toyopuc` to `PlcComm.Toyopuc`; updated namespace and assembly name accordingly.
- Unified `Directory.Build.props` with `TreatWarningsAsErrors`, `EnableNETAnalyzers`, and `AnalysisLevel=latest-recommended`.
- Cleaned up `PlcComm.Toyopuc.csproj`: removed redundant `AssemblyName`, `RootNamespace`, `Product`, and `IsPackable` properties; improved `Title`.
- Fixed `README.md` and `USER_GUIDE.md` examples to use correct namespace (`PlcComm.Toyopuc`).

## [0.1.0] - 2026-03-19

### Added
- .NET 9.0 TOYOPUC computer-link client (`ToyopucDeviceClient`) with TCP and UDP support.
- Model-aware addressing profiles and device catalog support.
- Validation CLI, Windows device monitor (`DeviceMonitor`), and scripted hardware validation.
- Release output includes `Toyopuc.DeviceMonitor.exe` under `artifacts\release\<version>`.
- Release automation via `release.bat` and GitHub Actions workflows.
- Hardware verification against TOYOPUC-Plus and Nano 10GX targets.

### Notes
- Initial public release under the `PlcComm.*` package family.
