using System.Globalization;
using System.Text;
using System.Text.Json;
using Toyopuc;

internal static class Program
{
    private static readonly string[] Prefixes = ["P1", "P2", "P3"];

    private static readonly string[] BasicPrefixedAreas =
    [
        "P", "K", "V", "T", "C", "L", "X", "Y", "M",
    ];

    private static readonly string[] DefaultAreas =
    [
        "P",
        "K",
        "V",
        "T",
        "C",
        "L",
        "X",
        "Y",
        "M",
        "EP",
        "EK",
        "EV",
        "ET",
        "EC",
        "EL",
        "EX",
        "EY",
        "EM",
        "GM",
        "GX",
        "GY",
    ];

    private static readonly JsonSerializerOptions SummaryJsonOptions = new() { WriteIndented = true };

    private static readonly ExpectedMismatchRule[] ExpectedMismatchRules =
    [
        new("PC10G:PC10 mode", "P1-V0000", "PC10G direct V low-base packed readback is target-specific at P1-V0000."),
        new("PC10G:PC10 mode", "P1-V00F0", "PC10G direct V low-base packed readback is target-specific at P1-V00F0."),
        new("PC10G:PC10 mode", "P2-V0000", "PC10G direct V low-base packed readback is target-specific at P2-V0000."),
        new("PC10G:PC10 mode", "P3-V0000", "PC10G direct V low-base packed readback is target-specific at P3-V0000."),
        new("PC10G:PC10 mode", "EV0E20", "PC10G direct EV packed readback is target-specific at EV0E20."),
        new("TOYOPUC-Plus:Plus Extended mode", "P1-V0000", "TOYOPUC-Plus direct V packed readback is target-specific at P1-V0000."),
        new("TOYOPUC-Plus:Plus Extended mode", "P1-V0010", "TOYOPUC-Plus direct V packed readback is target-specific at P1-V0010."),
        new("TOYOPUC-Plus:Plus Extended mode", "P1-V00D0", "TOYOPUC-Plus direct V low/high-byte readback is target-specific at P1-V00D0."),
        new("TOYOPUC-Plus:Plus Extended mode", "P1-V00F0", "TOYOPUC-Plus direct V low/high-byte readback is target-specific at P1-V00F0."),
        new("TOYOPUC-Plus:Plus Extended mode", "P2-V0000", "TOYOPUC-Plus direct V packed readback is target-specific at P2-V0000."),
        new("TOYOPUC-Plus:Plus Extended mode", "P2-V0010", "TOYOPUC-Plus direct V packed readback is target-specific at P2-V0010."),
        new("TOYOPUC-Plus:Plus Extended mode", "P3-V0000", "TOYOPUC-Plus direct V low-byte readback is target-specific at P3-V0000."),
        new("TOYOPUC-Plus:Plus Extended mode", "P3-V0010", "TOYOPUC-Plus direct V packed readback is target-specific at P3-V0010."),
        new("TOYOPUC-Plus:Plus Extended mode", "P1-X0000", "TOYOPUC-Plus direct X low-base packed readback is target-specific at P1-X0000."),
        new("TOYOPUC-Plus:Plus Extended mode", "P2-X0000", "TOYOPUC-Plus direct X low-base packed readback is target-specific at P2-X0000."),
        new("TOYOPUC-Plus:Plus Extended mode", "P3-X0000", "TOYOPUC-Plus direct X low-byte readback is target-specific at P3-X0000."),
        new("TOYOPUC-Plus:Plus Extended mode", "P1-Y0000", "TOYOPUC-Plus direct Y low-base packed readback is target-specific at P1-Y0000."),
        new("TOYOPUC-Plus:Plus Extended mode", "P2-Y0000", "TOYOPUC-Plus direct Y low-base packed readback is target-specific at P2-Y0000."),
        new("TOYOPUC-Plus:Plus Extended mode", "P3-Y0000", "TOYOPUC-Plus direct Y low-byte readback is target-specific at P3-Y0000."),
        new("TOYOPUC-Plus:Plus Extended mode", "EV0E20", "TOYOPUC-Plus direct EV low/high-byte readback is target-specific at EV0E20."),
    ];

    public static int Main(string[] args)
    {
        if (args.Contains("--help", StringComparer.OrdinalIgnoreCase)
            || args.Contains("-h", StringComparer.OrdinalIgnoreCase))
        {
            PrintUsage();
            return 0;
        }

        var options = ProbeOptions.Parse(args, DefaultAreas);
        var cases = BuildCases(options);

        Console.WriteLine(FormattableString.Invariant($"connect      : {options.Protocol}://{options.Host}:{options.Port}"));
        Console.WriteLine(FormattableString.Invariant($"profile      : {options.Profile}"));
        Console.WriteLine(FormattableString.Invariant($"sample-count : {options.SampleCount}"));
        Console.WriteLine(FormattableString.Invariant($"retries      : {options.Retries}"));
        Console.WriteLine(FormattableString.Invariant($"retry-delay  : {options.RetryDelaySeconds:F3}s"));
        Console.WriteLine($"groups       : {string.Join(", ", BuildGroupLabels(options.Areas))}");
        Console.WriteLine(FormattableString.Invariant($"cases        : {cases.Count}"));
        if (!string.IsNullOrWhiteSpace(options.CsvPath))
        {
            Console.WriteLine($"csv          : {options.CsvPath}");
        }

        if (!string.IsNullOrWhiteSpace(options.SummaryJsonPath))
        {
            Console.WriteLine($"summary-json : {options.SummaryJsonPath}");
        }

        Console.WriteLine();

        if (options.DryRun)
        {
            foreach (var probeCase in cases)
            {
                Console.WriteLine(
                    FormattableString.Invariant(
                        $"{probeCase.Label,-7} {probeCase.BitDevice,-10} {probeCase.WordDevice,-9} {probeCase.LowDevice,-9} {probeCase.HighDevice,-9} pattern=0x{probeCase.Pattern:X4}"));
            }

            return 0;
        }

        EnsureParentDirectory(options.CsvPath);
        EnsureParentDirectory(options.SummaryJsonPath);

        var startedAt = DateTimeOffset.Now;
        var results = new List<ProbeResult>(cases.Count);

        for (var index = 0; index < cases.Count; index++)
        {
            var probeCase = cases[index];
            var result = ExecuteCase(options, probeCase);
            results.Add(result);
            var errorText = string.IsNullOrWhiteSpace(result.Error) ? string.Empty : $" error={result.Error}";
            var expectedText = result.ExpectedMismatchObserved ? $" expected={result.ExpectedMismatchNote}" : string.Empty;

            Console.WriteLine(
                FormattableString.Invariant(
                    $"[{index + 1,3}/{cases.Count}] {probeCase.Label,-7} {probeCase.BitDevice,-10} pattern=0x{probeCase.Pattern:X4} word={(result.WordMatches ? "OK" : "NG")} low={(result.LowMatches ? "OK" : "NG")} high={(result.HighMatches ? "OK" : "NG")} restore={(result.RestoreMatchesBaseline ? "OK" : "NG")}{expectedText}{errorText}"));
        }

        var finishedAt = DateTimeOffset.Now;
        var summary = BuildSummary(options, cases.Count, results, startedAt, finishedAt);

        if (!string.IsNullOrWhiteSpace(options.CsvPath))
        {
            File.WriteAllText(options.CsvPath, BuildCsv(results), Encoding.UTF8);
        }

        if (!string.IsNullOrWhiteSpace(options.SummaryJsonPath))
        {
            File.WriteAllText(options.SummaryJsonPath, JsonSerializer.Serialize(summary, SummaryJsonOptions));
        }

        Console.WriteLine();
        Console.WriteLine(
            FormattableString.Invariant(
                $"summary      : ok={summary.StrictPassCases} expected={summary.ExpectedMismatchCases} total={summary.PassedCases}/{summary.TotalCases} failed={summary.FailedCases} duration={(finishedAt - startedAt).TotalSeconds:F1}s"));

        if (summary.FailedCases == 0)
        {
            return 0;
        }

        foreach (var failure in results.Where(static x => !x.Passed))
        {
            Console.WriteLine(
                FormattableString.Invariant(
                    $"fail         : {failure.Label} {failure.BitDevice} pattern=0x{failure.Pattern:X4} word={FormatOptionalHex(failure.AfterWord)} low={FormatOptionalHex(failure.AfterLow)} high={FormatOptionalHex(failure.AfterHigh)} error={failure.Error ?? "<none>"}"));
        }

        return 1;
    }

    private static ProbeSummary BuildSummary(
        ProbeOptions options,
        int totalCases,
        IReadOnlyList<ProbeResult> results,
        DateTimeOffset startedAt,
        DateTimeOffset finishedAt)
    {
        var groupSummaries = results
            .GroupBy(static x => x.Label, StringComparer.Ordinal)
            .OrderBy(static x => x.Key, StringComparer.Ordinal)
            .Select(group => new ProbeGroupSummary(
                group.Key,
                group.Count(),
                group.Count(static x => x.StrictPass),
                group.Count(static x => x.ExpectedMismatchObserved),
                group.Count(static x => x.Passed),
                group.Count(static x => !x.Passed)))
            .ToArray();

        return new ProbeSummary(
            options.Host,
            options.Port,
            options.Protocol,
            options.Profile,
            startedAt,
            finishedAt,
            totalCases,
            results.Count(static x => x.Passed),
            results.Count(static x => x.StrictPass),
            results.Count(static x => x.ExpectedMismatchObserved),
            results.Count(static x => !x.Passed),
            groupSummaries,
            results.Where(static x => !x.Passed).ToArray());
    }

    private static ProbeResult ExecuteCase(ProbeOptions options, ProbeCase probeCase)
    {
        var result = new ProbeResult(
            probeCase.Label,
            probeCase.Area,
            probeCase.Prefix,
            probeCase.SampleOrdinal,
            probeCase.BitStart,
            probeCase.PackedIndex,
            probeCase.Pattern,
            probeCase.BitDevice,
            probeCase.WordDevice,
            probeCase.LowDevice,
            probeCase.HighDevice);

        bool[]? baselineBits = null;
        int? baselineWord = null;
        int? baselineLow = null;
        int? baselineHigh = null;
        var restoreNeeded = false;

        try
        {
            using (var client = CreateClient(options))
            {
                baselineBits = ReadBits(client, probeCase.BitDevice);
                (baselineWord, baselineLow, baselineHigh) = ReadPacked(client, probeCase);

                restoreNeeded = true;
                client.Write(probeCase.BitDevice, ToBitValues(probeCase.Pattern));
                (result.AfterWord, result.AfterLow, result.AfterHigh) = ReadPacked(client, probeCase);

                result.WordMatches = result.AfterWord == probeCase.Pattern;
                result.LowMatches = result.AfterLow == (probeCase.Pattern & 0x00FF);
                result.HighMatches = result.AfterHigh == ((probeCase.Pattern >> 8) & 0x00FF);
            }
        }
        catch (Exception exception)
        {
            result.Error = exception.Message;
        }
        finally
        {
            if (restoreNeeded && baselineBits is not null)
            {
                try
                {
                    using var restoreClient = CreateClient(options);
                    restoreClient.Write(probeCase.BitDevice, baselineBits);
                    (result.RestoredWord, result.RestoredLow, result.RestoredHigh) = ReadPacked(restoreClient, probeCase);
                    result.RestoreMatchesBaseline =
                        result.RestoredWord == baselineWord
                        && result.RestoredLow == baselineLow
                        && result.RestoredHigh == baselineHigh;
                }
                catch (Exception restoreException)
                {
                    result.RestoreError = restoreException.Message;
                    result.RestoreMatchesBaseline = false;
                }
            }
        }

        result.BaselineWord = baselineWord;
        result.BaselineLow = baselineLow;
        result.BaselineHigh = baselineHigh;
        if (string.IsNullOrWhiteSpace(result.Error) && !string.IsNullOrWhiteSpace(result.RestoreError))
        {
            result.Error = result.RestoreError;
        }

        ApplyExpectation(options.Profile, result);

        return result;
    }

    private static void ApplyExpectation(string profile, ProbeResult result)
    {
        var cleanExecution = string.IsNullOrWhiteSpace(result.Error) && string.IsNullOrWhiteSpace(result.RestoreError);
        var strictPass =
            result.WordMatches
            && result.LowMatches
            && result.HighMatches
            && result.RestoreMatchesBaseline
            && cleanExecution;

        result.StrictPass = strictPass;

        if (TryGetExpectedMismatchRule(profile, result.BitDevice, out var rule))
        {
            result.ExpectedMismatch = true;
            result.ExpectedMismatchNote = rule.Note;
            result.ExpectedMismatchObserved =
                !strictPass
                && result.RestoreMatchesBaseline
                && cleanExecution;
            result.Passed = result.ExpectedMismatchObserved;

            if (!result.Passed && strictPass)
            {
                result.Error = $"Expected target-specific mismatch was not observed. {rule.Note}";
            }
            else if (!result.Passed && string.IsNullOrWhiteSpace(result.Error))
            {
                result.Error = $"Expected target-specific mismatch did not match the allowed pattern. {rule.Note}";
            }

            return;
        }

        result.Passed = strictPass;
    }

    private static bool TryGetExpectedMismatchRule(string profile, string bitDevice, out ExpectedMismatchRule rule)
    {
        for (var index = 0; index < ExpectedMismatchRules.Length; index++)
        {
            var candidate = ExpectedMismatchRules[index];
            if (candidate.Profile.Equals(profile, StringComparison.Ordinal)
                && candidate.BitDevice.Equals(bitDevice, StringComparison.Ordinal))
            {
                rule = candidate;
                return true;
            }
        }

        rule = default!;
        return false;
    }

    private static ToyopucDeviceClient CreateClient(ProbeOptions options)
    {
        return new ToyopucDeviceClient(
            options.Host,
            options.Port,
            protocol: options.Protocol,
            timeout: options.TimeoutSeconds,
            retries: options.Retries,
            retryDelay: options.RetryDelaySeconds,
            deviceProfile: options.Profile);
    }

    private static IReadOnlyList<ProbeCase> BuildCases(ProbeOptions options)
    {
        var cases = new List<ProbeCase>();
        var familyIndex = 0;

        foreach (var area in options.Areas)
        {
            if (IsBasicPrefixedArea(area))
            {
                for (var prefixIndex = 0; prefixIndex < Prefixes.Length; prefixIndex++)
                {
                    var prefix = Prefixes[prefixIndex];
                    AddCases(cases, options, area, prefix, familyIndex, prefixIndex);
                }
            }
            else
            {
                AddCases(cases, options, area, prefix: null, familyIndex, prefixIndex: -1);
            }

            familyIndex++;
        }

        return cases;
    }

    private static void AddCases(
        List<ProbeCase> cases,
        ProbeOptions options,
        string area,
        string? prefix,
        int familyIndex,
        int prefixIndex)
    {
        var descriptor = ToyopucDeviceCatalog.GetAreaDescriptor(area, options.Profile);
        var prefixed = !string.IsNullOrWhiteSpace(prefix);
        var starts = SelectStarts(descriptor, prefixed, options.Profile, options.SampleCount);

        for (var sampleIndex = 0; sampleIndex < starts.Count; sampleIndex++)
        {
            var bitStart = starts[sampleIndex];
            var packedIndex = bitStart >> 4;
            var pattern = BuildPattern(familyIndex, prefixIndex, sampleIndex, bitStart);
            var label = prefix is null ? area : $"{prefix}-{area}";
            cases.Add(
                new ProbeCase(
                    label,
                    area,
                    prefix,
                    sampleIndex + 1,
                    bitStart,
                    packedIndex,
                    pattern,
                    BuildDevice(prefix, area, bitStart, descriptor.AddressWidth),
                    BuildDevice(prefix, area, packedIndex, descriptor.PackedAddressWidth, "W"),
                    BuildDevice(prefix, area, packedIndex, descriptor.PackedAddressWidth, "L"),
                    BuildDevice(prefix, area, packedIndex, descriptor.PackedAddressWidth, "H")));
        }
    }

    private static IReadOnlyList<int> SelectStarts(
        ToyopucAreaDescriptor descriptor,
        bool prefixed,
        string profile,
        int sampleCount)
    {
        var ranges = ToyopucDeviceCatalog.GetSupportedRanges(descriptor.Area, prefixed, unit: "bit", packed: false, profile);
        var candidates = new List<int>();
        foreach (var range in ranges)
        {
            var lastStart = range.End - 0x000F;
            if (lastStart < range.Start)
            {
                continue;
            }

            for (var value = range.Start; value <= lastStart; value += 0x0010)
            {
                candidates.Add(value);
            }
        }

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException(
                $"No 16-bit aligned bit starts available for {(prefixed ? "prefixed" : "direct")} {descriptor.Area} in {profile}");
        }

        if (candidates.Count <= sampleCount)
        {
            return candidates;
        }

        if (sampleCount == 1)
        {
            return new[] { candidates[0] };
        }

        var selected = new List<int>(sampleCount);
        for (var index = 0; index < sampleCount; index++)
        {
            var candidateIndex = (int)((long)index * (candidates.Count - 1) / (sampleCount - 1));
            var candidate = candidates[candidateIndex];
            if (selected.Count == 0 || selected[^1] != candidate)
            {
                selected.Add(candidate);
            }
        }

        if (selected.Count == sampleCount)
        {
            return selected;
        }

        foreach (var candidate in candidates)
        {
            if (selected.Count == sampleCount)
            {
                break;
            }

            if (!selected.Contains(candidate))
            {
                selected.Add(candidate);
            }
        }

        selected.Sort();
        return selected;
    }

    private static (int Word, int Low, int High) ReadPacked(ToyopucDeviceClient client, ProbeCase probeCase)
    {
        var values = client.ReadMany(new object[] { probeCase.WordDevice, probeCase.LowDevice, probeCase.HighDevice });
        return (ToInt32(values[0]), ToInt32(values[1]), ToInt32(values[2]));
    }

    private static bool[] ReadBits(ToyopucDeviceClient client, string bitDevice)
    {
        var values = client.Read(bitDevice, 16);
        return values switch
        {
            object[] items => items.Select(ToBool).ToArray(),
            _ => throw new InvalidOperationException($"Unexpected bit read result type for {bitDevice}: {values.GetType().FullName}"),
        };
    }

    private static bool[] ToBitValues(int pattern)
    {
        var bits = new bool[16];
        for (var index = 0; index < bits.Length; index++)
        {
            bits[index] = ((pattern >> index) & 0x01) != 0;
        }

        return bits;
    }

    private static int BuildPattern(int familyIndex, int prefixIndex, int sampleIndex, int bitStart)
    {
        var salt = ((familyIndex + 1) << 11) ^ ((prefixIndex + 2) << 7) ^ ((sampleIndex + 1) << 3) ^ (bitStart >> 4);
        var pattern = (0xA55A ^ salt ^ (salt << 5) ^ (salt >> 3)) & 0xFFFF;
        if (pattern == 0x0000 || pattern == 0xFFFF || ((pattern >> 8) & 0xFF) == (pattern & 0xFF))
        {
            pattern ^= 0x5AA5;
        }

        if (pattern == 0x0000 || pattern == 0xFFFF)
        {
            pattern ^= 0x1357;
        }

        return pattern & 0xFFFF;
    }

    private static string BuildDevice(string? prefix, string area, int index, int width, string suffix = "")
    {
        var addressText = index.ToString($"X{width}", CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(prefix)
            ? $"{area}{addressText}{suffix}"
            : $"{prefix}-{area}{addressText}{suffix}";
    }

    private static int ToInt32(object value)
    {
        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static bool ToBool(object value)
    {
        return value switch
        {
            bool flag => flag,
            byte number => number != 0,
            sbyte number => number != 0,
            short number => number != 0,
            ushort number => number != 0,
            int number => number != 0,
            uint number => number != 0,
            long number => number != 0,
            ulong number => number != 0,
            string text when bool.TryParse(text, out var parsed) => parsed,
            _ => throw new InvalidOperationException($"Unexpected bit value type: {value.GetType().FullName}"),
        };
    }

    private static IReadOnlyList<string> BuildGroupLabels(IReadOnlyList<string> areas)
    {
        var labels = new List<string>();
        foreach (var area in areas)
        {
            if (IsBasicPrefixedArea(area))
            {
                labels.AddRange(Prefixes.Select(prefix => $"{prefix}-{area}"));
            }
            else
            {
                labels.Add(area);
            }
        }

        return labels;
    }

    private static bool IsBasicPrefixedArea(string area)
    {
        return Array.IndexOf(BasicPrefixedAreas, area) >= 0;
    }

    private static string BuildCsv(IReadOnlyList<ProbeResult> results)
    {
        var builder = new StringBuilder();
        builder.AppendLine("label,area,prefix,sample_ordinal,bit_start,packed_index,pattern,bit_device,word_device,low_device,high_device,baseline_word,baseline_low,baseline_high,after_word,after_low,after_high,restored_word,restored_low,restored_high,strict_pass,expected_mismatch,expected_mismatch_observed,word_matches,low_matches,high_matches,restore_matches_baseline,passed,expected_note,error,restore_error");
        foreach (var result in results)
        {
            builder
                .Append(Csv(result.Label)).Append(',')
                .Append(Csv(result.Area)).Append(',')
                .Append(Csv(result.Prefix ?? string.Empty)).Append(',')
                .Append(result.SampleOrdinal.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append("0x").Append(result.BitStart.ToString("X4", CultureInfo.InvariantCulture)).Append(',')
                .Append("0x").Append(result.PackedIndex.ToString("X3", CultureInfo.InvariantCulture)).Append(',')
                .Append("0x").Append(result.Pattern.ToString("X4", CultureInfo.InvariantCulture)).Append(',')
                .Append(Csv(result.BitDevice)).Append(',')
                .Append(Csv(result.WordDevice)).Append(',')
                .Append(Csv(result.LowDevice)).Append(',')
                .Append(Csv(result.HighDevice)).Append(',')
                .Append(Csv(FormatOptionalHex(result.BaselineWord))).Append(',')
                .Append(Csv(FormatOptionalHex(result.BaselineLow))).Append(',')
                .Append(Csv(FormatOptionalHex(result.BaselineHigh))).Append(',')
                .Append(Csv(FormatOptionalHex(result.AfterWord))).Append(',')
                .Append(Csv(FormatOptionalHex(result.AfterLow))).Append(',')
                .Append(Csv(FormatOptionalHex(result.AfterHigh))).Append(',')
                .Append(Csv(FormatOptionalHex(result.RestoredWord))).Append(',')
                .Append(Csv(FormatOptionalHex(result.RestoredLow))).Append(',')
                .Append(Csv(FormatOptionalHex(result.RestoredHigh))).Append(',')
                .Append(result.StrictPass ? "true" : "false").Append(',')
                .Append(result.ExpectedMismatch ? "true" : "false").Append(',')
                .Append(result.ExpectedMismatchObserved ? "true" : "false").Append(',')
                .Append(result.WordMatches ? "true" : "false").Append(',')
                .Append(result.LowMatches ? "true" : "false").Append(',')
                .Append(result.HighMatches ? "true" : "false").Append(',')
                .Append(result.RestoreMatchesBaseline ? "true" : "false").Append(',')
                .Append(result.Passed ? "true" : "false").Append(',')
                .Append(Csv(result.ExpectedMismatchNote ?? string.Empty)).Append(',')
                .Append(Csv(result.Error ?? string.Empty)).Append(',')
                .Append(Csv(result.RestoreError ?? string.Empty))
                .AppendLine();
        }

        return builder.ToString();
    }

    private static string Csv(string value)
    {
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static string FormatOptionalHex(int? value)
    {
        return value is null
            ? string.Empty
            : "0x" + value.Value.ToString("X4", CultureInfo.InvariantCulture);
    }

    private static void EnsureParentDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(parent);
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Toyopuc bit-to-packed readback probe");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project examples\\Toyopuc.BitPatternProbe -- [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --host <name>           default: 192.168.250.101");
        Console.WriteLine("  --port <number>         default: 1025");
        Console.WriteLine("  --protocol <tcp|udp>    default: tcp");
        Console.WriteLine("  --profile <name>        default: PC10G:PC10 mode");
        Console.WriteLine("  --areas <csv>           default: P,K,V,T,C,L,X,Y,M,EP,EK,EV,ET,EC,EL,EX,EY,EM,GM,GX,GY");
        Console.WriteLine("  --sample-count <n>      default: 10");
        Console.WriteLine("  --timeout <seconds>     default: 5.0");
        Console.WriteLine("  --retries <count>       default: 1");
        Console.WriteLine("  --retry-delay <seconds> default: 0.2");
        Console.WriteLine("  --csv <path>            default: logs\\bit_pattern_pc10g_direct.csv");
        Console.WriteLine("  --summary-json <path>   default: logs\\bit_pattern_pc10g_direct.json");
        Console.WriteLine("  --dry-run               list planned cases without touching the PLC");
    }
}

internal sealed record ProbeOptions
{
    public string Host { get; init; } = "192.168.250.101";

    public int Port { get; init; } = 1025;

    public string Protocol { get; init; } = "tcp";

    public string Profile { get; init; } = "PC10G:PC10 mode";

    public IReadOnlyList<string> Areas { get; init; } = Array.Empty<string>();

    public int SampleCount { get; init; } = 10;

    public double TimeoutSeconds { get; init; } = 5.0;

    public int Retries { get; init; } = 1;

    public double RetryDelaySeconds { get; init; } = 0.2;

    public string CsvPath { get; init; } = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "logs", "bit_pattern_pc10g_direct.csv"));

    public string SummaryJsonPath { get; init; } = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "logs", "bit_pattern_pc10g_direct.json"));

    public bool DryRun { get; init; }

    public static ProbeOptions Parse(string[] args, IReadOnlyList<string> defaultAreas)
    {
        var options = new ProbeOptions { Areas = defaultAreas.ToArray() };
        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--host":
                    options = options with { Host = ReadValue(args, ref index, arg) };
                    break;
                case "--port":
                    options = options with { Port = ParseInt32(ReadValue(args, ref index, arg)) };
                    break;
                case "--protocol":
                    options = options with { Protocol = ReadValue(args, ref index, arg).ToLowerInvariant() };
                    break;
                case "--profile":
                    options = options with { Profile = ReadValue(args, ref index, arg) };
                    break;
                case "--areas":
                    options = options with { Areas = ParseAreas(ReadValue(args, ref index, arg), defaultAreas) };
                    break;
                case "--sample-count":
                    options = options with { SampleCount = ParseInt32(ReadValue(args, ref index, arg)) };
                    break;
                case "--timeout":
                    options = options with { TimeoutSeconds = ParseDouble(ReadValue(args, ref index, arg)) };
                    break;
                case "--retries":
                    options = options with { Retries = ParseInt32(ReadValue(args, ref index, arg)) };
                    break;
                case "--retry-delay":
                    options = options with { RetryDelaySeconds = ParseDouble(ReadValue(args, ref index, arg)) };
                    break;
                case "--csv":
                    options = options with { CsvPath = Path.GetFullPath(ReadValue(args, ref index, arg), Environment.CurrentDirectory) };
                    break;
                case "--summary-json":
                    options = options with { SummaryJsonPath = Path.GetFullPath(ReadValue(args, ref index, arg), Environment.CurrentDirectory) };
                    break;
                case "--dry-run":
                    options = options with { DryRun = true };
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {arg}", nameof(args));
            }
        }

        if (options.SampleCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "--sample-count must be >= 1");
        }

        if (options.Retries < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "--retries must be >= 0");
        }

        if (options.RetryDelaySeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "--retry-delay must be >= 0");
        }

        return options with { Profile = ToyopucDeviceProfiles.NormalizeName(options.Profile) };
    }

    private static string ReadValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for {optionName}", nameof(args));
        }

        index++;
        return args[index];
    }

    private static int ParseInt32(string value)
    {
        return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? int.Parse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            : int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    private static double ParseDouble(string value)
    {
        return double.Parse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
    }

    private static IReadOnlyList<string> ParseAreas(string value, IReadOnlyList<string> defaultAreas)
    {
        var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static x => x.ToUpperInvariant())
            .ToArray();
        if (parts.Length == 0)
        {
            throw new ArgumentException("areas must not be empty", nameof(value));
        }

        foreach (var part in parts)
        {
            if (!defaultAreas.Contains(part, StringComparer.Ordinal))
            {
                throw new ArgumentException($"Unsupported area in --areas: {part}", nameof(value));
            }
        }

        return parts;
    }
}

internal sealed record ProbeCase(
    string Label,
    string Area,
    string? Prefix,
    int SampleOrdinal,
    int BitStart,
    int PackedIndex,
    int Pattern,
    string BitDevice,
    string WordDevice,
    string LowDevice,
    string HighDevice);

internal sealed record ProbeResult(
    string Label,
    string Area,
    string? Prefix,
    int SampleOrdinal,
    int BitStart,
    int PackedIndex,
    int Pattern,
    string BitDevice,
    string WordDevice,
    string LowDevice,
    string HighDevice)
{
    public int? BaselineWord { get; set; }

    public int? BaselineLow { get; set; }

    public int? BaselineHigh { get; set; }

    public int? AfterWord { get; set; }

    public int? AfterLow { get; set; }

    public int? AfterHigh { get; set; }

    public int? RestoredWord { get; set; }

    public int? RestoredLow { get; set; }

    public int? RestoredHigh { get; set; }

    public bool WordMatches { get; set; }

    public bool LowMatches { get; set; }

    public bool HighMatches { get; set; }

    public bool RestoreMatchesBaseline { get; set; }

    public bool StrictPass { get; set; }

    public bool ExpectedMismatch { get; set; }

    public bool ExpectedMismatchObserved { get; set; }

    public bool Passed { get; set; }

    public string? ExpectedMismatchNote { get; set; }

    public string? Error { get; set; }

    public string? RestoreError { get; set; }
}

internal sealed record ProbeSummary(
    string Host,
    int Port,
    string Protocol,
    string Profile,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    int TotalCases,
    int PassedCases,
    int StrictPassCases,
    int ExpectedMismatchCases,
    int FailedCases,
    IReadOnlyList<ProbeGroupSummary> Groups,
    IReadOnlyList<ProbeResult> Failures);

internal sealed record ProbeGroupSummary(
    string Label,
    int TotalCases,
    int StrictPassCases,
    int ExpectedMismatchCases,
    int PassedCases,
    int FailedCases);

internal sealed record ExpectedMismatchRule(
    string Profile,
    string BitDevice,
    string Note);
