using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using PlcComm.Toyopuc;

namespace PlcComm.Toyopuc.Tests;

public sealed class ToyopucClientExtensionsTests
{
    private const double LocalTestTimeoutSeconds = 2.0;

    [Fact]
    public void ToyopucAddress_Normalize_PreservesPrefixAndSuffix()
    {
        var normalized = ToyopucAddress.Normalize(
            "p1-d0000l",
            ToyopucAddressingOptions.FromProfile("PC10G:PC10 mode"),
            "PC10G:PC10 mode");

        Assert.Equal("P1-D0000L", normalized);
    }

    [Fact]
    public async Task OpenAndConnectAsync_ReturnsQueuedClientWithRelayConfiguration()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var acceptTask = listener.AcceptTcpClientAsync();
        await using var client = await ToyopucDeviceClientExtensions.OpenAndConnectAsync(
            new ToyopucConnectionOptions("127.0.0.1")
            {
                Port = port,
                RelayHops = "P1-L2:N2",
                DeviceProfile = "PC10G:PC10 mode",
            });

        using var server = await acceptTask;

        Assert.True(client.IsOpen);
        Assert.True(client.UsesRelay);
        Assert.Equal("PC10G:PC10 mode", client.DeviceProfile);
        Assert.Single(client.RelayHops!);
        Assert.Equal((0x12, 2), client.RelayHops![0]);
    }

    [Fact]
    public async Task ReadDWordsChunkedAsync_AdvancesByWholeDwordBoundaries()
    {
        await using var server = new ScriptedToyopucServer(frame =>
        {
            if (frame.SequenceEqual(ToyopucProtocol.BuildWordRead(0x1100, 2)))
                return BuildResponse(0x1C, new byte[] { 0x01, 0x00, 0x01, 0x00 });
            if (frame.SequenceEqual(ToyopucProtocol.BuildWordRead(0x1102, 2)))
                return BuildResponse(0x1C, new byte[] { 0x02, 0x00, 0x02, 0x00 });
            if (frame.SequenceEqual(ToyopucProtocol.BuildWordRead(0x1104, 2)))
                return BuildResponse(0x1C, new byte[] { 0x03, 0x00, 0x03, 0x00 });
            return BuildResponse(0x10, new byte[] { 0x40 });
        });

        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            server.Port,
            transport: ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(LocalTestTimeoutSeconds));

        var values = await client.ReadDWordsChunkedAsync("D0100", 3, 1);

        Assert.Equal(new uint[] { 0x00010001, 0x00020002, 0x00030003 }, values);
        Assert.Equal(
            new[]
            {
                Convert.ToHexString(ToyopucProtocol.BuildWordRead(0x1100, 2)),
                Convert.ToHexString(ToyopucProtocol.BuildWordRead(0x1102, 2)),
                Convert.ToHexString(ToyopucProtocol.BuildWordRead(0x1104, 2)),
            },
            server.ReceivedFrames.ToArray());
    }

    [Fact]
    public async Task ReadWordsSingleRequestAsync_UsesOneExtWordReadForProgramDevices()
    {
        var expected = ToyopucProtocol.BuildExtWordRead(0x01, 0x1000, 2);
        await using var server = new ScriptedToyopucServer(_ => BuildResponse(0x94, new byte[] { 0x34, 0x12, 0x78, 0x56 }));

        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            server.Port,
            transport: ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(LocalTestTimeoutSeconds),
            addressingOptions: ToyopucAddressingOptions.Pc10GMode);

        var values = await client.ReadWordsSingleRequestAsync("P1-D0000", 2);

        Assert.Equal(new ushort[] { 0x1234, 0x5678 }, values);
        Assert.Equal([Convert.ToHexString(expected)], server.ReceivedFrames.ToArray());
    }

    [Fact]
    public async Task WriteWordsSingleRequestAsync_UsesOneExtWordWriteForProgramDevices()
    {
        var expected = ToyopucProtocol.BuildExtWordWrite(0x01, 0x1000, new[] { 0x1234, 0x5678 });
        await using var server = new ScriptedToyopucServer(_ => BuildResponse(0x95, Array.Empty<byte>()));

        await using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            server.Port,
            transport: ToyopucTransportMode.Tcp,
            timeout: TimeSpan.FromSeconds(LocalTestTimeoutSeconds),
            addressingOptions: ToyopucAddressingOptions.Pc10GMode);

        await client.WriteWordsSingleRequestAsync("P1-D0000", new ushort[] { 0x1234, 0x5678 });

        Assert.Equal([Convert.ToHexString(expected)], server.ReceivedFrames.ToArray());
    }

    private static byte[] BuildResponse(int cmd, byte[] data)
    {
        var length = 1 + data.Length;
        return new[] { (byte)0x80, (byte)0x00, (byte)(length & 0xFF), (byte)((length >> 8) & 0xFF), (byte)(cmd & 0xFF) }
            .Concat(data)
            .ToArray();
    }

    private sealed class ScriptedToyopucServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly Func<byte[], byte[]> _responseFactory;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _serverTask;

        public ConcurrentQueue<string> ReceivedFrames { get; } = new();

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public ScriptedToyopucServer(Func<byte[], byte[]> responseFactory)
        {
            _responseFactory = responseFactory;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            _serverTask = Task.Run(RunAsync);
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _listener.Stop();
            try
            {
                await _serverTask;
            }
            catch
            {
            }

            _cts.Dispose();
        }

        private async Task RunAsync()
        {
            try
            {
                using var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                await using var stream = client.GetStream();
                while (!_cts.IsCancellationRequested)
                {
                    var frame = await ReadFrameAsync(stream, _cts.Token);
                    if (frame.Length == 0)
                        break;

                    ReceivedFrames.Enqueue(Convert.ToHexString(frame));
                    var response = _responseFactory(frame);
                    await stream.WriteAsync(response, _cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (SocketException)
            {
            }
            catch (IOException)
            {
            }
        }

        private static async Task<byte[]> ReadFrameAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            var header = new byte[4];
            var read = await stream.ReadAsync(header, cancellationToken);
            if (read == 0)
                return Array.Empty<byte>();

            while (read < header.Length)
            {
                var chunk = await stream.ReadAsync(header.AsMemory(read), cancellationToken);
                if (chunk == 0)
                    throw new IOException("Unexpected end of stream");
                read += chunk;
            }

            var length = header[2] | (header[3] << 8);
            var body = new byte[length];
            var offset = 0;
            while (offset < body.Length)
            {
                var chunk = await stream.ReadAsync(body.AsMemory(offset), cancellationToken);
                if (chunk == 0)
                    throw new IOException("Unexpected end of stream");
                offset += chunk;
            }

            return header.Concat(body).ToArray();
        }
    }
}
