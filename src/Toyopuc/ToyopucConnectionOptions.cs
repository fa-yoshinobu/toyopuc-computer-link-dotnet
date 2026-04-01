namespace PlcComm.Toyopuc;

/// <summary>
/// Explicit connection options for a stable TOYOPUC device session.
/// </summary>
/// <remarks>
/// This type keeps transport, profile, retry, and relay settings explicit for the
/// unified high-level connection flow and generated API documentation.
/// </remarks>
/// <param name="Host">PLC IP address or hostname.</param>
public sealed record ToyopucConnectionOptions(string Host)
{
    /// <summary>Gets or sets the PLC port number.</summary>
    /// <remarks>The default TOYOPUC communication port is <c>1025</c>.</remarks>
    public int Port { get; init; } = 1025;

    /// <summary>Gets or sets the communication timeout.</summary>
    /// <remarks>A zero value falls back to <see cref="EffectiveTimeout"/>.</remarks>
    public TimeSpan Timeout { get; init; }

    /// <summary>Gets or sets the transport protocol.</summary>
    public ToyopucTransportMode Transport { get; init; } = ToyopucTransportMode.Tcp;

    /// <summary>Gets or sets the optional device profile name.</summary>
    /// <remarks>
    /// Supply a known profile name when you want documented profile-based address resolution instead of
    /// a fully generic session.
    /// </remarks>
    public string? DeviceProfile { get; init; }

    /// <summary>Gets or sets the optional relay hop chain text.</summary>
    /// <remarks>Leave this empty for direct connections; provide relay hops for routed sessions.</remarks>
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
