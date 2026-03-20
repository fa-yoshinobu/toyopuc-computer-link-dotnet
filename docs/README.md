# (JTEKT TOYOPUC) Computer Link .NET

[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Static Analysis: dotnet format](https://img.shields.io/badge/Lint-dotnet%20format-blue.svg)](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format)

A modern, high-performance .NET client library for JTEKT TOYOPUC computer-link communication. Supporting TOYOPUC-Plus, Nano 10GX, and other compatible models.

## Key Features

- **Modern .NET**: Built on .NET 9.0 with native async/await support.
- **Single-File Distribution**: Capable of producing self-contained, single-file executables.
- **Industrial Reliability**: Optimized for high-frequency monitoring and data collection.
- **Zero Mojibake**: English-only documentation and strict UTF-8 standards.
- **CI-Ready**: Built-in quality checks and publishing via `run_ci.bat`.

## Quick Start

### Installation
```bash
# Add NuGet package (Coming Soon)
# dotnet add package Toyopuc
```

### Basic Usage
```csharp
using Toyopuc;

// Connect to a TOYOPUC PLC
using var client = new ToyopucDeviceClient("192.168.1.5", 1025);

// Read P1-D0000 (Program 1, Word)
var value = await client.ReadAsync("P1-D0000");
Console.WriteLine($"Value: {value}");

// Write to P1-M0000 (Program 1, Bit)
await client.WriteAsync("P1-M0000", true);
```

## Documentation

Follows the workspace-wide hierarchical documentation policy:

- [**User Guide**](docsrc/user/USER_GUIDE.md): Connection setup and API reference.
- [**Troubleshooting**](docsrc/user/TROUBLESHOOTING.md): Common issues and FAQ.
- [**QA Reports**](docsrc/validation/reports/): Formal evidence of communication with real Toyopuc hardware.
- [**Architecture**](docsrc/maintainer/ARCHITECTURE.md): Design philosophy and background.
- [**Release Process**](docsrc/maintainer/RELEASE_PROCESS.md): Details on building and NuGet packaging.

## Development and CI

Quality is managed via `run_ci.bat`.

### Local CI and Publish
```bash
run_ci.bat
```
Validates the code and publishes a self-contained Single-File EXE to the `publish/` directory.

## License

Distributed under the [MIT License](LICENSE).

