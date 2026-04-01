namespace PlcComm.Toyopuc;

/// <summary>
/// Factory helpers for creating connected queued TOYOPUC clients.
/// </summary>
/// <remarks>
/// This factory is the preferred application entry point when you want explicit profile,
/// relay, retry, and timeout settings captured in one documented type.
/// </remarks>
public static class ToyopucDeviceClientFactory
{
    /// <summary>
    /// Creates, configures, and opens a queued TOYOPUC client.
    /// </summary>
    /// <param name="options">Explicit connection options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A connected queued client.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The host name is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A configured port, local port, or receive buffer size falls outside the supported range.
    /// </exception>
    /// <remarks>
    /// When <see cref="ToyopucConnectionOptions.RelayHops"/> is supplied, the returned queued client keeps
    /// the normalized relay chain available through <see cref="QueuedToyopucDeviceClient.RelayHops"/>.
    /// </remarks>
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
