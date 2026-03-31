# TODO: Toyopuc Computer Link .NET

This file tracks the remaining tasks and known issues for the Toyopuc Computer Link .NET library.

Only explicit real-hardware follow-up remains.

## 1. Validation and Test Stability
- [ ] **Smoke / Soak Coverage**: Re-run the example applications against current hardware and capture the results as reproducible reports.

## 2. Cross-Stack API Alignment

- [ ] **Align the high-level helper surface**: Keep the public entry points intentionally parallel to the sibling .NET libraries around `OpenAndConnectAsync`, `ReadTypedAsync`, `WriteTypedAsync`, `WriteBitInWordAsync`, `ReadNamedAsync`, and `PollAsync`.
- [ ] **Promote reusable address helpers**: Review whether device parse/normalize/format helpers should be made public so app integrations do not need to duplicate Toyopuc address handling.
- [ ] **Define a stable connection-options model**: Keep Toyopuc-specific settings such as profile selection, relay hops, local port, retries, and retry delay explicit while still matching the common connection-shape used by the other .NET stacks.
- [ ] **Preserve semantic atomicity by default**: Allow segmentation only on protocol-defined boundaries such as FR or PC10 block limits. Do not silently split one logical value or one user-visible logical block into different semantics.
- [ ] **Preserve semantic atomicity by default**: Allow segmentation only on protocol-defined boundaries such as FR or PC10 block limits. Do not silently split one logical value or one user-visible logical block into different semantics.

## 2. Cross-Stack API Alignment

- [ ] **Align the high-level helper surface**: Keep the public entry points intentionally parallel to the sibling .NET libraries around `OpenAndConnectAsync`, `ReadTypedAsync`, `WriteTypedAsync`, `WriteBitInWordAsync`, `ReadNamedAsync`, and `PollAsync`.
- [ ] **Promote reusable address helpers**: Review whether device parse/normalize/format helpers should be made public so app integrations do not need to duplicate Toyopuc address handling.
- [ ] **Define a stable connection-options model**: Keep Toyopuc-specific settings such as profile selection, relay hops, local port, retries, and retry delay explicit while still matching the common connection-shape used by the other .NET stacks.

