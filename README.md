[![CI](https://github.com/fa-yoshinobu/plc-comm-computerlink-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/fa-yoshinobu/plc-comm-computerlink-dotnet/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/PlcComm.Toyopuc.svg)](https://www.nuget.org/packages/PlcComm.Toyopuc/)
[![Documentation](https://img.shields.io/badge/docs-GitHub_Pages-blue.svg)](https://fa-yoshinobu.github.io/plc-comm-computerlink-dotnet/)
[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/fa-yoshinobu/plc-comm-computerlink-dotnet/blob/main/LICENSE)
[![Static Analysis: dotnet format](https://img.shields.io/badge/Lint-dotnet%20format-blue.svg)](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format)

# Computer Link Protocol for .NET

![Illustration](https://raw.githubusercontent.com/fa-yoshinobu/plc-comm-computerlink-dotnet/main/docsrc/assets/toyopuc.png)

A user-focused .NET library for JTEKT TOYOPUC Computer Link communication.
The recommended entry point is the high-level queued client created by
`ToyopucDeviceClientFactory`.

## Key Features

- High-level device access such as `P1-D0000`, `P1-M0000`, `ES0000`, and `FR000000`
- Explicit connection options and optional relay-hops setup
- Typed helpers for `U`, `S`, `D`, `L`, and `F`
- Snapshot helpers such as `ReadManyAsync`, `ReadNamedAsync`, and `PollAsync`
- Explicit single-request and chunked contiguous block helpers
- Public address parsing and normalization helpers
- FR file-register helpers and relay helpers for common application work
- Ready-to-run examples for minimal reads, cookbook-style usage, monitoring, and soak runs

## Quick Start

### Installation

Install from NuGet:

- Package page: https://www.nuget.org/packages/PlcComm.Toyopuc/

```powershell
dotnet add package PlcComm.Toyopuc
```

Or add a package reference directly:

```xml
<PackageReference Include="PlcComm.Toyopuc" Version="0.1.3" />
```

You can also reference `src/Toyopuc/PlcComm.Toyopuc.csproj` directly from a local solution during development.

### High-level example

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

Basic area families `P/K/V/T/C/L/X/Y/M/S/N/R/D` should use a `P1-`, `P2-`, or `P3-` prefix when a profile is in use.

## Common User Tasks

- Read or write one device: `ReadAsync`, `WriteAsync`
- Read several devices together: `ReadManyAsync`, `ReadNamedAsync`
- Read 32-bit integers or float32 values: `ReadDWordsSingleRequestAsync`, `ReadTypedAsync`
- Change one flag bit inside a word: `WriteBitInWordAsync`
- Read contiguous word blocks: `ReadWordsSingleRequestAsync`, `ReadDWordsSingleRequestAsync`
- Read large contiguous ranges explicitly: `ReadWordsChunkedAsync`, `ReadDWordsChunkedAsync`
- Persist FR data: `ReadFrAsync`, `WriteFrAsync`, `CommitFrAsync`
- Poll a small watch list repeatedly: `PollAsync`

Address helper:

```csharp
string canonical = ToyopucAddress.Normalize("p1-d0000", profile: "TOYOPUC-Plus:Plus Extended mode");
Console.WriteLine(canonical); // P1-D0000
```

Use `*SingleRequestAsync` when one logical request must remain one protocol
operation. Use `*ChunkedAsync` only when protocol-defined boundary splitting is
acceptable for that device family and data set.

## User Docs

- [User Guide](https://github.com/fa-yoshinobu/plc-comm-computerlink-dotnet/blob/main/docsrc/user/USER_GUIDE.md)
- [Examples Guide](https://github.com/fa-yoshinobu/plc-comm-computerlink-dotnet/blob/main/examples/README.md)
- [High-Level API Contract](https://github.com/fa-yoshinobu/plc-comm-computerlink-dotnet/blob/main/HIGH_LEVEL_API_CONTRACT.md)

Start with these example programs:

- `examples/PlcComm.Toyopuc.MinimalRead`
- `examples/PlcComm.Toyopuc.HighLevelSample`
- `examples/PlcComm.Toyopuc.SoakMonitor`

Engineering and validation documents remain under `docsrc/maintainer/`.

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
