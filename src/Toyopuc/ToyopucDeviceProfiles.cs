namespace PlcComm.Toyopuc;

public static class ToyopucDeviceProfiles
{
    public static ToyopucDeviceProfile Generic { get; } = new(
        "Generic",
        ToyopucAddressingOptions.Generic,
        CreateGenericAreas());

    public static ToyopucDeviceProfile ToyopucPlusStandard { get; } = new(
        "TOYOPUC-Plus:Plus Standard mode",
        ToyopucAddressingOptions.ToyopucPlusStandard,
        CreateToyopucPlusStandardAreas());

    public static ToyopucDeviceProfile ToyopucPlusExtended { get; } = new(
        "TOYOPUC-Plus:Plus Extended mode",
        ToyopucAddressingOptions.ToyopucPlusExtended,
        CreateToyopucPlusAreas());

    public static ToyopucDeviceProfile Nano10GxMode { get; } = new(
        "Nano 10GX:Nano 10GX mode",
        ToyopucAddressingOptions.Nano10GxMode,
        CreateNano10GxModeAreas());

    public static ToyopucDeviceProfile Nano10GxCompatible { get; } = new(
        "Nano 10GX:Compatible mode",
        ToyopucAddressingOptions.Nano10GxCompatible,
        CreateNano10GxAreas());

    public static ToyopucDeviceProfile Pc10GStandardPc3Jg { get; } = new(
        "PC10G:PC10 standard/PC3JG mode",
        ToyopucAddressingOptions.Pc10GStandardPc3Jg,
        CreatePc10StandardPc3JgAreas());

    public static ToyopucDeviceProfile Pc10GMode { get; } = new(
        "PC10G:PC10 mode",
        ToyopucAddressingOptions.Pc10GMode,
        CreatePc10ModeAreas());

    public static ToyopucDeviceProfile Pc3JxPc3Separate { get; } = new(
        "PC3JX:PC3 separate mode",
        ToyopucAddressingOptions.Pc3JxPc3Separate,
        CreatePc3JxPc3Areas());

    public static ToyopucDeviceProfile Pc3JxPlusExpansion { get; } = new(
        "PC3JX:Plus expansion mode",
        ToyopucAddressingOptions.Pc3JxPlusExpansion,
        CreatePc3JxPlusAreas());

    public static ToyopucDeviceProfile Pc3JgMode { get; } = new(
        "PC3JG:PC3JG mode",
        ToyopucAddressingOptions.Pc3JgMode,
        CreatePc3JgModeAreas());

    public static ToyopucDeviceProfile Pc3JgPc3Separate { get; } = new(
        "PC3JG:PC3 separate mode",
        ToyopucAddressingOptions.Pc3JgPc3Separate,
        CreatePc3JgPc3Areas());

    private static readonly string[] ProfileNames =
    [
        Generic.Name,
        ToyopucPlusStandard.Name,
        ToyopucPlusExtended.Name,
        Nano10GxMode.Name,
        Nano10GxCompatible.Name,
        Pc10GStandardPc3Jg.Name,
        Pc10GMode.Name,
        Pc3JxPc3Separate.Name,
        Pc3JxPlusExpansion.Name,
        Pc3JgMode.Name,
        Pc3JgPc3Separate.Name,
    ];

    public static IReadOnlyList<string> GetNames()
    {
        return ProfileNames;
    }

    public static string NormalizeName(string? profile)
    {
        return FromName(profile).Name;
    }

    public static ToyopucDeviceProfile FromName(string? profile)
    {
        if (string.IsNullOrWhiteSpace(profile)
            || profile.Equals(Generic.Name, StringComparison.OrdinalIgnoreCase))
        {
            return Generic;
        }

        if (profile.Equals(ToyopucPlusStandard.Name, StringComparison.OrdinalIgnoreCase))
        {
            return ToyopucPlusStandard;
        }

        if (profile.Equals(ToyopucPlusExtended.Name, StringComparison.OrdinalIgnoreCase))
        {
            return ToyopucPlusExtended;
        }

        if (profile.Equals(Nano10GxMode.Name, StringComparison.OrdinalIgnoreCase))
        {
            return Nano10GxMode;
        }

        if (profile.Equals(Nano10GxCompatible.Name, StringComparison.OrdinalIgnoreCase))
        {
            return Nano10GxCompatible;
        }

        if (profile.Equals(Pc10GStandardPc3Jg.Name, StringComparison.OrdinalIgnoreCase))
        {
            return Pc10GStandardPc3Jg;
        }

        if (profile.Equals(Pc10GMode.Name, StringComparison.OrdinalIgnoreCase))
        {
            return Pc10GMode;
        }

        if (profile.Equals(Pc3JxPc3Separate.Name, StringComparison.OrdinalIgnoreCase))
        {
            return Pc3JxPc3Separate;
        }

        if (profile.Equals(Pc3JxPlusExpansion.Name, StringComparison.OrdinalIgnoreCase))
        {
            return Pc3JxPlusExpansion;
        }

        if (profile.Equals(Pc3JgMode.Name, StringComparison.OrdinalIgnoreCase))
        {
            return Pc3JgMode;
        }

        if (profile.Equals(Pc3JgPc3Separate.Name, StringComparison.OrdinalIgnoreCase))
        {
            return Pc3JgPc3Separate;
        }

        throw new ArgumentException($"Unknown device profile: {profile}", nameof(profile));
    }

    private static IReadOnlyList<ToyopucAreaDescriptor> CreateGenericAreas()
    {
        return
        [
            PrefixedSplitBitArea("P", lowEnd: 0x01FF, highEnd: 0x17FF),
            PrefixedBitArea("K", 0x02FF),
            PrefixedSplitBitArea("V", lowEnd: 0x00FF, highEnd: 0x17FF),
            PrefixedSplitBitArea("T", lowEnd: 0x01FF, highEnd: 0x17FF),
            PrefixedSplitBitArea("C", lowEnd: 0x01FF, highEnd: 0x17FF),
            PrefixedSplitBitArea("L", lowEnd: 0x07FF, highEnd: 0x2FFF),
            PrefixedBitArea("X", 0x07FF),
            PrefixedBitArea("Y", 0x07FF),
            PrefixedSplitBitArea("M", lowEnd: 0x07FF, highEnd: 0x17FF),
            PrefixedSplitWordArea("S", lowEnd: 0x03FF, highEnd: 0x13FF),
            PrefixedSplitWordArea("N", lowEnd: 0x01FF, highEnd: 0x17FF),
            PrefixedWordArea("R", 0x07FF),
            PrefixedWordArea("D", 0x2FFF),
            WordArea("B", directEnd: 0x1FFF),
            ExtBitArea("EP", 0x0FFF),
            ExtBitArea("EK", 0x0FFF),
            ExtBitArea("EV", 0x0FFF),
            ExtBitArea("ET", 0x07FF),
            ExtBitArea("EC", 0x07FF),
            ExtBitArea("EL", 0x1FFF),
            ExtBitArea("EX", 0x07FF),
            ExtBitArea("EY", 0x07FF),
            ExtBitArea("EM", 0x1FFF),
            ExtBitArea("GM", 0xFFFF),
            ExtBitArea("GX", 0xFFFF),
            ExtBitArea("GY", 0xFFFF),
            ExtWordArea("ES", 0x07FF),
            ExtWordArea("EN", 0x07FF),
            ExtWordArea("H", 0x07FF),
            ExtWordArea("U", 0x1FFFF),
            ExtWordArea("EB", 0x3FFFF),
            FrArea(0x1FFFFF),
        ];
    }

    private static IReadOnlyList<ToyopucAreaDescriptor> CreateToyopucPlusStandardAreas()
    {
        return
        [
            PrefixedBitArea("P", 0x01FF),
            PrefixedBitArea("K", 0x02FF),
            PrefixedBitArea("V", 0x00FF),
            PrefixedBitArea("T", 0x01FF),
            PrefixedBitArea("C", 0x01FF),
            PrefixedBitArea("L", 0x07FF),
            PrefixedBitArea("X", 0x07FF),
            PrefixedBitArea("Y", 0x07FF),
            PrefixedBitArea("M", 0x07FF),
            PrefixedWordArea("S", 0x03FF),
            PrefixedWordArea("N", 0x01FF),
            PrefixedWordArea("R", 0x07FF),
            PrefixedWordArea("D", 0x0FFF),
            ExtBitArea("EP", 0x0FFF),
            ExtBitArea("EK", 0x0FFF),
            ExtBitArea("EV", 0x0FFF),
            ExtBitArea("ET", 0x07FF),
            ExtBitArea("EC", 0x07FF),
            ExtBitArea("EL", 0x1FFF),
            ExtBitArea("EX", 0x07FF),
            ExtBitArea("EY", 0x07FF),
            ExtBitArea("EM", 0x1FFF),
            ExtWordArea("ES", 0x07FF),
            ExtWordArea("EN", 0x07FF),
            ExtWordArea("H", 0x07FF),
        ];
    }

    private static IReadOnlyList<ToyopucAreaDescriptor> CreateToyopucPlusAreas()
    {
        return
        [
            PrefixedBitArea("P", 0x01FF),
            PrefixedBitArea("K", 0x02FF),
            PrefixedBitArea("V", 0x00FF),
            PrefixedBitArea("T", 0x01FF),
            PrefixedBitArea("C", 0x01FF),
            PrefixedBitArea("L", 0x07FF),
            PrefixedBitArea("X", 0x07FF),
            PrefixedBitArea("Y", 0x07FF),
            PrefixedBitArea("M", 0x07FF),
            PrefixedWordArea("S", 0x03FF),
            PrefixedWordArea("N", 0x01FF),
            PrefixedWordArea("R", 0x07FF),
            PrefixedWordArea("D", 0x0FFF),
            ExtBitArea("EP", 0x0FFF),
            ExtBitArea("EK", 0x0FFF),
            ExtBitArea("EV", 0x0FFF),
            ExtBitArea("ET", 0x07FF),
            ExtBitArea("EC", 0x07FF),
            ExtBitArea("EL", 0x1FFF),
            ExtBitArea("EX", 0x07FF),
            ExtBitArea("EY", 0x07FF),
            ExtBitArea("EM", 0x1FFF),
            ExtBitArea("GM", 0xFFFF),
            ExtBitArea("GX", 0xFFFF),
            ExtBitArea("GY", 0xFFFF),
            ExtWordArea("ES", 0x07FF),
            ExtWordArea("EN", 0x07FF),
            ExtWordArea("H", 0x07FF),
            ExtWordArea("U", 0x07FFF),
        ];
    }

    private static IReadOnlyList<ToyopucAreaDescriptor> CreateNano10GxModeAreas()
    {
        return
        [
            PrefixedSplitBitArea("P", lowEnd: 0x01FF, highEnd: 0x17FF),
            PrefixedBitArea("K", 0x02FF),
            PrefixedSplitBitArea("V", lowEnd: 0x00FF, highEnd: 0x17FF),
            PrefixedSplitBitArea("T", lowEnd: 0x01FF, highEnd: 0x17FF),
            PrefixedSplitBitArea("C", lowEnd: 0x01FF, highEnd: 0x17FF),
            PrefixedSplitBitArea("L", lowEnd: 0x07FF, highEnd: 0x2FFF),
            PrefixedBitArea("X", 0x07FF),
            PrefixedBitArea("Y", 0x07FF),
            PrefixedSplitBitArea("M", lowEnd: 0x07FF, highEnd: 0x17FF),
            PrefixedSplitWordArea("S", lowEnd: 0x03FF, highEnd: 0x13FF),
            PrefixedSplitWordArea("N", lowEnd: 0x01FF, highEnd: 0x17FF),
            PrefixedWordArea("R", 0x07FF),
            PrefixedWordArea("D", 0x2FFF),
            ExtBitArea("EP", 0x0FFF),
            ExtBitArea("EK", 0x0FFF),
            ExtBitArea("EV", 0x0FFF),
            ExtBitArea("ET", 0x07FF),
            ExtBitArea("EC", 0x07FF),
            ExtBitArea("EL", 0x1FFF),
            ExtBitArea("EX", 0x07FF),
            ExtBitArea("EY", 0x07FF),
            ExtBitArea("EM", 0x1FFF),
            ExtBitArea("GM", 0xFFFF),
            ExtBitArea("GX", 0xFFFF),
            ExtBitArea("GY", 0xFFFF),
            ExtWordArea("ES", 0x07FF),
            ExtWordArea("EN", 0x07FF),
            ExtWordArea("H", 0x07FF),
            ExtWordArea("U", 0x1FFFF),
            ExtWordArea("EB", 0x3FFFF),
            FrArea(0x1FFFFF),
        ];
    }

    private static IReadOnlyList<ToyopucAreaDescriptor> CreateNano10GxAreas()
    {
        return CreateNano10GxModeAreas();
    }

    private static IReadOnlyList<ToyopucAreaDescriptor> CreatePc10StandardPc3JgAreas()
    {
        return
        [
            PrefixedBitArea("P", 0x01FF),
            PrefixedBitArea("K", 0x02FF),
            PrefixedBitArea("V", 0x00FF),
            PrefixedBitArea("T", 0x01FF),
            PrefixedBitArea("C", 0x01FF),
            PrefixedBitArea("L", 0x07FF),
            PrefixedBitArea("X", 0x07FF),
            PrefixedBitArea("Y", 0x07FF),
            PrefixedBitArea("M", 0x07FF),
            PrefixedWordArea("S", 0x03FF),
            PrefixedWordArea("N", 0x01FF),
            PrefixedWordArea("R", 0x07FF),
            PrefixedWordArea("D", 0x0FFF),
            WordArea("B", directEnd: 0x1FFF),
            ExtBitArea("EP", 0x0FFF),
            ExtBitArea("EK", 0x0FFF),
            ExtBitArea("EV", 0x0FFF),
            ExtBitArea("ET", 0x07FF),
            ExtBitArea("EC", 0x07FF),
            ExtBitArea("EL", 0x1FFF),
            ExtBitArea("EX", 0x07FF),
            ExtBitArea("EY", 0x07FF),
            ExtBitArea("EM", 0x1FFF),
            ExtBitArea("GM", 0xFFFF),
            ExtBitArea("GX", 0xFFFF),
            ExtBitArea("GY", 0xFFFF),
            ExtWordArea("ES", 0x07FF),
            ExtWordArea("EN", 0x07FF),
            ExtWordArea("H", 0x07FF),
            ExtWordArea("U", 0x07FFF),
            ExtWordArea("EB", 0x1FFFF),
        ];
    }

    private static IReadOnlyList<ToyopucAreaDescriptor> CreatePc10ModeAreas()
    {
        return
        [
            PrefixedSplitBitArea("P", lowEnd: 0x01FF, highEnd: 0x17FF),
            PrefixedBitArea("K", 0x02FF),
            PrefixedSplitBitArea("V", lowEnd: 0x00FF, highEnd: 0x17FF),
            PrefixedSplitBitArea("T", lowEnd: 0x01FF, highEnd: 0x17FF),
            PrefixedSplitBitArea("C", lowEnd: 0x01FF, highEnd: 0x17FF),
            PrefixedSplitBitArea("L", lowEnd: 0x07FF, highEnd: 0x2FFF),
            PrefixedBitArea("X", 0x07FF),
            PrefixedBitArea("Y", 0x07FF),
            PrefixedSplitBitArea("M", lowEnd: 0x07FF, highEnd: 0x17FF),
            PrefixedSplitWordArea("S", lowEnd: 0x03FF, highEnd: 0x13FF),
            PrefixedSplitWordArea("N", lowEnd: 0x01FF, highEnd: 0x17FF),
            PrefixedWordArea("R", 0x07FF),
            PrefixedWordArea("D", 0x2FFF),
            WordArea("B", directEnd: 0x1FFF),
            ExtBitArea("EP", 0x0FFF),
            ExtBitArea("EK", 0x0FFF),
            ExtBitArea("EV", 0x0FFF),
            ExtBitArea("ET", 0x07FF),
            ExtBitArea("EC", 0x07FF),
            ExtBitArea("EL", 0x1FFF),
            ExtBitArea("EX", 0x07FF),
            ExtBitArea("EY", 0x07FF),
            ExtBitArea("EM", 0x1FFF),
            ExtBitArea("GM", 0xFFFF, packedDirectEnd: 0x0FFF),
            ExtBitArea("GX", 0xFFFF),
            ExtBitArea("GY", 0xFFFF),
            ExtWordArea("ES", 0x07FF),
            ExtWordArea("EN", 0x07FF),
            ExtWordArea("H", 0x07FF),
            ExtWordArea("U", 0x1FFFF),
            ExtWordArea("EB", 0x3FFFF),
            FrArea(0x1FFFFF),
        ];
    }

    private static IReadOnlyList<ToyopucAreaDescriptor> CreatePc3JxPc3Areas()
    {
        return
        [
            PrefixedBitArea("P", 0x01FF),
            PrefixedBitArea("K", 0x02FF),
            PrefixedBitArea("V", 0x00FF),
            PrefixedBitArea("T", 0x01FF),
            PrefixedBitArea("C", 0x01FF),
            PrefixedBitArea("L", 0x07FF),
            PrefixedBitArea("X", 0x07FF),
            PrefixedBitArea("Y", 0x07FF),
            PrefixedBitArea("M", 0x07FF),
            PrefixedWordArea("S", 0x03FF),
            PrefixedWordArea("N", 0x01FF),
            PrefixedWordArea("R", 0x07FF),
            PrefixedWordArea("D", 0x2FFF),
            WordArea("B", directEnd: 0x1FFF),
            ExtBitArea("EP", 0x0FFF),
            ExtBitArea("EK", 0x0FFF),
            ExtBitArea("EV", 0x0FFF),
            ExtBitArea("ET", 0x07FF),
            ExtBitArea("EC", 0x07FF),
            ExtBitArea("EL", 0x1FFF),
            ExtBitArea("EX", 0x07FF),
            ExtBitArea("EY", 0x07FF),
            ExtBitArea("EM", 0x1FFF),
            ExtWordArea("ES", 0x07FF),
            ExtWordArea("EN", 0x07FF),
            ExtWordArea("H", 0x07FF),
            ExtWordArea("U", 0x07FFF),
        ];
    }

    private static IReadOnlyList<ToyopucAreaDescriptor> CreatePc3JxPlusAreas()
    {
        return
        [
            PrefixedBitArea("P", 0x01FF),
            PrefixedBitArea("K", 0x02FF),
            PrefixedBitArea("V", 0x00FF),
            PrefixedBitArea("T", 0x01FF),
            PrefixedBitArea("C", 0x01FF),
            PrefixedBitArea("L", 0x07FF),
            PrefixedBitArea("X", 0x07FF),
            PrefixedBitArea("Y", 0x07FF),
            PrefixedBitArea("M", 0x07FF),
            PrefixedWordArea("S", 0x03FF),
            PrefixedWordArea("N", 0x01FF),
            PrefixedWordArea("R", 0x07FF),
            PrefixedWordArea("D", 0x0FFF),
            ExtBitArea("EP", 0x0FFF),
            ExtBitArea("EK", 0x0FFF),
            ExtBitArea("EV", 0x0FFF),
            ExtBitArea("ET", 0x07FF),
            ExtBitArea("EC", 0x07FF),
            ExtBitArea("EL", 0x1FFF),
            ExtBitArea("EX", 0x07FF),
            ExtBitArea("EY", 0x07FF),
            ExtBitArea("EM", 0x1FFF),
            ExtBitArea("GM", 0xFFFF),
            ExtBitArea("GX", 0xFFFF),
            ExtBitArea("GY", 0xFFFF),
            ExtWordArea("ES", 0x07FF),
            ExtWordArea("EN", 0x07FF),
            ExtWordArea("H", 0x07FF),
            ExtWordArea("U", 0x07FFF),
        ];
    }

    private static IReadOnlyList<ToyopucAreaDescriptor> CreatePc3JgModeAreas()
    {
        return
        [
            PrefixedBitArea("P", 0x01FF),
            PrefixedBitArea("K", 0x02FF),
            PrefixedBitArea("V", 0x00FF),
            PrefixedBitArea("T", 0x01FF),
            PrefixedBitArea("C", 0x01FF),
            PrefixedBitArea("L", 0x07FF),
            PrefixedBitArea("X", 0x07FF),
            PrefixedBitArea("Y", 0x07FF),
            PrefixedBitArea("M", 0x07FF),
            PrefixedWordArea("S", 0x03FF),
            PrefixedWordArea("N", 0x01FF),
            PrefixedWordArea("R", 0x07FF),
            PrefixedWordArea("D", 0x0FFF),
            WordArea("B", directEnd: 0x1FFF),
            ExtBitArea("EP", 0x0FFF),
            ExtBitArea("EK", 0x0FFF),
            ExtBitArea("EV", 0x0FFF),
            ExtBitArea("ET", 0x07FF),
            ExtBitArea("EC", 0x07FF),
            ExtBitArea("EL", 0x1FFF),
            ExtBitArea("EX", 0x07FF),
            ExtBitArea("EY", 0x07FF),
            ExtBitArea("EM", 0x1FFF),
            ExtBitArea("GM", 0xFFFF),
            ExtBitArea("GX", 0xFFFF),
            ExtBitArea("GY", 0xFFFF),
            ExtWordArea("ES", 0x07FF),
            ExtWordArea("EN", 0x07FF),
            ExtWordArea("H", 0x07FF),
            ExtWordArea("U", 0x07FFF),
            ExtWordArea("EB", 0x1FFFF),
        ];
    }

    private static IReadOnlyList<ToyopucAreaDescriptor> CreatePc3JgPc3Areas()
    {
        return
        [
            PrefixedBitArea("P", 0x01FF),
            PrefixedBitArea("K", 0x02FF),
            PrefixedBitArea("V", 0x00FF),
            PrefixedBitArea("T", 0x01FF),
            PrefixedBitArea("C", 0x01FF),
            PrefixedBitArea("L", 0x07FF),
            PrefixedBitArea("X", 0x07FF),
            PrefixedBitArea("Y", 0x07FF),
            PrefixedBitArea("M", 0x07FF),
            PrefixedWordArea("S", 0x03FF),
            PrefixedWordArea("N", 0x01FF),
            PrefixedWordArea("R", 0x07FF),
            PrefixedWordArea("D", 0x0FFF),
            WordArea("B", directEnd: 0x1FFF),
            ExtBitArea("EP", 0x0FFF),
            ExtBitArea("EK", 0x0FFF),
            ExtBitArea("EV", 0x0FFF),
            ExtBitArea("ET", 0x07FF),
            ExtBitArea("EC", 0x07FF),
            ExtBitArea("EL", 0x1FFF),
            ExtBitArea("EX", 0x07FF),
            ExtBitArea("EY", 0x07FF),
            ExtBitArea("EM", 0x1FFF),
            ExtBitArea("GM", 0xFFFF),
            ExtBitArea("GX", 0xFFFF),
            ExtBitArea("GY", 0xFFFF),
            ExtWordArea("ES", 0x07FF),
            ExtWordArea("EN", 0x07FF),
            ExtWordArea("H", 0x07FF),
            ExtWordArea("U", 0x07FFF),
            ExtWordArea("EB", 0x1FFFF),
        ];
    }

    private static ToyopucAreaDescriptor SplitBitArea(string area, int lowEnd, int highEnd)
    {
        return BitArea(
            area,
            [Range(0x0000, lowEnd), Range(0x1000, highEnd)],
            [Range(0x0000, lowEnd), Range(0x1000, highEnd)]);
    }

    private static ToyopucAreaDescriptor PrefixedSplitBitArea(string area, int lowEnd, int highEnd)
    {
        return BitArea(
            area,
            [],
            [Range(0x0000, lowEnd), Range(0x1000, highEnd)]);
    }

    private static ToyopucAreaDescriptor SplitWordArea(string area, int lowEnd, int highEnd)
    {
        return WordArea(
            area,
            [Range(0x0000, lowEnd), Range(0x1000, highEnd)],
            [Range(0x0000, lowEnd), Range(0x1000, highEnd)]);
    }

    private static ToyopucAreaDescriptor PrefixedSplitWordArea(string area, int lowEnd, int highEnd)
    {
        return WordArea(
            area,
            [],
            [Range(0x0000, lowEnd), Range(0x1000, highEnd)]);
    }

    private static ToyopucAreaDescriptor BitArea(string area, int directEnd, int? prefixedEnd)
    {
        return BitArea(
            area,
            [Range(0x0000, directEnd)],
            prefixedEnd is null ? [] : [Range(0x0000, prefixedEnd.Value)]);
    }

    private static ToyopucAreaDescriptor PrefixedBitArea(string area, int prefixedEnd)
    {
        return BitArea(area, [], [Range(0x0000, prefixedEnd)]);
    }

    private static ToyopucAreaDescriptor BitArea(
        string area,
        IReadOnlyList<ToyopucAddressRange> directRanges,
        IReadOnlyList<ToyopucAddressRange> prefixedRanges)
    {
        return Area(
            area,
            directRanges,
            prefixedRanges,
            supportsPackedWord: true,
            addressWidth: 4,
            suggestedStartStep: 0x10);
    }

    private static ToyopucAreaDescriptor WordArea(string area, int directEnd, int? prefixedEnd = null)
    {
        return WordArea(
            area,
            [Range(0x0000, directEnd)],
            prefixedEnd is null ? [] : [Range(0x0000, prefixedEnd.Value)]);
    }

    private static ToyopucAreaDescriptor PrefixedWordArea(string area, int prefixedEnd)
    {
        return WordArea(area, [], [Range(0x0000, prefixedEnd)]);
    }

    private static ToyopucAreaDescriptor WordArea(
        string area,
        IReadOnlyList<ToyopucAddressRange> directRanges,
        IReadOnlyList<ToyopucAddressRange> prefixedRanges)
    {
        return Area(
            area,
            directRanges,
            prefixedRanges,
            supportsPackedWord: false,
            addressWidth: 4,
            suggestedStartStep: 0x10);
    }

    private static ToyopucAreaDescriptor ExtBitArea(string area, int directEnd, int? packedDirectEnd = null)
    {
        return Area(
            area,
            [Range(0x0000, directEnd)],
            [],
            supportsPackedWord: true,
            addressWidth: 4,
            suggestedStartStep: 0x10,
            packedDirectRangesOverride: packedDirectEnd is null ? null : [Range(0x0000, packedDirectEnd.Value)]);
    }

    private static ToyopucAreaDescriptor ExtWordArea(string area, int directEnd)
    {
        return Area(
            area,
            [Range(0x0000, directEnd)],
            [],
            supportsPackedWord: false,
            addressWidth: 5,
            suggestedStartStep: 0x100);
    }

    private static ToyopucAreaDescriptor FrArea(int directEnd)
    {
        return Area(
            "FR",
            [Range(0x000000, directEnd)],
            [],
            supportsPackedWord: false,
            addressWidth: 6,
            suggestedStartStep: 0x1000);
    }

    private static ToyopucAreaDescriptor Area(
        string area,
        IReadOnlyList<ToyopucAddressRange> directRanges,
        IReadOnlyList<ToyopucAddressRange> prefixedRanges,
        bool supportsPackedWord,
        int addressWidth,
        int suggestedStartStep,
        IReadOnlyList<ToyopucAddressRange>? packedDirectRangesOverride = null,
        IReadOnlyList<ToyopucAddressRange>? packedPrefixedRangesOverride = null)
    {
        return new ToyopucAreaDescriptor(
            area,
            directRanges,
            prefixedRanges,
            supportsPackedWord,
            addressWidth,
            suggestedStartStep,
            packedDirectRangesOverride,
            packedPrefixedRangesOverride);
    }

    private static ToyopucAddressRange Range(int start, int end)
    {
        return new ToyopucAddressRange(start, end);
    }
}
