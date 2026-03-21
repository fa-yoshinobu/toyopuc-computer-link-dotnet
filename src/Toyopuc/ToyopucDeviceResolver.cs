namespace PlcComm.Toyopuc;

public static class ToyopucDeviceResolver
{
    private static readonly HashSet<string> BasicBitAreas = new(StringComparer.Ordinal)
    {
        "P", "K", "V", "T", "C", "L", "X", "Y", "M",
    };

    private static readonly HashSet<string> BasicWordAreas = new(StringComparer.Ordinal)
    {
        "S", "N", "R", "D", "B",
    };

    private static readonly HashSet<string> ExtBitAreas = new(StringComparer.Ordinal)
    {
        "EP", "EK", "EV", "ET", "EC", "EL", "EX", "EY", "EM", "GX", "GY", "GM",
    };

    private static readonly HashSet<string> ExtWordAreas = new(StringComparer.Ordinal)
    {
        "ES", "EN", "H", "U", "EB", "FR",
    };

    private static readonly string[] PackedWordAreaCandidates = BuildAreaCandidates(BasicBitAreas, ExtBitAreas);
    private static readonly string[] ByteAreaCandidates = BuildAreaCandidates(BasicBitAreas, BasicWordAreas, ExtBitAreas, ExtWordAreas);
    private static readonly string[] DefaultAreaCandidates = BuildAreaCandidates(ExtBitAreas, ExtWordAreas, BasicBitAreas, BasicWordAreas);

    private static readonly IReadOnlyDictionary<string, int> PrefixProgramNumber = new Dictionary<string, int>
    {
        ["P1"] = 0x01,
        ["P2"] = 0x02,
        ["P3"] = 0x03,
    };

    private static readonly IReadOnlyDictionary<string, (int No, int ByteBase)> ExtBitSpecs =
        new Dictionary<string, (int No, int ByteBase)>
        {
            ["EP"] = (0x00, 0x0000),
            ["EK"] = (0x00, 0x0200),
            ["EV"] = (0x00, 0x0400),
            ["ET"] = (0x00, 0x0600),
            ["EC"] = (0x00, 0x0600),
            ["EL"] = (0x00, 0x0700),
            ["EX"] = (0x00, 0x0B00),
            ["EY"] = (0x00, 0x0B00),
            ["EM"] = (0x00, 0x0C00),
            ["GX"] = (0x07, 0x0000),
            ["GY"] = (0x07, 0x0000),
            ["GM"] = (0x07, 0x2000),
        };

    public static ResolvedDevice ResolveDevice(string device, ToyopucAddressingOptions? options = null, string? profile = null)
    {
        var normalizedProfile = string.IsNullOrWhiteSpace(profile)
            ? null
            : ToyopucDeviceProfiles.NormalizeName(profile);
        options ??= normalizedProfile is null
            ? ToyopucAddressingOptions.Default
            : ToyopucAddressingOptions.FromProfile(normalizedProfile);
        var (prefix, area, unit) = InferUnitAndArea(device);
        var text = device.Trim().ToUpperInvariant();

        if (prefix is not null)
        {
            var (exNo, parsed) = ToyopucAddress.ParsePrefixedAddress(text, unit);
            ValidateProfileAccess(parsed, prefix, normalizedProfile, device);
            if (unit == "bit")
            {
                var (bitNo, address) = ToyopucAddress.EncodeProgramBitAddress(parsed);
                return new ResolvedDevice(
                    text,
                    "program-bit",
                    "bit",
                    parsed.Area,
                    parsed.Index,
                    Prefix: prefix,
                    Packed: parsed.Packed,
                    No: PrefixProgramNumber[prefix],
                    Address: address,
                    BitNo: bitNo,
                    Address32: ToyopucAddress.EncodePc10BitAddress(parsed) | (exNo << 19));
            }

            if (unit == "word")
            {
                if (parsed.Packed && (BasicWordAreas.Contains(parsed.Area) || ExtWordAreas.Contains(parsed.Area)))
                {
                    throw new ArgumentException($"W suffix is only valid for bit-device families: {text}", nameof(device));
                }

                return new ResolvedDevice(
                    text,
                    "program-word",
                    "word",
                    parsed.Area,
                    parsed.Index,
                    Prefix: prefix,
                    Packed: parsed.Packed,
                    No: PrefixProgramNumber[prefix],
                    Address: ToyopucAddress.EncodeProgramWordAddress(parsed));
            }

            return new ResolvedDevice(
                text,
                "program-byte",
                "byte",
                parsed.Area,
                parsed.Index,
                Prefix: prefix,
                High: parsed.High,
                Packed: parsed.Packed,
                No: PrefixProgramNumber[prefix],
                Address: ToyopucAddress.EncodeProgramByteAddress(parsed));
        }

        var parsedAddress = ToyopucAddress.ParseAddress(text, unit);
        ValidateProfileAccess(parsedAddress, prefix: null, normalizedProfile, device);

        if (unit == "bit")
        {
            if (TryResolveDirectPc10Bit(parsedAddress, text, options, out var directPc10Bit))
            {
                return directPc10Bit;
            }

            if (BasicBitAreas.Contains(parsedAddress.Area))
            {
                return new ResolvedDevice(
                    text,
                    "basic-bit",
                    "bit",
                    parsedAddress.Area,
                    parsedAddress.Index,
                    Packed: parsedAddress.Packed,
                    BasicAddress: ToyopucAddress.EncodeBitAddress(parsedAddress));
            }

            return ResolveExtBit(parsedAddress, text);
        }

        if (unit == "word")
        {
            if (parsedAddress.Packed && (BasicWordAreas.Contains(parsedAddress.Area) || ExtWordAreas.Contains(parsedAddress.Area)))
            {
                throw new ArgumentException($"W suffix is only valid for bit-device families: {text}", nameof(device));
            }

            if (TryResolveDirectPc10Derived(parsedAddress, text, options, out var directPc10DerivedWord))
            {
                return directPc10DerivedWord;
            }

            if (BasicWordAreas.Contains(parsedAddress.Area) || BasicBitAreas.Contains(parsedAddress.Area))
            {
                return new ResolvedDevice(
                    text,
                    "basic-word",
                    "word",
                    parsedAddress.Area,
                    parsedAddress.Index,
                    Packed: parsedAddress.Packed,
                    BasicAddress: ToyopucAddress.EncodeWordAddress(parsedAddress));
            }

            if (parsedAddress.Area == "U" && parsedAddress.Index >= 0x08000 && options.UseUpperUPc10)
            {
                return new ResolvedDevice(
                    text,
                    "pc10-word",
                    "word",
                    parsedAddress.Area,
                    parsedAddress.Index,
                    Packed: parsedAddress.Packed,
                    Address32: Pc10UAddress32(parsedAddress.Index));
            }

            if (parsedAddress.Area == "EB" && parsedAddress.Index <= 0x3FFFF && options.UseEbPc10)
            {
                return new ResolvedDevice(
                    text,
                    "pc10-word",
                    "word",
                    parsedAddress.Area,
                    parsedAddress.Index,
                    Packed: parsedAddress.Packed,
                    Address32: Pc10EbAddress32(parsedAddress.Index));
            }

            if (parsedAddress.Area == "FR" && options.UseFrPc10)
            {
                return new ResolvedDevice(
                    text,
                    "pc10-word",
                    "word",
                    parsedAddress.Area,
                    parsedAddress.Index,
                    Packed: parsedAddress.Packed,
                    Address32: ToyopucAddress.EncodeFrWordAddr32(parsedAddress.Index));
            }

            var ext = ToyopucAddress.EncodeExtNoAddress(parsedAddress.Area, parsedAddress.Index, "word");
            return new ResolvedDevice(
                text,
                "ext-word",
                "word",
                parsedAddress.Area,
                parsedAddress.Index,
                Packed: parsedAddress.Packed,
                No: ext.No,
                Address: ext.Address);
        }

        if (TryResolveDirectPc10Derived(parsedAddress, text, options, out var directPc10DerivedByte))
        {
            return directPc10DerivedByte;
        }

        if (BasicWordAreas.Contains(parsedAddress.Area) || BasicBitAreas.Contains(parsedAddress.Area))
        {
            return new ResolvedDevice(
                text,
                "basic-byte",
                "byte",
                parsedAddress.Area,
                parsedAddress.Index,
                High: parsedAddress.High,
                Packed: parsedAddress.Packed,
                BasicAddress: ToyopucAddress.EncodeByteAddress(parsedAddress));
        }

        if (parsedAddress.Area == "U" && parsedAddress.Index >= 0x08000 && options.UseUpperUPc10)
        {
            return new ResolvedDevice(
                text,
                "pc10-byte",
                "byte",
                parsedAddress.Area,
                parsedAddress.Index,
                High: parsedAddress.High,
                Packed: parsedAddress.Packed,
                Address32: Pc10UAddress32(parsedAddress.Index, byteAddress: true, high: parsedAddress.High));
        }

        if (parsedAddress.Area == "EB" && parsedAddress.Index <= 0x3FFFF && options.UseEbPc10)
        {
            return new ResolvedDevice(
                text,
                "pc10-byte",
                "byte",
                parsedAddress.Area,
                parsedAddress.Index,
                High: parsedAddress.High,
                Packed: parsedAddress.Packed,
                Address32: Pc10EbAddress32(parsedAddress.Index, byteAddress: true, high: parsedAddress.High));
        }

        if (parsedAddress.Area == "FR")
        {
            throw new ArgumentException("FR does not support byte access; use word access via PC10 block commands", nameof(device));
        }

        var extByte = ToyopucAddress.EncodeExtNoAddress(parsedAddress.Area, (parsedAddress.Index * 2) + (parsedAddress.High ? 1 : 0), "byte");
        return new ResolvedDevice(
            text,
            "ext-byte",
            "byte",
            parsedAddress.Area,
            parsedAddress.Index,
            High: parsedAddress.High,
            Packed: parsedAddress.Packed,
            No: extByte.No,
            Address: extByte.Address);
    }

    private static (string? Prefix, string Area, string Unit) InferUnitAndArea(string device)
    {
        var text = device.Trim().ToUpperInvariant();
        string? prefix = null;
        var body = text;
        if (text.StartsWith("P1-", StringComparison.Ordinal)
            || text.StartsWith("P2-", StringComparison.Ordinal)
            || text.StartsWith("P3-", StringComparison.Ordinal))
        {
            var parts = text.Split('-', 2);
            prefix = parts[0];
            body = parts[1];
        }

        if (body.EndsWith('W'))
        {
            var packedWord = body[..^1];
            foreach (var candidate in PackedWordAreaCandidates)
            {
                if (packedWord.StartsWith(candidate, StringComparison.Ordinal))
                {
                    return (prefix, candidate, "word");
                }
            }

            throw new ArgumentException(
                $"Unknown packed word area in '{device}'. Valid packed-word areas: {string.Join(", ", PackedWordAreaCandidates.Select(a => a + "W"))}.",
                nameof(device));
        }

        if (body.EndsWith('L') || body.EndsWith('H'))
        {
            var byteText = body[..^1];
            foreach (var candidate in ByteAreaCandidates)
            {
                if (byteText.StartsWith(candidate, StringComparison.Ordinal))
                {
                    return (prefix, candidate, "byte");
                }
            }

            throw new ArgumentException(
                $"Unknown byte address area in '{device}'. Valid areas: {string.Join(", ", ByteAreaCandidates.Select(a => a + "L/H"))}.",
                nameof(device));
        }

        foreach (var candidate in DefaultAreaCandidates)
        {
            if (body.StartsWith(candidate, StringComparison.Ordinal))
            {
                return (prefix, candidate, ExtBitAreas.Contains(candidate) || BasicBitAreas.Contains(candidate) ? "bit" : "word");
            }
        }

        throw new ArgumentException(
            $"Unknown device area in '{device}'. Valid areas: {string.Join(", ", DefaultAreaCandidates)}.",
            nameof(device));
    }

    private static int Pc10UAddress32(int index, bool byteAddress = false, bool high = false)
    {
        if (index is < 0x08000 or > 0x1FFFF)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "U PC10 range is 0x08000-0x1FFFF");
        }

        var block = index / 0x8000;
        var exNo = 0x03 + block;
        var byteOffset = (index % 0x8000) * 2 + (byteAddress && high ? 1 : 0);
        if (byteAddress && !high)
        {
            byteOffset = (index % 0x8000) * 2;
        }

        return ToyopucAddress.EncodeExNoByteU32(exNo, byteOffset);
    }

    private static int Pc10EbAddress32(int index, bool byteAddress = false, bool high = false)
    {
        if (index is < 0x00000 or > 0x3FFFF)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "EB PC10 range is 0x00000-0x3FFFF");
        }

        var block = index / 0x8000;
        var exNo = 0x10 + block;
        var byteOffset = (index % 0x8000) * 2 + (byteAddress && high ? 1 : 0);
        if (byteAddress && !high)
        {
            byteOffset = (index % 0x8000) * 2;
        }

        return ToyopucAddress.EncodeExNoByteU32(exNo, byteOffset);
    }

    private static ResolvedDevice ResolveExtBit(ParsedAddress parsedAddress, string text)
    {
        var spec = ExtBitSpecs[parsedAddress.Area];
        return new ResolvedDevice(
            text,
            "ext-bit",
            "bit",
            parsedAddress.Area,
            parsedAddress.Index,
            No: spec.No,
            BitNo: parsedAddress.Index & 0x07,
            Address: spec.ByteBase + (parsedAddress.Index >> 3));
    }

    private static bool TryResolveDirectPc10Bit(
        ParsedAddress parsedAddress,
        string text,
        ToyopucAddressingOptions options,
        out ResolvedDevice resolved)
    {
        var allowed = parsedAddress.Area switch
        {
            "P" or "V" or "T" or "C" => options.UseUpperBitPc10 && parsedAddress.Index is >= 0x1000 and <= 0x17FF,
            "L" => options.UseUpperBitPc10 && parsedAddress.Index is >= 0x1000 and <= 0x2FFF,
            "M" => options.UseUpperMBitPc10 && parsedAddress.Index is >= 0x1000 and <= 0x17FF,
            _ => false,
        };

        if (!allowed)
        {
            resolved = null!;
            return false;
        }

        resolved = new ResolvedDevice(
            text,
            "pc10-bit",
            "bit",
            parsedAddress.Area,
            parsedAddress.Index,
            Packed: parsedAddress.Packed,
            Address32: ToyopucAddress.EncodePc10BitAddress(parsedAddress));
        return true;
    }

    private static bool TryResolveDirectPc10Derived(
        ParsedAddress parsedAddress,
        string text,
        ToyopucAddressingOptions options,
        out ResolvedDevice resolved)
    {
        var allowed = parsedAddress.Area switch
        {
            "P" or "V" or "T" or "C" or "L" => options.UseUpperBitPc10 && parsedAddress.Index >= 0x100,
            "M" => options.UseUpperMBitPc10 && parsedAddress.Index >= 0x100,
            _ => false,
        };

        if (!allowed)
        {
            resolved = null!;
            return false;
        }

        var byteAddress = parsedAddress.Unit switch
        {
            "word" => ToyopucAddress.EncodeWordAddress(parsedAddress) * 2,
            "byte" => ToyopucAddress.EncodeByteAddress(parsedAddress),
            _ => throw new ArgumentException($"Unsupported direct PC10 derived unit: {parsedAddress.Unit}", nameof(parsedAddress)),
        };

        resolved = new ResolvedDevice(
            text,
            parsedAddress.Unit == "word" ? "pc10-word" : "pc10-byte",
            parsedAddress.Unit,
            parsedAddress.Area,
            parsedAddress.Index,
            High: parsedAddress.High,
            Packed: parsedAddress.Packed,
            Address32: ToyopucAddress.EncodeExNoByteU32(0x00, byteAddress));
        return true;
    }

    private static void ValidateProfileAccess(ParsedAddress parsedAddress, string? prefix, string? profile, string device)
    {
        var profileName = profile ?? "Generic";
        var descriptor = ToyopucDeviceCatalog.GetAreaDescriptor(parsedAddress.Area, profile);
        if (parsedAddress.Packed && !descriptor.SupportsPackedWord)
        {
            throw new ArgumentException(
                $"W suffix is not available for area {parsedAddress.Area} in profile '{profileName}': {device}",
                nameof(device));
        }

        var derived = descriptor.UsesDerivedAccess(parsedAddress.Unit, parsedAddress.Packed);
        var expectedWidth = descriptor.GetAddressWidth(parsedAddress.Unit, parsedAddress.Packed);
        if (parsedAddress.DigitCount is int actualWidth && actualWidth > expectedWidth)
        {
            throw new ArgumentException(
                $"Address width out of range for profile '{profileName}': {device} (max {expectedWidth} hex digits)",
                nameof(device));
        }

        if (string.IsNullOrWhiteSpace(profile))
        {
            return;
        }

        var prefixed = prefix is not null;
        var ranges = descriptor.GetRanges(prefixed, parsedAddress.Unit, parsedAddress.Packed);
        if (ranges.Count == 0)
        {
            var accessMode = prefixed ? "prefixed" : "direct";
            var accessKind = derived ? " derived-word/byte" : string.Empty;
            throw new ArgumentException(
                $"Area {parsedAddress.Area} is not available for{accessKind} {accessMode} access in profile '{profileName}': {device}",
                nameof(device));
        }

        var supported = false;
        for (var i = 0; i < ranges.Count; i++)
        {
            if (ranges[i].Contains(parsedAddress.Index))
            {
                supported = true;
                break;
            }
        }

        if (!supported)
        {
            throw new ArgumentException(
                $"Address out of range for profile '{profileName}': {device}",
                nameof(device));
        }
    }

    private static string[] BuildAreaCandidates(params IEnumerable<string>[] groups)
    {
        var list = new List<string>();
        foreach (var group in groups)
        {
            foreach (var item in group)
            {
                list.Add(item);
            }
        }

        list.Sort(static (left, right) =>
        {
            var lengthCompare = right.Length.CompareTo(left.Length);
            return lengthCompare != 0 ? lengthCompare : string.CompareOrdinal(left, right);
        });
        return list.ToArray();
    }
}
