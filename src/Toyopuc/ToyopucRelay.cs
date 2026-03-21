using System.Globalization;
using System.Text.RegularExpressions;

namespace PlcComm.Toyopuc;

public static class ToyopucRelay
{
    private static readonly Regex PreferredPattern = new(
        @"P([0-9A-Fa-f])[-:]L([0-9A-Fa-f])\s*:\s*N([0-9A-Fa-fx]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex CompactPattern = new(
        @"([0-9A-Fa-f])[-:]([0-9A-Fa-f]):([0-9A-Fa-fx]+)",
        RegexOptions.Compiled);

    public static IReadOnlyList<(int LinkNo, int StationNo)> ParseRelayHops(string text)
    {
        var hops = new List<(int LinkNo, int StationNo)>();
        foreach (var part in text.Split(','))
        {
            var item = part.Trim();
            if (string.IsNullOrEmpty(item))
            {
                continue;
            }

            var preferred = PreferredPattern.Match(item);
            if (preferred.Success)
            {
                var link = (Convert.ToInt32(preferred.Groups[1].Value, 16) << 4)
                    | Convert.ToInt32(preferred.Groups[2].Value, 16);
                var station = ParseInteger(preferred.Groups[3].Value);
                if (station < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(text), "N number must be >= 1");
                }

                hops.Add((link, station));
                continue;
            }

            var compact = CompactPattern.Match(item);
            if (compact.Success)
            {
                var link = (Convert.ToInt32(compact.Groups[1].Value, 16) << 4)
                    | Convert.ToInt32(compact.Groups[2].Value, 16);
                var station = ParseInteger(compact.Groups[3].Value);
                hops.Add((link, station));
                continue;
            }

            var separator = item.IndexOf(':');
            if (separator < 0)
            {
                throw new ArgumentException("each hop must be LINK:STATION or P1-L2:N2", nameof(text));
            }

            var linkText = item[..separator];
            var stationText = item[(separator + 1)..];
            hops.Add((ParseInteger(linkText), ParseInteger(stationText)));
        }

        if (hops.Count == 0)
        {
            throw new ArgumentException("at least one hop is required", nameof(text));
        }

        return hops;
    }

    public static IReadOnlyList<(int LinkNo, int StationNo)> NormalizeRelayHops(object hops)
    {
        return hops switch
        {
            string text => ParseRelayHops(text),
            IEnumerable<(int LinkNo, int StationNo)> typed => NormalizeRelayHops(typed),
            _ => throw new ArgumentException("hops must be a relay string or an enumerable of (link, station) tuples", nameof(hops)),
        };
    }

    public static IReadOnlyList<(int LinkNo, int StationNo)> NormalizeRelayHops(IEnumerable<(int LinkNo, int StationNo)> hops)
    {
        (int LinkNo, int StationNo)[] normalized;
        if (hops is ICollection<(int LinkNo, int StationNo)> collection)
        {
            normalized = new (int LinkNo, int StationNo)[collection.Count];
            var index = 0;
            foreach (var hop in hops)
            {
                normalized[index++] = (hop.LinkNo & 0xFF, hop.StationNo & 0xFFFF);
            }
        }
        else
        {
            var list = new List<(int LinkNo, int StationNo)>();
            foreach (var hop in hops)
            {
                list.Add((hop.LinkNo & 0xFF, hop.StationNo & 0xFFFF));
            }

            normalized = list.ToArray();
        }

        if (normalized.Length == 0)
        {
            throw new ArgumentException("at least one hop is required", nameof(hops));
        }

        return normalized;
    }

    public static string FormatRelayHop(int linkNo, int stationNo)
    {
        return $"P{(linkNo >> 4) & 0x0F:X}-L{linkNo & 0x0F:X}:N{stationNo} (0x{linkNo:X2}:0x{stationNo:X4})";
    }

    public static (ResponseFrame Response, byte[] Padding) ParseRelayInnerResponse(byte[] innerRaw)
    {
        if (innerRaw.Length < 3)
        {
            throw new ToyopucProtocolError("Inner relay response too short");
        }

        var innerLength = innerRaw[0] | (innerRaw[1] << 8);
        var expected = 2 + innerLength;
        if (innerRaw.Length < expected)
        {
            throw new ToyopucProtocolError(
                $"Inner relay response truncated: expected {expected} bytes, got {innerRaw.Length} bytes");
        }

        var innerFrame = new byte[2 + expected];
        innerFrame[0] = ToyopucProtocol.FtResponse;
        innerFrame[1] = 0x00;
        Buffer.BlockCopy(innerRaw, 0, innerFrame, 2, expected);
        var padding = new byte[innerRaw.Length - expected];
        Buffer.BlockCopy(innerRaw, expected, padding, 0, padding.Length);
        return (ToyopucProtocol.ParseResponse(innerFrame), padding);
    }

    public static (IReadOnlyList<RelayLayer> Layers, ResponseFrame? FinalResponse) UnwrapRelayResponseChain(ResponseFrame response)
    {
        var layers = new List<RelayLayer>();
        var current = response;

        while (current.Cmd == 0x60)
        {
            if (current.Data.Length < 4)
            {
                throw new ToyopucProtocolError("Relay response data too short");
            }

            var linkNo = current.Data[0];
            var stationNo = current.Data[1] | (current.Data[2] << 8);
            var ack = current.Data[3];
            var innerRaw = new byte[current.Data.Length - 4];
            Buffer.BlockCopy(current.Data, 4, innerRaw, 0, innerRaw.Length);

            if (ack != 0x06)
            {
                layers.Add(new RelayLayer(linkNo, stationNo, ack, innerRaw));
                return (layers, null);
            }

            var (innerResponse, padding) = ParseRelayInnerResponse(innerRaw);
            layers.Add(new RelayLayer(linkNo, stationNo, ack, innerRaw, padding));
            current = innerResponse;
        }

        return (layers, current);
    }

    private static int ParseInteger(string value)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return Convert.ToInt32(value, 16);
        }

        return int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }
}
