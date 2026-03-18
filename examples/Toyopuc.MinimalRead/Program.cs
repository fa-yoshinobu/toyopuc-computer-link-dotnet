using System.Globalization;
using Toyopuc;

if (args.Contains("--help", StringComparer.OrdinalIgnoreCase)
    || args.Contains("-h", StringComparer.OrdinalIgnoreCase))
{
    PrintUsage();
    return;
}

var host = args.ElementAtOrDefault(0) ?? "192.168.250.101";
var port = TryParseInt32(args.ElementAtOrDefault(1), 1025);
var protocol = (args.ElementAtOrDefault(2) ?? "tcp").ToLowerInvariant();
var device = (args.ElementAtOrDefault(3) ?? "P1-D0000").ToUpperInvariant();
var profileName = args.ElementAtOrDefault(4) ?? "TOYOPUC-Plus:Plus Extended mode";
var profile = ToyopucAddressingOptions.FromProfile(profileName);

using var plc = new ToyopucDeviceClient(
    host,
    port,
    protocol: protocol,
    addressingOptions: profile,
    deviceProfile: profileName);

var status = plc.ReadCpuStatus();
var clock = plc.ReadClock().AsDateTime();
var value = plc.Read(device);

Console.WriteLine($"connect    : {protocol}://{host}:{port}");
Console.WriteLine($"profile    : {profileName}");
Console.WriteLine($"cpu-status : {status.RawBytesHex}");
Console.WriteLine($"clock      : {clock:yyyy-MM-dd HH:mm:ss}");
Console.WriteLine($"{device,-10}: {FormatValue(value)}");

static string FormatValue(object value)
{
    return value switch
    {
        bool bit => bit ? "1" : "0",
        byte b => $"0x{b:X2}",
        int word => $"0x{word:X4}",
        _ => value.ToString() ?? string.Empty,
    };
}

static int TryParseInt32(string? value, int fallback)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return fallback;
    }

    if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
    {
        return int.Parse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    return int.Parse(value, CultureInfo.InvariantCulture);
}

static void PrintUsage()
{
    Console.WriteLine("Toyopuc minimal read example");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project examples\\Toyopuc.MinimalRead -- [host] [port] [tcp|udp] [device] [profile]");
    Console.WriteLine();
    Console.WriteLine("Example:");
    Console.WriteLine("  dotnet run --project examples\\Toyopuc.MinimalRead -- 192.168.250.101 1025 tcp P1-D0000 \"TOYOPUC-Plus:Plus Extended mode\"");
}
