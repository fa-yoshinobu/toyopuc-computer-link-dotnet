using System.Globalization;

namespace Toyopuc.Tests;

public sealed class ProfileMatrixCsvTests
{
    [Fact]
    public void MatrixHeader_MatchesCanonicalProfileNames()
    {
        var matrix = DeviceProfileMatrix.Load();

        Assert.Equal(ToyopucDeviceProfiles.GetNames(), matrix.ProfileNames);
    }

    [Fact]
    public void MatrixSupportedAreas_MatchCatalogForEveryProfileAndAccessMode()
    {
        var matrix = DeviceProfileMatrix.Load();

        foreach (var profile in matrix.ProfileNames)
        {
            AssertAreas(prefixed: false);
            AssertAreas(prefixed: true);

            void AssertAreas(bool prefixed)
            {
                var expected = matrix.Rows
                    .Where(row => row.Prefixed == prefixed && row.HasSupport(profile))
                    .Select(row => row.Area)
                    .OrderBy(static area => area, StringComparer.Ordinal)
                    .ToArray();
                var actual = ToyopucDeviceCatalog.GetAreas(prefixed, profile)
                    .OrderBy(static area => area, StringComparer.Ordinal)
                    .ToArray();

                Assert.Equal(expected, actual);
            }
        }
    }

    [Fact]
    public void MatrixRows_MatchCatalogMetadataAndRanges()
    {
        var matrix = DeviceProfileMatrix.Load();

        foreach (var row in matrix.Rows)
        {
            var supportingProfiles = matrix.ProfileNames
                .Where(row.HasSupport)
                .ToArray();

            Assert.NotEmpty(supportingProfiles);

            foreach (var profile in supportingProfiles)
            {
                var descriptor = ToyopucDeviceCatalog.GetAreaDescriptor(row.Area, profile);
                var actualRanges = ToyopucDeviceCatalog.GetSupportedRanges(row.Area, row.Prefixed, profile);
                var expectedRanges = row.GetRanges(profile);

                Assert.Equal(row.PackedWord, descriptor.SupportsPackedWord);
                Assert.Equal(row.AddressWidth, descriptor.AddressWidth);
                Assert.Equal(row.SuggestedStartStep, descriptor.SuggestedStartStep);
                Assert.Equal(expectedRanges, actualRanges);
            }
        }
    }

    private sealed record DeviceProfileMatrix(
        IReadOnlyList<string> ProfileNames,
        IReadOnlyList<MatrixRow> Rows)
    {
        private static readonly Lazy<DeviceProfileMatrix> Cached = new(LoadCore);

        public static DeviceProfileMatrix Load()
        {
            return Cached.Value;
        }

        private static DeviceProfileMatrix LoadCore()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "device_profile_matrix_r2.csv");
            var lines = File.ReadAllLines(path);

            Assert.NotEmpty(lines);

            var header = ParseCsvLine(lines[0]);
            const int fixedColumnCount = 6;
            var profileNames = header.Skip(fixedColumnCount).ToArray();
            var rows = new List<MatrixRow>(capacity: Math.Max(0, lines.Length - 1));

            for (var i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }

                var columns = ParseCsvLine(lines[i]);
                Assert.Equal(header.Count, columns.Count);

                var rangesByProfile = new Dictionary<string, IReadOnlyList<ToyopucAddressRange>>(StringComparer.Ordinal);
                for (var profileIndex = 0; profileIndex < profileNames.Length; profileIndex++)
                {
                    rangesByProfile[profileNames[profileIndex]] = ParseRanges(columns[fixedColumnCount + profileIndex]);
                }

                rows.Add(new MatrixRow(
                    Area: columns[0],
                    Prefixed: columns[1].Equals("prefixed", StringComparison.OrdinalIgnoreCase),
                    PackedWord: columns[3].Equals("yes", StringComparison.OrdinalIgnoreCase),
                    AddressWidth: int.Parse(columns[4], CultureInfo.InvariantCulture),
                    SuggestedStartStep: ParseHex(columns[5]),
                    RangesByProfile: rangesByProfile));
            }

            return new DeviceProfileMatrix(profileNames, rows);
        }

        private static List<string> ParseCsvLine(string line)
        {
            var results = new List<string>();
            var current = new System.Text.StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (ch == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }

                    continue;
                }

                if (ch == ',' && !inQuotes)
                {
                    results.Add(current.ToString());
                    current.Clear();
                    continue;
                }

                current.Append(ch);
            }

            results.Add(current.ToString());
            return results;
        }

        private static IReadOnlyList<ToyopucAddressRange> ParseRanges(string value)
        {
            if (value == "-")
            {
                return Array.Empty<ToyopucAddressRange>();
            }

            return value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(static segment =>
                {
                    var parts = segment.Split('-', StringSplitOptions.TrimEntries);
                    Assert.Equal(2, parts.Length);
                    return new ToyopucAddressRange(ParseHex(parts[0]), ParseHex(parts[1]));
                })
                .ToArray();
        }

        private static int ParseHex(string value)
        {
            var normalized = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? value[2..]
                : value;
            return int.Parse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
    }

    private sealed record MatrixRow(
        string Area,
        bool Prefixed,
        bool PackedWord,
        int AddressWidth,
        int SuggestedStartStep,
        IReadOnlyDictionary<string, IReadOnlyList<ToyopucAddressRange>> RangesByProfile)
    {
        public bool HasSupport(string profile)
        {
            return GetRanges(profile).Count > 0;
        }

        public IReadOnlyList<ToyopucAddressRange> GetRanges(string profile)
        {
            return RangesByProfile[profile];
        }
    }
}
