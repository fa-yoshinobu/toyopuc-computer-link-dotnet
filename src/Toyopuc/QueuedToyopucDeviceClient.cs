using System.Threading;

namespace PlcComm.Toyopuc;

/// <summary>
/// A wrapper for <see cref="ToyopucDeviceClient"/> that serializes compound async operations.
/// </summary>
public sealed class QueuedToyopucDeviceClient : IAsyncDisposable, IDisposable
{
    private readonly ToyopucDeviceClient _client;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IReadOnlyList<(int LinkNo, int StationNo)>? _relayHops;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueuedToyopucDeviceClient"/> class.
    /// </summary>
    /// <param name="client">The underlying TOYOPUC client.</param>
    /// <param name="relayHops">Optional relay hop configuration.</param>
    public QueuedToyopucDeviceClient(ToyopucDeviceClient client, object? relayHops = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _relayHops = relayHops is null ? null : ToyopucRelay.NormalizeRelayHops(relayHops);
    }

    /// <summary>Gets the wrapped low-level client.</summary>
    public ToyopucDeviceClient InnerClient => _client;

    /// <summary>Gets the configured relay hops, if any.</summary>
    public IReadOnlyList<(int LinkNo, int StationNo)>? RelayHops => _relayHops;

    /// <summary>Gets a value indicating whether relay mode is enabled.</summary>
    public bool UsesRelay => _relayHops is not null;

    /// <summary>Gets the PLC host.</summary>
    public string Host => _client.Host;

    /// <summary>Gets the PLC port.</summary>
    public int Port => _client.Port;

    /// <summary>Gets the selected transport protocol.</summary>
    public ToyopucTransportMode Transport => _client.Transport;

    /// <summary>Gets or sets the operation timeout.</summary>
    public TimeSpan Timeout
    {
        get => _client.Timeout;
        set => _client.Timeout = value;
    }

    /// <summary>Gets the normalized device profile name, if any.</summary>
    public string? DeviceProfile => _client.DeviceProfile;

    /// <summary>Gets the addressing options used by the wrapped client.</summary>
    public ToyopucAddressingOptions AddressingOptions => _client.AddressingOptions;

    /// <summary>Gets or sets a value indicating whether transport trace frames are captured.</summary>
    public bool CaptureTraceFrames
    {
        get => _client.CaptureTraceFrames;
        set => _client.CaptureTraceFrames = value;
    }

    /// <summary>Gets or sets the raw trace callback.</summary>
    public Action<ToyopucTraceFrame>? TraceHook
    {
        get => _client.TraceHook;
        set => _client.TraceHook = value;
    }

    /// <summary>Gets a value indicating whether the underlying transport is open.</summary>
    public bool IsOpen => _client.IsOpen;

    /// <summary>Opens the connection asynchronously with exclusive access.</summary>
    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _client.OpenAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Executes an async operation with exclusive access to the wrapped client.</summary>
    public async Task<T> ExecuteAsync<T>(
        Func<ToyopucDeviceClient, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await operation(_client).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Executes an async operation with exclusive access to the wrapped client.</summary>
    public async Task ExecuteAsync(
        Func<ToyopucDeviceClient, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await operation(_client).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Disposes the wrapper and the underlying client.</summary>
    public void Dispose()
    {
        _gate.Dispose();
        _client.Dispose();
    }

    /// <summary>Disposes the wrapper and the underlying client asynchronously.</summary>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
