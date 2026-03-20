# User Guide: TOYOPUC Computer Link .NET

This guide explains how to use the `Toyopuc` library to communicate with JTEKT TOYOPUC PLCs using the Computer Link protocol.

## 1. Getting Started

### Installation
Add a project reference to `src/Toyopuc/Toyopuc.csproj` or include the compiled DLL in your .NET 9.0 project.

### Basic Usage
The library provides `ToyopucDeviceClient` for high-level, string-based device access.

```csharp
using Toyopuc;

// Initialize the client
using var client = new ToyopucDeviceClient("192.168.1.5", 1025);

// Read a word (P1-D0000)
var value = await client.ReadAsync("P1-D0000");
Console.WriteLine($"P1-D0000: {value}");

// Write a bit (P1-M0000)
await client.WriteAsync("P1-M0000", true);
```

## 2. Device Addressing
The library supports TOYOPUC-style addressing:
- **Format**: `P[ProgramNo]-[DeviceType][Address]`
- **Examples**:
    - `P1-D0000`: Program 1, Data Register 0.
    - `P2-M0100`: Program 2, Internal Relay 100.
    - `P1-X0000`: Program 1, Input 0.
    - `P1-Y0000`: Program 1, Output 0.

## 3. Advanced Features
- **Async Native**: Built with Task-based Asynchronous Pattern (TAP).
- **Transport Support**: Primary support for TCP/IP.
- **Model Compatibility**: Verified with TOYOPUC-Plus and Nano 10GX.

Refer to [Troubleshooting](TROUBLESHOOTING.md) for common connection issues.
