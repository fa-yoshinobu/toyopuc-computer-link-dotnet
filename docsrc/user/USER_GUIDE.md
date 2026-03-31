# User Guide: TOYOPUC Computer Link .NET

This guide explains the high-level .NET API only.
Use it when you want to read and write TOYOPUC devices by device string such as `P1-D0000`, `P1-M0000`, `ES0000`, or `FR000000`.

## Choose an API

Use `QueuedToyopucDeviceClient` as the main application client type. Create it
through `ToyopucDeviceClientFactory.OpenAndConnectAsync`.
On top of that client, use these high-level helpers when they match your task:

- `ReadTypedAsync` / `WriteTypedAsync`
- `ReadNamedAsync`
- `ReadWordsSingleRequestAsync` / `ReadDWordsSingleRequestAsync`
- `ReadWordsChunkedAsync` / `ReadDWordsChunkedAsync`
- `WriteBitInWordAsync`
- `PollAsync`
- `ToyopucAddress.Normalize`

## Quick Start

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
```

## Device Addressing

### Address Format

```text
[Program]-[AreaType][Address]
```

- `Program`
  `P1`, `P2`, or `P3`
- `AreaType`
  device family such as `D`, `M`, `ES`, `FR`
- `Address`
  decimal or hexadecimal device number

When a profile is in use, basic families `P/K/V/T/C/L/X/Y/M/S/N/R/D` should be written as `P1-*`, `P2-*`, or `P3-*`.

Use `ToyopucAddress.Normalize` when you want a stable string form:

```csharp
string canonical = ToyopucAddress.Normalize("p1-d0000", profile: "TOYOPUC-Plus:Plus Extended mode");
Console.WriteLine(canonical); // P1-D0000
```

### Common Device Families

| Area | Type | Example |
| --- | --- | --- |
| `D` | Data register (word) | `P1-D0100` |
| `S` | Special register (word) | `P1-S0000` |
| `N` | File register (word) | `P1-N0100` |
| `M` | Internal relay (bit) | `P1-M0100` |
| `P` | Shared relay (bit) | `P1-P0000` |
| `X` | Input relay (bit) | `P1-X0000` |
| `Y` | Output relay (bit) | `P1-Y0000` |
| `T` | Timer (bit) | `P1-T0000` |
| `C` | Counter (bit) | `P1-C0000` |
| `ES` | Extended special register (word) | `ES0000` |
| `EN` | Extended file register (word) | `EN0000` |
| `U` | Extended word area | `U00000` |
| `EB` | Extended block word area | `EB00000` |
| `FR` | File-register flash area | `FR000000` |

Extended areas such as `ES`, `EN`, `H`, `U`, `EB`, and `FR` do not require a program prefix.

### Byte and Packed-Word Suffixes

Append `L`, `H`, or `W` to bit-area addresses for byte or packed-word access:

```csharp
var packed = await client.ReadAsync("P1-M0010W");
var lowByte = await client.ReadAsync("P1-M0010L");
var highByte = await client.ReadAsync("P1-M0010H");
```

## Common Tasks

### Read and Write a Single Device

```csharp
var value = await client.ReadAsync("P1-D0000");
await client.WriteAsync("P1-D0001", 1234);
await client.WriteAsync("P1-M0000", 1);
```

### Read and Write Several Devices Together

```csharp
var snapshot = await client.ReadManyAsync(["P1-D0000", "P1-D0001", "P1-M0000"]);
Console.WriteLine(snapshot[0]);

await client.WriteManyAsync(
[
    new KeyValuePair<object, object>("P1-D0000", 10),
    new KeyValuePair<object, object>("P1-D0001", 20),
    new KeyValuePair<object, object>("P1-M0000", 0),
]);
```

### Read Typed Values

Use these type codes with `ReadTypedAsync` and `WriteTypedAsync`:

| dtype | Type | Size |
| --- | --- | --- |
| `"U"` | unsigned 16-bit int | 1 word |
| `"S"` | signed 16-bit int | 1 word |
| `"D"` | unsigned 32-bit int | 2 words |
| `"L"` | signed 32-bit int | 2 words |
| `"F"` | float32 | 2 words |

```csharp
var floatValue = await client.ReadTypedAsync("P1-D0100", "F");
var signedValue = await client.ReadTypedAsync("P1-D0200", "L");

await client.WriteTypedAsync("P1-D0100", "F", 3.14f);
await client.WriteTypedAsync("P1-D0200", "S", -100);
```

### Read Contiguous Blocks

```csharp
ushort[] words = await client.ReadWordsSingleRequestAsync("P1-D0000", 10);
uint[] dwords = await client.ReadDWordsSingleRequestAsync("P1-D0200", 4);
```

Use explicit chunked helpers only when multi-request splitting is acceptable:

```csharp
ushort[] longWords = await client.ReadWordsChunkedAsync("P1-D0000", 200, maxWordsPerRequest: 64);
uint[] longDwords = await client.ReadDWordsChunkedAsync("P1-D0200", 40, maxDwordsPerRequest: 32);
```

### Change One Bit Inside a Word

```csharp
await client.WriteBitInWordAsync("P1-D0100", bitIndex: 3, value: true);
```

### Read a Typed Snapshot by Name

Address notation:

| Format | Meaning |
| --- | --- |
| `"P1-D0100"` | unsigned 16-bit word |
| `"P1-D0100:F"` | float32 |
| `"P1-D0100:S"` | signed 16-bit word |
| `"P1-D0100:D"` | unsigned 32-bit value |
| `"P1-D0100:L"` | signed 32-bit value |
| `"P1-D0100.3"` | bit 3 inside the word |

```csharp
var result = await client.ReadNamedAsync(
[
    "P1-D0100",
    "P1-D0101:F",
    "P1-D0102:S",
    "P1-D0100.3",
]);

Console.WriteLine(result["P1-D0101:F"]);
```

### Poll Values Repeatedly

```csharp
var count = 0;
await foreach (var snapshot in client.PollAsync(["P1-D0100", "P1-D0101:F"], TimeSpan.FromSeconds(1)))
{
    Console.WriteLine(snapshot["P1-D0100"]);
    count++;
    if (count >= 3)
    {
        break;
    }
}
```

### FR File-Register Access

Use FR helpers when you need non-volatile file-register data.

```csharp
var currentValue = await client.ReadFrAsync("FR000000");
await client.WriteFrAsync("FR000000", 0x1234, commit: false);
await client.CommitFrAsync("FR000000", wait: true);
```

Use `commit: true` only when you intentionally want to persist the modified FR block to flash.

### Relay Access

Relay helpers are also available from `ToyopucDeviceClient`.

```csharp
var status = await client.RelayReadCpuStatusAsync("P1-L2:N2");
var wordValue = await client.RelayReadWordsAsync("P1-L2:N2", "P1-D0000", count: 1);
await client.RelayWriteAsync("P1-L2:N2", "P1-M0000", 1);
```

If relay hops are stable for the whole session, you can also set them in
`ToyopucConnectionOptions.RelayHops` and use one shared queued client.

## Error Handling

| Exception | Condition |
| --- | --- |
| `ToyopucError` | PLC returned an error response |
| `ToyopucProtocolError` | Malformed or unexpected protocol data |
| `TimeoutException` | Communication timeout |

```csharp
try
{
    var value = await client.ReadAsync("P1-D0000");
}
catch (TimeoutException)
{
    Console.WriteLine("Timeout: check IP address and Computer Link port.");
}
catch (ToyopucProtocolError ex)
{
    Console.WriteLine($"Protocol error: {ex.Message}");
}
catch (ToyopucError ex)
{
    Console.WriteLine($"PLC error: {ex.Message}");
}
```

## Sample Programs

Start with these examples:

- `examples/PlcComm.Toyopuc.MinimalRead`
- `examples/PlcComm.Toyopuc.HighLevelSample`
- `examples/PlcComm.Toyopuc.SoakMonitor`
