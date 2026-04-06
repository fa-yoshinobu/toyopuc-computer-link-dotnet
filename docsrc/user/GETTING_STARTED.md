# Getting Started

## Start Here

Use this package when you want the shortest .NET path to TOYOPUC Computer Link communication through the public high-level API.

Recommended first path:

1. Install `PlcComm.Toyopuc`.
2. Choose the correct `DeviceProfile`.
3. Open one queued client through `ToyopucDeviceClientFactory.OpenAndConnectAsync`.
4. Read one safe word such as `P1-D0000`.
5. Write only to a known-safe test word or bit after the first read is stable.

## First PLC Registers To Try

Start with these first:

- `P1-D0000`
- `P1-D0001`
- `P1-M0000`
- `P1-D0200:F`

Do not start with these:

- relay hops
- `FR` writes
- large chunked reads

## Minimal Connection Pattern

```csharp
var options = new ToyopucConnectionOptions("192.168.250.100")
{
    Port = 1025,
    DeviceProfile = "TOYOPUC-Plus:Plus Extended mode",
};

await using var client = await ToyopucDeviceClientFactory.OpenAndConnectAsync(options);
```

If a profile is in use, basic area families should use the correct `P1-`, `P2-`, or `P3-` prefix.

## First Successful Run

Recommended order:

1. `ReadAsync("P1-D0000")`
2. `WriteAsync("P1-D0001", 1234)` only on a safe test word
3. `WriteAsync("P1-M0000", 1)` only on a safe test bit
4. `ReadNamedAsync(["P1-D0000", "P1-D0200:F", "P1-D0000.0"])`

Expected result:

- connection opens successfully
- one prefixed word read succeeds
- typed and mixed snapshot reads succeed after the first plain read

## Common Beginner Checks

If the first read fails, check these in order:

- correct host and port
- correct `DeviceProfile`
- correct `P1-`, `P2-`, or `P3-` prefix
- start with `P1-D0000` instead of `FR` or relay addresses

## Next Pages

- [Supported PLC Registers](./SUPPORTED_REGISTERS.md)
- [Latest Communication Verification](./LATEST_COMMUNICATION_VERIFICATION.md)
- [User Guide](./USER_GUIDE.md)
