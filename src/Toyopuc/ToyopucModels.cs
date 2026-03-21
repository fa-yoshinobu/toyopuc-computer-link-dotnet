using System.Globalization;

namespace PlcComm.Toyopuc;

public sealed record ResponseFrame(byte Ft, byte Rc, byte Cmd, byte[] Data);

public sealed record TransportTraceFrame(byte[] Tx, byte[]? Rx);

public enum ToyopucTransportMode { Tcp, Udp }

public enum ToyopucTraceDirection { Send, Receive }

public sealed record ToyopucTraceFrame(
    ToyopucTraceDirection Direction,
    byte[] Data,
    DateTime Timestamp);

public sealed record ClockData(
    int Second,
    int Minute,
    int Hour,
    int Day,
    int Month,
    int Year2Digit,
    int Weekday)
{
    public DateTime AsDateTime(int yearBase = 2000)
    {
        return new DateTime(yearBase + Year2Digit, Month, Day, Hour, Minute, Second);
    }
}

public sealed class CpuStatusData
{
    public CpuStatusData(
        byte data1,
        byte data2,
        byte data3,
        byte data4,
        byte data5,
        byte data6,
        byte data7,
        byte data8)
    {
        Data1 = data1;
        Data2 = data2;
        Data3 = data3;
        Data4 = data4;
        Data5 = data5;
        Data6 = data6;
        Data7 = data7;
        Data8 = data8;
    }

    public byte Data1 { get; }
    public byte Data2 { get; }
    public byte Data3 { get; }
    public byte Data4 { get; }
    public byte Data5 { get; }
    public byte Data6 { get; }
    public byte Data7 { get; }
    public byte Data8 { get; }

    public byte[] RawBytes => new[] { Data1, Data2, Data3, Data4, Data5, Data6, Data7, Data8 };

    public string RawBytesHex => string.Join(" ", RawBytes.Select(static b => b.ToString("X2", CultureInfo.InvariantCulture)));

    public string RawHex()
    {
        return RawBytesHex;
    }

    public bool Run => (Data1 & 0x80) != 0;
    public bool UnderStop => (Data1 & 0x40) != 0;
    public bool UnderStopRequestContinuity => (Data1 & 0x20) != 0;
    public bool UnderPseudoStop => (Data1 & 0x10) != 0;
    public bool DebugMode => (Data1 & 0x08) != 0;
    public bool IoMonitorUserMode => (Data1 & 0x04) != 0;
    public bool Pc3Mode => (Data1 & 0x02) != 0;
    public bool Pc10Mode => (Data1 & 0x01) != 0;

    public bool FatalFailure => (Data2 & 0x80) != 0;
    public bool FaintFailure => (Data2 & 0x40) != 0;
    public bool Alarm => (Data2 & 0x20) != 0;
    public bool IoAllocationParameterAltered => (Data2 & 0x08) != 0;
    public bool WithMemoryCard => (Data2 & 0x04) != 0;

    public bool MemoryCardOperation => (Data3 & 0x80) != 0;
    public bool WriteProtectedProgramInfo => (Data3 & 0x40) != 0;

    public bool ReadProtectedSystemMemory => (Data4 & 0x80) != 0;
    public bool WriteProtectedSystemMemory => (Data4 & 0x40) != 0;
    public bool ReadProtectedSystemIo => (Data4 & 0x20) != 0;
    public bool WriteProtectedSystemIo => (Data4 & 0x10) != 0;

    public bool Trace => (Data5 & 0x80) != 0;
    public bool ScanSamplingTrace => (Data5 & 0x40) != 0;
    public bool PeriodicSamplingTrace => (Data5 & 0x20) != 0;
    public bool EnableDetected => (Data5 & 0x10) != 0;
    public bool TriggerDetected => (Data5 & 0x08) != 0;
    public bool OneScanStep => (Data5 & 0x04) != 0;
    public bool OneBlockStep => (Data5 & 0x02) != 0;
    public bool OneInstructionStep => (Data5 & 0x01) != 0;

    public bool IoOffline => (Data6 & 0x80) != 0;
    public bool RemoteRunSetting => (Data6 & 0x40) != 0;
    public bool StatusLatchSetting => (Data6 & 0x20) != 0;

    public bool WritePriorityLimitedProgramInfo => (Data7 & 0x40) != 0;
    public bool AbnormalWriteFlashRegister => (Data7 & 0x20) != 0;
    public bool UnderWritingFlashRegister => (Data7 & 0x10) != 0;
    public bool AbnormalWriteEquipmentInfo => (Data7 & 0x08) != 0;
    public bool AbnormalWritingEquipmentInfo => (Data7 & 0x04) != 0;
    public bool AbnormalWriteDuringRun => (Data7 & 0x02) != 0;
    public bool UnderWritingDuringRun => (Data7 & 0x01) != 0;

    public bool Program3Running => (Data8 & 0x08) != 0;
    public bool Program2Running => (Data8 & 0x04) != 0;
    public bool Program1Running => (Data8 & 0x02) != 0;
}

public sealed record ParsedAddress(
    string Area,
    int Index,
    string Unit,
    bool High = false,
    bool Packed = false,
    int? DigitCount = null);

public sealed record ExNoAddress32(
    int ExNo,
    int Address,
    string Unit);

public sealed record ExtNoAddress(
    int No,
    int Address,
    string Unit);

public sealed record ResolvedDevice(
    string Text,
    string Scheme,
    string Unit,
    string Area,
    int Index,
    string? Prefix = null,
    bool High = false,
    bool Packed = false,
    int? BasicAddress = null,
    int? No = null,
    int? Address = null,
    int? BitNo = null,
    int? Address32 = null);

public sealed record RelayLayer(
    int LinkNo,
    int StationNo,
    int Ack,
    byte[] InnerRaw,
    byte[]? Padding = null);
