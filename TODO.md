# TODO: Toyopuc Computer Link .NET

This file tracks the remaining tasks and known issues for the Toyopuc Computer Link .NET library.

## 1. Validation and Test Stability
- [ ] **Formal Hardware Evidence**: Add `docs/validation/reports/` entries for the current `ToyopucDeviceClient`, async APIs, and 32-bit helper coverage.
- [ ] **Example Test Dependency**: Resolve or redesign the `ExampleEntryPointTests` dependency on the missing `soak_monitor_10gx_core.bat` asset.
- [ ] **Smoke / Soak Coverage**: Re-run the example applications against current hardware and capture the results as reproducible reports.

## 2. Documentation and API Audit
- [ ] **Naming Sweep**: Audit user and maintainer docs for any remaining stale examples that do not use `ToyopucDeviceClient` and `ReadAsync` / `WriteAsync`.
- [ ] **Sample Consistency**: Review the example set so the quick-start path and the validation tools use the same public vocabulary.

## 3. Quality Maintenance
- [ ] **Analyzer Pass**: Run `dotnet format` and the full analyzer set on the whole solution after the naming changes settle.
