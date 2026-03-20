using System.Buffers.Binary;

namespace Toyopuc;

public static class ToyopucProtocol
{
    public const byte FtCommand = 0x00;
    public const byte FtResponse = 0x80;

    private static byte[] CreateCommandFrame(int cmd, int dataLength)
    {
        var frame = new byte[5 + dataLength];
        var length = 1 + dataLength;
        frame[0] = FtCommand;
        frame[1] = 0x00;
        frame[2] = (byte)(length & 0xFF);
        frame[3] = (byte)((length >> 8) & 0xFF);
        frame[4] = (byte)(cmd & 0xFF);
        return frame;
    }

    private static void WriteU16(byte[] buffer, int offset, int value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset, 2), unchecked((ushort)value));
    }

    private static byte[] MaterializeByteValues(IEnumerable<int> values)
    {
        if (values is ICollection<int> collection)
        {
            var bytes = new byte[collection.Count];
            var index = 0;
            foreach (var value in values)
            {
                bytes[index++] = (byte)(value & 0xFF);
            }

            return bytes;
        }

        var list = new List<byte>();
        foreach (var value in values)
        {
            list.Add((byte)(value & 0xFF));
        }

        return list.ToArray();
    }

    public static byte[] BuildCommand(int cmd, byte[]? data = null)
    {
        var payload = data ?? Array.Empty<byte>();
        var frame = CreateCommandFrame(cmd, payload.Length);
        if (payload.Length > 0)
        {
            Buffer.BlockCopy(payload, 0, frame, 5, payload.Length);
        }

        return frame;
    }

    public static ResponseFrame ParseResponse(byte[] frame)
    {
        if (frame.Length < 5)
        {
            throw new ToyopucProtocolError("Response too short");
        }

        var ft = frame[0];
        var rc = frame[1];
        var length = frame[2] | (frame[3] << 8);
        var expected = 4 + length;
        if (frame.Length != expected)
        {
            throw new ToyopucProtocolError($"Invalid length: expected {expected} bytes, got {frame.Length} bytes");
        }

        var data = new byte[Math.Max(0, frame.Length - 5)];
        if (data.Length > 0)
        {
            Buffer.BlockCopy(frame, 5, data, 0, data.Length);
        }

        return new ResponseFrame(ft, rc, frame[4], data);
    }

    public static byte[] PackU16LittleEndian(int value)
    {
        var buffer = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, unchecked((ushort)value));
        return buffer;
    }

    public static int[] UnpackU16LittleEndian(byte[] data)
    {
        if (data.Length % 2 != 0)
        {
            throw new ToyopucProtocolError("Word data length must be even");
        }

        var values = new int[data.Length / 2];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(i * 2, 2));
        }

        return values;
    }

    public static int PackExtBitSpec(int number, int bit)
    {
        if (number is < 0 or > 0x0F)
        {
            throw new ToyopucProtocolError("Extended bit No must fit in 4 bits");
        }

        if (bit is < 0 or > 0x0F)
        {
            throw new ToyopucProtocolError("Extended bit position must fit in 4 bits");
        }

        return ((bit & 0x0F) << 4) | (number & 0x0F);
    }

    public static int PackBcd(int value)
    {
        if (value is < 0 or > 99)
        {
            throw new ToyopucProtocolError("BCD value out of range");
        }

        return ((value / 10) << 4) | (value % 10);
    }

    public static int UnpackBcd(int value)
    {
        var high = (value >> 4) & 0x0F;
        var low = value & 0x0F;
        if (high > 9 || low > 9)
        {
            throw new ToyopucProtocolError($"Invalid BCD byte: 0x{value:X2}");
        }

        return (high * 10) + low;
    }

    public static byte[] BuildClockRead()
    {
        var frame = CreateCommandFrame(0x32, 2);
        frame[5] = 0x70;
        frame[6] = 0x00;
        return frame;
    }

    public static byte[] BuildCpuStatusRead()
    {
        var frame = CreateCommandFrame(0x32, 2);
        frame[5] = 0x11;
        frame[6] = 0x00;
        return frame;
    }

    public static byte[] BuildCpuStatusReadA0()
    {
        var frame = CreateCommandFrame(0xA0, 2);
        frame[5] = 0x01;
        frame[6] = 0x10;
        return frame;
    }

    public static byte[] BuildClockWrite(
        int second,
        int minute,
        int hour,
        int day,
        int month,
        int year2Digit,
        int weekday)
    {
        if (weekday is < 0 or > 6)
        {
            throw new ToyopucProtocolError("Weekday must be in range 0-6");
        }

        var frame = CreateCommandFrame(0x32, 9);
        frame[5] = 0x71;
        frame[6] = 0x00;
        frame[7] = (byte)PackBcd(second);
        frame[8] = (byte)PackBcd(minute);
        frame[9] = (byte)PackBcd(hour);
        frame[10] = (byte)PackBcd(day);
        frame[11] = (byte)PackBcd(month);
        frame[12] = (byte)PackBcd(year2Digit);
        frame[13] = (byte)PackBcd(weekday);
        return frame;
    }

    public static ClockData ParseClockData(byte[] data)
    {
        if (data.Length != 9 || data[0] != 0x70 || data[1] != 0x00)
        {
            throw new ToyopucProtocolError("Clock read response must be 9 bytes starting with 70 00");
        }

        return new ClockData(
            UnpackBcd(data[2]),
            UnpackBcd(data[3]),
            UnpackBcd(data[4]),
            UnpackBcd(data[5]),
            UnpackBcd(data[6]),
            UnpackBcd(data[7]),
            UnpackBcd(data[8]));
    }

    public static CpuStatusData ParseCpuStatusData(byte[] data)
    {
        if (data.Length != 10 || data[0] != 0x11 || data[1] != 0x00)
        {
            throw new ToyopucProtocolError("CPU status response must be 10 bytes starting with 11 00");
        }

        return new CpuStatusData(data[2], data[3], data[4], data[5], data[6], data[7], data[8], data[9]);
    }

    public static CpuStatusData ParseCpuStatusDataA0(byte[] data)
    {
        if (data.Length != 10 || data[0] != 0x01 || data[1] != 0x10)
        {
            throw new ToyopucProtocolError("A0 CPU status response must be 10 bytes starting with 01 10");
        }

        return new CpuStatusData(data[2], data[3], data[4], data[5], data[6], data[7], data[8], data[9]);
    }

    public static byte[] ParseCpuStatusDataA0Raw(byte[] data)
    {
        return ParseCpuStatusDataA0(data).RawBytes;
    }

    public static byte[] BuildWordRead(int address, int count)
    {
        var frame = CreateCommandFrame(0x1C, 4);
        WriteU16(frame, 5, address);
        WriteU16(frame, 7, count);
        return frame;
    }

    public static byte[] BuildWordWrite(int address, IEnumerable<int> values)
    {
        var words = values.ToArray();
        var frame = CreateCommandFrame(0x1D, 2 + (words.Length * 2));
        WriteU16(frame, 5, address);
        for (var i = 0; i < words.Length; i++)
        {
            WriteU16(frame, 7 + (i * 2), words[i]);
        }

        return frame;
    }

    public static byte[] BuildByteRead(int address, int count)
    {
        var frame = CreateCommandFrame(0x1E, 4);
        WriteU16(frame, 5, address);
        WriteU16(frame, 7, count);
        return frame;
    }

    public static byte[] BuildByteWrite(int address, IEnumerable<int> values)
    {
        var bytes = MaterializeByteValues(values);
        var frame = CreateCommandFrame(0x1F, 2 + bytes.Length);
        WriteU16(frame, 5, address);
        if (bytes.Length > 0)
        {
            Buffer.BlockCopy(bytes, 0, frame, 7, bytes.Length);
        }

        return frame;
    }

    public static byte[] BuildBitRead(int address)
    {
        var frame = CreateCommandFrame(0x20, 2);
        WriteU16(frame, 5, address);
        return frame;
    }

    public static byte[] BuildBitWrite(int address, int value)
    {
        var frame = CreateCommandFrame(0x21, 3);
        WriteU16(frame, 5, address);
        frame[7] = (byte)(value != 0 ? 1 : 0);
        return frame;
    }

    public static byte[] BuildMultiWordRead(IEnumerable<int> addresses)
    {
        var items = addresses.ToArray();
        var frame = CreateCommandFrame(0x22, items.Length * 2);
        for (var i = 0; i < items.Length; i++)
        {
            WriteU16(frame, 5 + (i * 2), items[i]);
        }

        return frame;
    }

    public static byte[] BuildMultiWordWrite(IEnumerable<(int Address, int Value)> pairs)
    {
        var items = pairs.ToArray();
        var frame = CreateCommandFrame(0x23, items.Length * 4);
        for (var i = 0; i < items.Length; i++)
        {
            WriteU16(frame, 5 + (i * 4), items[i].Address);
            WriteU16(frame, 7 + (i * 4), items[i].Value);
        }

        return frame;
    }

    public static byte[] BuildMultiByteRead(IEnumerable<int> addresses)
    {
        var items = addresses.ToArray();
        var frame = CreateCommandFrame(0x24, items.Length * 2);
        for (var i = 0; i < items.Length; i++)
        {
            WriteU16(frame, 5 + (i * 2), items[i]);
        }

        return frame;
    }

    public static byte[] BuildMultiByteWrite(IEnumerable<(int Address, int Value)> pairs)
    {
        var items = pairs.ToArray();
        var frame = CreateCommandFrame(0x25, items.Length * 3);
        for (var i = 0; i < items.Length; i++)
        {
            WriteU16(frame, 5 + (i * 3), items[i].Address);
            frame[7 + (i * 3)] = (byte)(items[i].Value & 0xFF);
        }

        return frame;
    }

    public static byte[] BuildExtWordRead(int number, int address, int count)
    {
        var frame = CreateCommandFrame(0x94, 5);
        frame[5] = (byte)(number & 0xFF);
        WriteU16(frame, 6, address);
        WriteU16(frame, 8, count);
        return frame;
    }

    public static byte[] BuildExtWordWrite(int number, int address, IEnumerable<int> values)
    {
        var words = values.ToArray();
        var frame = CreateCommandFrame(0x95, 3 + (words.Length * 2));
        frame[5] = (byte)(number & 0xFF);
        WriteU16(frame, 6, address);
        for (var i = 0; i < words.Length; i++)
        {
            WriteU16(frame, 8 + (i * 2), words[i]);
        }

        return frame;
    }

    public static byte[] BuildExtByteRead(int number, int address, int count)
    {
        var frame = CreateCommandFrame(0x96, 5);
        frame[5] = (byte)(number & 0xFF);
        WriteU16(frame, 6, address);
        WriteU16(frame, 8, count);
        return frame;
    }

    public static byte[] BuildExtByteWrite(int number, int address, IEnumerable<int> values)
    {
        var bytes = MaterializeByteValues(values);
        var frame = CreateCommandFrame(0x97, 3 + bytes.Length);
        frame[5] = (byte)(number & 0xFF);
        WriteU16(frame, 6, address);
        if (bytes.Length > 0)
        {
            Buffer.BlockCopy(bytes, 0, frame, 8, bytes.Length);
        }

        return frame;
    }

    public static byte[] BuildExtMultiRead(
        IEnumerable<(int No, int Bit, int Address)> bitPoints,
        IEnumerable<(int No, int Address)> bytePoints,
        IEnumerable<(int No, int Address)> wordPoints)
    {
        var bits = bitPoints.ToArray();
        var bytes = bytePoints.ToArray();
        var words = wordPoints.ToArray();
        var frame = CreateCommandFrame(0x98, 3 + (bits.Length * 3) + (bytes.Length * 3) + (words.Length * 3));
        frame[5] = (byte)(bits.Length & 0xFF);
        frame[6] = (byte)(bytes.Length & 0xFF);
        frame[7] = (byte)(words.Length & 0xFF);
        var offset = 8;

        foreach (var (number, bit, address) in bits)
        {
            frame[offset++] = (byte)PackExtBitSpec(number, bit);
            WriteU16(frame, offset, address);
            offset += 2;
        }

        foreach (var (number, address) in bytes)
        {
            frame[offset++] = (byte)(number & 0xFF);
            WriteU16(frame, offset, address);
            offset += 2;
        }

        foreach (var (number, address) in words)
        {
            frame[offset++] = (byte)(number & 0xFF);
            WriteU16(frame, offset, address);
            offset += 2;
        }

        return frame;
    }

    public static byte[] BuildExtMultiWrite(
        IEnumerable<(int No, int Bit, int Address, int Value)> bitPoints,
        IEnumerable<(int No, int Address, int Value)> bytePoints,
        IEnumerable<(int No, int Address, int Value)> wordPoints)
    {
        var bits = bitPoints.ToArray();
        var bytes = bytePoints.ToArray();
        var words = wordPoints.ToArray();
        var frame = CreateCommandFrame(0x99, 3 + (bits.Length * 4) + (bytes.Length * 4) + (words.Length * 5));
        frame[5] = (byte)(bits.Length & 0xFF);
        frame[6] = (byte)(bytes.Length & 0xFF);
        frame[7] = (byte)(words.Length & 0xFF);
        var offset = 8;

        foreach (var (number, bit, address, value) in bits)
        {
            frame[offset++] = (byte)PackExtBitSpec(number, bit);
            WriteU16(frame, offset, address);
            offset += 2;
            frame[offset++] = (byte)(value & 0x01);
        }

        foreach (var (number, address, value) in bytes)
        {
            frame[offset++] = (byte)(number & 0xFF);
            WriteU16(frame, offset, address);
            offset += 2;
            frame[offset++] = (byte)(value & 0xFF);
        }

        foreach (var (number, address, value) in words)
        {
            frame[offset++] = (byte)(number & 0xFF);
            WriteU16(frame, offset, address);
            offset += 2;
            WriteU16(frame, offset, value);
            offset += 2;
        }

        return frame;
    }

    public static byte[] BuildPc10BlockRead(int address32, int count)
    {
        var frame = CreateCommandFrame(0xC2, 6);
        WriteU16(frame, 5, address32 & 0xFFFF);
        WriteU16(frame, 7, (address32 >> 16) & 0xFFFF);
        WriteU16(frame, 9, count);
        return frame;
    }

    public static byte[] BuildPc10BlockWrite(int address32, byte[] dataBytes)
    {
        var frame = CreateCommandFrame(0xC3, 4 + dataBytes.Length);
        WriteU16(frame, 5, address32 & 0xFFFF);
        WriteU16(frame, 7, (address32 >> 16) & 0xFFFF);
        if (dataBytes.Length > 0)
        {
            Buffer.BlockCopy(dataBytes, 0, frame, 9, dataBytes.Length);
        }

        return frame;
    }

    public static byte[] BuildPc10MultiRead(byte[] payload)
    {
        return BuildCommand(0xC4, payload);
    }

    public static byte[] BuildPc10MultiWrite(byte[] payload)
    {
        return BuildCommand(0xC5, payload);
    }

    public static byte[] BuildFrRegister(int exNo)
    {
        var frame = CreateCommandFrame(0xCA, 1);
        frame[5] = (byte)(exNo & 0xFF);
        return frame;
    }

    public static byte[] BuildRelayCommand(int linkNo, int stationNo, byte[] innerPayload, int enq = 0x05)
    {
        var inner = NormalizeInnerPayload(innerPayload);
        var frame = CreateCommandFrame(0x60, 5 + inner.Length);
        frame[5] = (byte)(linkNo & 0xFF);
        frame[6] = (byte)(stationNo & 0xFF);
        frame[7] = (byte)((stationNo >> 8) & 0xFF);
        frame[8] = (byte)(enq & 0xFF);
        Buffer.BlockCopy(inner, 0, frame, 9, inner.Length);
        frame[^1] = 0x00;
        return frame;
    }

    public static byte[] BuildRelayNested(IEnumerable<(int LinkNo, int StationNo)> hops, byte[] innerPayload)
    {
        var hopList = hops.ToArray();
        if (hopList.Length == 0)
        {
            throw new ArgumentException("at least one relay hop is required", nameof(hops));
        }

        var inner = NormalizeInnerPayload(innerPayload);
        byte[]? frame = null;
        for (var i = hopList.Length - 1; i >= 0; i--)
        {
            frame = BuildRelayCommand(hopList[i].LinkNo, hopList[i].StationNo, inner);
            inner = FrameToInnerPayload(frame);
        }

        return frame ?? throw new InvalidOperationException("Relay frame construction failed");
    }

    private static byte[] NormalizeInnerPayload(byte[] innerPayload)
    {
        if (innerPayload.Length < 3)
        {
            throw new ArgumentException("inner payload must contain at least LL, LH, and CMD bytes", nameof(innerPayload));
        }

        byte[] trimmed;
        if (innerPayload[0] == FtCommand)
        {
            if (innerPayload.Length < 5)
            {
                throw new ArgumentException("inner command frame too short", nameof(innerPayload));
            }

            if (innerPayload[1] != 0x00)
            {
                throw new ArgumentException("relay inner frame must be a command request (RC=0x00)", nameof(innerPayload));
            }

            trimmed = new byte[innerPayload.Length - 2];
            Buffer.BlockCopy(innerPayload, 2, trimmed, 0, trimmed.Length);
        }
        else
        {
            trimmed = new byte[innerPayload.Length];
            Buffer.BlockCopy(innerPayload, 0, trimmed, 0, trimmed.Length);
        }

        if (trimmed.Length < 3)
        {
            throw new ArgumentException("inner payload must contain LL, LH, and CMD bytes", nameof(innerPayload));
        }

        var innerLength = trimmed[0] | (trimmed[1] << 8);
        if (innerLength + 2 != trimmed.Length)
        {
            throw new ArgumentException(
                $"inner payload length mismatch: len={trimmed.Length} vs expected {innerLength + 2}",
                nameof(innerPayload));
        }

        return trimmed;
    }

    private static byte[] FrameToInnerPayload(byte[] frame)
    {
        if (frame.Length < 5 || frame[0] != FtCommand || frame[1] != 0x00)
        {
            throw new ArgumentException("relay frame must be a normal command request", nameof(frame));
        }

        var payload = new byte[frame.Length - 2];
        Buffer.BlockCopy(frame, 2, payload, 0, payload.Length);
        return payload;
    }
}
