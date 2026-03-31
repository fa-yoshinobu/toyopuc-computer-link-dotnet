namespace PlcComm.Toyopuc;

/// <summary>
/// Explicit connection options for a stable TOYOPUC device session.
/// </summary>
/// <param name="Host">PLC IP address or hostname.</param>
public sealed record ToyopucConnectionOptions(string Host)
{
    /// <summary>Gets or sets the PLC port number. Defaults to 1025.</summary>
    public int Port { get; init; } = 1025;

    /// <summary>Gets or sets the communication timeout.</summary>
    public TimeSpan Timeout { get; init; }

    /// <summary>Gets or sets the transport protocol.</summary>
    public ToyopucTransportMode Transport { get; init; } = ToyopucTransportMode.Tcp;

    /// <summary>Gets or sets the optional device profile name.</summary>
    public string? DeviceProfile { get; init; }

    /// <summary>Gets or sets the optional relay hop chain text.</summary>
    public string? RelayHops { get; init; }

    /// <summary>Gets or sets the local UDP port. Ignored for TCP.</summary>
    public int LocalPort { get; init; }

    /// <summary>Gets or sets the retry count for transport operations.</summary>
    public int Retries { get; init; }

    /// <summary>Gets or sets the retry delay.</summary>
    public TimeSpan RetryDelay { get; init; }

    /// <summary>Gets or sets the receive buffer size.</summary>
    public int RecvBufsize { get; init; } = 8192;

    /// <summary>Gets the effective timeout used for a new client instance.</summary>
    public TimeSpan EffectiveTimeout => Timeout == default ? TimeSpan.FromSeconds(3) : Timeout;

    /// <summary>Gets the effective retry delay used for a new client instance.</summary>
    public TimeSpan EffectiveRetryDelay => RetryDelay == default ? TimeSpan.FromMilliseconds(200) : RetryDelay;
}
