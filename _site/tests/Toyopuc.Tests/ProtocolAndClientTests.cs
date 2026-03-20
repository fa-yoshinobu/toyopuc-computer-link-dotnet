using System.Net;
using System.Net.Sockets;

namespace Toyopuc.Tests;

public class ProtocolAndClientTests
{
    private const double LocalTestTimeoutSeconds = 3.0;

    [Fact]
    public void BuildWordReadFrame_MatchesExpectedBytes()
    {
        var frame = ToyopucProtocol.BuildWordRead(0x1100, 1);

        Assert.Equal(new byte[] { 0x00, 0x00, 0x05, 0x00, 0x1C, 0x00, 0x11, 0x01, 0x00 }, frame);
    }

    [Fact]
    public void ParseCpuStatusData_ReturnsFlags()
    {
        var status = ToyopucProtocol.ParseCpuStatusData(new byte[] { 0x11, 0x00, 0x81, 0x20, 0x00, 0x00, 0x00, 0x00, 0x10, 0x02 });

        Assert.True(status.Run);
        Assert.True(status.Alarm);
        Assert.True(status.UnderWritingFlashRegister);
        Assert.True(status.Program1Running);
    }

    [Fact]
    public async Task HighLevelClient_ReadsWordViaTcpServer()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        byte[]? requestFrame = null;
        var serverTask = Task.Run(async () =>
        {
            using var serverClient = await listener.AcceptTcpClientAsync();
            await using var stream = serverClient.GetStream();
            requestFrame = await ReadFrameAsync(stream);
            var response = BuildResponse(0x1C, new byte[] { 0x34, 0x12 });
            await stream.WriteAsync(response);
        });

        using var client = new ToyopucDeviceClient("127.0.0.1", port, protocol: "tcp", timeout: LocalTestTimeoutSeconds)
        {
            CaptureTraceFrames = true,
        };
        var result = client.Read("D0100");

        await serverTask;

        Assert.Equal(0x1234, Assert.IsType<int>(result));
        Assert.Equal(ToyopucProtocol.BuildWordRead(0x1100, 1), requestFrame);
        var trace = Assert.Single(client.TraceFrames);
        Assert.Equal(requestFrame, trace.Tx);
        Assert.Equal(BuildResponse(0x1C, new byte[] { 0x34, 0x12 }), trace.Rx);
    }

    [Fact]
    public async Task HighLevelClient_ReadsDWordViaSingleWordReadFrame()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        byte[]? requestFrame = null;
        var serverTask = Task.Run(async () =>
        {
            using var serverClient = await listener.AcceptTcpClientAsync();
            await using var stream = serverClient.GetStream();
            requestFrame = await ReadFrameAsync(stream);
            await stream.WriteAsync(BuildResponse(0x1C, new byte[] { 0x78, 0x56, 0x34, 0x12 }));
        });

        using var client = new ToyopucDeviceClient("127.0.0.1", port, protocol: "tcp", timeout: LocalTestTimeoutSeconds);
        var result = client.ReadDWord("D0100");

        await serverTask;

        Assert.Equal(0x12345678u, result);
        Assert.Equal(ToyopucProtocol.BuildWordRead(0x1100, 2), requestFrame);
    }

    [Fact]
    public async Task HighLevelClient_ReadsFloat32ViaSingleWordReadFrame()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        byte[]? requestFrame = null;
        var serverTask = Task.Run(async () =>
        {
            using var serverClient = await listener.AcceptTcpClientAsync();
            await using var stream = serverClient.GetStream();
            requestFrame = await ReadFrameAsync(stream);
            await stream.WriteAsync(BuildResponse(0x1C, new byte[] { 0x00, 0x00, 0xC0, 0x3F }));
        });

        using var client = new ToyopucDeviceClient("127.0.0.1", port, protocol: "tcp", timeout: LocalTestTimeoutSeconds);
        var result = client.ReadFloat32("D0100");

        await serverTask;

        Assert.Equal(1.5f, result);
        Assert.Equal(ToyopucProtocol.BuildWordRead(0x1100, 2), requestFrame);
    }

    [Fact]
    public async Task HighLevelClient_ReadsDWordAsyncViaSingleWordReadFrame()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        byte[]? requestFrame = null;
        var serverTask = Task.Run(async () =>
        {
            using var serverClient = await listener.AcceptTcpClientAsync();
            await using var stream = serverClient.GetStream();
            requestFrame = await ReadFrameAsync(stream);
            await stream.WriteAsync(BuildResponse(0x1C, new byte[] { 0x78, 0x56, 0x34, 0x12 }));
        });

        using var client = new ToyopucDeviceClient("127.0.0.1", port, protocol: "tcp", timeout: LocalTestTimeoutSeconds);
        var result = await client.ReadDWordAsync("D0100");

        await serverTask;

        Assert.Equal(0x12345678u, result);
        Assert.Equal(ToyopucProtocol.BuildWordRead(0x1100, 2), requestFrame);
    }

    [Fact]
    public async Task HighLevelClient_WritesFloat32ViaSingleWordWriteFrame()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        byte[]? requestFrame = null;
        var serverTask = Task.Run(async () =>
        {
            using var serverClient = await listener.AcceptTcpClientAsync();
            await using var stream = serverClient.GetStream();
            requestFrame = await ReadFrameAsync(stream);
            await stream.WriteAsync(BuildResponse(0x1D, Array.Empty<byte>()));
        });

        using var client = new ToyopucDeviceClient("127.0.0.1", port, protocol: "tcp", timeout: LocalTestTimeoutSeconds);
        client.WriteFloat32("D0100", 1.5f);

        await serverTask;

        Assert.Equal(ToyopucProtocol.BuildWordWrite(0x1100, new[] { 0x0000, 0x3FC0 }), requestFrame);
    }

    [Fact]
    public async Task HighLevelClient_ReadsSequentialWordsInSingleFrame()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        byte[]? requestFrame = null;
        var serverTask = Task.Run(async () =>
        {
            using var serverClient = await listener.AcceptTcpClientAsync();
            await using var stream = serverClient.GetStream();
            requestFrame = await ReadFrameAsync(stream);
            await stream.WriteAsync(BuildResponse(0x1C, new byte[] { 0x11, 0x11, 0x22, 0x22, 0x33, 0x33 }));
        });

        using var client = new ToyopucDeviceClient("127.0.0.1", port, protocol: "tcp", timeout: LocalTestTimeoutSeconds)
        {
            CaptureTraceFrames = true,
        };
        var result = Assert.IsType<object[]>(client.Read("D0100", 3));

        await serverTask;

        Assert.Equal(new object[] { 0x1111, 0x2222, 0x3333 }, result);
        Assert.Equal(ToyopucProtocol.BuildWordRead(0x1100, 3), requestFrame);
        Assert.Single(client.TraceFrames);
    }

    [Fact]
    public async Task HighLevelClient_ReadsSequentialProgramWordsInSingleFrame()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        byte[]? requestFrame = null;
        var serverTask = Task.Run(async () =>
        {
            using var serverClient = await listener.AcceptTcpClientAsync();
            await using var stream = serverClient.GetStream();
            requestFrame = await ReadFrameAsync(stream);
            await stream.WriteAsync(BuildResponse(0x94, new byte[] { 0x11, 0x11, 0x22, 0x22 }));
        });

        using var client = new ToyopucDeviceClient("127.0.0.1", port, protocol: "tcp", timeout: LocalTestTimeoutSeconds)
        {
            CaptureTraceFrames = true,
        };
        var result = Assert.IsType<object[]>(client.Read("P1-D0000", 2));

        await serverTask;

        Assert.Equal(new object[] { 0x1111, 0x2222 }, result);
        Assert.Equal(ToyopucProtocol.BuildExtWordRead(0x01, 0x1000, 2), requestFrame);
        Assert.Single(client.TraceFrames);
    }

    [Fact]
    public async Task HighLevelClient_ReadsUpperUBoundaryWithMixedFrames()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        byte[]? firstRequest = null;
        byte[]? secondRequest = null;
        var serverTask = Task.Run(async () =>
        {
            using var serverClient = await listener.AcceptTcpClientAsync();
            await using var stream = serverClient.GetStream();
            firstRequest = await ReadFrameAsync(stream);
            await stream.WriteAsync(BuildResponse(0x94, new byte[] { 0x11, 0x11 }));
            secondRequest = await ReadFrameAsync(stream);
            await stream.WriteAsync(BuildResponse(0xC2, new byte[] { 0x22, 0x22 }));
        });

        using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            port,
            protocol: "tcp",
            timeout: LocalTestTimeoutSeconds,
            addressingOptions: ToyopucAddressingOptions.Nano10GxCompatible)
        {
            CaptureTraceFrames = true,
        };
        var result = Assert.IsType<object[]>(client.Read("U07FFF", 2));

        await serverTask;

        Assert.Equal(new object[] { 0x1111, 0x2222 }, result);
        Assert.Equal(ToyopucProtocol.BuildExtWordRead(0x08, 0x7FFF, 1), firstRequest);
        Assert.Equal(ToyopucProtocol.BuildPc10BlockRead(ToyopucAddress.EncodeExNoByteU32(0x04, 0x0000), 2), secondRequest);
        Assert.Equal(2, client.TraceFrames.Count);
    }

    [Fact]
    public async Task HighLevelClient_CapturesLastFramesWithoutTraceListByDefault()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var serverTask = Task.Run(async () =>
        {
            using var serverClient = await listener.AcceptTcpClientAsync();
            await using var stream = serverClient.GetStream();
            await ReadFrameAsync(stream);
            await stream.WriteAsync(BuildResponse(0x1C, new byte[] { 0x34, 0x12 }));
        });

        using var client = new ToyopucDeviceClient("127.0.0.1", port, protocol: "tcp", timeout: LocalTestTimeoutSeconds);
        var result = client.Read("D0100");

        await serverTask;

        Assert.Equal(0x1234, Assert.IsType<int>(result));
        Assert.Empty(client.TraceFrames);
        Assert.Equal(ToyopucProtocol.BuildWordRead(0x1100, 1), client.LastTx);
        Assert.Equal(BuildResponse(0x1C, new byte[] { 0x34, 0x12 }), client.LastRx);
    }

    [Fact]
    public async Task HighLevelClient_ReadManyBasicWords_UsesSingleMultiReadFrame()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        byte[]? requestFrame = null;
        var serverTask = Task.Run(async () =>
        {
            using var serverClient = await listener.AcceptTcpClientAsync();
            await using var stream = serverClient.GetStream();
            requestFrame = await ReadFrameAsync(stream);
            await stream.WriteAsync(BuildResponse(0x22, new byte[] { 0x34, 0x12, 0x78, 0x56 }));
        });

        using var client = new ToyopucDeviceClient("127.0.0.1", port, protocol: "tcp", timeout: LocalTestTimeoutSeconds)
        {
            CaptureTraceFrames = true,
        };
        var result = client.ReadMany(new object[] { "D0100", "D0102" });

        await serverTask;

        Assert.Equal(new object[] { 0x1234, 0x5678 }, result);
        Assert.Equal(ToyopucProtocol.BuildMultiWordRead(new[] { 0x1100, 0x1102 }), requestFrame);
        Assert.Single(client.TraceFrames);
    }

    [Fact]
    public async Task HighLevelClient_ReadsSequentialExtBits_FromPackedMultiReadResponse()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        byte[]? requestFrame = null;
        var serverTask = Task.Run(async () =>
        {
            using var serverClient = await listener.AcceptTcpClientAsync();
            await using var stream = serverClient.GetStream();
            requestFrame = await ReadFrameAsync(stream);
            await stream.WriteAsync(BuildResponse(0x98, new byte[] { 0x5A, 0xA5 }));
        });

        using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            port,
            protocol: "tcp",
            timeout: LocalTestTimeoutSeconds,
            deviceProfile: ToyopucDeviceProfiles.Pc10GMode.Name)
        {
            CaptureTraceFrames = true,
        };
        var result = Assert.IsType<object[]>(client.Read("GM0400", 16));

        await serverTask;

        var expectedRequest = ToyopucProtocol.BuildExtMultiRead(
            Enumerable.Range(0, 16).Select(static offset => (0x07, (0x0400 + offset) & 0x07, 0x2000 + ((0x0400 + offset) >> 3))),
            Array.Empty<(int No, int Address)>(),
            Array.Empty<(int No, int Address)>());

        Assert.Equal(
            new object[]
            {
                false, true, false, true, true, false, true, false,
                true, false, true, false, false, true, false, true,
            },
            result);
        Assert.Equal(expectedRequest, requestFrame);
        Assert.Single(client.TraceFrames);
    }

    [Fact]
    public async Task HighLevelClient_ReadManyPc10Words_UsesSingleMultiReadFrame()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        byte[]? requestFrame = null;
        var serverTask = Task.Run(async () =>
        {
            using var serverClient = await listener.AcceptTcpClientAsync();
            await using var stream = serverClient.GetStream();
            requestFrame = await ReadFrameAsync(stream);
            await stream.WriteAsync(BuildResponse(0xC4, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x34, 0x12, 0x78, 0x56 }));
        });

        using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            port,
            protocol: "tcp",
            timeout: LocalTestTimeoutSeconds,
            addressingOptions: ToyopucAddressingOptions.Nano10GxCompatible)
        {
            CaptureTraceFrames = true,
        };
        var result = client.ReadMany(new object[] { "U08000", "EB00000" });

        await serverTask;

        Assert.Equal(new object[] { 0x1234, 0x5678 }, result);
        Assert.Equal(
            ToyopucProtocol.BuildPc10MultiRead(
                BuildPc10MultiWordReadPayloadForTest(
                    ToyopucAddress.EncodeExNoByteU32(0x04, 0x0000),
                    ToyopucAddress.EncodeExNoByteU32(0x10, 0x0000))),
            requestFrame);
        Assert.Single(client.TraceFrames);
    }

    [Fact]
    public async Task HighLevelClient_ReadManyPackedPc10Words_UsesSegmentedBlockReads()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        byte[]? firstRequest = null;
        byte[]? secondRequest = null;
        var serverTask = Task.Run(async () =>
        {
            using var serverClient = await listener.AcceptTcpClientAsync();
            await using var stream = serverClient.GetStream();
            firstRequest = await ReadFrameAsync(stream);
            await stream.WriteAsync(BuildResponse(0xC2, new byte[] { 0x34, 0x12, 0x78, 0x56 }));
            secondRequest = await ReadFrameAsync(stream);
            await stream.WriteAsync(BuildResponse(0xC2, new byte[] { 0xBC, 0x9A }));
        });

        using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            port,
            protocol: "tcp",
            timeout: LocalTestTimeoutSeconds,
            addressingOptions: ToyopucAddressingOptions.Pc10GMode)
        {
            CaptureTraceFrames = true,
        };
        var result = client.ReadMany(new object[] { "M100W", "M101W", "M110W" });

        await serverTask;

        Assert.Equal(new object[] { 0x1234, 0x5678, 0x9ABC }, result);
        Assert.Equal(ToyopucProtocol.BuildPc10BlockRead(ToyopucAddress.EncodeExNoByteU32(0x00, 0x0500), 4), firstRequest);
        Assert.Equal(ToyopucProtocol.BuildPc10BlockRead(ToyopucAddress.EncodeExNoByteU32(0x00, 0x0520), 2), secondRequest);
        Assert.Equal(2, client.TraceFrames.Count);
    }

    [Fact]
    public async Task HighLevelClient_ReadsSequentialPc10Bits_UsesSingleMultiReadFrame()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        byte[]? requestFrame = null;
        var serverTask = Task.Run(async () =>
        {
            using var serverClient = await listener.AcceptTcpClientAsync();
            await using var stream = serverClient.GetStream();
            requestFrame = await ReadFrameAsync(stream);
            await stream.WriteAsync(BuildResponse(0xC4, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x5A, 0xA5 }));
        });

        using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            port,
            protocol: "tcp",
            timeout: LocalTestTimeoutSeconds,
            addressingOptions: ToyopucAddressingOptions.Pc10GMode)
        {
            CaptureTraceFrames = true,
        };
        var result = Assert.IsType<object[]>(client.Read("P17F0", 16));

        await serverTask;

        Assert.Equal(
            new object[]
            {
                false, true, false, true, true, false, true, false,
                true, false, true, false, false, true, false, true,
            },
            result);
        Assert.Equal(
            ToyopucProtocol.BuildPc10MultiRead(
                BuildPc10MultiBitReadPayloadForTest(
                    Enumerable.Range(0, 16)
                        .Select(static offset => ToyopucAddress.EncodePc10BitAddress(new ParsedAddress("P", 0x17F0 + offset, "bit")))
                        .ToArray())),
            requestFrame);
        Assert.Single(client.TraceFrames);
    }

    [Fact]
    public async Task HighLevelClient_ReadsFrAcrossBlockBoundary_InSeparatePc10BlockReads()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        byte[]? firstRequest = null;
        byte[]? secondRequest = null;
        var serverTask = Task.Run(async () =>
        {
            using var serverClient = await listener.AcceptTcpClientAsync();
            await using var stream = serverClient.GetStream();
            firstRequest = await ReadFrameAsync(stream);
            await stream.WriteAsync(BuildResponse(0xC2, new byte[] { 0x34, 0x12 }));
            secondRequest = await ReadFrameAsync(stream);
            await stream.WriteAsync(BuildResponse(0xC2, new byte[] { 0x78, 0x56 }));
        });

        using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            port,
            protocol: "tcp",
            timeout: LocalTestTimeoutSeconds,
            addressingOptions: ToyopucAddressingOptions.Nano10GxCompatible)
        {
            CaptureTraceFrames = true,
        };
        var result = Assert.IsType<object[]>(client.Read("FR007FFF", 2));

        await serverTask;

        Assert.Equal(new object[] { 0x1234, 0x5678 }, result);
        Assert.Equal(ToyopucProtocol.BuildPc10BlockRead(ToyopucAddress.EncodeFrWordAddr32(0x007FFF), 2), firstRequest);
        Assert.Equal(ToyopucProtocol.BuildPc10BlockRead(ToyopucAddress.EncodeFrWordAddr32(0x008000), 2), secondRequest);
        Assert.Equal(2, client.TraceFrames.Count);
    }

    [Fact]
    public async Task HighLevelClient_WritesSequentialWordsInSingleFrame()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        byte[]? requestFrame = null;
        var serverTask = Task.Run(async () =>
        {
            using var serverClient = await listener.AcceptTcpClientAsync();
            await using var stream = serverClient.GetStream();
            requestFrame = await ReadFrameAsync(stream);
            await stream.WriteAsync(BuildResponse(0x1D, Array.Empty<byte>()));
        });

        using var client = new ToyopucDeviceClient("127.0.0.1", port, protocol: "tcp", timeout: LocalTestTimeoutSeconds)
        {
            CaptureTraceFrames = true,
        };
        client.Write("D0100", new[] { 0x1234, 0x5678 });

        await serverTask;

        Assert.Equal(ToyopucProtocol.BuildWordWrite(0x1100, new[] { 0x1234, 0x5678 }), requestFrame);
        Assert.Single(client.TraceFrames);
    }

    [Fact]
    public async Task HighLevelClient_WritesSequentialPc10Bits_UsesSingleMultiWriteFrame()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        byte[]? requestFrame = null;
        var serverTask = Task.Run(async () =>
        {
            using var serverClient = await listener.AcceptTcpClientAsync();
            await using var stream = serverClient.GetStream();
            requestFrame = await ReadFrameAsync(stream);
            await stream.WriteAsync(BuildResponse(0xC5, Array.Empty<byte>()));
        });

        using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            port,
            protocol: "tcp",
            timeout: LocalTestTimeoutSeconds,
            addressingOptions: ToyopucAddressingOptions.Pc10GMode)
        {
            CaptureTraceFrames = true,
        };
        client.Write("P17F0", new[]
        {
            false, true, false, true, true, false, true, false,
            true, false, true, false, false, true, false, true,
        });

        await serverTask;

        Assert.Equal(
            ToyopucProtocol.BuildPc10MultiWrite(
                BuildPc10MultiBitWritePayloadForTest(
                    Enumerable.Range(0, 16)
                        .Select(static offset => (
                            ToyopucAddress.EncodePc10BitAddress(new ParsedAddress("P", 0x17F0 + offset, "bit")),
                            ((0xA55A >> offset) & 0x01)))
                        .ToArray())),
            requestFrame);
        Assert.Single(client.TraceFrames);
    }

    [Fact]
    public async Task HighLevelClient_WriteManyBasicWords_UsesSingleMultiWriteFrame()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        byte[]? requestFrame = null;
        var serverTask = Task.Run(async () =>
        {
            using var serverClient = await listener.AcceptTcpClientAsync();
            await using var stream = serverClient.GetStream();
            requestFrame = await ReadFrameAsync(stream);
            await stream.WriteAsync(BuildResponse(0x23, Array.Empty<byte>()));
        });

        using var client = new ToyopucDeviceClient("127.0.0.1", port, protocol: "tcp", timeout: LocalTestTimeoutSeconds)
        {
            CaptureTraceFrames = true,
        };
        client.WriteMany(
            new[]
            {
                new KeyValuePair<object, object>("D0100", 0x1234),
                new KeyValuePair<object, object>("D0102", 0x5678),
            });

        await serverTask;

        Assert.Equal(ToyopucProtocol.BuildMultiWordWrite(new[] { (0x1100, 0x1234), (0x1102, 0x5678) }), requestFrame);
        Assert.Single(client.TraceFrames);
    }

    [Fact]
    public async Task HighLevelClient_WriteManyPc10WordsAcrossBlocks_SplitsIntoPc10BlockWrites()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        byte[]? firstRequest = null;
        byte[]? secondRequest = null;
        var serverTask = Task.Run(async () =>
        {
            using var serverClient = await listener.AcceptTcpClientAsync();
            await using var stream = serverClient.GetStream();
            firstRequest = await ReadFrameAsync(stream);
            await stream.WriteAsync(BuildResponse(0xC3, Array.Empty<byte>()));
            secondRequest = await ReadFrameAsync(stream);
            await stream.WriteAsync(BuildResponse(0xC3, Array.Empty<byte>()));
        });

        using var client = new ToyopucDeviceClient(
            "127.0.0.1",
            port,
            protocol: "tcp",
            timeout: LocalTestTimeoutSeconds,
            addressingOptions: ToyopucAddressingOptions.Nano10GxCompatible)
        {
            CaptureTraceFrames = true,
        };
        client.WriteMany(
            new[]
            {
                new KeyValuePair<object, object>("U08000", 0x1234),
                new KeyValuePair<object, object>("EB00000", 0x5678),
            });

        await serverTask;

        Assert.Equal(
            ToyopucProtocol.BuildPc10BlockWrite(
                ToyopucAddress.EncodeExNoByteU32(0x04, 0x0000),
                new byte[] { 0x34, 0x12 }),
            firstRequest);
        Assert.Equal(
            ToyopucProtocol.BuildPc10BlockWrite(
                ToyopucAddress.EncodeExNoByteU32(0x10, 0x0000),
                new byte[] { 0x78, 0x56 }),
            secondRequest);
        Assert.Equal(2, client.TraceFrames.Count);
    }

    [Fact]
    public async Task HighLevelClient_WritesFrWordsViaSinglePc10BlockWrite()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        byte[]? requestFrame = null;
        var serverTask = Task.Run(async () =>
        {
            using var serverClient = await listener.AcceptTcpClientAsync();
            await using var stream = serverClient.GetStream();
            requestFrame = await ReadFrameAsync(stream);
            await stream.WriteAsync(BuildResponse(0xC3, Array.Empty<byte>()));
        });

        using var client = new ToyopucDeviceClient("127.0.0.1", port, protocol: "tcp", timeout: LocalTestTimeoutSeconds)
        {
            CaptureTraceFrames = true,
        };
        client.WriteFr("FR000000", new[] { 0x1234, 0x5678 }, commit: false);

        await serverTask;

        Assert.Equal(
            ToyopucProtocol.BuildPc10BlockWrite(
                ToyopucAddress.EncodeFrWordAddr32(0),
                new byte[] { 0x34, 0x12, 0x78, 0x56 }),
            requestFrame);
        Assert.Single(client.TraceFrames);
    }

    private static async Task<byte[]> ReadFrameAsync(NetworkStream stream)
    {
        var header = new byte[4];
        await ReadExactlyAsync(stream, header);
        var length = header[2] | (header[3] << 8);
        var body = new byte[length];
        await ReadExactlyAsync(stream, body);
        return header.Concat(body).ToArray();
    }

    private static async Task ReadExactlyAsync(NetworkStream stream, byte[] buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset));
            if (read == 0)
            {
                throw new IOException("Unexpected end of stream");
            }

            offset += read;
        }
    }

    private static byte[] BuildResponse(int cmd, byte[] data)
    {
        var length = 1 + data.Length;
        return new[] { (byte)0x80, (byte)0x00, (byte)(length & 0xFF), (byte)((length >> 8) & 0xFF), (byte)(cmd & 0xFF) }
            .Concat(data)
            .ToArray();
    }

    private static byte[] BuildPc10MultiWordReadPayloadForTest(params int[] addresses32)
    {
        var payload = new byte[4 + (addresses32.Length * 4)];
        payload[2] = (byte)(addresses32.Length & 0xFF);
        for (var i = 0; i < addresses32.Length; i++)
        {
            payload[4 + (i * 4)] = (byte)(addresses32[i] & 0xFF);
            payload[5 + (i * 4)] = (byte)((addresses32[i] >> 8) & 0xFF);
            payload[6 + (i * 4)] = (byte)((addresses32[i] >> 16) & 0xFF);
            payload[7 + (i * 4)] = (byte)((addresses32[i] >> 24) & 0xFF);
        }

        return payload;
    }

    private static byte[] BuildPc10MultiBitReadPayloadForTest(params int[] addresses32)
    {
        var payload = new byte[4 + (addresses32.Length * 4)];
        payload[0] = (byte)(addresses32.Length & 0xFF);
        for (var i = 0; i < addresses32.Length; i++)
        {
            payload[4 + (i * 4)] = (byte)(addresses32[i] & 0xFF);
            payload[5 + (i * 4)] = (byte)((addresses32[i] >> 8) & 0xFF);
            payload[6 + (i * 4)] = (byte)((addresses32[i] >> 16) & 0xFF);
            payload[7 + (i * 4)] = (byte)((addresses32[i] >> 24) & 0xFF);
        }

        return payload;
    }

    private static byte[] BuildPc10MultiBitWritePayloadForTest(params (int Address32, int Value)[] items)
    {
        var payload = new byte[4 + (items.Length * 4) + ((items.Length + 7) / 8)];
        payload[0] = (byte)(items.Length & 0xFF);
        for (var i = 0; i < items.Length; i++)
        {
            payload[4 + (i * 4)] = (byte)(items[i].Address32 & 0xFF);
            payload[5 + (i * 4)] = (byte)((items[i].Address32 >> 8) & 0xFF);
            payload[6 + (i * 4)] = (byte)((items[i].Address32 >> 16) & 0xFF);
            payload[7 + (i * 4)] = (byte)((items[i].Address32 >> 24) & 0xFF);
            if ((items[i].Value & 0x01) != 0)
            {
                payload[4 + (items.Length * 4) + (i / 8)] |= (byte)(1 << (i % 8));
            }
        }

        return payload;
    }
}
