using System.Buffers;
using System.Net;
using System.Net.Sockets;

namespace Toyopuc;

public partial class ToyopucClient : IDisposable, IAsyncDisposable
{
    private static readonly IReadOnlyDictionary<int, string> ErrorCodeDescriptions = new Dictionary<int, string>
    {
        [0x11] = "CPU module hardware failure",
        [0x20] = "Relay command ENQ fixed data is not 0x05",
        [0x21] = "Invalid transfer number in relay command",
        [0x23] = "Invalid command code",
        [0x24] = "Invalid subcommand code",
        [0x25] = "Invalid command-format data byte",
        [0x26] = "Invalid function-call operand count",
        [0x31] = "Write or function call prohibited during sequence operation",
        [0x32] = "Command not executable during stop continuity",
        [0x33] = "Debug function called while not in debug mode",
        [0x34] = "Access prohibited by configuration",
        [0x35] = "Execution-priority limiting configuration prohibits execution",
        [0x36] = "Execution-priority limiting by another device prohibits execution",
        [0x39] = "Reset required after writing I/O parameters before scan start",
        [0x3C] = "Command not executable during fatal failure",
        [0x3D] = "Competing process prevents execution",
        [0x3E] = "Command not executable because reset exists",
        [0x3F] = "Command not executable because of stop duration",
        [0x40] = "Address or address+count is out of range",
        [0x41] = "Word/byte count is out of range",
        [0x42] = "Undesignated data was sent",
        [0x43] = "Invalid function-call operand",
        [0x52] = "Timer/counter set or current value access command mismatch",
        [0x66] = "No reply from relay link module",
        [0x70] = "Relay link module not executable",
        [0x72] = "No reply from relay link module",
        [0x73] = "Relay command collision on same link module; retry required",
    };

    private const int FrBlockWords = 0x8000;
    private const int FrMaxIndex = 0x1FFFFF;
    private const int FrIoChunkWords = 0x0200;

    private Socket? _socket;
    private IPEndPoint? _remoteEndPoint;
    private byte[]? _lastTx;
    private byte[]? _lastRx;
    private bool? _frWaitPrefersA0;
    private bool? _relayFrWaitPrefersA0;
    private readonly List<TransportTraceFrame> _traceFrames = new();

    public ToyopucClient(
        string host,
        int port,
        int localPort = 0,
        string protocol = "tcp",
        double timeout = 3.0,
        int retries = 0,
        double retryDelay = 0.2,
        int recvBufsize = 8192)
    {
        Host = host;
        Port = port;
        LocalPort = localPort;
        Protocol = protocol.ToLowerInvariant();
        Timeout = timeout;
        Retries = Math.Max(0, retries);
        RetryDelay = retryDelay;
        RecvBufsize = recvBufsize;
    }

    public string Host { get; }
    public int Port { get; }
    public int LocalPort { get; }
    public string Protocol { get; }
    public double Timeout { get; }
    public int Retries { get; }
    public double RetryDelay { get; }
    public int RecvBufsize { get; }
    public bool CaptureTraceFrames { get; set; }

    public byte[]? LastTx => _lastTx?.ToArray();
    public byte[]? LastRx => _lastRx?.ToArray();
    public IReadOnlyList<TransportTraceFrame> TraceFrames =>
        _traceFrames.Select(static frame => new TransportTraceFrame(frame.Tx.ToArray(), frame.Rx?.ToArray())).ToArray();

    public virtual void Connect()
    {
        if (_socket is not null)
        {
            return;
        }

        var remoteAddress = ResolveRemoteAddress(Host);
        _remoteEndPoint = new IPEndPoint(remoteAddress, Port);

        Socket socket;
        if (Protocol == "tcp")
        {
            socket = new Socket(remoteAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            ConfigureSocket(socket);
            ConnectWithTimeout(socket, _remoteEndPoint, Timeout);
        }
        else if (Protocol == "udp")
        {
            socket = new Socket(remoteAddress.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            ConfigureSocket(socket);
            if (LocalPort != 0)
            {
                socket.Bind(CreateAnyEndPoint(remoteAddress.AddressFamily, LocalPort));
            }
        }
        else
        {
            throw new ArgumentException("protocol must be 'tcp' or 'udp'", nameof(Protocol));
        }

        _socket = socket;
    }

    public virtual void Close()
    {
        if (_socket is not null)
        {
            try
            {
                _socket.Dispose();
            }
            finally
            {
                _socket = null;
            }
        }

        _lastTx = null;
        _lastRx = null;
        _traceFrames.Clear();
    }

    public void Dispose()
    {
        Close();
        GC.SuppressFinalize(this);
    }

    public void ClearTraceFrames()
    {
        _traceFrames.Clear();
    }

    public ResponseFrame SendRaw(int cmd, byte[]? data = null)
    {
        return SendAndReceive(ToyopucProtocol.BuildCommand(cmd, data));
    }

    public ResponseFrame SendPayload(byte[] payload)
    {
        return SendAndReceive(payload);
    }

    public int[] ReadWords(int address, int count)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildWordRead(address, count));
        EnsureCommand(response, 0x1C);
        return ToyopucProtocol.UnpackU16LittleEndian(response.Data);
    }

    public void WriteWords(int address, IEnumerable<int> values)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildWordWrite(address, values));
        EnsureCommand(response, 0x1D);
    }

    public byte[] ReadBytes(int address, int count)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildByteRead(address, count));
        EnsureCommand(response, 0x1E);
        return response.Data;
    }

    public void WriteBytes(int address, IEnumerable<int> values)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildByteWrite(address, values));
        EnsureCommand(response, 0x1F);
    }

    public bool ReadBit(int address)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildBitRead(address));
        EnsureCommand(response, 0x20);
        if (response.Data.Length != 1)
        {
            throw new ToyopucProtocolError("Bit read response must be 1 byte");
        }

        return response.Data[0] != 0;
    }

    public void WriteBit(int address, bool value)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildBitWrite(address, value ? 1 : 0));
        EnsureCommand(response, 0x21);
    }

    public int[] ReadWordsMulti(IEnumerable<int> addresses)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildMultiWordRead(addresses));
        EnsureCommand(response, 0x22);
        return ToyopucProtocol.UnpackU16LittleEndian(response.Data);
    }

    public void WriteWordsMulti(IEnumerable<(int Address, int Value)> pairs)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildMultiWordWrite(pairs));
        EnsureCommand(response, 0x23);
    }

    public byte[] ReadBytesMulti(IEnumerable<int> addresses)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildMultiByteRead(addresses));
        EnsureCommand(response, 0x24);
        return response.Data;
    }

    public void WriteBytesMulti(IEnumerable<(int Address, int Value)> pairs)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildMultiByteWrite(pairs));
        EnsureCommand(response, 0x25);
    }

    public int[] ReadExtWords(int number, int address, int count)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildExtWordRead(number, address, count));
        EnsureCommand(response, 0x94);
        return ToyopucProtocol.UnpackU16LittleEndian(response.Data);
    }

    public void WriteExtWords(int number, int address, IEnumerable<int> values)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildExtWordWrite(number, address, values));
        EnsureCommand(response, 0x95);
    }

    public byte[] ReadExtBytes(int number, int address, int count)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildExtByteRead(number, address, count));
        EnsureCommand(response, 0x96);
        return response.Data;
    }

    public void WriteExtBytes(int number, int address, IEnumerable<int> values)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildExtByteWrite(number, address, values));
        EnsureCommand(response, 0x97);
    }

    public byte[] ReadExtMulti(
        IEnumerable<(int No, int Bit, int Address)> bitPoints,
        IEnumerable<(int No, int Address)> bytePoints,
        IEnumerable<(int No, int Address)> wordPoints)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildExtMultiRead(bitPoints, bytePoints, wordPoints));
        EnsureCommand(response, 0x98);
        return response.Data;
    }

    public void WriteExtMulti(
        IEnumerable<(int No, int Bit, int Address, int Value)> bitPoints,
        IEnumerable<(int No, int Address, int Value)> bytePoints,
        IEnumerable<(int No, int Address, int Value)> wordPoints)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildExtMultiWrite(bitPoints, bytePoints, wordPoints));
        EnsureCommand(response, 0x99);
    }

    public byte[] Pc10BlockRead(int address32, int count)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildPc10BlockRead(address32, count));
        EnsureCommand(response, 0xC2);
        return response.Data;
    }

    public void Pc10BlockWrite(int address32, byte[] dataBytes)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildPc10BlockWrite(address32, dataBytes));
        EnsureCommand(response, 0xC3);
    }

    public byte[] Pc10MultiRead(byte[] payload)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildPc10MultiRead(payload));
        EnsureCommand(response, 0xC4);
        return response.Data;
    }

    public void Pc10MultiWrite(byte[] payload)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildPc10MultiWrite(payload));
        EnsureCommand(response, 0xC5);
    }

    public int[] ReadFrWords(int index, int count)
    {
        var values = new int[count];
        var offset = 0;
        foreach (var (chunkIndex, chunkWords) in IterateFrIoSegments(index, count))
        {
            var chunk = ToyopucProtocol.UnpackU16LittleEndian(Pc10BlockRead(ToyopucAddress.EncodeFrWordAddr32(chunkIndex), chunkWords * 2));
            Array.Copy(chunk, 0, values, offset, chunkWords);
            offset += chunkWords;
        }

        return values;
    }

    public void WriteFrWords(int index, IEnumerable<int> values, bool commit = false)
    {
        WriteFrWordsEx(index, values, commit, commit);
    }

    public void WriteFrWordsEx(
        int index,
        IEnumerable<int> values,
        bool commit = false,
        bool wait = false,
        double timeout = 30.0,
        double pollInterval = 0.2)
    {
        var normalizedValues = NormalizeWordValues(values);
        if (normalizedValues.Length == 0)
        {
            throw new ArgumentException("values must contain at least one word", nameof(values));
        }

        var offset = 0;
        foreach (var (blockIndex, blockWords) in IterateFrSegments(index, normalizedValues.Length))
        {
            var blockOffset = 0;
            while (blockOffset < blockWords)
            {
                var chunkWords = Math.Min(blockWords - blockOffset, FrIoChunkWords);
                Pc10BlockWrite(
                    ToyopucAddress.EncodeFrWordAddr32(blockIndex + blockOffset),
                    PackWordSlice(normalizedValues, offset, chunkWords));
                offset += chunkWords;
                blockOffset += chunkWords;
            }

            if (commit)
            {
                CommitFrBlock(blockIndex, wait, timeout, pollInterval);
            }
        }
    }

    public CpuStatusData? CommitFrBlock(int index, bool wait = false, double timeout = 30.0, double pollInterval = 0.2)
    {
        FrRegister(ToyopucAddress.FrBlockExNo(index));
        return wait ? WaitFrWriteComplete(timeout, pollInterval) : null;
    }

    public CpuStatusData? CommitFrRange(int index, int count = 1, bool wait = false, double timeout = 30.0, double pollInterval = 0.2)
    {
        CpuStatusData? lastStatus = null;
        foreach (var blockIndex in FrCommitBlocks(index, count))
        {
            lastStatus = CommitFrBlock(blockIndex, wait, timeout, pollInterval);
        }

        return lastStatus;
    }

    public void WriteFrWordsCommitted(int index, IEnumerable<int> values)
    {
        WriteFrWordsEx(index, values, commit: true, wait: true);
    }

    public void FrRegister(int exNo)
    {
        var response = SendAndReceive(ToyopucProtocol.BuildFrRegister(exNo));
        EnsureCommand(response, 0xCA);
    }

    public ResponseFrame RelayCommand(int linkNo, int stationNo, byte[] innerPayload)
    {
        return SendAndReceive(ToyopucProtocol.BuildRelayCommand(linkNo, stationNo, innerPayload));
    }

    public ResponseFrame RelayNested(IEnumerable<(int LinkNo, int StationNo)> hops, byte[] innerPayload)
    {
        return SendAndReceive(ToyopucProtocol.BuildRelayNested(hops, innerPayload));
    }

    public ResponseFrame SendViaRelay(object hops, byte[] innerPayload)
    {
        var outer = RelayNested(ToyopucRelay.NormalizeRelayHops(hops), innerPayload);
        var (layers, finalResponse) = ToyopucRelay.UnwrapRelayResponseChain(outer);
        if (finalResponse is null)
        {
            var lastLayer = layers[^1];
            throw new ToyopucProtocolError(
                $"Relay NAK at link=0x{lastLayer.LinkNo:X2}, station=0x{lastLayer.StationNo:X4}, ack=0x{lastLayer.Ack:X2}");
        }

        return finalResponse;
    }

    public int[] RelayReadWords(object hops, int address, int count)
    {
        var response = SendViaRelay(hops, ToyopucProtocol.BuildWordRead(address, count));
        EnsureCommand(response, 0x1C, "Unexpected CMD in relay word-read response");
        return ToyopucProtocol.UnpackU16LittleEndian(response.Data);
    }

    public void RelayWriteWords(object hops, int address, IEnumerable<int> values)
    {
        var response = SendViaRelay(hops, ToyopucProtocol.BuildWordWrite(address, values));
        EnsureCommand(response, 0x1D, "Unexpected CMD in relay word-write response");
    }

    public ClockData RelayReadClock(object hops)
    {
        var response = SendViaRelay(hops, ToyopucProtocol.BuildClockRead());
        EnsureCommand(response, 0x32, "Unexpected CMD in relay clock response");
        try
        {
            return ToyopucProtocol.ParseClockData(response.Data);
        }
        catch (Exception exception)
        {
            throw new ToyopucProtocolError($"Failed to parse relay clock response data={Convert.ToHexStringLower(response.Data)}", exception);
        }
    }

    public void RelayWriteClock(object hops, DateTime value)
    {
        var weekday = (int)value.DayOfWeek;
        var response = SendViaRelay(
            hops,
            ToyopucProtocol.BuildClockWrite(
                value.Second,
                value.Minute,
                value.Hour,
                value.Day,
                value.Month,
                value.Year % 100,
                weekday));
        EnsureCommand(response, 0x32, "Unexpected CMD in relay clock-write response");
        if (!response.Data.SequenceEqual(new byte[] { 0x71, 0x00 }))
        {
            throw new ToyopucProtocolError("Unexpected relay clock-write response body");
        }
    }

    public CpuStatusData RelayReadCpuStatus(object hops)
    {
        var response = SendViaRelay(hops, ToyopucProtocol.BuildCpuStatusRead());
        EnsureCommand(response, 0x32, "Unexpected CMD in relay CPU status response");
        try
        {
            return ToyopucProtocol.ParseCpuStatusData(response.Data);
        }
        catch (Exception exception)
        {
            throw new ToyopucProtocolError($"Failed to parse relay CPU status response data={Convert.ToHexStringLower(response.Data)}", exception);
        }
    }

    public byte[] RelayReadCpuStatusA0Raw(object hops)
    {
        var response = SendViaRelay(hops, ToyopucProtocol.BuildCpuStatusReadA0());
        EnsureCommand(response, 0xA0, "Unexpected CMD in relay A0 CPU status response");
        try
        {
            return ToyopucProtocol.ParseCpuStatusDataA0Raw(response.Data);
        }
        catch (Exception exception)
        {
            throw new ToyopucProtocolError($"Failed to parse relay A0 CPU status response data={Convert.ToHexStringLower(response.Data)}", exception);
        }
    }

    public CpuStatusData RelayReadCpuStatusA0(object hops)
    {
        var response = SendViaRelay(hops, ToyopucProtocol.BuildCpuStatusReadA0());
        EnsureCommand(response, 0xA0, "Unexpected CMD in relay A0 CPU status response");
        try
        {
            return ToyopucProtocol.ParseCpuStatusDataA0(response.Data);
        }
        catch (Exception exception)
        {
            throw new ToyopucProtocolError($"Failed to parse relay A0 CPU status response data={Convert.ToHexStringLower(response.Data)}", exception);
        }
    }

    public void RelayWriteFrWords(object hops, int index, IEnumerable<int> values, bool commit = false)
    {
        RelayWriteFrWordsEx(hops, index, values, commit, commit);
    }

    public void RelayWriteFrWordsEx(
        object hops,
        int index,
        IEnumerable<int> values,
        bool commit = false,
        bool wait = false,
        double timeout = 30.0,
        double pollInterval = 0.2)
    {
        var normalizedValues = NormalizeWordValues(values);
        if (normalizedValues.Length == 0)
        {
            throw new ArgumentException("values must contain at least one word", nameof(values));
        }

        var offset = 0;
        foreach (var (blockIndex, blockWords) in IterateFrSegments(index, normalizedValues.Length))
        {
            var blockOffset = 0;
            while (blockOffset < blockWords)
            {
                var chunkWords = Math.Min(blockWords - blockOffset, FrIoChunkWords);
                var response = SendViaRelay(
                    hops,
                    ToyopucProtocol.BuildPc10BlockWrite(
                        ToyopucAddress.EncodeFrWordAddr32(blockIndex + blockOffset),
                        PackWordSlice(normalizedValues, offset, chunkWords)));
                EnsureCommand(response, 0xC3, "Unexpected CMD in relay FR block-write response");
                offset += chunkWords;
                blockOffset += chunkWords;
            }

            if (commit)
            {
                RelayCommitFrBlock(hops, blockIndex, wait, timeout, pollInterval);
            }
        }
    }

    public void RelayFrRegister(object hops, int exNo)
    {
        var response = SendViaRelay(hops, ToyopucProtocol.BuildFrRegister(exNo));
        EnsureCommand(response, 0xCA, "Unexpected CMD in relay FR-register response");
    }

    public CpuStatusData? RelayCommitFrBlock(object hops, int index, bool wait = false, double timeout = 30.0, double pollInterval = 0.2)
    {
        RelayFrRegister(hops, ToyopucAddress.FrBlockExNo(index));
        return wait ? RelayWaitFrWriteComplete(hops, timeout, pollInterval) : null;
    }

    public CpuStatusData? RelayCommitFrRange(object hops, int index, int count = 1, bool wait = false, double timeout = 30.0, double pollInterval = 0.2)
    {
        CpuStatusData? lastStatus = null;
        foreach (var blockIndex in FrCommitBlocks(index, count))
        {
            lastStatus = RelayCommitFrBlock(hops, blockIndex, wait, timeout, pollInterval);
        }

        return lastStatus;
    }

    public CpuStatusData RelayWaitFrWriteComplete(object hops, double timeout = 30.0, double pollInterval = 0.2)
    {
        var deadline = DateTime.UtcNow.AddSeconds(Math.Max(0.0, timeout));
        var interval = TimeSpan.FromSeconds(Math.Max(0.01, pollInterval));
        while (true)
        {
            CpuStatusData status;
            var useA0 = _relayFrWaitPrefersA0 is not false;
            if (useA0)
            {
                try
                {
                    status = RelayReadCpuStatusA0(hops);
                    _relayFrWaitPrefersA0 = true;
                }
                catch (ToyopucError)
                {
                    var error = ExtractResponseErrorCode(_lastRx) ?? ExtractRelayNakErrorCode(_lastRx);
                    if (error is 0x23 or 0x24 or 0x25 or 0x26)
                    {
                        _relayFrWaitPrefersA0 = false;
                        status = RelayReadCpuStatus(hops);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            else
            {
                status = RelayReadCpuStatus(hops);
            }

            if (status.AbnormalWriteFlashRegister)
            {
                throw new ToyopucError("FR flash write failed: abnormal_write_flash_register=1");
            }

            if (!status.UnderWritingFlashRegister)
            {
                return status;
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw new ToyopucTimeoutError("Timed out waiting for relay FR flash write completion");
            }

            Thread.Sleep(interval);
        }
    }

    public ClockData ReadClock()
    {
        var response = SendAndReceive(ToyopucProtocol.BuildClockRead());
        EnsureCommand(response, 0x32);
        try
        {
            return ToyopucProtocol.ParseClockData(response.Data);
        }
        catch (Exception exception)
        {
            throw new ToyopucProtocolError($"Failed to parse clock response data={Convert.ToHexStringLower(response.Data)}", exception);
        }
    }

    public CpuStatusData ReadCpuStatus()
    {
        var response = SendAndReceive(ToyopucProtocol.BuildCpuStatusRead());
        EnsureCommand(response, 0x32);
        try
        {
            return ToyopucProtocol.ParseCpuStatusData(response.Data);
        }
        catch (Exception exception)
        {
            throw new ToyopucProtocolError($"Failed to parse CPU status response data={Convert.ToHexStringLower(response.Data)}", exception);
        }
    }

    public byte[] ReadCpuStatusA0Raw()
    {
        var response = SendAndReceive(ToyopucProtocol.BuildCpuStatusReadA0());
        EnsureCommand(response, 0xA0);
        try
        {
            return ToyopucProtocol.ParseCpuStatusDataA0Raw(response.Data);
        }
        catch (Exception exception)
        {
            throw new ToyopucProtocolError($"Failed to parse A0 CPU status response data={Convert.ToHexStringLower(response.Data)}", exception);
        }
    }

    public CpuStatusData ReadCpuStatusA0()
    {
        var response = SendAndReceive(ToyopucProtocol.BuildCpuStatusReadA0());
        EnsureCommand(response, 0xA0);
        try
        {
            return ToyopucProtocol.ParseCpuStatusDataA0(response.Data);
        }
        catch (Exception exception)
        {
            throw new ToyopucProtocolError($"Failed to parse A0 CPU status response data={Convert.ToHexStringLower(response.Data)}", exception);
        }
    }

    public CpuStatusData WaitFrWriteComplete(double timeout = 30.0, double pollInterval = 0.2)
    {
        var deadline = DateTime.UtcNow.AddSeconds(Math.Max(0.0, timeout));
        var interval = TimeSpan.FromSeconds(Math.Max(0.01, pollInterval));
        while (true)
        {
            CpuStatusData status;
            var useA0 = _frWaitPrefersA0 is not false;
            if (useA0)
            {
                try
                {
                    status = ReadCpuStatusA0();
                    _frWaitPrefersA0 = true;
                }
                catch (ToyopucError)
                {
                    var error = ExtractResponseErrorCode(_lastRx);
                    if (error is 0x23 or 0x24 or 0x25)
                    {
                        _frWaitPrefersA0 = false;
                        status = ReadCpuStatus();
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            else
            {
                status = ReadCpuStatus();
            }

            if (status.AbnormalWriteFlashRegister)
            {
                throw new ToyopucError("FR flash write failed: abnormal_write_flash_register=1");
            }

            if (!status.UnderWritingFlashRegister)
            {
                return status;
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw new ToyopucTimeoutError("Timed out waiting for FR flash write completion");
            }

            Thread.Sleep(interval);
        }
    }

    public void WriteClock(DateTime value)
    {
        var weekday = (int)value.DayOfWeek;
        var response = SendAndReceive(
            ToyopucProtocol.BuildClockWrite(
                value.Second,
                value.Minute,
                value.Hour,
                value.Day,
                value.Month,
                value.Year % 100,
                weekday));
        EnsureCommand(response, 0x32);
        if (!response.Data.SequenceEqual(new byte[] { 0x71, 0x00 }))
        {
            throw new ToyopucProtocolError("Unexpected clock write response body");
        }
    }

    protected static void EnsureCommand(ResponseFrame response, int expectedCommand, string? message = null)
    {
        if (response.Cmd != expectedCommand)
        {
            throw new ToyopucProtocolError(message ?? "Unexpected CMD in response");
        }
    }

    protected static string FormatResponseError(ResponseFrame response)
    {
        var message = $"Response error rc=0x{response.Rc:X2}";
        if (response.Rc == 0x10)
        {
            var error = response.Data.Length > 0 ? response.Data[^1] : response.Cmd;
            var detail = ErrorCodeDescriptions.TryGetValue(error, out var description) ? description : "Unknown error code";
            return $"{message}, error_code=0x{error:X2} ({detail}), data={Convert.ToHexStringLower(response.Data)}";
        }

        return $"{message}, data={Convert.ToHexStringLower(response.Data)}";
    }

    protected ResponseFrame SendAndReceive(byte[] payload)
    {
        var attempt = 0;
        Exception? lastError = null;

        while (attempt <= Retries)
        {
            attempt++;
            if (_socket is null)
            {
                Connect();
            }

            _lastTx = payload;
            _lastRx = null;

            try
            {
                byte[] frame;
                if (Protocol == "tcp")
                {
                    Span<byte> header = stackalloc byte[4];
                    SendAll(payload);
                    ReceiveExact(header);
                    var length = header[2] | (header[3] << 8);
                    frame = new byte[header.Length + length];
                    header.CopyTo(frame);
                    ReceiveExact(frame.AsSpan(header.Length, length));
                }
                else
                {
                    frame = SendAndReceiveUdp(payload);
                }

                _lastRx = frame;
                if (CaptureTraceFrames)
                {
                    _traceFrames.Add(new TransportTraceFrame(payload.ToArray(), frame.ToArray()));
                }
                var response = ToyopucProtocol.ParseResponse(frame);
                if (response.Ft != ToyopucProtocol.FtResponse)
                {
                    throw new ToyopucProtocolError($"Unexpected frame type: 0x{response.Ft:X2}");
                }

                if (response.Rc != 0x00)
                {
                    throw new ToyopucError(FormatResponseError(response));
                }

                return response;
            }
            catch (SocketException exception) when (exception.SocketErrorCode == SocketError.TimedOut)
            {
                lastError = new ToyopucTimeoutError("Send/receive timeout", exception);
                if (attempt <= Retries)
                {
                    RetryDelaySleep();
                    Close();
                    continue;
                }

                throw (ToyopucTimeoutError)lastError;
            }
            catch (TimeoutException exception)
            {
                lastError = new ToyopucTimeoutError("Connect timeout", exception);
                if (attempt <= Retries)
                {
                    RetryDelaySleep();
                    Close();
                    continue;
                }

                throw (ToyopucTimeoutError)lastError;
            }
            catch (ToyopucError exception)
            {
                lastError = exception;
                if (attempt <= Retries && IsRetryableResponseError(exception))
                {
                    RetryDelaySleep();
                    Close();
                    continue;
                }

                throw;
            }
            catch (Exception exception) when (exception is SocketException or ObjectDisposedException or InvalidOperationException)
            {
                lastError = new ToyopucError("Socket error", exception);
                if (attempt <= Retries)
                {
                    RetryDelaySleep();
                    Close();
                    continue;
                }

                throw (ToyopucError)lastError;
            }
        }

        if (lastError is not null)
        {
            throw lastError;
        }

        throw new ToyopucError("Send/receive failed");
    }

    private static IPAddress ResolveRemoteAddress(string host)
    {
        if (IPAddress.TryParse(host, out var address))
        {
            return address;
        }

        var addresses = Dns.GetHostAddresses(host);
        if (addresses.Length == 0)
        {
            throw new ToyopucError($"Failed to resolve host: {host}");
        }

        return addresses.FirstOrDefault(static address => address.AddressFamily == AddressFamily.InterNetwork)
            ?? addresses[0];
    }

    private static EndPoint CreateAnyEndPoint(AddressFamily addressFamily, int port)
    {
        return new IPEndPoint(addressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, port);
    }

    private static void ConnectWithTimeout(Socket socket, EndPoint endPoint, double timeoutSeconds)
    {
        var result = socket.BeginConnect(endPoint, null, null);
        if (!result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(timeoutSeconds)))
        {
            socket.Dispose();
            throw new TimeoutException("Timed out while connecting");
        }

        socket.EndConnect(result);
    }

    private void ConfigureSocket(Socket socket)
    {
        var timeoutMs = Math.Max(1, (int)Math.Ceiling(Timeout * 1000.0));
        socket.ReceiveTimeout = timeoutMs;
        socket.SendTimeout = timeoutMs;
        if (socket.SocketType == SocketType.Stream && socket.ProtocolType == ProtocolType.Tcp)
        {
            socket.NoDelay = true;
        }
    }

    private void SendAll(byte[] payload)
    {
        if (_socket is null)
        {
            throw new InvalidOperationException("Socket is not connected");
        }

        var offset = 0;
        while (offset < payload.Length)
        {
            var sent = _socket.Send(payload, offset, payload.Length - offset, SocketFlags.None);
            if (sent <= 0)
            {
                throw new ToyopucProtocolError("Connection closed while sending");
            }

            offset += sent;
        }
    }

    private void ReceiveExact(Span<byte> buffer)
    {
        if (_socket is null)
        {
            throw new InvalidOperationException("Socket is not connected");
        }

        while (!buffer.IsEmpty)
        {
            var received = _socket.Receive(buffer, SocketFlags.None);
            if (received <= 0)
            {
                throw new ToyopucProtocolError("Connection closed while receiving");
            }

            buffer = buffer[received..];
        }
    }

    private byte[] ReceiveExact(int count)
    {
        var buffer = new byte[count];
        ReceiveExact(buffer);
        return buffer;
    }

    private byte[] SendAndReceiveUdp(byte[] payload)
    {
        if (_socket is null || _remoteEndPoint is null)
        {
            throw new InvalidOperationException("Socket is not connected");
        }

        _socket.SendTo(payload, _remoteEndPoint);
        var buffer = ArrayPool<byte>.Shared.Rent(RecvBufsize);
        try
        {
            EndPoint remote = CreateAnyEndPoint(_remoteEndPoint.AddressFamily, 0);
            var received = _socket.ReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref remote);
            return buffer.AsSpan(0, received).ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private void RetryDelaySleep()
    {
        if (RetryDelay > 0)
        {
            Thread.Sleep(TimeSpan.FromSeconds(RetryDelay));
        }
    }

    private static bool IsRetryableResponseError(ToyopucError exception)
    {
        return exception.Message.Contains("error_code=0x73", StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] PackWordSlice(IReadOnlyList<int> values, int offset, int count)
    {
        var data = new byte[count * 2];
        for (var i = 0; i < count; i++)
        {
            var value = values[offset + i] & 0xFFFF;
            data[i * 2] = (byte)(value & 0xFF);
            data[(i * 2) + 1] = (byte)((value >> 8) & 0xFF);
        }

        return data;
    }

    private static int[] NormalizeWordValues(IEnumerable<int> values)
    {
        if (values is ICollection<int> collection)
        {
            var normalized = new int[collection.Count];
            var index = 0;
            foreach (var value in values)
            {
                normalized[index++] = value & 0xFFFF;
            }

            return normalized;
        }

        var list = new List<int>();
        foreach (var value in values)
        {
            list.Add(value & 0xFFFF);
        }

        return list.ToArray();
    }

    private static int ValidateFrIndex(int index)
    {
        if (index < 0 || index > FrMaxIndex)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "FR index out of range (0x000000-0x1FFFFF)");
        }

        return index;
    }

    private static IEnumerable<(int SegmentStart, int WordCount)> IterateFrSegments(int startIndex, int wordCount)
    {
        var index = ValidateFrIndex(startIndex);
        var remaining = wordCount;
        if (remaining < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(wordCount), "word_count must be >= 1");
        }

        while (remaining > 0)
        {
            var blockOffset = index % FrBlockWords;
            var chunk = Math.Min(remaining, FrBlockWords - blockOffset);
            yield return (index, chunk);
            index += chunk;
            remaining -= chunk;
        }
    }

    private static IEnumerable<(int SegmentStart, int WordCount)> IterateFrIoSegments(int startIndex, int wordCount, int maxChunkWords = FrIoChunkWords)
    {
        if (maxChunkWords < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxChunkWords), "max_chunk_words must be >= 1");
        }

        foreach (var (blockIndex, blockWords) in IterateFrSegments(startIndex, wordCount))
        {
            var offset = 0;
            while (offset < blockWords)
            {
                var chunk = Math.Min(blockWords - offset, maxChunkWords);
                yield return (blockIndex + offset, chunk);
                offset += chunk;
            }
        }
    }

    private static IReadOnlyList<int> FrCommitBlocks(int startIndex, int wordCount)
    {
        var blocks = new List<int>();
        foreach (var (segmentStart, _) in IterateFrSegments(startIndex, wordCount))
        {
            blocks.Add(segmentStart);
        }

        return blocks.ToArray();
    }

    private static int? ExtractResponseErrorCode(byte[]? frame)
    {
        if (frame is null)
        {
            return null;
        }

        try
        {
            var response = ToyopucProtocol.ParseResponse(frame);
            if (response.Rc != 0x10)
            {
                return null;
            }

            return response.Data.Length > 0 ? response.Data[^1] : response.Cmd;
        }
        catch
        {
            return null;
        }
    }

    private static int? ExtractRelayNakErrorCode(byte[]? frame)
    {
        if (frame is null)
        {
            return null;
        }

        try
        {
            var response = ToyopucProtocol.ParseResponse(frame);
            if (response.Cmd != 0x60)
            {
                return null;
            }

            var current = response;
            while (current.Cmd == 0x60)
            {
                if (current.Data.Length < 4)
                {
                    return null;
                }

                var ack = current.Data[3];
                var innerRaw = current.Data.AsSpan(4).ToArray();
                if (ack != 0x06)
                {
                    if (innerRaw.Length < 3)
                    {
                        return null;
                    }

                    var innerLength = innerRaw[0] | (innerRaw[1] << 8);
                    if (innerLength < 1 || innerRaw.Length < 2 + innerLength)
                    {
                        return null;
                    }

                    return innerRaw[2];
                }

                var (innerResponse, _) = ToyopucRelay.ParseRelayInnerResponse(innerRaw);
                current = innerResponse;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
