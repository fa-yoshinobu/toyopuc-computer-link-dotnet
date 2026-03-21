using System.Globalization;

namespace PlcComm.Toyopuc.DeviceMonitor;

internal static class DeviceMonitorCatalogHelper
{
    private static readonly string[] PrefixedProgramChoices = ["P1", "P2", "P3", "Direct"];
    private static readonly string[] DirectProgramChoice = ["Direct"];

    public static string NormalizeProfile(string? profile)
    {
        try
        {
            return ToyopucDeviceProfiles.NormalizeName(profile);
        }
        catch
        {
            return ToyopucDeviceProfiles.Generic.Name;
        }
    }

    public static DeviceMonitorProgramChoices GetProgramChoices(string? profile, string? currentProgram = null)
    {
        var normalizedProfile = NormalizeProfile(profile);
        var choices = ToyopucDeviceCatalog.GetAreas(prefixed: true, normalizedProfile).Count > 0
            ? PrefixedProgramChoices
            : DirectProgramChoice;
        var selected = choices.Contains(NormalizeProgram(currentProgram), StringComparer.Ordinal)
            ? NormalizeProgram(currentProgram)
            : "Direct";

        return new DeviceMonitorProgramChoices(choices, selected, choices.Length > 1);
    }

    public static DeviceMonitorAreaChoices GetAreaChoices(string? profile, string? program, string? currentArea = null)
    {
        var normalizedProfile = NormalizeProfile(profile);
        var areas = ToyopucDeviceCatalog.GetAreas(prefixed: IsPrefixedProgram(program), normalizedProfile);
        var normalizedArea = string.IsNullOrWhiteSpace(currentArea) ? string.Empty : currentArea.Trim().ToUpperInvariant();
        var selected = areas.Count == 0
            ? string.Empty
            : areas.Contains(normalizedArea, StringComparer.Ordinal)
                ? normalizedArea
                : areas[0];

        return new DeviceMonitorAreaChoices(areas, selected);
    }

    public static DeviceMonitorStartAddressChoices GetStartAddressChoices(
        string? profile,
        string? program,
        string? area,
        string? currentText)
    {
        var normalizedProfile = NormalizeProfile(profile);
        var normalizedArea = NormalizeArea(area);
        var normalizedProgram = NormalizeProgram(program);
        var prefix = normalizedProgram == "Direct" ? null : normalizedProgram;

        var descriptor = ToyopucDeviceCatalog.GetAreaDescriptor(normalizedArea, normalizedProfile);
        var usesPackedWord = descriptor.SupportsPackedWord;
        var width = descriptor.GetAddressWidth("word", usesPackedWord);
        var selectedText = TryNormalizeHexText(currentText, width)
            ?? 0.ToString($"X{width}", CultureInfo.InvariantCulture);

        var candidates = usesPackedWord
            ? ToyopucDeviceCatalog.GetSuggestedStartAddresses(
                descriptor.Area,
                prefix,
                unit: "word",
                packed: true,
                profile: normalizedProfile)
            : ToyopucDeviceCatalog.GetSuggestedStartAddresses(descriptor.Area, prefix, normalizedProfile);

        if (candidates.Contains(selectedText, StringComparer.Ordinal))
        {
            return new DeviceMonitorStartAddressChoices(candidates, selectedText, width, usesPackedWord);
        }

        var fallback = candidates.Count > 0 ? candidates[0] : selectedText;
        return new DeviceMonitorStartAddressChoices(candidates, fallback, width, usesPackedWord);
    }

    public static DeviceMonitorSelectionRequest BuildSelectionRequest(
        string? profile,
        string? program,
        string? area,
        string? startText)
    {
        var normalizedProfile = NormalizeProfile(profile);
        var normalizedArea = NormalizeArea(area);
        var normalizedProgram = NormalizeProgram(program);
        var prefixed = normalizedProgram != "Direct";

        var descriptor = ToyopucDeviceCatalog.GetAreaDescriptor(normalizedArea, normalizedProfile);
        var usesPackedWord = descriptor.SupportsPackedWord;
        var ranges = usesPackedWord
            ? ToyopucDeviceCatalog.GetSupportedRanges(
                normalizedArea,
                prefixed,
                unit: "word",
                packed: true,
                profile: normalizedProfile)
            : ToyopucDeviceCatalog.GetSupportedRanges(normalizedArea, prefixed, normalizedProfile);
        var start = ParseHex(startText);
        var rangeIndex = -1;
        for (var i = 0; i < ranges.Count; i++)
        {
            if (ranges[i].Contains(start))
            {
                rangeIndex = i;
                break;
            }
        }

        if (rangeIndex < 0)
        {
            var width = descriptor.GetAddressWidth("word", usesPackedWord);
            throw new ArgumentOutOfRangeException(
                nameof(startText),
                $"start address must be inside {FormatRanges(ranges, width)} for {normalizedArea}");
        }

        var initialOffset = CountOffsetFromAreaStart(ranges, rangeIndex, start);

        return new DeviceMonitorSelectionRequest(
            normalizedProgram == "Direct" ? string.Empty : $"{normalizedProgram}-",
            normalizedArea,
            descriptor.GetAddressWidth("word", usesPackedWord),
            usesPackedWord ? "W" : string.Empty,
            0x1,
            initialOffset,
            ranges);
    }

    public static string? TryNormalizeHexText(string? text, int width)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            var value = ParseHex(text);
            return value.ToString($"X{width}", CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    public static int ParseHex(string? text)
    {
        var normalizedText = text?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            throw new ArgumentException("hex value is required", nameof(text));
        }

        if (normalizedText.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            normalizedText = normalizedText[2..];
        }

        return int.Parse(normalizedText, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    private static string NormalizeProgram(string? program)
    {
        var normalized = string.IsNullOrWhiteSpace(program)
            ? "Direct"
            : program.Trim().ToUpperInvariant();
        return normalized is "P1" or "P2" or "P3" ? normalized : "Direct";
    }

    private static bool IsPrefixedProgram(string? program)
    {
        return NormalizeProgram(program) != "Direct";
    }

    private static string NormalizeArea(string? area)
    {
        var normalized = area?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("device is required", nameof(area));
        }

        return normalized;
    }

    private static int CountOffsetFromAreaStart(
        IReadOnlyList<ToyopucAddressRange> ranges,
        int rangeIndex,
        int start)
    {
        var offset = 0;
        for (var i = 0; i < rangeIndex; i++)
        {
            offset = checked(offset + checked(ranges[i].End - ranges[i].Start + 1));
        }

        return checked(offset + checked(start - ranges[rangeIndex].Start));
    }

    private static string FormatRanges(IReadOnlyList<ToyopucAddressRange> ranges, int width)
    {
        return string.Join(
            ", ",
            ranges.Select(range =>
                $"{range.Start.ToString($"X{width}", CultureInfo.InvariantCulture)}-{range.End.ToString($"X{width}", CultureInfo.InvariantCulture)}"));
    }
}

internal readonly record struct DeviceMonitorProgramChoices(
    IReadOnlyList<string> Choices,
    string Selected,
    bool IsEnabled);

internal readonly record struct DeviceMonitorAreaChoices(
    IReadOnlyList<string> Areas,
    string Selected);

internal readonly record struct DeviceMonitorStartAddressChoices(
    IReadOnlyList<string> Candidates,
    string Selected,
    int Width,
    bool UsesPackedWord);

internal readonly record struct DeviceMonitorSelectionRequest(
    string Prefix,
    string Area,
    int Width,
    string Suffix,
    int Step,
    int InitialOffset,
    IReadOnlyList<ToyopucAddressRange> Ranges);
