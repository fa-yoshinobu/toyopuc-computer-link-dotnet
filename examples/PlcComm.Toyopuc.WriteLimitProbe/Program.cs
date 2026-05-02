using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using PlcComm.Toyopuc;

if (args.Contains("--help", StringComparer.OrdinalIgnoreCase)
    || args.Contains("-h", StringComparer.OrdinalIgnoreCase))
{
    PrintUsage();
    return;
}

ProbeOptions options;
try
{
    options = ProbeOptions.Parse(args);
}
catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
{
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 1;
    return;
}

Directory.CreateDirectory(Path.GetDirectoryName(options.SummaryJsonPath)!);

Console.WriteLine($"connect     : {options.Transport}://{options.Host}:{options.Port}");
Console.WriteLine($"profile     : {options.Profile}");
Console.WriteLine($"summary-json: {options.SummaryJsonPath}");
Console.WriteLine($"cases       : {string.Join(", ", options.Cases.Select(static x => $"{x.Device}[{x.SuccessCount}/{x.FailureCount}]"))}");
Console.WriteLine();

var results = new List<ProbeResult>(capacity: options.Cases.Count * 2);
foreach (var probeCase in options.Cases)
{
    Console.WriteLine($"[write-ok] {probeCase.Device} count={probeCase.SuccessCount}");
    var successResult = ExecuteWriteProbe(probeCase.Device, probeCase.SuccessCount, probeCase.Seed, expectSuccess: true);
    results.Add(successResult);
    Console.WriteLine($"  succeeded={successResult.WriteSucceeded} verify_ok={successResult.VerifyOk} recheck_ok={successResult.RecheckOk} error={successResult.Error ?? "<none>"}");

    Console.WriteLine($"[write-ng] {probeCase.Device} count={probeCase.FailureCount}");
    var failureResult = ExecuteWriteProbe(probeCase.Device, probeCase.FailureCount, probeCase.Seed, expectSuccess: false);
    results.Add(failureResult);
    Console.WriteLine($"  expected_failure={failureResult.ExpectedFailure} changed={failureResult.ChangedWordsAfterFailure} restored={failureResult.RestoredAfterFailure} error={failureResult.Error ?? "<none>"}");
}

await File.WriteAllTextAsync(options.SummaryJsonPath, JsonSerializer.Serialize(results, ProbeJson.Options));

Console.WriteLine();
Console.WriteLine($"summary-json: {options.SummaryJsonPath}");

return;

ProbeResult ExecuteWriteProbe(string device, int count, int seed, bool expectSuccess)
{
    var result = new ProbeResult(device, count, $"0x{seed:X4}", expectSuccess);
    var baseline = Array.Empty<int>();

    using var client = CreateClient();
    try
    {
        baseline = ReadWords(client, device, count);
        var values = BuildRamp(count, seed);

        try
        {
            WriteWords(client, device, values);
            result.WriteSucceeded = true;
            result.VerifyOk = ReadWords(client, device, count).SequenceEqual(values);
            RestoreInChunks(client, device, baseline, options.ChunkSize);
            result.RecheckOk = ReadWords(client, device, count).SequenceEqual(baseline);
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
            result.ExpectedFailure = !expectSuccess;

            try
            {
                var after = ReadAfterFailure(device, count);
                result.ChangedWordsAfterFailure = CountDifferences(baseline, after);

                if (result.ChangedWordsAfterFailure == 0)
                {
                    result.RestoredAfterFailure = true;
                }
                else
                {
                    using var restoreClient = CreateClient();
                    RestoreInChunks(restoreClient, device, baseline, options.ChunkSize);
                    result.RestoredAfterFailure = ReadWords(restoreClient, device, count).SequenceEqual(baseline);
                }
            }
            catch (Exception readEx)
            {
                result.AfterFailureReadError = readEx.Message;
            }
        }
    }
    catch (Exception ex)
    {
        result.Error = ex.Message;
    }

    return result;
}

ToyopucDeviceClient CreateClient()
{
    return new ToyopucDeviceClient(
        options.Host,
        options.Port,
        transport: Enum.Parse<ToyopucTransportMode>(options.Transport, ignoreCase: true),
        timeout: TimeSpan.FromSeconds(options.TimeoutSeconds),
        addressingOptions: options.AddressingOptions,
        deviceProfile: options.Profile);
}

int[] ReadWords(ToyopucDeviceClient client, string device, int count)
{
    var value = client.Read(device, count);
    return value switch
    {
        int word => new[] { word },
        object[] items => items.Select(ToInt32).ToArray(),
        Array array => array.Cast<object>().Select(ToInt32).ToArray(),
        _ => throw new InvalidOperationException($"Unexpected read result type: {value.GetType().FullName}"),
    };
}

int[] ReadAfterFailure(string device, int count)
{
    Thread.Sleep(options.ReconnectDelayMs);
    using var retryClient = CreateClient();
    return ReadWords(retryClient, device, count);
}

void WriteWords(ToyopucDeviceClient client, string device, int[] values)
{
    client.Write(device, values);
}

void RestoreInChunks(ToyopucDeviceClient client, string device, int[] baseline, int chunkSize)
{
    for (var offset = 0; offset < baseline.Length; offset += chunkSize)
    {
        var take = Math.Min(chunkSize, baseline.Length - offset);
        var chunkDevice = OffsetDevice(device, offset);
        var chunk = new int[take];
        Array.Copy(baseline, offset, chunk, 0, take);
        WriteWords(client, chunkDevice, chunk);
    }
}

string OffsetDevice(string device, int delta)
{
    var match = Regex.Match(device, "^(?:(?<prefix>P[123])-)?(?<area>[A-Z]+)(?<num>[0-9A-F]+)$", RegexOptions.CultureInvariant);
    if (!match.Success)
    {
        throw new InvalidOperationException($"Unsupported device format: {device}");
    }

    var prefix = match.Groups["prefix"].Success ? match.Groups["prefix"].Value + "-" : string.Empty;
    var area = match.Groups["area"].Value;
    var numberText = match.Groups["num"].Value;
    var width = numberText.Length;
    var value = int.Parse(numberText, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    return $"{prefix}{area}{(value + delta).ToString($"X{width}", CultureInfo.InvariantCulture)}";
}

int[] BuildRamp(int count, int seed)
{
    var values = new int[count];
    for (var i = 0; i < count; i++)
    {
        values[i] = (seed + i) & 0xFFFF;
    }

    return values;
}

int CountDifferences(int[] left, int[] right)
{
    var count = 0;
    var length = Math.Min(left.Length, right.Length);
    for (var i = 0; i < length; i++)
    {
        if (left[i] != right[i])
        {
            count++;
        }
    }

    return count + Math.Abs(left.Length - right.Length);
}

int ToInt32(object value)
{
    return Convert.ToInt32(value, CultureInfo.InvariantCulture);
}

static void PrintUsage()
{
    Console.WriteLine("Toyopuc safe write-limit confirmation");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project examples\\Toyopuc.WriteLimitProbe -- [options]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --host <name>              default: 192.168.250.100");
    Console.WriteLine("  --port <number>            default: 1025");
    Console.WriteLine("  --protocol <tcp|udp>       default: tcp");
    Console.WriteLine("  --profile <name>           required; no device profile is inferred");
    Console.WriteLine("  --cases <spec,...>         default: P1-D0000:622:623:0x4100,U00000:621:622:0x4200,U08000:621:622:0x4300,EB00000:621:622:0x4400");
    Console.WriteLine("  --summary-json <path>      default: logs\\direct_length_limit_pc10g_rerun\\summary.json");
    Console.WriteLine("  --timeout <seconds>        default: 5.0");
    Console.WriteLine("  --reconnect-delay-ms <ms>  default: 300");
    Console.WriteLine("  --chunk-size <words>       restore chunk size, default: 256");
}

sealed record ProbeCase(string Device, int SuccessCount, int FailureCount, int Seed);

sealed record ProbeOptions
{
    private const string DefaultCases = "P1-D0000:622:623:0x4100,U00000:621:622:0x4200,U08000:621:622:0x4300,EB00000:621:622:0x4400";

    public string Host { get; init; } = "192.168.250.100";

    public int Port { get; init; } = 1025;

    public string Transport { get; init; } = "tcp";

    public string Profile { get; init; } = string.Empty;

    public IReadOnlyList<ProbeCase> Cases { get; init; } = ParseCases(DefaultCases);

    public string SummaryJsonPath { get; init; } = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "logs", "direct_length_limit_pc10g_rerun", "summary.json"));

    public double TimeoutSeconds { get; init; } = 5.0;

    public int ReconnectDelayMs { get; init; } = 300;

    public int ChunkSize { get; init; } = 256;

    public ToyopucAddressingOptions AddressingOptions => ToyopucAddressingOptions.FromProfile(Profile);

    public static ProbeOptions Parse(string[] args)
    {
        var options = new ProbeOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--host":
                    options = options with { Host = ReadValue(args, ref i, arg) };
                    break;
                case "--port":
                    options = options with { Port = ParseInt32(ReadValue(args, ref i, arg)) };
                    break;
                case "--protocol":
                    options = options with { Transport = ReadValue(args, ref i, arg).ToLowerInvariant() };
                    break;
                case "--profile":
                    options = options with { Profile = ReadValue(args, ref i, arg) };
                    break;
                case "--cases":
                    options = options with { Cases = ParseCases(ReadValue(args, ref i, arg)) };
                    break;
                case "--summary-json":
                    options = options with { SummaryJsonPath = Path.GetFullPath(ReadValue(args, ref i, arg), Environment.CurrentDirectory) };
                    break;
                case "--timeout":
                    options = options with { TimeoutSeconds = double.Parse(ReadValue(args, ref i, arg), CultureInfo.InvariantCulture) };
                    break;
                case "--reconnect-delay-ms":
                    options = options with { ReconnectDelayMs = ParseInt32(ReadValue(args, ref i, arg)) };
                    break;
                case "--chunk-size":
                    options = options with { ChunkSize = ParseInt32(ReadValue(args, ref i, arg)) };
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {arg}", nameof(args));
            }
        }

        if (options.Port < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "--port must be >= 1");
        }

        if (options.TimeoutSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "--timeout must be > 0");
        }

        if (options.ReconnectDelayMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "--reconnect-delay-ms must be >= 0");
        }

        if (options.ChunkSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "--chunk-size must be >= 1");
        }

        if (string.IsNullOrWhiteSpace(options.Profile))
        {
            throw new ArgumentException("--profile is required. Specify it explicitly; no device profile is inferred from defaults.", nameof(args));
        }

        _ = options.AddressingOptions;
        return options;
    }

    private static IReadOnlyList<ProbeCase> ParseCases(string value)
    {
        var parts = value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseCase)
            .ToArray();

        if (parts.Length == 0)
        {
            throw new ArgumentException("--cases must contain at least one item", nameof(value));
        }

        return parts;
    }

    private static ProbeCase ParseCase(string value)
    {
        var parts = value.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length != 4)
        {
            throw new ArgumentException($"Invalid case format: {value}", nameof(value));
        }

        return new ProbeCase(
            parts[0].ToUpperInvariant(),
            ParseInt32(parts[1]),
            ParseInt32(parts[2]),
            ParseInt32(parts[3]));
    }

    private static string ReadValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for {option}", nameof(args));
        }

        index++;
        return args[index];
    }

    private static int ParseInt32(string value)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.Parse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        return int.Parse(value, CultureInfo.InvariantCulture);
    }
}

static class ProbeJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
    };
}

sealed class ProbeResult(string device, int count, string seed, bool expectSuccess)
{
    public string Device { get; } = device;

    public int Count { get; } = count;

    public string Seed { get; } = seed;

    public bool ExpectSuccess { get; } = expectSuccess;

    public bool WriteSucceeded { get; set; }

    public bool ExpectedFailure { get; set; }

    public string? Error { get; set; }

    public string? AfterFailureReadError { get; set; }

    public int ChangedWordsAfterFailure { get; set; }

    public bool? RestoredAfterFailure { get; set; }

    public bool? VerifyOk { get; set; }

    public bool? RecheckOk { get; set; }
}
