using System.Globalization;
using System.Runtime.CompilerServices;

namespace PlcComm.Toyopuc;

/// <summary>
/// Extension methods for <see cref="ToyopucDeviceClient"/> providing typed read/write helpers,
/// bit-in-word access, named-device reads, and polling.
/// </summary>
public static class ToyopucDeviceClientExtensions
{
    /// <summary>
    /// Reads one device value and converts it to the specified type.
    /// </summary>
    /// <param name="client">The client to use.</param>
    /// <param name="device">Device address string.</param>
    /// <param name="dtype">
    /// Type code: "U" = ushort, "S" = short (signed 16-bit),
    /// "D" = uint (32-bit), "L" = int (signed 32-bit), "F" = float32.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<object> ReadTypedAsync(
        this ToyopucDeviceClient client,
        string device,
        string dtype,
        CancellationToken ct = default)
    {
        switch (dtype.ToUpperInvariant())
        {
            case "F":
            {
                var floats = await client.ReadFloat32sAsync(device, 1, cancellationToken: ct).ConfigureAwait(false);
                return floats[0];
            }
            case "D":
            {
                var dwords = await client.ReadDWordsAsync(device, 1, cancellationToken: ct).ConfigureAwait(false);
                return dwords[0];
            }
            case "L":
            {
                var dwords = await client.ReadDWordsAsync(device, 1, cancellationToken: ct).ConfigureAwait(false);
                return unchecked((int)dwords[0]);
            }
            case "S":
            {
                var raw = await client.ReadAsync(device, 1, ct).ConfigureAwait(false);
                return unchecked((short)Convert.ToUInt16(raw, CultureInfo.InvariantCulture));
            }
            default: // "U"
            {
                var raw = await client.ReadAsync(device, 1, ct).ConfigureAwait(false);
                return Convert.ToUInt16(raw, CultureInfo.InvariantCulture);
            }
        }
    }

    /// <summary>
    /// Writes one device value using the specified type.
    /// </summary>
    /// <param name="client">The client to use.</param>
    /// <param name="device">Device address string.</param>
    /// <param name="dtype">Type code — same as <see cref="ReadTypedAsync"/>.</param>
    /// <param name="value">Value to write.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task WriteTypedAsync(
        this ToyopucDeviceClient client,
        string device,
        string dtype,
        object value,
        CancellationToken ct = default)
    {
        switch (dtype.ToUpperInvariant())
        {
            case "F":
                await client.WriteFloat32sAsync(device, [Convert.ToSingle(value, CultureInfo.InvariantCulture)], cancellationToken: ct)
                    .ConfigureAwait(false);
                break;
            case "D":
                await client.WriteDWordsAsync(device, [Convert.ToUInt32(value, CultureInfo.InvariantCulture)], cancellationToken: ct)
                    .ConfigureAwait(false);
                break;
            case "L":
                await client.WriteDWordsAsync(device, [unchecked((uint)Convert.ToInt32(value, CultureInfo.InvariantCulture))], cancellationToken: ct)
                    .ConfigureAwait(false);
                break;
            default: // "U" / "S"
                await client.WriteAsync(device, value, ct).ConfigureAwait(false);
                break;
        }
    }

    // -----------------------------------------------------------------------
    // Bit-in-word
    // -----------------------------------------------------------------------

    /// <summary>
    /// Performs a read-modify-write to set a single bit within a word device.
    /// </summary>
    /// <param name="client">The client to use.</param>
    /// <param name="device">Word device address string.</param>
    /// <param name="bitIndex">Bit position within the word (0–15).</param>
    /// <param name="value">New bit value.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task WriteBitInWordAsync(
        this ToyopucDeviceClient client,
        string device,
        int bitIndex,
        bool value,
        CancellationToken ct = default)
    {
        if (bitIndex is < 0 or > 15)
            throw new ArgumentOutOfRangeException(nameof(bitIndex), "bitIndex must be 0-15.");
        var raw = await client.ReadAsync(device, 1, ct).ConfigureAwait(false);
        int cur = Convert.ToUInt16(raw, CultureInfo.InvariantCulture);
        if (value) cur |=   1 << bitIndex;
        else       cur &= ~(1 << bitIndex);
        await client.WriteAsync(device, (ushort)(cur & 0xFFFF), ct).ConfigureAwait(false);
    }

    // -----------------------------------------------------------------------
    // Named-device read
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads multiple devices by address string and returns results in a dictionary.
    /// </summary>
    /// <remarks>
    /// Address format examples:
    /// <list type="bullet">
    ///   <item><description>"D100" — ushort</description></item>
    ///   <item><description>"D100:F" — float32</description></item>
    ///   <item><description>"D100:S" — signed short</description></item>
    ///   <item><description>"D100:D" — unsigned 32-bit</description></item>
    ///   <item><description>"D100:L" — signed 32-bit</description></item>
    ///   <item><description>"D100.3" — bit 3 within word (bool)</description></item>
    /// </list>
    /// </remarks>
    public static async Task<IReadOnlyDictionary<string, object>> ReadNamedAsync(
        this ToyopucDeviceClient client,
        IEnumerable<string> addresses,
        CancellationToken ct = default)
    {
        var result = new Dictionary<string, object>();
        foreach (var address in addresses)
        {
            var (baseAddr, dtype, bitIdx) = ParseAddress(address);
            if (dtype == "BIT_IN_WORD")
            {
                var raw = await client.ReadAsync(baseAddr, 1, ct).ConfigureAwait(false);
                int w = Convert.ToUInt16(raw, CultureInfo.InvariantCulture);
                result[address] = ((w >> (bitIdx ?? 0)) & 1) != 0;
            }
            else
            {
                result[address] = await client.ReadTypedAsync(baseAddr, dtype, ct).ConfigureAwait(false);
            }
        }
        return result;
    }

    // -----------------------------------------------------------------------
    // Polling
    // -----------------------------------------------------------------------

    /// <summary>
    /// Continuously polls the specified devices at the given interval, yielding a snapshot each cycle.
    /// </summary>
    /// <param name="client">The client to use.</param>
    /// <param name="addresses">Device addresses to poll (same format as <see cref="ReadNamedAsync"/>).</param>
    /// <param name="interval">Time between polls.</param>
    /// <param name="ct">Cancellation token to stop polling.</param>
    public static async IAsyncEnumerable<IReadOnlyDictionary<string, object>> PollAsync(
        this ToyopucDeviceClient client,
        IEnumerable<string> addresses,
        TimeSpan interval,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var addrList = addresses.ToList();
        while (!ct.IsCancellationRequested)
        {
            yield return await client.ReadNamedAsync(addrList, ct).ConfigureAwait(false);
            await Task.Delay(interval, ct).ConfigureAwait(false);
        }
    }

    // -----------------------------------------------------------------------
    // Connection helper
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads <paramref name="count"/> contiguous word values starting at <paramref name="device"/>.
    /// </summary>
    public static async Task<ushort[]> ReadWordsAsync(
        this ToyopucDeviceClient client,
        object device,
        int count,
        CancellationToken ct = default)
    {
        var raw = await client.ReadAsync(device, count, ct).ConfigureAwait(false);
        if (count == 1)
            return [Convert.ToUInt16(raw, CultureInfo.InvariantCulture)];
        var arr = (object[])raw;
        var result = new ushort[arr.Length];
        for (int i = 0; i < arr.Length; i++)
            result[i] = Convert.ToUInt16(arr[i], CultureInfo.InvariantCulture);
        return result;
    }

    /// <summary>
    /// Reads <paramref name="count"/> contiguous DWord (32-bit unsigned) values starting at <paramref name="device"/>.
    /// Combines adjacent word pairs (lo, hi).
    /// </summary>
    public static async Task<uint[]> ReadDWordsAsync(
        this ToyopucDeviceClient client,
        object device,
        int count,
        CancellationToken ct = default)
    {
        var words = await client.ReadWordsAsync(device, count * 2, ct).ConfigureAwait(false);
        var result = new uint[count];
        for (int i = 0; i < count; i++)
            result[i] = (uint)(words[i * 2] | (words[i * 2 + 1] << 16));
        return result;
    }

    public static async Task<ToyopucDeviceClient> OpenAndConnectAsync(
        string host,
        int port = 1025,
        CancellationToken ct = default)
    {
        var client = new ToyopucDeviceClient(host, port);
        await client.OpenAsync(ct).ConfigureAwait(false);
        return client;
    }

    // -----------------------------------------------------------------------
    // Internal helpers
    // -----------------------------------------------------------------------

    // "D100:F" → ("D100", "F", null),  "D100.3" → ("D100", "BIT_IN_WORD", 3)
    private static (string Base, string DType, int? BitIdx) ParseAddress(string address)
    {
        if (address.Contains(':'))
        {
            int i = address.IndexOf(':');
            return (address[..i], address[(i + 1)..].ToUpperInvariant(), null);
        }
        if (address.Contains('.'))
        {
            int i = address.IndexOf('.');
            if (int.TryParse(address[(i + 1)..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var bit))
                return (address[..i], "BIT_IN_WORD", bit);
        }
        return (address, "U", null);
    }
}
