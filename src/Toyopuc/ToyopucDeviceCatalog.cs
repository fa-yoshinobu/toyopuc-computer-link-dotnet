using System.Globalization;

namespace PlcComm.Toyopuc;

public static class ToyopucDeviceCatalog
{
    public static IReadOnlyList<ToyopucAreaDescriptor> GetAreaDescriptors(string? profile = null)
    {
        return ToyopucDeviceProfiles.FromName(profile).Areas;
    }

    public static IReadOnlyList<string> GetAreas(bool prefixed, string? profile = null)
    {
        var descriptors = GetAreaDescriptors(profile);
        var areas = new List<string>(descriptors.Count);
        for (var i = 0; i < descriptors.Count; i++)
        {
            var descriptor = descriptors[i];
            if ((prefixed && descriptor.SupportsPrefixed) || (!prefixed && descriptor.SupportsDirect))
            {
                areas.Add(descriptor.Area);
            }
        }

        return areas;
    }

    public static ToyopucAreaDescriptor GetAreaDescriptor(string area, string? profile = null)
    {
        var normalizedArea = area.Trim().ToUpperInvariant();
        var descriptors = GetAreaDescriptors(profile);
        for (var i = 0; i < descriptors.Count; i++)
        {
            if (descriptors[i].Area == normalizedArea)
            {
                return descriptors[i];
            }
        }

        throw new ArgumentException($"Unknown area for profile '{profile ?? "Generic"}': {area}", nameof(area));
    }

    public static IReadOnlyList<ToyopucAddressRange> GetSupportedRanges(string area, bool prefixed, string? profile = null)
    {
        return GetSupportedRanges(area, prefixed, packed: false, profile);
    }

    public static IReadOnlyList<ToyopucAddressRange> GetSupportedRanges(string area, bool prefixed, bool packed, string? profile = null)
    {
        var unit = packed ? "word" : GetAreaDescriptor(area, profile).SupportsPackedWord ? "bit" : "word";
        return GetSupportedRanges(area, prefixed, unit, packed, profile);
    }

    public static IReadOnlyList<ToyopucAddressRange> GetSupportedRanges(
        string area,
        bool prefixed,
        string unit,
        bool packed = false,
        string? profile = null)
    {
        var descriptor = GetAreaDescriptor(area, profile);
        return GetSupportedRanges(descriptor, prefixed, unit, packed);
    }

    public static ToyopucAddressRange GetSupportedRange(string area, bool prefixed, string? profile = null)
    {
        return GetSupportedRange(area, prefixed, packed: false, profile);
    }

    public static ToyopucAddressRange GetSupportedRange(string area, bool prefixed, bool packed, string? profile = null)
    {
        var ranges = GetSupportedRanges(area, prefixed, packed, profile);
        if (ranges.Count == 1)
        {
            return ranges[0];
        }

        throw new InvalidOperationException(
            $"Area {area} for profile '{profile ?? "Generic"}' has multiple ranges; use {nameof(GetSupportedRanges)} instead.");
    }

    public static ToyopucAddressRange GetSupportedRange(
        string area,
        bool prefixed,
        string unit,
        bool packed = false,
        string? profile = null)
    {
        var ranges = GetSupportedRanges(area, prefixed, unit, packed, profile);
        if (ranges.Count == 1)
        {
            return ranges[0];
        }

        throw new InvalidOperationException(
            $"Area {area} for profile '{profile ?? "Generic"}' has multiple ranges; use {nameof(GetSupportedRanges)} instead.");
    }

    public static bool IsSupportedIndex(string area, int index, bool prefixed, string? profile = null)
    {
        return IsSupportedIndex(area, index, prefixed, packed: false, profile);
    }

    public static bool IsSupportedIndex(string area, int index, bool prefixed, bool packed, string? profile = null)
    {
        var unit = packed ? "word" : GetAreaDescriptor(area, profile).SupportsPackedWord ? "bit" : "word";
        return IsSupportedIndex(area, index, prefixed, unit, packed, profile);
    }

    public static bool IsSupportedIndex(
        string area,
        int index,
        bool prefixed,
        string unit,
        bool packed = false,
        string? profile = null)
    {
        try
        {
            return GetSupportedRanges(area, prefixed, unit, packed, profile).Any(range => range.Contains(index));
        }
        catch
        {
            return false;
        }
    }

    public static IReadOnlyList<string> GetSuggestedStartAddresses(
        string area,
        string? prefix = null,
        string? profile = null)
    {
        var prefixed = !string.IsNullOrWhiteSpace(prefix);
        var descriptor = GetAreaDescriptor(area, profile);
        var unit = descriptor.SupportsPackedWord ? "bit" : "word";
        var ranges = GetSupportedRanges(descriptor, prefixed, unit, packed: false);
        return GetSuggestedStartAddresses(descriptor, ranges, prefix, unit, packed: false, ToyopucDeviceProfiles.FromName(profile).AddressingOptions);
    }

    public static IReadOnlyList<string> GetSuggestedStartAddresses(
        string area,
        ToyopucAddressingOptions? options)
    {
        return GetSuggestedStartAddresses(area, prefix: null, options);
    }

    public static IReadOnlyList<string> GetSuggestedStartAddresses(
        string area,
        string? prefix,
        ToyopucAddressingOptions? options)
    {
        var prefixed = !string.IsNullOrWhiteSpace(prefix);
        var descriptor = GetAreaDescriptor(area, profile: null);
        var unit = descriptor.SupportsPackedWord ? "bit" : "word";
        var ranges = GetSupportedRanges(descriptor, prefixed, unit, packed: false);
        return GetSuggestedStartAddresses(descriptor, ranges, prefix, unit, packed: false, options ?? ToyopucAddressingOptions.Default);
    }

    public static IReadOnlyList<string> GetSuggestedStartAddresses(
        string area,
        string? prefix,
        string unit,
        bool packed,
        string? profile)
    {
        var prefixed = !string.IsNullOrWhiteSpace(prefix);
        var descriptor = GetAreaDescriptor(area, profile);
        var ranges = GetSupportedRanges(descriptor, prefixed, unit, packed);
        return GetSuggestedStartAddresses(descriptor, ranges, prefix, unit, packed, ToyopucDeviceProfiles.FromName(profile).AddressingOptions);
    }

    private static IReadOnlyList<string> GetSuggestedStartAddresses(
        ToyopucAreaDescriptor descriptor,
        IReadOnlyList<ToyopucAddressRange> ranges,
        string? prefix,
        string unit,
        bool packed,
        ToyopucAddressingOptions options)
    {
        var normalizedPrefix = NormalizePrefix(prefix);
        ValidateAccess(descriptor, normalizedPrefix);

        var suffix = unit switch
        {
            "word" when packed => "W",
            "byte" => "L",
            _ => string.Empty,
        };
        var addressPrefix = normalizedPrefix is null ? string.Empty : $"{normalizedPrefix}-";
        var results = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var range in ranges)
        {
            for (var value = range.Start; value >= range.Start && value <= range.End; value += descriptor.SuggestedStartStep)
            {
                AddCandidate(value);

                if (WillOverflowNext(value, descriptor.SuggestedStartStep))
                {
                    break;
                }
            }

            AddCandidate(range.End);
        }

        return results;

        void AddCandidate(int value)
        {
            var width = descriptor.GetAddressWidth(unit, packed);
            var candidate = value.ToString($"X{width}", CultureInfo.InvariantCulture);
            if (seen.Add(candidate) && CanResolve($"{addressPrefix}{descriptor.Area}{candidate}{suffix}", options))
            {
                results.Add(candidate);
            }
        }
    }

    private static IReadOnlyList<ToyopucAddressRange> GetSupportedRanges(
        ToyopucAreaDescriptor descriptor,
        bool prefixed,
        string unit,
        bool packed)
    {
        var derived = descriptor.UsesDerivedAccess(unit, packed);
        var ranges = descriptor.GetRanges(prefixed, unit, packed);
        if (ranges.Count == 0)
        {
            if (derived && descriptor.SupportsPackedWord)
            {
                var derivedAccessMode = prefixed ? "prefixed" : "direct";
                throw new ArgumentException($"Area {descriptor.Area} does not support derived word/byte access for {derivedAccessMode} use", nameof(descriptor));
            }

            var accessMode = prefixed ? "prefixed" : "direct";
            throw new ArgumentException($"Area {descriptor.Area} is not available for {accessMode} access", nameof(descriptor));
        }

        return ranges;
    }

    private static string? NormalizePrefix(string? prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return null;
        }

        return prefix.Trim().ToUpperInvariant();
    }

    private static void ValidateAccess(ToyopucAreaDescriptor descriptor, string? prefix)
    {
        if (prefix is null)
        {
            _ = GetSupportedRanges(descriptor, prefixed: false, unit: descriptor.SupportsPackedWord ? "bit" : "word", packed: false);
            return;
        }

        if (prefix is not ("P1" or "P2" or "P3"))
        {
            throw new ArgumentException($"Unsupported prefix: {prefix}", nameof(prefix));
        }

        _ = GetSupportedRanges(descriptor, prefixed: true, unit: descriptor.SupportsPackedWord ? "bit" : "word", packed: false);
    }

    private static bool CanResolve(string device, ToyopucAddressingOptions options)
    {
        try
        {
            _ = ToyopucDeviceResolver.ResolveDevice(device, options);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool WillOverflowNext(int value, int step)
    {
        return value > int.MaxValue - step;
    }
}
