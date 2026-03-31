# High-Level API Contract

This document defines the target public API shape for the Toyopuc Computer Link .NET library.
Backward compatibility is not a design constraint for this contract.

This contract is intentionally aligned with:

- `plc-comm-slmp-dotnet`
- `plc-comm-hostlink-dotnet`

## 1. Design Goals

- keep one obvious high-level entry point for application code
- keep typed read/write helpers consistent across the three .NET PLC libraries
- make connection options explicit instead of implicit
- preserve semantic atomicity by default
- forbid hidden fallback splitting that changes the meaning of one logical request

## 2. Primary Client Shape

Application-facing code should use a connected, async-safe client wrapper as the primary entry point.

Target shape:

```csharp
public sealed record ToyopucConnectionOptions(
    string Host,
    int Port = 1025,
    TimeSpan Timeout = default,
    ToyopucTransport Transport = ToyopucTransport.Tcp,
    string? DeviceProfile = null,
    string? RelayHops = null,
    int LocalPort = 0,
    int Retries = 0,
    TimeSpan RetryDelay = default
);

public static class ToyopucDeviceClientFactory
{
    public static Task<QueuedToyopucDeviceClient> OpenAndConnectAsync(
        ToyopucConnectionOptions options,
        CancellationToken cancellationToken = default);
}
```

Notes:

- the returned client must be safe to share across multiple async callers
- Toyopuc-specific routing and retry settings must stay explicit
- relay and direct connection modes must be selected by configuration, not hidden probing

## 3. Required High-Level Methods

The primary client should expose or clearly own these operations:

```csharp
Task<object> ReadTypedAsync(
    string address,
    string dtype,
    CancellationToken cancellationToken = default);

Task WriteTypedAsync(
    string address,
    string dtype,
    object value,
    CancellationToken cancellationToken = default);

Task WriteBitInWordAsync(
    string address,
    int bitIndex,
    bool value,
    CancellationToken cancellationToken = default);

Task<IReadOnlyDictionary<string, object>> ReadNamedAsync(
    IEnumerable<string> addresses,
    CancellationToken cancellationToken = default);

IAsyncEnumerable<IReadOnlyDictionary<string, object>> PollAsync(
    IEnumerable<string> addresses,
    TimeSpan interval,
    CancellationToken cancellationToken = default);
```

## 4. Contiguous Read/Write Contract

Contiguous block access must distinguish between three behaviors:

### 4.1 Single-request behavior

Use this when the caller requires one protocol request or an error.

```csharp
Task<ushort[]> ReadWordsSingleRequestAsync(
    string start,
    int count,
    CancellationToken cancellationToken = default);

Task<uint[]> ReadDWordsSingleRequestAsync(
    string start,
    int count,
    CancellationToken cancellationToken = default);

Task WriteWordsSingleRequestAsync(
    string start,
    IReadOnlyList<ushort> values,
    CancellationToken cancellationToken = default);

Task WriteDWordsSingleRequestAsync(
    string start,
    IReadOnlyList<uint> values,
    CancellationToken cancellationToken = default);
```

### 4.2 Semantic-atomic behavior

Use this when the caller cares about logical value integrity but accepts documented protocol boundaries.

- do not split one logical `DWord` / `Float32`
- do not split one caller-visible logical block through hidden fallback logic
- protocol-defined boundaries such as FR or PC10 block limits are acceptable only when they preserve the intended semantics and are documented
- if the library cannot preserve semantics, return an error

### 4.3 Explicit chunked behavior

Use this only when the caller explicitly opts into segmentation.

```csharp
Task<ushort[]> ReadWordsChunkedAsync(
    string start,
    int count,
    int maxWordsPerRequest,
    CancellationToken cancellationToken = default);

Task<uint[]> ReadDWordsChunkedAsync(
    string start,
    int count,
    int maxDwordsPerRequest,
    CancellationToken cancellationToken = default);

Task WriteWordsChunkedAsync(
    string start,
    IReadOnlyList<ushort> values,
    int maxWordsPerRequest,
    CancellationToken cancellationToken = default);

Task WriteDWordsChunkedAsync(
    string start,
    IReadOnlyList<uint> values,
    int maxDwordsPerRequest,
    CancellationToken cancellationToken = default);
```

## 5. Atomicity Rules

These rules are normative.

- `ReadTypedAsync`, `WriteTypedAsync`, `WriteBitInWordAsync`, and `ReadNamedAsync` must preserve logical value integrity
- default APIs must not silently split one logical request into different semantics after an error
- fallback retry with a different write shape must be opt-in and explicitly named
- if the library cannot preserve the requested semantics, it should return an error

For Toyopuc specifically:

- segmentation on FR or PC10 boundaries is acceptable only when that boundary is part of the documented protocol access model
- throughput-oriented run planning must not be hidden behind APIs that users would reasonably treat as one semantic write
- if an operation is non-atomic by design, that must be reflected in the API name or documentation

## 6. Address Helper Contract

String address handling should be public and reusable instead of duplicated in UI or adapter code.

Target shape:

```csharp
public static class ToyopucAddress
{
    public static bool TryParse(string text, out ParsedAddress address);
    public static ParsedAddress Parse(string text);
    public static string Format(ParsedAddress address);
    public static string Normalize(string text);
}
```

High-level logical address helpers should remain available for forms such as:

- `P1-D0100`
- `P1-D0200:L`
- `P1-D0300:F`
- `P1-D0100.3`
- relay-qualified addresses through explicit connection options or relay-aware helpers

## 7. Error Contract

- invalid address text should fail deterministically during parsing
- unsupported dtype should fail before any transport call
- operations that require preserved semantics should fail instead of silently degrading into chunked behavior
- protocol and device errors should stay visible to callers

## 8. Non-Goals

- no hidden fallback splitting
- no requirement to preserve old extension-method naming if a cleaner public surface is chosen
