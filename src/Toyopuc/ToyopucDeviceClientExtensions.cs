using System.Globalization;

namespace Toyopuc;

/// <summary>
/// Extension methods for <see cref="ToyopucDeviceClient"/> providing typed read/write helpers.
/// </summary>
public static class ToyopucDeviceClientExtensions
{
    /// <summary>
    /// Reads one device value and converts it to the specified type.
    /// </summary>
    /// <param name="client">The client to use.</param>
    /// <param name="device">Device address string.</param>
    /// <param name="dtype">
    /// Type code: "W" = ushort, "I" = short (signed 16-bit),
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
            case "I":
            {
                var raw = await client.ReadAsync(device, 1, ct).ConfigureAwait(false);
                return unchecked((short)Convert.ToUInt16(raw, CultureInfo.InvariantCulture));
            }
            default: // "W"
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
            default: // "W" / "I"
                await client.WriteAsync(device, value, ct).ConfigureAwait(false);
                break;
        }
    }
}
