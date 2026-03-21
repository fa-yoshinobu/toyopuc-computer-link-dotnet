using System.Globalization;
using System.Text;
using System.Text.Json;
using PlcComm.Toyopuc;

return await RunAsync(args).ConfigureAwait(false);

static async Task<int> RunAsync(string[] args)
{
    ToyopucDeviceClient? plc = null;
    SoakLogger? logger = null;
    PollCsvWriter? pollCsv = null;
    var cancelSource = new CancellationTokenSource();
    ConsoleCancelEventHandler? cancelHandler = null;

    try
    {
        var options = SoakMonitorOptions.Parse(args);
        if (options.ShowHelp)
        {
            SoakMonitorOptions.PrintUsage();
            return 0;
        }

        logger = new SoakLogger(options.LogPath);
        pollCsv = new PollCsvWriter(options.PollCsvPath, options.Devices);
        var summary = new SoakRunSummary
        {
            StartedAtUtc = DateTimeOffset.UtcNow,
        };

        cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancelSource.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        var addressingOptions = options.BuildAddressingOptions();
        logger.Info($"connect : {options.Transport}://{options.Host}:{options.Port}");
        if (options.LocalPort != 0)
        {
            logger.Info($"local   : {options.LocalPort}");
        }

        if (!string.IsNullOrWhiteSpace(options.Hops))
        {
            logger.Info($"relay   : {options.Hops}");
        }

        if (!string.IsNullOrWhiteSpace(options.LogPath))
        {
            logger.Info($"log     : {options.LogPath}");
        }

        if (!string.IsNullOrWhiteSpace(options.PollCsvPath))
        {
            logger.Info($"pollcsv : {options.PollCsvPath}");
        }

        if (!string.IsNullOrWhiteSpace(options.SummaryJsonPath))
        {
            logger.Info($"summary : {options.SummaryJsonPath}");
        }

        if (options.ShouldLogAddressingProfile)
        {
            logger.Info($"profile : {options.Profile}");
            logger.Info(
                $"pc10    : upper-u={(addressingOptions.UseUpperUPc10 ? "on" : "off")} eb={(addressingOptions.UseEbPc10 ? "on" : "off")} fr={(addressingOptions.UseFrPc10 ? "on" : "off")}");
        }

        logger.Info(
            $"monitor : interval={FormatTimeSpan(options.Interval)} duration={(options.Duration is null ? "infinite" : FormatTimeSpan(options.Duration.Value))} reconnect={(options.AutoReconnect ? "on" : "off")} max-consecutive-failures={options.MaxConsecutiveFailures}");
        logger.Info($"devices : {string.Join(", ", options.Devices)}");

        var deadlineUtc = options.Duration is null ? (DateTimeOffset?)null : DateTimeOffset.UtcNow + options.Duration.Value;
        var consecutiveFailures = 0;
        var pollNumber = 0;
        var deviceObjects = options.Devices.Select(static device => (object)device).ToArray();

        while (!cancelSource.IsCancellationRequested)
        {
            if (deadlineUtc is not null && DateTimeOffset.UtcNow >= deadlineUtc.Value)
            {
                summary.StopReason = "duration-complete";
                break;
            }

            if (plc is null)
            {
                plc = CreateClient(options, addressingOptions);
                summary.ConnectionAttempts++;
                logger.Info($"session  : open #{summary.ConnectionAttempts}");
            }

            pollNumber++;
            summary.TotalPolls++;
            var pollStartedUtc = DateTimeOffset.UtcNow;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var values = string.IsNullOrWhiteSpace(options.Hops)
                    ? plc.ReadMany(deviceObjects)
                    : plc.RelayReadMany(options.Hops, deviceObjects);

                stopwatch.Stop();
                summary.SuccessfulPolls++;
                summary.LastSuccessAtUtc = DateTimeOffset.UtcNow;
                consecutiveFailures = 0;

                if (ShouldLogSuccess(options, pollNumber))
                {
                    logger.Info(
                        $"ok      : poll={pollNumber} elapsed={stopwatch.ElapsedMilliseconds}ms values={FormatManyResult(options.Devices, values)}");
                }

                pollCsv?.WriteSuccess(pollStartedUtc, pollNumber, stopwatch.ElapsedMilliseconds, values);
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                summary.FailedPolls++;
                summary.LastFailureAtUtc = DateTimeOffset.UtcNow;
                summary.LastError = exception.Message;
                consecutiveFailures++;

                logger.Error(
                    $"fail    : poll={pollNumber} consecutive={consecutiveFailures} elapsed={stopwatch.ElapsedMilliseconds}ms error={exception.Message}");
                pollCsv?.WriteFailure(pollStartedUtc, pollNumber, stopwatch.ElapsedMilliseconds, exception.Message);

                if (!options.AutoReconnect)
                {
                    summary.StopReason = "failure-no-reconnect";
                    break;
                }

                if (consecutiveFailures >= options.MaxConsecutiveFailures)
                {
                    summary.StopReason = "max-consecutive-failures";
                    break;
                }

                summary.Reconnects++;
                plc.Dispose();
                plc = null;

                if (options.ReconnectDelay > TimeSpan.Zero)
                {
                    logger.Error(
                        $"reconnect: wait={FormatTimeSpan(options.ReconnectDelay)} next-attempt={summary.ConnectionAttempts + 1}");
                    await DelayAsync(options.ReconnectDelay, cancelSource.Token).ConfigureAwait(false);
                }

                continue;
            }

            var remaining = options.Interval - stopwatch.Elapsed;
            if (remaining > TimeSpan.Zero)
            {
                await DelayAsync(remaining, cancelSource.Token).ConfigureAwait(false);
            }
        }

        summary.CompletedAtUtc = DateTimeOffset.UtcNow;
        if (summary.StopReason is null)
        {
            summary.StopReason = cancelSource.IsCancellationRequested ? "cancelled" : "completed";
        }

        logger.Info(
            $"summary : stop={summary.StopReason} polls={summary.TotalPolls} ok={summary.SuccessfulPolls} ng={summary.FailedPolls} reconnects={summary.Reconnects} sessions={summary.ConnectionAttempts} elapsed={FormatTimeSpan(summary.CompletedAtUtc - summary.StartedAtUtc)}");
        if (!string.IsNullOrWhiteSpace(summary.LastError))
        {
            logger.Error($"lasterr  : {summary.LastError}");
        }

        WriteSummaryJson(options, summary);
        return summary.FailedPolls == 0 ? 0 : 1;
    }
    catch (OperationCanceledException)
    {
        return 0;
    }
    catch (Exception exception)
    {
        logger ??= new SoakLogger(null);
        logger.Error($"error   : {exception.Message}");
        return 1;
    }
    finally
    {
        if (cancelHandler is not null)
        {
            Console.CancelKeyPress -= cancelHandler;
        }

        plc?.Dispose();
        pollCsv?.Dispose();
        logger?.Dispose();
        cancelSource.Dispose();
    }
}

static ToyopucDeviceClient CreateClient(SoakMonitorOptions options, ToyopucAddressingOptions addressingOptions)
{
    return new ToyopucDeviceClient(
        options.Host,
        options.Port,
        localPort: options.LocalPort,
        transport: Enum.Parse<ToyopucTransportMode>(options.Transport, ignoreCase: true),
        timeout: TimeSpan.FromSeconds(options.Timeout),
        retries: options.Retries,
        addressingOptions: addressingOptions,
        deviceProfile: options.Profile);
}

static bool ShouldLogSuccess(SoakMonitorOptions options, int pollNumber)
{
    return pollNumber == 1 || options.Verbose || pollNumber % options.SuccessLogInterval == 0;
}

static async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
{
    try
    {
        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
    }
    catch (TaskCanceledException)
    {
    }
}

static void WriteSummaryJson(SoakMonitorOptions options, SoakRunSummary summary)
{
    if (string.IsNullOrWhiteSpace(options.SummaryJsonPath))
    {
        return;
    }

    var fullPath = Path.GetFullPath(options.SummaryJsonPath);
    var directory = Path.GetDirectoryName(fullPath);
    if (!string.IsNullOrEmpty(directory))
    {
        Directory.CreateDirectory(directory);
    }

    var payload = new
    {
        started_at_utc = summary.StartedAtUtc,
        completed_at_utc = summary.CompletedAtUtc,
        elapsed = (summary.CompletedAtUtc - summary.StartedAtUtc).ToString(),
        stop_reason = summary.StopReason,
        total_polls = summary.TotalPolls,
        successful_polls = summary.SuccessfulPolls,
        failed_polls = summary.FailedPolls,
        reconnects = summary.Reconnects,
        connection_attempts = summary.ConnectionAttempts,
        last_success_at_utc = summary.LastSuccessAtUtc,
        last_failure_at_utc = summary.LastFailureAtUtc,
        last_error = summary.LastError,
        config = new
        {
            options.Host,
            options.Port,
            options.Transport,
            options.LocalPort,
            options.Timeout,
            options.Retries,
            options.Profile,
            options.Hops,
            options.Devices,
            interval = options.Interval.ToString(),
            duration = options.Duration?.ToString(),
            reconnect_delay = options.ReconnectDelay.ToString(),
            options.MaxConsecutiveFailures,
            options.AutoReconnect,
        },
    };

    var json = JsonSerializer.Serialize(payload, SoakJson.Options);
    File.WriteAllText(fullPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
}

static string FormatManyResult(IReadOnlyList<string> devices, IReadOnlyList<object> values)
{
    return SoakFormatting.FormatManyResult(devices, values);
}

static string FormatTimeSpan(TimeSpan value)
{
    return value.TotalHours >= 1
        ? value.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)
        : value.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
}

internal sealed record SoakMonitorOptions
{
    public string Host { get; private init; } = "127.0.0.1";
    public int Port { get; private init; } = 15000;
    public string Transport { get; private init; } = "tcp";
    public int LocalPort { get; private init; }
    public double Timeout { get; private init; } = 3.0;
    public int Retries { get; private init; }
    public string Profile { get; private init; } = "Generic";
    public bool? UseUpperUPc10 { get; private init; }
    public bool? UseEbPc10 { get; private init; }
    public bool? UseFrPc10 { get; private init; }
    public string Hops { get; private init; } = string.Empty;
    public string[] Devices { get; private init; } = Array.Empty<string>();
    public TimeSpan Interval { get; private init; } = TimeSpan.FromSeconds(1);
    public TimeSpan? Duration { get; private init; }
    public TimeSpan ReconnectDelay { get; private init; } = TimeSpan.FromSeconds(2);
    public int MaxConsecutiveFailures { get; private init; } = 5;
    public bool AutoReconnect { get; private init; } = true;
    public int SuccessLogInterval { get; private init; } = 1;
    public string? LogPath { get; private init; }
    public string? PollCsvPath { get; private init; }
    public string? SummaryJsonPath { get; private init; }
    public bool Verbose { get; private init; }
    public bool ShowHelp { get; private init; }

    public bool ShouldLogAddressingProfile =>
        !string.Equals(Profile, "Generic", StringComparison.OrdinalIgnoreCase)
        || UseUpperUPc10 is not null
        || UseEbPc10 is not null
        || UseFrPc10 is not null;

    public static SoakMonitorOptions Parse(string[] args)
    {
        var options = new SoakMonitorOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--help":
                case "-h":
                    options = options with { ShowHelp = true };
                    break;
                case "--host":
                    options = options with { Host = ReadValue(args, ref i, arg) };
                    break;
                case "--port":
                    options = options with { Port = ParseInt32(ReadValue(args, ref i, arg)) };
                    break;
                case "--protocol":
                    options = options with { Transport = ReadValue(args, ref i, arg).ToLowerInvariant() };
                    break;
                case "--local-port":
                    options = options with { LocalPort = ParseInt32(ReadValue(args, ref i, arg)) };
                    break;
                case "--timeout":
                    options = options with { Timeout = double.Parse(ReadValue(args, ref i, arg), CultureInfo.InvariantCulture) };
                    break;
                case "--retries":
                    options = options with { Retries = ParseInt32(ReadValue(args, ref i, arg)) };
                    break;
                case "--profile":
                    options = options with { Profile = ReadValue(args, ref i, arg) };
                    break;
                case "--enable-u-pc10":
                    options = options with { UseUpperUPc10 = true };
                    break;
                case "--disable-u-pc10":
                    options = options with { UseUpperUPc10 = false };
                    break;
                case "--enable-eb-pc10":
                    options = options with { UseEbPc10 = true };
                    break;
                case "--disable-eb-pc10":
                    options = options with { UseEbPc10 = false };
                    break;
                case "--enable-fr-pc10":
                    options = options with { UseFrPc10 = true };
                    break;
                case "--disable-fr-pc10":
                    options = options with { UseFrPc10 = false };
                    break;
                case "--hops":
                    options = options with { Hops = ReadValue(args, ref i, arg) };
                    break;
                case "--devices":
                    options = options with { Devices = ParseDeviceList(ReadValue(args, ref i, arg)) };
                    break;
                case "--interval":
                    options = options with { Interval = ParseDuration(ReadValue(args, ref i, arg)) };
                    break;
                case "--duration":
                    options = options with { Duration = ParseDuration(ReadValue(args, ref i, arg)) };
                    break;
                case "--reconnect-delay":
                    options = options with { ReconnectDelay = ParseDuration(ReadValue(args, ref i, arg)) };
                    break;
                case "--max-consecutive-failures":
                    options = options with { MaxConsecutiveFailures = ParseInt32(ReadValue(args, ref i, arg)) };
                    break;
                case "--no-auto-reconnect":
                    options = options with { AutoReconnect = false };
                    break;
                case "--success-log-interval":
                    options = options with { SuccessLogInterval = ParseInt32(ReadValue(args, ref i, arg)) };
                    break;
                case "--log":
                    options = options with { LogPath = ReadValue(args, ref i, arg) };
                    break;
                case "--poll-csv":
                    options = options with { PollCsvPath = ReadValue(args, ref i, arg) };
                    break;
                case "--summary-json":
                    options = options with { SummaryJsonPath = ReadValue(args, ref i, arg) };
                    break;
                case "--verbose":
                    options = options with { Verbose = true };
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {arg}");
            }
        }

        if (options.ShowHelp)
        {
            return options;
        }

        if (options.Devices.Length == 0)
        {
            throw new ArgumentException("--devices is required");
        }

        if (options.Interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "--interval must be > 0");
        }

        if (options.Duration is not null && options.Duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "--duration must be > 0");
        }

        if (options.ReconnectDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "--reconnect-delay must be >= 0");
        }

        if (options.MaxConsecutiveFailures < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "--max-consecutive-failures must be >= 1");
        }

        if (options.SuccessLogInterval < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "--success-log-interval must be >= 1");
        }

        _ = options.BuildAddressingOptions();
        return options;
    }

    public ToyopucAddressingOptions BuildAddressingOptions()
    {
        var options = ToyopucAddressingOptions.FromProfile(Profile);
        return options with
        {
            UseUpperUPc10 = UseUpperUPc10 ?? options.UseUpperUPc10,
            UseEbPc10 = UseEbPc10 ?? options.UseEbPc10,
            UseFrPc10 = UseFrPc10 ?? options.UseFrPc10,
        };
    }

    public static void PrintUsage()
    {
        Console.WriteLine("Toyopuc soak monitor");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --host <host>                   default: 127.0.0.1");
        Console.WriteLine("  --port <port>                   default: 15000");
        Console.WriteLine("  --protocol <tcp|udp>            default: tcp");
        Console.WriteLine("  --local-port <port>             default: 0");
        Console.WriteLine("  --timeout <seconds>             default: 3.0");
        Console.WriteLine("  --retries <count>               default: 0");
        Console.WriteLine("  --profile <name>                addressing profile, default: Generic");
        Console.WriteLine("  --enable-u-pc10                 force PC10 for U08000+");
        Console.WriteLine("  --disable-u-pc10                disable PC10 for U08000+");
        Console.WriteLine("  --enable-eb-pc10                force PC10 for EB");
        Console.WriteLine("  --disable-eb-pc10               disable PC10 for EB");
        Console.WriteLine("  --enable-fr-pc10                force PC10 for FR");
        Console.WriteLine("  --disable-fr-pc10               disable PC10 for FR");
        Console.WriteLine("  --hops <relay hops>             optional relay path, e.g. P1-L2:N2");
        Console.WriteLine("  --devices <a,b,c>               required fixed device set to poll");
        Console.WriteLine("  --interval <time>               poll interval, default: 1s");
        Console.WriteLine("  --duration <time>               optional total run time; omit for infinite");
        Console.WriteLine("  --reconnect-delay <time>        delay after a failed poll, default: 2s");
        Console.WriteLine("  --max-consecutive-failures <n>  stop after n failures, default: 5");
        Console.WriteLine("  --no-auto-reconnect             stop on first failed poll");
        Console.WriteLine("  --success-log-interval <n>      log every nth success, default: 1");
        Console.WriteLine("  --log <path>                    optional text log file");
        Console.WriteLine("  --poll-csv <path>               optional per-poll CSV");
        Console.WriteLine("  --summary-json <path>           optional JSON summary");
        Console.WriteLine("  --verbose                       log every successful poll");
        Console.WriteLine();
        Console.WriteLine("Time syntax examples:");
        Console.WriteLine("  500ms   2s   1m   2h   00:30:00");
        Console.WriteLine();
        Console.WriteLine("Example:");
        Console.WriteLine("  dotnet run --project examples\\Toyopuc.SoakMonitor -- --host 192.168.250.101 --port 1025 --protocol tcp --profile \"Nano 10GX:Compatible mode\" --hops \"P1-L2:N4,P1-L2:N6,P1-L2:N2\" --devices P1-D0000,P1-M0000,U08000 --interval 2s --duration 30m --retries 3 --log logs\\soak.log --poll-csv logs\\soak.csv --summary-json logs\\soak_summary.json");
    }

    private static string ReadValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for {option}");
        }

        index++;
        return args[index];
    }

    private static int ParseInt32(string value)
    {
        return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? Convert.ToInt32(value[2..], 16)
            : int.Parse(value, CultureInfo.InvariantCulture);
    }

    private static string[] ParseDeviceList(string value)
    {
        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static device => device.ToUpperInvariant())
            .ToArray();
    }

    private static TimeSpan ParseDuration(string value)
    {
        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        if (value.EndsWith("ms", StringComparison.OrdinalIgnoreCase))
        {
            return TimeSpan.FromMilliseconds(double.Parse(value[..^2], CultureInfo.InvariantCulture));
        }

        if (value.EndsWith("s", StringComparison.OrdinalIgnoreCase))
        {
            return TimeSpan.FromSeconds(double.Parse(value[..^1], CultureInfo.InvariantCulture));
        }

        if (value.EndsWith("m", StringComparison.OrdinalIgnoreCase))
        {
            return TimeSpan.FromMinutes(double.Parse(value[..^1], CultureInfo.InvariantCulture));
        }

        if (value.EndsWith("h", StringComparison.OrdinalIgnoreCase))
        {
            return TimeSpan.FromHours(double.Parse(value[..^1], CultureInfo.InvariantCulture));
        }

        if (value.EndsWith("d", StringComparison.OrdinalIgnoreCase))
        {
            return TimeSpan.FromDays(double.Parse(value[..^1], CultureInfo.InvariantCulture));
        }

        return TimeSpan.FromSeconds(double.Parse(value, CultureInfo.InvariantCulture));
    }
}

internal sealed class SoakRunSummary
{
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
    public int TotalPolls { get; set; }
    public int SuccessfulPolls { get; set; }
    public int FailedPolls { get; set; }
    public int Reconnects { get; set; }
    public int ConnectionAttempts { get; set; }
    public DateTimeOffset? LastSuccessAtUtc { get; set; }
    public DateTimeOffset? LastFailureAtUtc { get; set; }
    public string? LastError { get; set; }
    public string? StopReason { get; set; }
}

internal static class SoakJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
    };
}

internal static class SoakFormatting
{
    public static string FormatManyResult(IReadOnlyList<string> devices, IReadOnlyList<object> values)
    {
        if (devices.Count != values.Count)
        {
            return $"device/value-count-mismatch devices={devices.Count} values={values.Count}";
        }

        var pairs = new string[devices.Count];
        for (var i = 0; i < devices.Count; i++)
        {
            pairs[i] = $"{devices[i]}={FormatValue(values[i])}";
        }

        return string.Join(", ", pairs);
    }

    public static string FormatValue(object value)
    {
        return value switch
        {
            bool bit => bit ? "1" : "0",
            byte b => $"0x{b:X2}",
            int word => $"0x{word:X4}",
            Array array => "[" + string.Join(", ", array.Cast<object>().Select(FormatValue)) + "]",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
        };
    }
}

internal sealed class SoakLogger : IDisposable
{
    private readonly StreamWriter? _writer;

    public SoakLogger(string? logPath)
    {
        if (string.IsNullOrWhiteSpace(logPath))
        {
            return;
        }

        var fullPath = Path.GetFullPath(logPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(fullPath, append: true, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true,
        };
    }

    public void Info(string message)
    {
        WriteLine(message, isError: false);
    }

    public void Error(string message)
    {
        WriteLine(message, isError: true);
    }

    public void Dispose()
    {
        _writer?.Dispose();
    }

    private void WriteLine(string message, bool isError)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}";
        if (isError)
        {
            Console.Error.WriteLine(line);
        }
        else
        {
            Console.WriteLine(line);
        }

        _writer?.WriteLine(line);
    }
}

internal sealed class PollCsvWriter : IDisposable
{
    private readonly StreamWriter? _writer;
    private readonly IReadOnlyList<string> _devices;

    public PollCsvWriter(string? path, IReadOnlyList<string> devices)
    {
        _devices = devices;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(fullPath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true,
        };
        _writer.WriteLine("timestamp_utc,poll,status,elapsed_ms,error,values");
    }

    public void WriteSuccess(DateTimeOffset timestamp, int poll, long elapsedMilliseconds, IReadOnlyList<object> values)
    {
        if (_writer is null)
        {
            return;
        }

        _writer.WriteLine(
            $"{timestamp:O},{poll},ok,{elapsedMilliseconds},,{Escape(SoakFormatting.FormatManyResult(_devices, values))}");
    }

    public void WriteFailure(DateTimeOffset timestamp, int poll, long elapsedMilliseconds, string error)
    {
        if (_writer is null)
        {
            return;
        }

        _writer.WriteLine(
            $"{timestamp:O},{poll},ng,{elapsedMilliseconds},{Escape(error)},");
    }

    public void Dispose()
    {
        _writer?.Dispose();
    }

    private static string Escape(string value)
    {
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
