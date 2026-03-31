namespace PlcComm.Toyopuc;

/// <summary>
/// Factory helpers for creating connected queued TOYOPUC clients.
/// </summary>
public static class ToyopucDeviceClientFactory
{
    /// <summary>
    /// Creates, configures, and opens a queued TOYOPUC client.
    /// </summary>
    /// <param name="options">Explicit connection options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A connected queued client.</returns>
    public static async Task<QueuedToyopucDeviceClient> OpenAndConnectAsync(
        ToyopucConnectionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Host))
            throw new ArgumentException("Host must not be empty.", nameof(options));
        if (options.Port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(options), "Port must be in the range 1-65535.");
        if (options.LocalPort is < 0 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(options), "LocalPort must be in the range 0-65535.");
        if (options.RecvBufsize < 1)
            throw new ArgumentOutOfRangeException(nameof(options), "RecvBufsize must be 1 or greater.");

        string? normalizedProfile = string.IsNullOrWhiteSpace(options.DeviceProfile)
            ? null
            : ToyopucDeviceProfiles.NormalizeName(options.DeviceProfile);

        var inner = new ToyopucDeviceClient(
            options.Host,
            options.Port,
            options.LocalPort,
            options.Transport,
            options.EffectiveTimeout,
            options.Retries,
            options.EffectiveRetryDelay,
            options.RecvBufsize,
            deviceProfile: normalizedProfile);

        var queued = new QueuedToyopucDeviceClient(inner, options.RelayHops);
        await queued.OpenAsync(cancellationToken).ConfigureAwait(false);
        return queued;
    }
}
