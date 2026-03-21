namespace PlcComm.Toyopuc;

public sealed record ToyopucAddressRange(int Start, int End)
{
    public bool Contains(int index)
    {
        return index >= Start && index <= End;
    }
}

public sealed record ToyopucAreaDescriptor(
    string Area,
    IReadOnlyList<ToyopucAddressRange> DirectRanges,
    IReadOnlyList<ToyopucAddressRange> PrefixedRanges,
    bool SupportsPackedWord,
    int AddressWidth,
    int SuggestedStartStep,
    IReadOnlyList<ToyopucAddressRange>? PackedDirectRangesOverride = null,
    IReadOnlyList<ToyopucAddressRange>? PackedPrefixedRangesOverride = null)
{
    public ToyopucAddressRange? DirectRange => DirectRanges.Count == 1 ? DirectRanges[0] : null;

    public ToyopucAddressRange? PrefixedRange => PrefixedRanges.Count == 1 ? PrefixedRanges[0] : null;

    public int PackedAddressWidth => Math.Max(1, AddressWidth - 1);

    public int DerivedAddressWidth => PackedAddressWidth;

    public ToyopucAddressRange? PackedDirectRange => TryGetRange(prefixed: false, packed: true);

    public ToyopucAddressRange? PackedPrefixedRange => TryGetRange(prefixed: true, packed: true);

    public bool SupportsDirect => DirectRanges.Count > 0;

    public bool SupportsPrefixed => PrefixedRanges.Count > 0;

    public bool UsesDerivedAccess(string unit, bool packed = false)
    {
        if (!SupportsPackedWord)
        {
            return false;
        }

        return unit == "byte" || (unit == "word" && packed);
    }

    public int GetAddressWidth(string unit, bool packed = false)
    {
        return UsesDerivedAccess(unit, packed) ? DerivedAddressWidth : AddressWidth;
    }

    public IReadOnlyList<ToyopucAddressRange> GetRanges(bool prefixed, bool packed)
    {
        if (!packed)
        {
            return prefixed ? PrefixedRanges : DirectRanges;
        }

        var overrides = prefixed ? PackedPrefixedRangesOverride : PackedDirectRangesOverride;
        if (overrides is not null)
        {
            return overrides;
        }

        var source = prefixed ? PrefixedRanges : DirectRanges;
        return source
            .Select(static range => new ToyopucAddressRange(range.Start >> 4, range.End >> 4))
            .Distinct()
            .ToArray();
    }

    public IReadOnlyList<ToyopucAddressRange> GetRanges(bool prefixed, string unit, bool packed = false)
    {
        return GetRanges(prefixed, UsesDerivedAccess(unit, packed));
    }

    private ToyopucAddressRange? TryGetRange(bool prefixed, bool packed)
    {
        var ranges = GetRanges(prefixed, packed);
        return ranges.Count == 1 ? ranges[0] : null;
    }
}

public sealed record ToyopucDeviceProfile(
    string Name,
    ToyopucAddressingOptions AddressingOptions,
    IReadOnlyList<ToyopucAreaDescriptor> Areas);
