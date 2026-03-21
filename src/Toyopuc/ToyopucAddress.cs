using System.Text.RegularExpressions;

namespace PlcComm.Toyopuc;

public static class ToyopucAddress
{
    private sealed record Segment(int Start, int End, int BaseAddress);

    private static readonly Regex AddressPattern = new(
        @"^(?<area>[A-Z]{1,2})(?<num>[0-9A-Fa-f]+)(?<suffix>[LHW])?$",
        RegexOptions.Compiled);

    private static readonly Regex PrefixedAddressPattern = new(
        @"^(?<prefix>P[123])-(?<area>[A-Z]{1,2})(?<num>[0-9A-Fa-f]+)(?<suffix>[LHW])?$",
        RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, int> WordBase = new Dictionary<string, int>
    {
        ["P"] = 0x0000,
        ["K"] = 0x0020,
        ["V"] = 0x0050,
        ["T"] = 0x0060,
        ["C"] = 0x0060,
        ["L"] = 0x0080,
        ["X"] = 0x0100,
        ["Y"] = 0x0100,
        ["M"] = 0x0180,
        ["S"] = 0x0200,
        ["N"] = 0x0600,
        ["R"] = 0x0800,
        ["D"] = 0x1000,
        ["B"] = 0x6000,
    };

    private static readonly IReadOnlyDictionary<string, int> ByteBase = new Dictionary<string, int>
    {
        ["P"] = 0x0000,
        ["K"] = 0x0040,
        ["V"] = 0x00A0,
        ["T"] = 0x00C0,
        ["C"] = 0x00C0,
        ["L"] = 0x0100,
        ["X"] = 0x0200,
        ["Y"] = 0x0200,
        ["M"] = 0x0300,
        ["S"] = 0x0400,
        ["N"] = 0x0C00,
        ["R"] = 0x1000,
        ["D"] = 0x2000,
        ["B"] = 0xC000,
    };

    private static readonly IReadOnlyDictionary<string, int> BitBase = new Dictionary<string, int>
    {
        ["P"] = 0x0000,
        ["K"] = 0x0200,
        ["V"] = 0x0500,
        ["T"] = 0x0600,
        ["C"] = 0x0600,
        ["L"] = 0x0800,
        ["X"] = 0x1000,
        ["Y"] = 0x1000,
        ["M"] = 0x1800,
    };

    private static readonly IReadOnlyDictionary<string, int> BitMaxIndex = new Dictionary<string, int>
    {
        ["P"] = 0x01FF,
        ["K"] = 0x02FF,
        ["V"] = 0x00FF,
        ["T"] = 0x01FF,
        ["C"] = 0x01FF,
        ["L"] = 0x07FF,
        ["X"] = 0x07FF,
        ["Y"] = 0x07FF,
        ["M"] = 0x07FF,
    };

    private static readonly IReadOnlyDictionary<string, Segment[]> Pc10BitSegments =
        new Dictionary<string, Segment[]>
        {
            ["P"] = new[] { new Segment(0x000, 0x1FF, 0x0000), new Segment(0x1000, 0x17FF, 0x0000) },
            ["K"] = new[] { new Segment(0x000, 0x2FF, 0x0000) },
            ["V"] = new[] { new Segment(0x000, 0x0FF, 0x0000), new Segment(0x1000, 0x17FF, 0x0000) },
            ["T"] = new[] { new Segment(0x000, 0x1FF, 0x0000), new Segment(0x1000, 0x17FF, 0x0000) },
            ["C"] = new[] { new Segment(0x000, 0x1FF, 0x0000), new Segment(0x1000, 0x17FF, 0x0000) },
            ["L"] = new[] { new Segment(0x000, 0x7FF, 0x0000), new Segment(0x1000, 0x2FFF, 0x0000) },
            ["X"] = new[] { new Segment(0x000, 0x7FF, 0x0000) },
            ["Y"] = new[] { new Segment(0x000, 0x7FF, 0x0000) },
            ["M"] = new[] { new Segment(0x000, 0x7FF, 0x0000), new Segment(0x1000, 0x17FF, 0x0000) },
        };

    private static readonly IReadOnlyDictionary<string, int> ProgramExNo = new Dictionary<string, int>
    {
        ["P1"] = 0x0D,
        ["P2"] = 0x0E,
        ["P3"] = 0x0F,
    };

    private static readonly IReadOnlyDictionary<string, (int No, int WordBase, int ByteBase)> ExtAreaMap =
        new Dictionary<string, (int No, int WordBase, int ByteBase)>
        {
            ["EP"] = (0x00, 0x0000, 0x0000),
            ["EK"] = (0x00, 0x0100, 0x0200),
            ["EV"] = (0x00, 0x0200, 0x0400),
            ["ET"] = (0x00, 0x0300, 0x0600),
            ["EC"] = (0x00, 0x0300, 0x0600),
            ["EL"] = (0x00, 0x0380, 0x0700),
            ["EX"] = (0x00, 0x0580, 0x0B00),
            ["EY"] = (0x00, 0x0580, 0x0B00),
            ["EM"] = (0x00, 0x0600, 0x0C00),
            ["ES"] = (0x00, 0x0800, 0x1000),
            ["EN"] = (0x00, 0x1000, 0x2000),
            ["H"] = (0x00, 0x1800, 0x3000),
            ["U"] = (0x08, 0x0000, 0x0000),
            ["GX"] = (0x07, 0x0000, 0x0000),
            ["GY"] = (0x07, 0x0000, 0x0000),
            ["GM"] = (0x07, 0x1000, 0x2000),
        };

    private static readonly IReadOnlyDictionary<string, Segment[]> ProgramBitSegments =
        new Dictionary<string, Segment[]>
        {
            ["P"] = new[] { new Segment(0x000, 0x1FF, 0x0000), new Segment(0x1000, 0x17FF, 0xC000) },
            ["K"] = new[] { new Segment(0x000, 0x2FF, 0x0040) },
            ["V"] = new[] { new Segment(0x000, 0x0FF, 0x00A0), new Segment(0x1000, 0x17FF, 0xC100) },
            ["T"] = new[] { new Segment(0x000, 0x1FF, 0x00C0), new Segment(0x1000, 0x17FF, 0xC200) },
            ["C"] = new[] { new Segment(0x000, 0x1FF, 0x00C0), new Segment(0x1000, 0x17FF, 0xC200) },
            ["L"] = new[] { new Segment(0x000, 0x7FF, 0x0100), new Segment(0x1000, 0x2FFF, 0xC400) },
            ["X"] = new[] { new Segment(0x000, 0x7FF, 0x0200) },
            ["Y"] = new[] { new Segment(0x000, 0x7FF, 0x0200) },
            ["M"] = new[] { new Segment(0x000, 0x7FF, 0x0300), new Segment(0x1000, 0x17FF, 0xC300) },
        };

    private static readonly IReadOnlyDictionary<string, Segment[]> ProgramWordSegments =
        new Dictionary<string, Segment[]>
        {
            ["S"] = new[] { new Segment(0x0000, 0x03FF, 0x0200), new Segment(0x1000, 0x13FF, 0x6400) },
            ["N"] = new[] { new Segment(0x0000, 0x01FF, 0x0600), new Segment(0x1000, 0x17FF, 0x6800) },
            ["R"] = new[] { new Segment(0x0000, 0x07FF, 0x0800) },
            ["D"] = new[] { new Segment(0x0000, 0x0FFF, 0x1000), new Segment(0x1000, 0x2FFF, 0x2000) },
        };

    private static readonly IReadOnlyDictionary<string, Segment[]> ProgramByteSegments =
        new Dictionary<string, Segment[]>
        {
            ["S"] = new[] { new Segment(0x0000, 0x03FF, 0x0400), new Segment(0x1000, 0x13FF, 0xC800) },
            ["N"] = new[] { new Segment(0x0000, 0x01FF, 0x0C00), new Segment(0x1000, 0x17FF, 0xD000) },
            ["R"] = new[] { new Segment(0x0000, 0x07FF, 0x1000) },
            ["D"] = new[] { new Segment(0x0000, 0x0FFF, 0x2000), new Segment(0x1000, 0x2FFF, 0x4000) },
        };

    private static readonly IReadOnlyDictionary<string, Segment[]> ProgramBitWordSegments;
    private static readonly IReadOnlyDictionary<string, Segment[]> ProgramBitByteSegments;

    static ToyopucAddress()
    {
        (ProgramBitWordSegments, ProgramBitByteSegments) = DeriveProgramSegmentsFromBitSegments();
    }

    public static ParsedAddress ParseAddress(string text, string unit, int radix = 16)
    {
        var match = AddressPattern.Match(text.Trim().ToUpperInvariant());
        if (!match.Success)
        {
            throw new ArgumentException($"Invalid address format: {text}", nameof(text));
        }

        var area = match.Groups["area"].Value;
        var number = Convert.ToInt32(match.Groups["num"].Value, radix);
        var suffix = match.Groups["suffix"].Success ? match.Groups["suffix"].Value : null;
        suffix = NormalizeSuffix(text, unit, suffix);
        return new ParsedAddress(area, number, unit, suffix == "H", suffix == "W", match.Groups["num"].Value.Length);
    }

    public static (int ExNo, ParsedAddress Address) ParsePrefixedAddress(string text, string unit, int radix = 16)
    {
        var match = PrefixedAddressPattern.Match(text.Trim().ToUpperInvariant());
        if (!match.Success)
        {
            throw new ArgumentException($"Invalid prefixed address format: {text}", nameof(text));
        }

        var prefix = match.Groups["prefix"].Value;
        var area = match.Groups["area"].Value;
        var number = Convert.ToInt32(match.Groups["num"].Value, radix);
        var suffix = match.Groups["suffix"].Success ? match.Groups["suffix"].Value : null;
        suffix = NormalizeSuffix(text, unit, suffix);
        return (ProgramExNo[prefix], new ParsedAddress(area, number, unit, suffix == "H", suffix == "W", match.Groups["num"].Value.Length));
    }

    public static int EncodeWordAddress(ParsedAddress address)
    {
        if (address.Unit != "word")
        {
            throw new ArgumentException("Expected word address", nameof(address));
        }

        if (address.Packed && !BitBase.ContainsKey(address.Area))
        {
            throw new ArgumentException($"W suffix is only valid for bit-device families: {address.Area}{address.Index:X}W", nameof(address));
        }

        if (!WordBase.TryGetValue(address.Area, out var baseAddress))
        {
            throw new ArgumentException($"Unsupported word area: {address.Area}", nameof(address));
        }

        return baseAddress + address.Index;
    }

    public static int EncodeByteAddress(ParsedAddress address)
    {
        if (address.Unit != "byte")
        {
            throw new ArgumentException("Expected byte address", nameof(address));
        }

        if (!ByteBase.TryGetValue(address.Area, out var baseAddress))
        {
            throw new ArgumentException($"Unsupported byte area: {address.Area}", nameof(address));
        }

        return baseAddress + (address.Index * 2) + (address.High ? 1 : 0);
    }

    public static int EncodeBitAddress(ParsedAddress address)
    {
        if (address.Unit != "bit")
        {
            throw new ArgumentException("Expected bit address", nameof(address));
        }

        if (!BitBase.TryGetValue(address.Area, out var baseAddress))
        {
            throw new ArgumentException($"Unsupported bit area: {address.Area}", nameof(address));
        }

        if (BitMaxIndex.TryGetValue(address.Area, out var maxIndex) && address.Index > maxIndex)
        {
            throw new ArgumentException($"Bit address out of range for {address.Area}: {address.Area}{address.Index:X4}", nameof(address));
        }

        return baseAddress + address.Index;
    }

    public static int EncodePc10BitAddress(ParsedAddress address)
    {
        if (address.Unit != "bit")
        {
            throw new ArgumentException("Expected bit address", nameof(address));
        }

        if (!BitBase.TryGetValue(address.Area, out var baseAddress))
        {
            throw new ArgumentException($"Unsupported bit area: {address.Area}", nameof(address));
        }

        if (!Pc10BitSegments.TryGetValue(address.Area, out var segments))
        {
            throw new ArgumentException($"Unsupported PC10 bit area: {address.Area}", nameof(address));
        }

        foreach (var segment in segments)
        {
            if (segment.Start <= address.Index && address.Index <= segment.End)
            {
                return baseAddress + address.Index;
            }
        }

        throw new ArgumentException($"PC10 bit address out of range: {address.Area}{address.Index:X4}", nameof(address));
    }

    public static int EncodeProgramWordAddress(ParsedAddress address)
    {
        if (address.Unit != "word")
        {
            throw new ArgumentException("Expected word address", nameof(address));
        }

        if (address.Packed && !ProgramBitSegments.ContainsKey(address.Area))
        {
            throw new ArgumentException(
                $"W suffix is only valid for prefixed bit-device families: {address.Area}{address.Index:X}W",
                nameof(address));
        }

        var segments = ProgramWordSegments.TryGetValue(address.Area, out var programSegments)
            ? programSegments
            : ProgramBitWordSegments.TryGetValue(address.Area, out var derivedWordSegments)
                ? derivedWordSegments
                : null;

        if (segments is null)
        {
            throw new ArgumentException($"Unsupported program word area: {address.Area}", nameof(address));
        }

        foreach (var segment in segments)
        {
            if (segment.Start <= address.Index && address.Index <= segment.End)
            {
                return segment.BaseAddress + (address.Index - segment.Start);
            }
        }

        throw new ArgumentException($"Program word address out of range: {address.Area}{address.Index:X4}", nameof(address));
    }

    public static int EncodeProgramByteAddress(ParsedAddress address)
    {
        if (address.Unit != "byte")
        {
            throw new ArgumentException("Expected byte address", nameof(address));
        }

        var segments = ProgramByteSegments.TryGetValue(address.Area, out var programSegments)
            ? programSegments
            : ProgramBitByteSegments.TryGetValue(address.Area, out var derivedByteSegments)
                ? derivedByteSegments
                : null;

        if (segments is null)
        {
            throw new ArgumentException($"Unsupported program byte area: {address.Area}", nameof(address));
        }

        foreach (var segment in segments)
        {
            if (segment.Start <= address.Index && address.Index <= segment.End)
            {
                return segment.BaseAddress + ((address.Index - segment.Start) * 2) + (address.High ? 1 : 0);
            }
        }

        var suffix = address.High ? "H" : "L";
        throw new ArgumentException($"Program byte address out of range: {address.Area}{address.Index:X4}{suffix}", nameof(address));
    }

    public static (int BitNo, int Address) EncodeProgramBitAddress(ParsedAddress address)
    {
        if (address.Unit != "bit")
        {
            throw new ArgumentException("Expected bit address", nameof(address));
        }

        if (!ProgramBitSegments.TryGetValue(address.Area, out var segments))
        {
            throw new ArgumentException($"Unsupported program bit area: {address.Area}", nameof(address));
        }

        foreach (var segment in segments)
        {
            if (segment.Start <= address.Index && address.Index <= segment.End)
            {
                var relative = address.Index - segment.Start;
                return (relative & 0x07, segment.BaseAddress + (relative >> 3));
            }
        }

        throw new ArgumentException($"Program bit address out of range: {address.Area}{address.Index:X4}", nameof(address));
    }

    public static int EncodeExNoBitU32(int exNo, int bitAddress)
    {
        return ((exNo & 0xFF) << 19) | (bitAddress & 0x7FFFF);
    }

    public static int EncodeExNoByteU32(int exNo, int byteAddress)
    {
        return ((exNo & 0xFF) << 16) | (byteAddress & 0xFFFF);
    }

    public static (int LowWord, int HighWord) SplitU32Words(int value)
    {
        return (value & 0xFFFF, (value >> 16) & 0xFFFF);
    }

    public static int FrBlockExNo(int index)
    {
        if (index is < 0x000000 or > 0x1FFFFF)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "FR index out of range (0x000000-0x1FFFFF)");
        }

        return 0x40 + (index / 0x8000);
    }

    public static int EncodeFrWordAddr32(int index)
    {
        var exNo = FrBlockExNo(index);
        var byteAddress = (index % 0x8000) * 2;
        return EncodeExNoByteU32(exNo, byteAddress);
    }

    public static ExtNoAddress EncodeExtNoAddress(string area, int index, string unit)
    {
        var normalizedArea = area.ToUpperInvariant();

        int number;
        var address = index;
        if (ExtAreaMap.TryGetValue(normalizedArea, out var areaMap))
        {
            number = areaMap.No;
            address = unit switch
            {
                "word" => areaMap.WordBase + index,
                "byte" => areaMap.ByteBase + index,
                _ => throw new ArgumentException($"Unsupported unit for extended No mapping: {unit}", nameof(unit)),
            };
        }
        else if (normalizedArea == "EB")
        {
            if (index is < 0x00000 or > 0x3FFFF)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "EB index out of range (0x00000-0x3FFFF)");
            }

            var block = index / 0x8000;
            number = 0x09 + block;
            address = index % 0x8000;
        }
        else if (normalizedArea == "FR")
        {
            if (index is < 0x000000 or > 0x1FFFFF)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "FR index out of range (0x000000-0x1FFFFF)");
            }

            var block = index / 0x8000;
            number = 0x40 + block;
            address = index % 0x8000;
        }
        else
        {
            throw new ArgumentException($"Unsupported extended area for No mapping: {area}", nameof(area));
        }

        if (address is < 0 or > 0xFFFF)
        {
            throw new ArgumentException("Address out of 16-bit range", nameof(index));
        }

        return new ExtNoAddress(number, address, unit);
    }

    private static string? NormalizeSuffix(string text, string unit, string? suffix)
    {
        if (unit == "byte" && suffix is null)
        {
            suffix = "L";
        }

        if (unit == "byte")
        {
            if (suffix is not ("L" or "H"))
            {
                throw new ArgumentException($"L/H suffix required for byte unit: {text}", nameof(text));
            }
        }
        else if (unit == "word")
        {
            if (suffix is not (null or "W"))
            {
                throw new ArgumentException($"W suffix only valid for packed word notation: {text}", nameof(text));
            }
        }
        else if (suffix is not null)
        {
            throw new ArgumentException($"Suffix only valid for byte/packed-word notation: {text}", nameof(text));
        }

        return suffix;
    }

    private static (IReadOnlyDictionary<string, Segment[]>, IReadOnlyDictionary<string, Segment[]>) DeriveProgramSegmentsFromBitSegments()
    {
        var wordSegments = new Dictionary<string, Segment[]>();
        var byteSegments = new Dictionary<string, Segment[]>();

        foreach (var (area, segments) in ProgramBitSegments)
        {
            wordSegments[area] = segments
                .Select(static segment => new Segment(segment.Start >> 4, segment.End >> 4, segment.BaseAddress >> 1))
                .ToArray();
            byteSegments[area] = segments
                .Select(static segment => new Segment(segment.Start >> 4, segment.End >> 4, segment.BaseAddress))
                .ToArray();
        }

        return (wordSegments, byteSegments);
    }
}
