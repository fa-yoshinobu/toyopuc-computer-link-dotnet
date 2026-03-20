using System.Globalization;

namespace Toyopuc.Tests;

public sealed class ResolverMatrixTests
{
    [Fact]
    public void SupportedBoundaryDevices_ResolveForEveryProfile()
    {
        foreach (var profile in ToyopucDeviceProfiles.GetNames())
        {
            var options = ToyopucAddressingOptions.FromProfile(profile);
            AssertSupportedDevices(profile, prefixed: false, options);
            AssertSupportedDevices(profile, prefixed: true, options);
        }
    }

    [Fact]
    public void UnsupportedBoundaryIndexes_AreRejectedByCatalogForEveryProfile()
    {
        foreach (var profile in ToyopucDeviceProfiles.GetNames())
        {
            AssertUnsupportedIndexes(profile, prefixed: false);
            AssertUnsupportedIndexes(profile, prefixed: true);
        }
    }

    [Fact]
    public void FrByteDevices_AreRejectedForProfilesThatSupportFr()
    {
        foreach (var profile in ToyopucDeviceProfiles.GetNames())
        {
            if (!ToyopucDeviceCatalog.GetAreas(prefixed: false, profile).Contains("FR", StringComparer.Ordinal))
            {
                continue;
            }

            var options = ToyopucAddressingOptions.FromProfile(profile);
            Assert.Throws<ArgumentException>(() => ToyopucDeviceResolver.ResolveDevice("FR000000L", options));
            Assert.Throws<ArgumentException>(() => ToyopucDeviceResolver.ResolveDevice("FR000000H", options));
        }
    }

    private static void AssertSupportedDevices(string profile, bool prefixed, ToyopucAddressingOptions options)
    {
        var prefix = prefixed ? "P1" : null;
        foreach (var area in ToyopucDeviceCatalog.GetAreas(prefixed, profile))
        {
            var descriptor = ToyopucDeviceCatalog.GetAreaDescriptor(area, profile);
            var ranges = ToyopucDeviceCatalog.GetSupportedRanges(area, prefixed, unit: descriptor.SupportsPackedWord ? "bit" : "word", packed: false, profile: profile);

            foreach (var index in ranges.SelectMany(GetBoundaryIndexes).Distinct().Order())
            {
                var defaultDevice = FormatDevice(prefix, descriptor, index, unit: descriptor.SupportsPackedWord ? "bit" : "word", packed: false, suffix: null);
                var resolvedDefault = ToyopucDeviceResolver.ResolveDevice(defaultDevice, options);
                Assert.Equal(area, resolvedDefault.Area);
                Assert.Equal(index, resolvedDefault.Index);
                Assert.Equal(prefix, resolvedDefault.Prefix);
                Assert.Equal(descriptor.SupportsPackedWord ? "bit" : "word", resolvedDefault.Unit);
                Assert.False(resolvedDefault.Packed);

                if (area == "FR")
                {
                    continue;
                }

                if (!SupportsByteVariant(prefixed, area, index, options))
                {
                    continue;
                }
            }

            if (area != "FR")
            {
                var byteRanges = ToyopucDeviceCatalog.GetSupportedRanges(area, prefixed, unit: "byte", packed: false, profile: profile);
                foreach (var index in byteRanges.SelectMany(GetBoundaryIndexes).Distinct().Order())
                {
                    if (!SupportsByteVariant(prefixed, area, index, options))
                    {
                        continue;
                    }

                    var lowByteDevice = FormatDevice(prefix, descriptor, index, unit: "byte", packed: false, suffix: "L");
                    var highByteDevice = FormatDevice(prefix, descriptor, index, unit: "byte", packed: false, suffix: "H");

                    var resolvedLow = ToyopucDeviceResolver.ResolveDevice(lowByteDevice, options, profile);
                    var resolvedHigh = ToyopucDeviceResolver.ResolveDevice(highByteDevice, options, profile);

                    Assert.Equal("byte", resolvedLow.Unit);
                    Assert.False(resolvedLow.High);
                    Assert.Equal(area, resolvedLow.Area);
                    Assert.Equal(index, resolvedLow.Index);

                    Assert.Equal("byte", resolvedHigh.Unit);
                    Assert.True(resolvedHigh.High);
                    Assert.Equal(area, resolvedHigh.Area);
                    Assert.Equal(index, resolvedHigh.Index);
                }
            }

            if (!descriptor.SupportsPackedWord)
            {
                continue;
            }

            var packedRanges = ToyopucDeviceCatalog.GetSupportedRanges(area, prefixed, unit: "word", packed: true, profile: profile);
            foreach (var index in packedRanges.SelectMany(GetBoundaryIndexes).Distinct().Order())
            {
                var packedDevice = FormatDevice(prefix, descriptor, index, unit: "word", packed: true, suffix: "W");
                var resolvedPacked = ToyopucDeviceResolver.ResolveDevice(packedDevice, options, profile);
                Assert.Equal("word", resolvedPacked.Unit);
                Assert.True(resolvedPacked.Packed);
                Assert.Equal(area, resolvedPacked.Area);
                Assert.Equal(index, resolvedPacked.Index);
            }
        }
    }

    private static void AssertUnsupportedIndexes(string profile, bool prefixed)
    {
        foreach (var descriptor in ToyopucDeviceCatalog.GetAreaDescriptors(profile))
        {
            var ranges = descriptor.GetRanges(prefixed, unit: descriptor.SupportsPackedWord ? "bit" : "word", packed: false);
            if (ranges.Count == 0)
            {
                continue;
            }

            var unsupportedIndexes = ranges
                .SelectMany(static range => new[] { range.Start - 1, range.End + 1 })
                .Where(index => index >= 0)
                .Where(index => ranges.All(range => !range.Contains(index)))
                .Distinct()
                .Order();

            foreach (var index in unsupportedIndexes)
            {
                Assert.False(
                    ToyopucDeviceCatalog.IsSupportedIndex(descriptor.Area, index, prefixed, profile),
                    $"{profile} {(prefixed ? "prefixed" : "direct")} {descriptor.Area}{index:X}");
            }

            if (!descriptor.SupportsPackedWord)
            {
                if (descriptor.Area == "FR")
                {
                    continue;
                }
            }

            var derivedRanges = descriptor.GetRanges(prefixed, unit: descriptor.SupportsPackedWord ? "word" : "byte", packed: descriptor.SupportsPackedWord);
            if (derivedRanges.Count == 0)
            {
                continue;
            }

            var unsupportedDerivedIndexes = derivedRanges
                .SelectMany(static range => new[] { range.Start - 1, range.End + 1 })
                .Where(index => index >= 0)
                .Where(index => derivedRanges.All(range => !range.Contains(index)))
                .Distinct()
                .Order();

            foreach (var index in unsupportedDerivedIndexes)
            {
                Assert.False(
                    ToyopucDeviceCatalog.IsSupportedIndex(
                        descriptor.Area,
                        index,
                        prefixed,
                        unit: descriptor.SupportsPackedWord ? "word" : "byte",
                        packed: descriptor.SupportsPackedWord,
                        profile: profile),
                    $"{profile} {(prefixed ? "prefixed" : "direct")} derived {descriptor.Area}{index:X}");
            }
        }
    }

    private static IEnumerable<int> GetBoundaryIndexes(ToyopucAddressRange range)
    {
        yield return range.Start;
        if (range.End != range.Start)
        {
            yield return range.End;
        }
    }

    private static string FormatDevice(
        string? prefix,
        ToyopucAreaDescriptor descriptor,
        int index,
        string unit,
        bool packed,
        string? suffix)
    {
        var width = descriptor.GetAddressWidth(unit, packed);
        var address = $"{descriptor.Area}{index.ToString($"X{width}", CultureInfo.InvariantCulture)}{suffix}";
        return prefix is null ? address : $"{prefix}-{address}";
    }

    private static bool SupportsByteVariant(bool prefixed, string area, int index, ToyopucAddressingOptions options)
    {
        if (prefixed)
        {
            return true;
        }

        if (area is "U" && index >= 0x08000)
        {
            return options.UseUpperUPc10;
        }

        if (area == "EB")
        {
            return index <= 0x07FFF || options.UseEbPc10;
        }

        return index <= 0x07FFF;
    }
}
