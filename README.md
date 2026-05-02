[![CI](https://github.com/fa-yoshinobu/plc-comm-computerlink-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/fa-yoshinobu/plc-comm-computerlink-dotnet/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/PlcComm.Toyopuc.svg)](https://www.nuget.org/packages/PlcComm.Toyopuc/)
[![Documentation](https://img.shields.io/badge/docs-GitHub_Pages-blue.svg)](https://fa-yoshinobu.github.io/plc-comm-computerlink-dotnet/)
[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/fa-yoshinobu/plc-comm-computerlink-dotnet/blob/main/LICENSE)
[![Static Analysis: dotnet format](https://img.shields.io/badge/Lint-dotnet%20format-blue.svg)](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format)

# Computer Link Protocol for .NET

![Illustration](https://raw.githubusercontent.com/fa-yoshinobu/plc-comm-computerlink-dotnet/main/docsrc/assets/toyopuc.png)

A user-focused .NET library for JTEKT TOYOPUC Computer Link communication.
The recommended entry point is the high-level queued client created by `ToyopucDeviceClientFactory`.

This README intentionally covers the public high-level API only:

- `ToyopucConnectionOptions`
- `ToyopucDeviceClientFactory.OpenAndConnectAsync`
- `ReadAsync` / `WriteAsync`
- `ReadTypedAsync` / `WriteTypedAsync`
- `WriteBitInWordAsync`
- `ReadManyAsync` / `ReadNamedAsync`
- `PollAsync`
- `ReadWordsSingleRequestAsync` / `ReadDWordsSingleRequestAsync`
- `ReadWordsChunkedAsync` / `ReadDWordsChunkedAsync`
- `ReadFrAsync` / `WriteFrAsync` / `CommitFrAsync`

## Quick Start

### Installation

- Package page: <https://www.nuget.org/packages/PlcComm.Toyopuc/>

```powershell
dotnet add package PlcComm.Toyopuc
```

Or add a package reference directly:

```xml
<PackageReference Include="PlcComm.Toyopuc" Version="0.1.8" />
```

### High-Level Example

```csharp
using PlcComm.Toyopuc;

var options = new ToyopucConnectionOptions("192.168.250.100")
{
    Port = 1025,
    DeviceProfile = "TOYOPUC-Plus:Plus Extended mode",
};

await using var client = await ToyopucDeviceClientFactory.OpenAndConnectAsync(options);

var word = await client.ReadAsync("P1-D0000");
Console.WriteLine($"P1-D0000 = {word}");

await client.WriteAsync("P1-D0001", 1234);
await client.WriteAsync("P1-M0000", 1);

var typed = await client.ReadTypedAsync("P1-D0200", "F");
Console.WriteLine($"P1-D0200:F = {typed}");

var snapshot = await client.ReadNamedAsync(["P1-D0000", "P1-D0200:F", "P1-D0000.0"]);
Console.WriteLine(snapshot["P1-D0000"]);
```

Basic area families `P/K/V/T/C/L/X/Y/M/S/N/R/D` require a `P1-`, `P2-`, or `P3-` prefix.

## Supported PLC Registers

Start with these public high-level families first:

- prefixed word/register areas: `P1-D0000`, `P1-S0000`, `P1-N0100`, `P1-R0000`
- prefixed bit/control areas: `P1-M0000`, `P1-X0000`, `P1-Y0000`, `P1-T0000`
- extension areas: `ES0000`, `EN0000`
- FR storage: `FR000000`
- typed and bit views: `P1-D0100:S`, `P1-D0200:D`, `P1-D0300:F`, `P1-D0000.3`

See the full public table in [Supported PLC Registers](https://github.com/fa-yoshinobu/plc-comm-computerlink-dotnet/blob/main/docsrc/user/SUPPORTED_REGISTERS.md).

## Public Documentation

- [Getting Started](https://github.com/fa-yoshinobu/plc-comm-computerlink-dotnet/blob/main/docsrc/user/GETTING_STARTED.md)
- [Supported PLC Registers](https://github.com/fa-yoshinobu/plc-comm-computerlink-dotnet/blob/main/docsrc/user/SUPPORTED_REGISTERS.md)
- [Latest Communication Verification](https://github.com/fa-yoshinobu/plc-comm-computerlink-dotnet/blob/main/docsrc/user/LATEST_COMMUNICATION_VERIFICATION.md)
- [User Guide](https://github.com/fa-yoshinobu/plc-comm-computerlink-dotnet/blob/main/docsrc/user/USER_GUIDE.md)
- [Examples Guide](https://github.com/fa-yoshinobu/plc-comm-computerlink-dotnet/blob/main/examples/README.md)
- [High-Level API Contract](https://github.com/fa-yoshinobu/plc-comm-computerlink-dotnet/blob/main/HIGH_LEVEL_API_CONTRACT.md)

Start with these example programs:

- `examples/PlcComm.Toyopuc.MinimalRead`
- `examples/PlcComm.Toyopuc.HighLevelSample`
- `examples/PlcComm.Toyopuc.SoakMonitor`

Profile-specific example apps and probe scripts require an explicit profile
argument; omitted profile values are rejected instead of being interpreted as a
default TOYOPUC model.

Maintainer-only notes and retained evidence live under `internal_docs/`.

## Latest Communication Verification

Latest direct `PC10G:PC10 mode` validation was refreshed on `2026-05-02` against
`192.168.250.100:1025` over TCP.

- release build: `OK`
- full `SmokeTest` suite: `ok=212 skip=0 ng=0`
- restored word write: `P1-D0100 0x0000 -> 0x1234 -> 0x0000`
- restored bit write: `P1-M0000 0 -> 1 -> 0`
- 60-second `SoakMonitor`: `polls=60 ok=60 ng=0 reconnects=0 sessions=1`

Detailed retained evidence is in
[`internal_docs/maintainer/TESTRESULTS.md`](https://github.com/fa-yoshinobu/plc-comm-computerlink-dotnet/blob/main/internal_docs/maintainer/TESTRESULTS.md).

## Common User Tasks

- read or write one device: `ReadAsync`, `WriteAsync`
- read several devices together: `ReadManyAsync`, `ReadNamedAsync`
- read 32-bit integers or float32 values: `ReadDWordsSingleRequestAsync`, `ReadTypedAsync`
- change one flag bit inside a word: `WriteBitInWordAsync`
- read contiguous word blocks: `ReadWordsSingleRequestAsync`, `ReadDWordsSingleRequestAsync`
- read large contiguous ranges explicitly: `ReadWordsChunkedAsync`, `ReadDWordsChunkedAsync`
- persist FR data: `ReadFrAsync`, `WriteFrAsync`, `CommitFrAsync`
- poll a small watch list repeatedly: `PollAsync`

## Development and CI

Run local CI:

```powershell
run_ci.bat
```

Run the release-style check including docs:

```powershell
release_check.bat
```

Pack the NuGet package locally:

```powershell
dotnet pack src\Toyopuc\PlcComm.Toyopuc.csproj -c Release
```

## License

Distributed under the [MIT License](https://github.com/fa-yoshinobu/plc-comm-computerlink-dotnet/blob/main/LICENSE).
