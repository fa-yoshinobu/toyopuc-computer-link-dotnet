using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace PlcComm.Toyopuc;

public partial class ToyopucDeviceClient : ToyopucClient
{
    private const int DeviceCacheMaxEntries = 512;
    private const int RunPlanCacheMaxEntries = 256;

    private sealed class ReadOnlyListSlice<T>(IReadOnlyList<T> source, int offset, int count) : IReadOnlyList<T>
    {
        public int Count { get; } = count;

        public T this[int index] => source[offset + index];

        public IEnumerator<T> GetEnumerator()
        {
            for (var i = 0; i < Count; i++)
            {
                yield return source[offset + i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    private static readonly IReadOnlyDictionary<string, int> ProgramPrefixExNo = new Dictionary<string, int>
    {
        ["P1"] = 0x0D,
        ["P2"] = 0x0E,
        ["P3"] = 0x0F,
    };

    public ToyopucAddressingOptions AddressingOptions { get; }
    public string? DeviceProfile { get; }
    private readonly ConcurrentDictionary<string, ResolvedDevice> _resolvedDeviceCache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int[]> _runPlanCache = new(StringComparer.Ordinal);

    public ToyopucDeviceClient(
        string host,
        int port,
        int localPort = 0,
        ToyopucTransportMode transport = ToyopucTransportMode.Tcp,
        TimeSpan timeout = default,
        int retries = 0,
        TimeSpan retryDelay = default,
        int recvBufsize = 8192,
        ToyopucAddressingOptions? addressingOptions = null,
        string? deviceProfile = null)
        : base(host, port, localPort, transport, timeout, retries, retryDelay, recvBufsize)
    {
        DeviceProfile = string.IsNullOrWhiteSpace(deviceProfile)
            ? null
            : ToyopucDeviceProfiles.NormalizeName(deviceProfile);
        AddressingOptions = addressingOptions
            ?? (DeviceProfile is null
                ? ToyopucAddressingOptions.Default
                : ToyopucAddressingOptions.FromProfile(DeviceProfile));
    }

    public ResolvedDevice ResolveDevice(string device)
    {
        var key = NormalizeDeviceCacheKey(device);
        if (_resolvedDeviceCache.Count >= DeviceCacheMaxEntries)
        {
            _resolvedDeviceCache.Clear();
        }

        return _resolvedDeviceCache.GetOrAdd(
            key,
            static (cacheKey, state) =>
                ToyopucDeviceResolver.ResolveDevice(cacheKey, state.AddressingOptions, state.DeviceProfile),
            (AddressingOptions, DeviceProfile));
    }

    public object RelayRead(object hops, object device, int count = 1)
    {
        var resolved = ResolveDeviceObject(device);
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "count must be >= 1");
        }

        if (count == 1)
        {
            return RelayReadOne(hops, resolved);
        }

        return RelayReadRuns(hops, ResolveSequentialDevices(resolved, count), splitPc10BlockBoundaries: true);
    }

    public void RelayWrite(object hops, object device, object value)
    {
        var resolved = ResolveDeviceObject(device);
        if (resolved.Unit == "bit")
        {
            if (TryEnumerateSequence(value, out var bitValues))
            {
                RelayWriteRuns(hops, ResolveSequentialWriteItems(resolved, MaterializeSequence(bitValues)), splitPc10BlockBoundaries: true);
                return;
            }

            RelayWriteOne(hops, resolved, value);
            return;
        }

        if (value is byte[] bytes)
        {
            RelayWriteRuns(hops, ResolveSequentialWriteItems(resolved, BoxBytes(bytes)), splitPc10BlockBoundaries: true);
            return;
        }

        if (TryEnumerateSequence(value, out var values))
        {
            RelayWriteRuns(hops, ResolveSequentialWriteItems(resolved, MaterializeSequence(values)), splitPc10BlockBoundaries: true);
            return;
        }

        RelayWriteOne(hops, resolved, value);
    }

    public object RelayReadWords(object hops, object device, int count = 1)
    {
        var resolved = ResolveDeviceObject(device);
        if (resolved.Unit != "word")
        {
            throw new ArgumentException("RelayReadWords() requires a word device", nameof(device));
        }

        return RelayRead(hops, resolved, count);
    }

    public void RelayWriteWords(object hops, object device, object value)
    {
        var resolved = ResolveDeviceObject(device);
        if (resolved.Unit != "word")
        {
            throw new ArgumentException("RelayWriteWords() requires a word device", nameof(device));
        }

        RelayWrite(hops, resolved, value);
    }

    public object[] RelayReadMany(object hops, IEnumerable<object> devices)
    {
        return RelayReadRuns(hops, ResolveDevices(devices), splitPc10BlockBoundaries: false);
    }

    public void RelayWriteMany(object hops, IEnumerable<KeyValuePair<object, object>> items)
    {
        RelayWriteRuns(hops, ResolveWriteItems(items), splitPc10BlockBoundaries: true);
    }

    public object ReadFr(object device, int count = 1)
    {
        var resolved = ResolveDeviceObject(device);
        if (resolved.Area != "FR" || resolved.Unit != "word")
        {
            throw new ArgumentException("ReadFr() requires an FR word device such as FR000000", nameof(device));
        }

        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "count must be >= 1");
        }

        var values = resolved.Scheme switch
        {
            "pc10-word" => ReadFrWords(resolved.Index, count),
            "ext-word" => ReadFrWordsViaExt(resolved.Index, count),
            _ => throw new ArgumentException($"Unsupported FR scheme: {resolved.Scheme}", nameof(device)),
        };
        return count == 1 ? values[0] : BoxWords(values);
    }

    public object RelayReadFr(object hops, object device, int count = 1)
    {
        var resolved = ResolveDeviceObject(device);
        if (resolved.Area != "FR" || resolved.Unit != "word")
        {
            throw new ArgumentException("RelayReadFr() requires an FR word device such as FR000000", nameof(device));
        }

        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "count must be >= 1");
        }

        var values = resolved.Scheme switch
        {
            "pc10-word" => RelayReadFrWordsPc10(hops, resolved.Index, count),
            "ext-word" => RelayReadFrWordsViaExt(hops, resolved.Index, count),
            _ => throw new ArgumentException($"Unsupported FR scheme: {resolved.Scheme}", nameof(device)),
        };
        return count == 1 ? values[0] : BoxWords(values);
    }

    public void WriteFr(
        object device,
        object value,
        bool commit = false,
        bool? wait = null,
        double timeout = 30.0,
        double pollInterval = 0.2)
    {
        var resolved = ResolveDeviceObject(device);
        if (resolved.Area != "FR" || resolved.Unit != "word")
        {
            throw new ArgumentException("WriteFr() requires an FR word device such as FR000000", nameof(device));
        }

        var values = NormalizeWordValues(value);
        var shouldWait = wait ?? commit;
        switch (resolved.Scheme)
        {
            case "pc10-word":
                WriteFrWordsEx(resolved.Index, values, commit, shouldWait, timeout, pollInterval);
                return;
            case "ext-word":
                WriteFrWordsViaExt(resolved.Index, values);
                if (commit)
                {
                    CommitFrRange(resolved.Index, values.Length, shouldWait, timeout, pollInterval);
                }

                return;
            default:
                throw new ArgumentException($"Unsupported FR scheme: {resolved.Scheme}", nameof(device));
        }
    }

    public void RelayWriteFr(
        object hops,
        object device,
        object value,
        bool commit = false,
        bool? wait = null,
        double timeout = 30.0,
        double pollInterval = 0.2)
    {
        var resolved = ResolveDeviceObject(device);
        if (resolved.Area != "FR" || resolved.Unit != "word")
        {
            throw new ArgumentException("RelayWriteFr() requires an FR word device such as FR000000", nameof(device));
        }

        var values = NormalizeWordValues(value);
        var shouldWait = wait ?? commit;
        switch (resolved.Scheme)
        {
            case "pc10-word":
                RelayWriteFrWordsEx(hops, resolved.Index, values, commit, shouldWait, timeout, pollInterval);
                return;
            case "ext-word":
                RelayWriteFrWordsViaExt(hops, resolved.Index, values);
                if (commit)
                {
                    RelayCommitFrRange(hops, resolved.Index, values.Length, shouldWait, timeout, pollInterval);
                }

                return;
            default:
                throw new ArgumentException($"Unsupported FR scheme: {resolved.Scheme}", nameof(device));
        }
    }

    public void CommitFr(object device, int count = 1, bool wait = false, double timeout = 30.0, double pollInterval = 0.2)
    {
        var resolved = ResolveDeviceObject(device);
        if (resolved.Area != "FR" || resolved.Unit != "word")
        {
            throw new ArgumentException("CommitFr() requires an FR word device such as FR000000", nameof(device));
        }

        CommitFrRange(resolved.Index, count, wait, timeout, pollInterval);
    }

    public void RelayCommitFr(object hops, object device, int count = 1, bool wait = false, double timeout = 30.0, double pollInterval = 0.2)
    {
        var resolved = ResolveDeviceObject(device);
        if (resolved.Area != "FR" || resolved.Unit != "word")
        {
            throw new ArgumentException("RelayCommitFr() requires an FR word device such as FR000000", nameof(device));
        }

        RelayCommitFrRange(hops, resolved.Index, count, wait, timeout, pollInterval);
    }

    public object Read(object device, int count = 1)
    {
        var resolved = ResolveDeviceObject(device);
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "count must be >= 1");
        }

        if (count == 1)
        {
            return ReadOne(resolved);
        }

        return ReadRuns(ResolveSequentialDevices(resolved, count), splitPc10BlockBoundaries: true);
    }

    public void Write(object device, object value)
    {
        var resolved = ResolveDeviceObject(device);
        if (resolved.Area == "FR")
        {
            RaiseGenericFrWriteError();
        }

        if (resolved.Unit == "bit")
        {
            if (TryEnumerateSequence(value, out var bitValues))
            {
                WriteRuns(ResolveSequentialWriteItems(resolved, MaterializeSequence(bitValues)), splitPc10BlockBoundaries: true);
                return;
            }

            WriteOne(resolved, value);
            return;
        }

        if (value is byte[] bytes)
        {
            WriteRuns(ResolveSequentialWriteItems(resolved, BoxBytes(bytes)), splitPc10BlockBoundaries: true);
            return;
        }

        if (TryEnumerateSequence(value, out var values))
        {
            WriteRuns(ResolveSequentialWriteItems(resolved, MaterializeSequence(values)), splitPc10BlockBoundaries: true);
            return;
        }

        WriteOne(resolved, value);
    }

    public object[] ReadMany(IEnumerable<object> devices)
    {
        return ReadRuns(ResolveDevices(devices), splitPc10BlockBoundaries: false);
    }

    public void WriteMany(IEnumerable<KeyValuePair<object, object>> items)
    {
        WriteRuns(ResolveWriteItems(items), splitPc10BlockBoundaries: true);
    }

    private ResolvedDevice ResolveDeviceObject(object device)
    {
        return device switch
        {
            string text => ResolveDevice(text),
            ResolvedDevice resolved => resolved,
            _ => throw new ArgumentException("device must be a string address or ResolvedDevice", nameof(device)),
        };
    }

    private ResolvedDevice[] ResolveDevices(IEnumerable<object> devices)
    {
        if (devices is ICollection<object> collection)
        {
            var resolved = new ResolvedDevice[collection.Count];
            var index = 0;
            foreach (var device in collection)
            {
                resolved[index++] = ResolveDeviceObject(device);
            }

            return resolved;
        }

        var list = new List<ResolvedDevice>();
        foreach (var device in devices)
        {
            list.Add(ResolveDeviceObject(device));
        }

        return list.ToArray();
    }

    private (ResolvedDevice Device, object Value)[] ResolveWriteItems(IEnumerable<KeyValuePair<object, object>> items)
    {
        if (items is ICollection<KeyValuePair<object, object>> collection)
        {
            var resolved = new (ResolvedDevice Device, object Value)[collection.Count];
            var index = 0;
            foreach (var item in collection)
            {
                resolved[index++] = (ResolveDeviceObject(item.Key), item.Value);
            }

            return resolved;
        }

        var list = new List<(ResolvedDevice Device, object Value)>();
        foreach (var item in items)
        {
            list.Add((ResolveDeviceObject(item.Key), item.Value));
        }

        return list.ToArray();
    }

    private static bool TryEnumerateSequence(object value, out IEnumerable sequence)
    {
        if (value is string)
        {
            sequence = Array.Empty<object>();
            return false;
        }

        if (value is byte[])
        {
            sequence = Array.Empty<object>();
            return false;
        }

        if (value is IEnumerable enumerable)
        {
            sequence = enumerable;
            return true;
        }

        sequence = Array.Empty<object>();
        return false;
    }

    private static int[] NormalizeWordValues(object value)
    {
        if (TryEnumerateSequence(value, out var sequence))
        {
            var list = new List<int>();
            foreach (var item in sequence)
            {
                list.Add(ToInt32Invariant(item));
            }

            return list.ToArray();
        }

        return new[] { ToInt32Invariant(value) };
    }

    private static object[] MaterializeSequence(IEnumerable sequence)
    {
        if (sequence is ICollection collection)
        {
            var values = new object[collection.Count];
            var index = 0;
            foreach (var item in sequence)
            {
                values[index++] = item!;
            }

            return values;
        }

        var list = new List<object>();
        foreach (var item in sequence)
        {
            list.Add(item!);
        }

        return list.ToArray();
    }

    private static int Require(int? value, string label)
    {
        return value ?? throw new ArgumentException($"Resolved device missing {label}");
    }

    private static int ToInt32Invariant(object value)
    {
        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static bool ToBooleanInvariant(object value)
    {
        return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
    }

    private static void WriteU16LittleEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    private static void WriteAddress32LittleEndian(byte[] buffer, int offset, int address32)
    {
        WriteU16LittleEndian(buffer, offset, address32 & 0xFFFF);
        WriteU16LittleEndian(buffer, offset + 2, (address32 >> 16) & 0xFFFF);
    }

    private static byte[] PackWordValues(IEnumerable<int> values)
    {
        var items = values as int[] ?? values.ToArray();
        var data = new byte[items.Length * 2];
        for (var i = 0; i < items.Length; i++)
        {
            WriteU16LittleEndian(data, i * 2, items[i] & 0xFFFF);
        }

        return data;
    }

    private static byte[] BuildPc10MultiWordReadPayload(IEnumerable<int> addresses32)
    {
        var items = addresses32.ToArray();
        var payload = new byte[4 + (items.Length * 4)];
        payload[2] = (byte)(items.Length & 0xFF);
        for (var i = 0; i < items.Length; i++)
        {
            WriteAddress32LittleEndian(payload, 4 + (i * 4), items[i]);
        }

        return payload;
    }

    private static byte[] PackPc10MultiWordPayload(IEnumerable<(int Address32, int Value)> addressValues)
    {
        var items = addressValues.ToArray();
        var payload = new byte[4 + (items.Length * 4) + (items.Length * 2)];
        payload[2] = (byte)(items.Length & 0xFF);
        for (var i = 0; i < items.Length; i++)
        {
            WriteAddress32LittleEndian(payload, 4 + (i * 4), items[i].Address32);
        }

        var valuesOffset = 4 + (items.Length * 4);
        for (var i = 0; i < items.Length; i++)
        {
            WriteU16LittleEndian(payload, valuesOffset + (i * 2), items[i].Value);
        }

        return payload;
    }

    private static int[] ReadPc10MultiWords(ToyopucClient client, IEnumerable<int> addresses32)
    {
        var items = addresses32.ToArray();
        return ParsePc10MultiWordData(client.Pc10MultiRead(BuildPc10MultiWordReadPayload(items)), items.Length);
    }

    private static int ReadPc10BlockWord(ToyopucClient client, int address32)
    {
        var data = client.Pc10BlockRead(address32, 2);
        if (data.Length < 2)
        {
            throw new ToyopucProtocolError("PC10 word-read response too short");
        }

        return data[0] | (data[1] << 8);
    }

    private static void WritePc10BlockWord(ToyopucClient client, int address32, int value)
    {
        client.Pc10BlockWrite(address32, new[] { (byte)(value & 0xFF), (byte)((value >> 8) & 0xFF) });
    }

    private static byte[] PackPc10MultiBitPayload(IEnumerable<(int Address32, int Value)> addressValues)
    {
        var items = addressValues.ToArray();
        var bitBytesOffset = 4 + (items.Length * 4);
        var payload = new byte[bitBytesOffset + ((items.Length + 7) / 8)];
        payload[0] = (byte)(items.Length & 0xFF);
        for (var i = 0; i < items.Length; i++)
        {
            WriteAddress32LittleEndian(payload, 4 + (i * 4), items[i].Address32);
            if ((items[i].Value & 0x01) != 0)
            {
                payload[bitBytesOffset + (i / 8)] |= (byte)(1 << (i % 8));
            }
        }

        return payload;
    }

    private static byte[] BuildPc10MultiBitReadPayload(IEnumerable<int> addresses32)
    {
        var items = addresses32.ToArray();
        var payload = new byte[4 + (items.Length * 4)];
        payload[0] = (byte)(items.Length & 0xFF);
        for (var i = 0; i < items.Length; i++)
        {
            WriteAddress32LittleEndian(payload, 4 + (i * 4), items[i]);
        }

        return payload;
    }

    private static int[] ReadPc10MultiBits(ToyopucClient client, IEnumerable<int> addresses32)
    {
        var items = addresses32.ToArray();
        return ParsePc10MultiBitData(client.Pc10MultiRead(BuildPc10MultiBitReadPayload(items)), items.Length);
    }

    private object[] ReadRuns(IReadOnlyList<ResolvedDevice> devices, bool splitPc10BlockBoundaries)
    {
        var results = new object[devices.Count];
        var index = 0;
        foreach (var runLength in GetRunPlan(devices, splitPc10BlockBoundaries))
        {
            var batchResults = ReadBatch(new ReadOnlyListSlice<ResolvedDevice>(devices, index, runLength));
            Array.Copy(batchResults, 0, results, index, runLength);
            index += runLength;
        }

        return results;
    }

    private object[] RelayReadRuns(object hops, IReadOnlyList<ResolvedDevice> devices, bool splitPc10BlockBoundaries)
    {
        var results = new object[devices.Count];
        var index = 0;
        foreach (var runLength in GetRunPlan(devices, splitPc10BlockBoundaries))
        {
            var batchResults = RelayReadBatch(hops, new ReadOnlyListSlice<ResolvedDevice>(devices, index, runLength));
            Array.Copy(batchResults, 0, results, index, runLength);
            index += runLength;
        }

        return results;
    }

    private void WriteRuns(IReadOnlyList<(ResolvedDevice Device, object Value)> items, bool splitPc10BlockBoundaries)
    {
        var index = 0;
        foreach (var runLength in GetRunPlan(items, splitPc10BlockBoundaries))
        {
            WriteBatch(new ReadOnlyListSlice<(ResolvedDevice Device, object Value)>(items, index, runLength));
            index += runLength;
        }
    }

    private void RelayWriteRuns(object hops, IReadOnlyList<(ResolvedDevice Device, object Value)> items, bool splitPc10BlockBoundaries)
    {
        var index = 0;
        foreach (var runLength in GetRunPlan(items, splitPc10BlockBoundaries))
        {
            RelayWriteBatch(hops, new ReadOnlyListSlice<(ResolvedDevice Device, object Value)>(items, index, runLength));
            index += runLength;
        }
    }

    private IReadOnlyList<ResolvedDevice> ResolveSequentialDevices(ResolvedDevice resolved, int count)
    {
        var devices = new ResolvedDevice[count];
        devices[0] = resolved;
        for (var i = 1; i < count; i++)
        {
            devices[i] = Offset(devices[i - 1], 1);
        }

        return devices;
    }

    private IReadOnlyList<(ResolvedDevice Device, object Value)> ResolveSequentialWriteItems(ResolvedDevice resolved, IReadOnlyList<object> values)
    {
        var items = new (ResolvedDevice Device, object Value)[values.Count];
        if (values.Count == 0)
        {
            return items;
        }

        items[0] = (resolved, values[0]);
        for (var i = 1; i < values.Count; i++)
        {
            items[i] = (Offset(items[i - 1].Device, 1), values[i]);
        }

        return items;
    }

    private static int GetBatchRunLength(IReadOnlyList<ResolvedDevice> devices, int startIndex, bool splitPc10BlockBoundaries)
    {
        var firstDevice = devices[startIndex];
        var key = GetBatchGroupKey(firstDevice);
        if (key is null)
        {
            return 1;
        }

        var pc10Block = splitPc10BlockBoundaries ? GetPc10BatchBlock(firstDevice) : null;
        var index = startIndex + 1;
        while (index < devices.Count
            && GetBatchGroupKey(devices[index]) == key
            && (!splitPc10BlockBoundaries || GetPc10BatchBlock(devices[index]) == pc10Block))
        {
            index++;
        }

        return index - startIndex;
    }

    private static int GetBatchRunLength(IReadOnlyList<(ResolvedDevice Device, object Value)> items, int startIndex, bool splitPc10BlockBoundaries)
    {
        var firstDevice = items[startIndex].Device;
        var key = GetBatchGroupKey(firstDevice);
        if (key is null)
        {
            return 1;
        }

        var pc10Block = splitPc10BlockBoundaries ? GetPc10BatchBlock(firstDevice) : null;
        var index = startIndex + 1;
        while (index < items.Count
            && GetBatchGroupKey(items[index].Device) == key
            && (!splitPc10BlockBoundaries || GetPc10BatchBlock(items[index].Device) == pc10Block))
        {
            index++;
        }

        return index - startIndex;
    }

    private int[] GetRunPlan(IReadOnlyList<ResolvedDevice> devices, bool splitPc10BlockBoundaries)
    {
        var key = BuildRunPlanKey(devices, splitPc10BlockBoundaries);
        if (_runPlanCache.Count >= RunPlanCacheMaxEntries)
        {
            _runPlanCache.Clear();
        }

        return _runPlanCache.GetOrAdd(
            key,
            static (_, state) => CompileRunPlan(state.Devices, state.SplitPc10BlockBoundaries),
            (Devices: devices, SplitPc10BlockBoundaries: splitPc10BlockBoundaries));
    }

    private int[] GetRunPlan(IReadOnlyList<(ResolvedDevice Device, object Value)> items, bool splitPc10BlockBoundaries)
    {
        var key = BuildRunPlanKey(items, splitPc10BlockBoundaries);
        if (_runPlanCache.Count >= RunPlanCacheMaxEntries)
        {
            _runPlanCache.Clear();
        }

        return _runPlanCache.GetOrAdd(
            key,
            static (_, state) => CompileRunPlan(state.Items, state.SplitPc10BlockBoundaries),
            (Items: items, SplitPc10BlockBoundaries: splitPc10BlockBoundaries));
    }

    private static string NormalizeDeviceCacheKey(string device)
    {
        return device.Trim().ToUpperInvariant();
    }

    private static string BuildRunPlanKey(IReadOnlyList<ResolvedDevice> devices, bool splitPc10BlockBoundaries)
    {
        var builder = new StringBuilder(2 + (devices.Count * 16));
        builder.Append(splitPc10BlockBoundaries ? '1' : '0');
        for (var i = 0; i < devices.Count; i++)
        {
            builder.Append('\u001F');
            builder.Append(devices[i].Text);
        }

        return builder.ToString();
    }

    private static string BuildRunPlanKey(IReadOnlyList<(ResolvedDevice Device, object Value)> items, bool splitPc10BlockBoundaries)
    {
        var builder = new StringBuilder(2 + (items.Count * 16));
        builder.Append(splitPc10BlockBoundaries ? '1' : '0');
        for (var i = 0; i < items.Count; i++)
        {
            builder.Append('\u001F');
            builder.Append(items[i].Device.Text);
        }

        return builder.ToString();
    }

    private static int[] CompileRunPlan(IReadOnlyList<ResolvedDevice> devices, bool splitPc10BlockBoundaries)
    {
        var runLengths = new List<int>();
        var index = 0;
        while (index < devices.Count)
        {
            var runLength = GetBatchRunLength(devices, index, splitPc10BlockBoundaries);
            runLengths.Add(runLength);
            index += runLength;
        }

        return runLengths.ToArray();
    }

    private static int[] CompileRunPlan(IReadOnlyList<(ResolvedDevice Device, object Value)> items, bool splitPc10BlockBoundaries)
    {
        var runLengths = new List<int>();
        var index = 0;
        while (index < items.Count)
        {
            var runLength = GetBatchRunLength(items, index, splitPc10BlockBoundaries);
            runLengths.Add(runLength);
            index += runLength;
        }

        return runLengths.ToArray();
    }

    private static string? GetBatchGroupKey(ResolvedDevice device)
    {
        return device.Scheme switch
        {
            "basic-word" => "basic-word",
            "basic-byte" => "basic-byte",
            "ext-bit" or "program-bit" => "ext-bit",
            "ext-word" or "program-word" => "ext-word",
            "ext-byte" or "program-byte" => "ext-byte",
            "pc10-bit" => "pc10-bit",
            "pc10-word" => "pc10-word",
            "pc10-byte" => "pc10-byte",
            _ => null,
        };
    }

    private static int? GetPc10BatchBlock(ResolvedDevice device)
    {
        return device.Scheme switch
        {
            "pc10-bit" or "pc10-word" or "pc10-byte" => Require(device.Address32, "pc10 addr32") >> 16,
            _ => null,
        };
    }

    private object[] ReadBatch(IReadOnlyList<ResolvedDevice> devices)
    {
        if (devices.Count == 0)
        {
            return Array.Empty<object>();
        }

        var group = GetBatchGroupKey(devices[0]);
        if (group is null || !AllDevicesInGroup(devices, group))
        {
            return ReadIndividually(devices);
        }

        return group switch
        {
            "basic-word" => ReadBasicWordBatch(devices),
            "basic-byte" => BoxBytes(ReadBytesMulti(CollectBasicAddresses(devices))),
            "ext-word" => ReadExtWordBatch(devices),
            "ext-byte" => ReadExtByteBatch(devices),
            "ext-bit" => ReadExtBitBatch(devices),
            "pc10-word" => ReadPc10WordBatch(devices),
            "pc10-bit" => BoxBooleanBits(ReadPc10MultiBits(this, CollectAddress32Values(devices))),
            "pc10-byte" => ReadPc10ByteBatch(devices),
            _ => ReadIndividually(devices),
        };
    }

    private object[] RelayReadBatch(object hops, IReadOnlyList<ResolvedDevice> devices)
    {
        if (devices.Count == 0)
        {
            return Array.Empty<object>();
        }

        var group = GetBatchGroupKey(devices[0]);
        if (group is null || !AllDevicesInGroup(devices, group))
        {
            return RelayReadIndividually(hops, devices);
        }

        return group switch
        {
            "basic-word" => RelayReadBasicWordBatch(hops, devices),
            "basic-byte" => RelayReadBasicByteBatch(hops, devices),
            "ext-word" => RelayReadExtWordBatch(hops, devices),
            "ext-byte" => RelayReadExtByteBatch(hops, devices),
            "ext-bit" => RelayReadExtBitBatch(hops, devices),
            "pc10-word" => RelayReadPc10WordBatch(hops, devices),
            "pc10-bit" => RelayReadPc10BitBatch(hops, devices),
            "pc10-byte" => RelayReadPc10ByteBatch(hops, devices),
            _ => RelayReadIndividually(hops, devices),
        };
    }

    private void WriteBatch(IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        var group = GetBatchGroupKey(items[0].Device);
        if (group is null
            || !AllItemsInGroup(items, group)
            || HasDuplicateDevices(items))
        {
            foreach (var item in items)
            {
                WriteOne(item.Device, item.Value);
            }

            return;
        }

        switch (group)
        {
            case "basic-word":
                WriteBasicWordBatch(items);
                return;
            case "basic-byte":
                WriteBytesMulti(CollectBasicAddressValues(items));
                return;
            case "ext-word":
                WriteExtWordBatch(items);
                return;
            case "ext-byte":
                WriteExtByteBatch(items);
                return;
            case "ext-bit":
                WriteExtBitBatch(items);
                return;
            case "pc10-word":
                WritePc10WordBatch(items);
                return;
            case "pc10-bit":
                Pc10MultiWrite(PackPc10MultiBitPayload(CollectAddress32BitValues(items)));
                return;
            case "pc10-byte":
                WritePc10ByteBatch(items);
                return;
            default:
                foreach (var item in items)
                {
                    WriteOne(item.Device, item.Value);
                }

                return;
        }
    }

    private void RelayWriteBatch(object hops, IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        var group = GetBatchGroupKey(items[0].Device);
        if (group is null
            || !AllItemsInGroup(items, group)
            || HasDuplicateDevices(items))
        {
            foreach (var item in items)
            {
                RelayWriteOne(hops, item.Device, item.Value);
            }

            return;
        }

        switch (group)
        {
            case "basic-word":
                RelayWriteBasicWordBatch(hops, items);
                return;
            case "basic-byte":
                {
                    var response = SendViaRelay(
                        hops,
                        ToyopucProtocol.BuildMultiByteWrite(CollectBasicAddressValues(items)));
                    EnsureCommand(response, 0x25, "Unexpected CMD in relay multi-byte-write response");
                    return;
                }
            case "ext-word":
                RelayWriteExtWordBatch(hops, items);
                return;
            case "ext-byte":
                RelayWriteExtByteBatch(hops, items);
                return;
            case "ext-bit":
                RelayWriteExtBitBatch(hops, items);
                return;
            case "pc10-word":
                RelayWritePc10WordBatch(hops, items);
                return;
            case "pc10-bit":
                {
                    var response = SendViaRelay(
                        hops,
                        ToyopucProtocol.BuildPc10MultiWrite(PackPc10MultiBitPayload(CollectAddress32BitValues(items))));
                    EnsureCommand(response, 0xC5, "Unexpected CMD in relay PC10 multi-write response");
                    return;
                }
            case "pc10-byte":
                RelayWritePc10ByteBatch(hops, items);
                return;
            default:
                foreach (var item in items)
                {
                    RelayWriteOne(hops, item.Device, item.Value);
                }

                return;
        }
    }

    private object[] ReadBasicWordBatch(IReadOnlyList<ResolvedDevice> devices)
    {
        if (TryGetConsecutiveStart(devices, static device => device.BasicAddress, 1, out var startAddress))
        {
            return BoxWords(ReadWords(startAddress, devices.Count));
        }

        return BoxWords(ReadWordsMulti(CollectBasicAddresses(devices)));
    }

    private object[] RelayReadBasicWordBatch(object hops, IReadOnlyList<ResolvedDevice> devices)
    {
        if (TryGetConsecutiveStart(devices, static device => device.BasicAddress, 1, out var startAddress))
        {
            var response = SendViaRelay(hops, ToyopucProtocol.BuildWordRead(startAddress, devices.Count));
            EnsureCommand(response, 0x1C, "Unexpected CMD in relay word-read response");
            return BoxWords(ToyopucProtocol.UnpackU16LittleEndian(response.Data));
        }

        var multiResponse = SendViaRelay(hops, ToyopucProtocol.BuildMultiWordRead(CollectBasicAddresses(devices)));
        EnsureCommand(multiResponse, 0x22, "Unexpected CMD in relay multi-word-read response");
        return BoxWords(ToyopucProtocol.UnpackU16LittleEndian(multiResponse.Data));
    }

    private object[] RelayReadBasicByteBatch(object hops, IReadOnlyList<ResolvedDevice> devices)
    {
        var response = SendViaRelay(hops, ToyopucProtocol.BuildMultiByteRead(CollectBasicAddresses(devices)));
        EnsureCommand(response, 0x24, "Unexpected CMD in relay multi-byte-read response");
        return BoxBytes(response.Data);
    }

    private object[] ReadExtWordBatch(IReadOnlyList<ResolvedDevice> devices)
    {
        if (TryGetUniformNumber(devices, out var number)
            && TryGetConsecutiveStart(devices, static device => device.Address, 1, out var startAddress))
        {
            return BoxWords(ReadExtWords(number, startAddress, devices.Count));
        }

        var data = ReadExtMulti(
            Array.Empty<(int No, int Bit, int Address)>(),
            Array.Empty<(int No, int Address)>(),
            CollectNoAddresses(devices));
        return BoxWords(ToyopucProtocol.UnpackU16LittleEndian(data));
    }

    private object[] RelayReadExtWordBatch(object hops, IReadOnlyList<ResolvedDevice> devices)
    {
        if (TryGetUniformNumber(devices, out var number)
            && TryGetConsecutiveStart(devices, static device => device.Address, 1, out var startAddress))
        {
            var response = SendViaRelay(hops, ToyopucProtocol.BuildExtWordRead(number, startAddress, devices.Count));
            EnsureCommand(response, 0x94, "Unexpected CMD in relay ext word-read response");
            return BoxWords(ToyopucProtocol.UnpackU16LittleEndian(response.Data));
        }

        var responseMulti = SendViaRelay(
            hops,
            ToyopucProtocol.BuildExtMultiRead(
                Array.Empty<(int No, int Bit, int Address)>(),
                Array.Empty<(int No, int Address)>(),
                CollectNoAddresses(devices)));
        EnsureCommand(responseMulti, 0x98, "Unexpected CMD in relay ext multi-read response");
        return BoxWords(ToyopucProtocol.UnpackU16LittleEndian(responseMulti.Data));
    }

    private object[] ReadExtByteBatch(IReadOnlyList<ResolvedDevice> devices)
    {
        if (TryGetUniformNumber(devices, out var number)
            && TryGetConsecutiveStart(devices, static device => device.Address, 1, out var startAddress))
        {
            return BoxBytes(ReadExtBytes(number, startAddress, devices.Count));
        }

        var data = ReadExtMulti(
            Array.Empty<(int No, int Bit, int Address)>(),
            CollectNoAddresses(devices),
            Array.Empty<(int No, int Address)>());
        return BoxBytes(data);
    }

    private object[] RelayReadExtByteBatch(object hops, IReadOnlyList<ResolvedDevice> devices)
    {
        if (TryGetUniformNumber(devices, out var number)
            && TryGetConsecutiveStart(devices, static device => device.Address, 1, out var startAddress))
        {
            var response = SendViaRelay(hops, ToyopucProtocol.BuildExtByteRead(number, startAddress, devices.Count));
            EnsureCommand(response, 0x96, "Unexpected CMD in relay ext byte-read response");
            return BoxBytes(response.Data);
        }

        var responseMulti = SendViaRelay(
            hops,
            ToyopucProtocol.BuildExtMultiRead(
                Array.Empty<(int No, int Bit, int Address)>(),
                CollectNoAddresses(devices),
                Array.Empty<(int No, int Address)>()));
        EnsureCommand(responseMulti, 0x98, "Unexpected CMD in relay ext multi-read response");
        return BoxBytes(responseMulti.Data);
    }

    private object[] ReadExtBitBatch(IReadOnlyList<ResolvedDevice> devices)
    {
        var data = ReadExtMulti(
            CollectNoBitAddresses(devices),
            Array.Empty<(int No, int Address)>(),
            Array.Empty<(int No, int Address)>());
        return BoxBooleanBits(ParseExtMultiBitData(data, devices.Count));
    }

    private object[] RelayReadExtBitBatch(object hops, IReadOnlyList<ResolvedDevice> devices)
    {
        var response = SendViaRelay(
            hops,
            ToyopucProtocol.BuildExtMultiRead(
                CollectNoBitAddresses(devices),
                Array.Empty<(int No, int Address)>(),
                Array.Empty<(int No, int Address)>()));
        EnsureCommand(response, 0x98, "Unexpected CMD in relay ext multi-read response");
        return BoxBooleanBits(ParseExtMultiBitData(response.Data, devices.Count));
    }

    private object[] ReadPc10WordBatch(IReadOnlyList<ResolvedDevice> devices)
    {
        if (TryGetConsecutivePc10BlockStart(devices, 2, out var startAddress))
        {
            return BoxWords(ToyopucProtocol.UnpackU16LittleEndian(Pc10BlockRead(startAddress, devices.Count * 2)));
        }

        if (ContainsPackedWordDevice(devices))
        {
            return ReadPc10WordBatchBySegments(this, devices);
        }

        return BoxWords(ReadPc10MultiWords(this, CollectAddress32Values(devices)));
    }

    private object[] RelayReadPc10WordBatch(object hops, IReadOnlyList<ResolvedDevice> devices)
    {
        if (TryGetConsecutivePc10BlockStart(devices, 2, out var startAddress))
        {
            var response = SendViaRelay(hops, ToyopucProtocol.BuildPc10BlockRead(startAddress, devices.Count * 2));
            EnsureCommand(response, 0xC2, "Unexpected CMD in relay PC10 block-read response");
            return BoxWords(ToyopucProtocol.UnpackU16LittleEndian(response.Data));
        }

        if (ContainsPackedWordDevice(devices))
        {
            return RelayReadPc10WordBatchBySegments(hops, devices);
        }

        var responseMulti = SendViaRelay(hops, ToyopucProtocol.BuildPc10MultiRead(BuildPc10MultiWordReadPayload(CollectAddress32Values(devices))));
        EnsureCommand(responseMulti, 0xC4, "Unexpected CMD in relay PC10 multi-read response");
        return BoxWords(ParsePc10MultiWordData(responseMulti.Data, devices.Count));
    }

    private static object[] ReadPc10WordBatchBySegments(ToyopucClient client, IReadOnlyList<ResolvedDevice> devices)
    {
        var results = new object[devices.Count];
        var segmentStart = 0;
        while (segmentStart < devices.Count)
        {
            var segmentLength = GetConsecutivePc10WordSegmentLength(devices, segmentStart);
            var startAddress = Require(devices[segmentStart].Address32, "pc10 addr32");
            var words = ToyopucProtocol.UnpackU16LittleEndian(client.Pc10BlockRead(startAddress, segmentLength * 2));
            if (words.Length < segmentLength)
            {
                throw new ToyopucProtocolError("PC10 block-read response too short");
            }

            for (var i = 0; i < segmentLength; i++)
            {
                results[segmentStart + i] = words[i];
            }

            segmentStart += segmentLength;
        }

        return results;
    }

    private object[] RelayReadPc10WordBatchBySegments(object hops, IReadOnlyList<ResolvedDevice> devices)
    {
        var results = new object[devices.Count];
        var segmentStart = 0;
        while (segmentStart < devices.Count)
        {
            var segmentLength = GetConsecutivePc10WordSegmentLength(devices, segmentStart);
            var startAddress = Require(devices[segmentStart].Address32, "pc10 addr32");
            var response = SendViaRelay(hops, ToyopucProtocol.BuildPc10BlockRead(startAddress, segmentLength * 2));
            EnsureCommand(response, 0xC2, "Unexpected CMD in relay PC10 block-read response");
            var words = ToyopucProtocol.UnpackU16LittleEndian(response.Data);
            if (words.Length < segmentLength)
            {
                throw new ToyopucProtocolError("Relay PC10 block-read response too short");
            }

            for (var i = 0; i < segmentLength; i++)
            {
                results[segmentStart + i] = words[i];
            }

            segmentStart += segmentLength;
        }

        return results;
    }

    private object[] RelayReadPc10BitBatch(object hops, IReadOnlyList<ResolvedDevice> devices)
    {
        var response = SendViaRelay(
            hops,
            ToyopucProtocol.BuildPc10MultiRead(BuildPc10MultiBitReadPayload(CollectAddress32Values(devices))));
        EnsureCommand(response, 0xC4, "Unexpected CMD in relay PC10 multi-read response");
        return BoxBooleanBits(ParsePc10MultiBitData(response.Data, devices.Count));
    }

    private object[] ReadPc10ByteBatch(IReadOnlyList<ResolvedDevice> devices)
    {
        if (TryGetConsecutivePc10BlockStart(devices, 1, out var startAddress))
        {
            return BoxBytes(Pc10BlockRead(startAddress, devices.Count));
        }

        return ReadIndividually(devices);
    }

    private object[] RelayReadPc10ByteBatch(object hops, IReadOnlyList<ResolvedDevice> devices)
    {
        if (TryGetConsecutivePc10BlockStart(devices, 1, out var startAddress))
        {
            var response = SendViaRelay(hops, ToyopucProtocol.BuildPc10BlockRead(startAddress, devices.Count));
            EnsureCommand(response, 0xC2, "Unexpected CMD in relay PC10 block-read response");
            return BoxBytes(response.Data);
        }

        return RelayReadIndividually(hops, devices);
    }

    private void WriteBasicWordBatch(IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var values = CollectIntValues(items);
        if (TryGetConsecutiveStart(items, static item => item.Device.BasicAddress, 1, out var startAddress))
        {
            WriteWords(startAddress, values);
            return;
        }

        WriteWordsMulti(CollectBasicAddressValues(items));
    }

    private void RelayWriteBasicWordBatch(object hops, IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var values = CollectIntValues(items);
        if (TryGetConsecutiveStart(items, static item => item.Device.BasicAddress, 1, out var startAddress))
        {
            var response = SendViaRelay(hops, ToyopucProtocol.BuildWordWrite(startAddress, values));
            EnsureCommand(response, 0x1D, "Unexpected CMD in relay word-write response");
            return;
        }

        var multiResponse = SendViaRelay(hops, ToyopucProtocol.BuildMultiWordWrite(CollectBasicAddressValues(items)));
        EnsureCommand(multiResponse, 0x23, "Unexpected CMD in relay multi-word-write response");
    }

    private void WriteExtWordBatch(IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var values = items.Select(static item => ToInt32Invariant(item.Value)).ToArray();
        if (TryGetUniformNumber(items, out var number)
            && TryGetConsecutiveStart(items, static item => item.Device.Address, 1, out var startAddress))
        {
            WriteExtWords(number, startAddress, values);
            return;
        }

        WriteExtMulti(
            Array.Empty<(int No, int Bit, int Address, int Value)>(),
            Array.Empty<(int No, int Address, int Value)>(),
            CollectNoAddressValues(items));
    }

    private void RelayWriteExtWordBatch(object hops, IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var values = CollectIntValues(items);
        if (TryGetUniformNumber(items, out var number)
            && TryGetConsecutiveStart(items, static item => item.Device.Address, 1, out var startAddress))
        {
            var response = SendViaRelay(hops, ToyopucProtocol.BuildExtWordWrite(number, startAddress, values));
            EnsureCommand(response, 0x95, "Unexpected CMD in relay ext word-write response");
            return;
        }

        var responseMulti = SendViaRelay(
            hops,
            ToyopucProtocol.BuildExtMultiWrite(
                Array.Empty<(int No, int Bit, int Address, int Value)>(),
                Array.Empty<(int No, int Address, int Value)>(),
                CollectNoAddressValues(items)));
        EnsureCommand(responseMulti, 0x99, "Unexpected CMD in relay ext multi-write response");
    }

    private void WriteExtByteBatch(IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var values = items.Select(static item => ToInt32Invariant(item.Value)).ToArray();
        if (TryGetUniformNumber(items, out var number)
            && TryGetConsecutiveStart(items, static item => item.Device.Address, 1, out var startAddress))
        {
            WriteExtBytes(number, startAddress, values);
            return;
        }

        WriteExtMulti(
            Array.Empty<(int No, int Bit, int Address, int Value)>(),
            CollectNoAddressValues(items),
            Array.Empty<(int No, int Address, int Value)>());
    }

    private void RelayWriteExtByteBatch(object hops, IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var values = CollectIntValues(items);
        if (TryGetUniformNumber(items, out var number)
            && TryGetConsecutiveStart(items, static item => item.Device.Address, 1, out var startAddress))
        {
            var response = SendViaRelay(hops, ToyopucProtocol.BuildExtByteWrite(number, startAddress, values));
            EnsureCommand(response, 0x97, "Unexpected CMD in relay ext byte-write response");
            return;
        }

        var responseMulti = SendViaRelay(
            hops,
            ToyopucProtocol.BuildExtMultiWrite(
                Array.Empty<(int No, int Bit, int Address, int Value)>(),
                CollectNoAddressValues(items),
                Array.Empty<(int No, int Address, int Value)>()));
        EnsureCommand(responseMulti, 0x99, "Unexpected CMD in relay ext multi-write response");
    }

    private void WriteExtBitBatch(IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        WriteExtMulti(
            CollectNoBitAddressValues(items),
            Array.Empty<(int No, int Address, int Value)>(),
            Array.Empty<(int No, int Address, int Value)>());
    }

    private void RelayWriteExtBitBatch(object hops, IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var response = SendViaRelay(
            hops,
            ToyopucProtocol.BuildExtMultiWrite(
                CollectNoBitAddressValues(items),
                Array.Empty<(int No, int Address, int Value)>(),
                Array.Empty<(int No, int Address, int Value)>()));
        EnsureCommand(response, 0x99, "Unexpected CMD in relay ext multi-write response");
    }

    private void WritePc10WordBatch(IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var values = CollectIntValues(items);
        if (TryGetConsecutivePc10BlockStart(items, 2, out var startAddress))
        {
            Pc10BlockWrite(startAddress, PackWordValues(values));
            return;
        }

        Pc10MultiWrite(PackPc10MultiWordPayload(CollectAddress32WordValues(items)));
    }

    private void RelayWritePc10WordBatch(object hops, IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var values = CollectIntValues(items);
        if (TryGetConsecutivePc10BlockStart(items, 2, out var startAddress))
        {
            var response = SendViaRelay(hops, ToyopucProtocol.BuildPc10BlockWrite(startAddress, PackWordValues(values)));
            EnsureCommand(response, 0xC3, "Unexpected CMD in relay PC10 block-write response");
            return;
        }

        var responseMulti = SendViaRelay(
            hops,
            ToyopucProtocol.BuildPc10MultiWrite(PackPc10MultiWordPayload(CollectAddress32WordValues(items))));
        EnsureCommand(responseMulti, 0xC5, "Unexpected CMD in relay PC10 multi-write response");
    }

    private void WritePc10ByteBatch(IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        if (TryGetConsecutivePc10BlockStart(items, 1, out var startAddress))
        {
            Pc10BlockWrite(startAddress, CollectByteValues(items));
            return;
        }

        foreach (var item in items)
        {
            WriteOne(item.Device, item.Value);
        }
    }

    private void RelayWritePc10ByteBatch(object hops, IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        if (TryGetConsecutivePc10BlockStart(items, 1, out var startAddress))
        {
            var response = SendViaRelay(
                hops,
                ToyopucProtocol.BuildPc10BlockWrite(startAddress, CollectByteValues(items)));
            EnsureCommand(response, 0xC3, "Unexpected CMD in relay PC10 block-write response");
            return;
        }

        foreach (var item in items)
        {
            RelayWriteOne(hops, item.Device, item.Value);
        }
    }

    private static bool TryGetConsecutiveStart(IReadOnlyList<ResolvedDevice> devices, Func<ResolvedDevice, int?> selector, int step, out int start)
    {
        start = default;
        if (devices.Count == 0)
        {
            return false;
        }

        var first = selector(devices[0]);
        if (first is null)
        {
            return false;
        }

        start = first.Value;
        for (var i = 1; i < devices.Count; i++)
        {
            var current = selector(devices[i]);
            if (current != start + (i * step))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetConsecutiveStart(IReadOnlyList<(ResolvedDevice Device, object Value)> items, Func<(ResolvedDevice Device, object Value), int?> selector, int step, out int start)
    {
        start = default;
        if (items.Count == 0)
        {
            return false;
        }

        var first = selector(items[0]);
        if (first is null)
        {
            return false;
        }

        start = first.Value;
        for (var i = 1; i < items.Count; i++)
        {
            var current = selector(items[i]);
            if (current != start + (i * step))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetConsecutivePc10BlockStart(IReadOnlyList<ResolvedDevice> devices, int step, out int start)
    {
        start = default;
        if (!TryGetConsecutiveStart(devices, static device => device.Address32, step, out start))
        {
            return false;
        }

        var block = Require(devices[0].Address32, "pc10 addr32") >> 16;
        for (var i = 1; i < devices.Count; i++)
        {
            if ((Require(devices[i].Address32, "pc10 addr32") >> 16) != block)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetConsecutivePc10BlockStart(IReadOnlyList<(ResolvedDevice Device, object Value)> items, int step, out int start)
    {
        start = default;
        if (!TryGetConsecutiveStart(items, static item => item.Device.Address32, step, out start))
        {
            return false;
        }

        var block = Require(items[0].Device.Address32, "pc10 addr32") >> 16;
        for (var i = 1; i < items.Count; i++)
        {
            if ((Require(items[i].Device.Address32, "pc10 addr32") >> 16) != block)
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsPackedWordDevice(IReadOnlyList<ResolvedDevice> devices)
    {
        for (var i = 0; i < devices.Count; i++)
        {
            if (devices[i].Scheme == "pc10-word" && devices[i].Unit == "word" && devices[i].Packed)
            {
                return true;
            }
        }

        return false;
    }

    private static int GetConsecutivePc10WordSegmentLength(IReadOnlyList<ResolvedDevice> devices, int startIndex)
    {
        var startAddress = Require(devices[startIndex].Address32, "pc10 addr32");
        var block = startAddress >> 16;
        var length = 1;
        var expectedAddress = startAddress + 2;
        for (var i = startIndex + 1; i < devices.Count; i++)
        {
            var currentAddress = Require(devices[i].Address32, "pc10 addr32");
            if ((currentAddress >> 16) != block || currentAddress != expectedAddress)
            {
                break;
            }

            length++;
            expectedAddress += 2;
        }

        return length;
    }

    private static bool TryGetUniformNumber(IReadOnlyList<ResolvedDevice> devices, out int number)
    {
        number = default;
        if (devices.Count == 0)
        {
            return false;
        }

        var firstNo = devices[0].No;
        if (firstNo is null)
        {
            return false;
        }

        var uniformNumber = firstNo.Value;
        number = uniformNumber;
        for (var i = 1; i < devices.Count; i++)
        {
            if (devices[i].No != uniformNumber)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetUniformNumber(IReadOnlyList<(ResolvedDevice Device, object Value)> items, out int number)
    {
        number = default;
        if (items.Count == 0)
        {
            return false;
        }

        var firstNo = items[0].Device.No;
        if (firstNo is null)
        {
            return false;
        }

        var uniformNumber = firstNo.Value;
        number = uniformNumber;
        for (var i = 1; i < items.Count; i++)
        {
            if (items[i].Device.No != uniformNumber)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasDuplicateDevices(IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (!seen.Add(item.Device.Text))
            {
                return true;
            }
        }

        return false;
    }

    private static bool AllDevicesInGroup(IReadOnlyList<ResolvedDevice> devices, string group)
    {
        for (var i = 1; i < devices.Count; i++)
        {
            if (GetBatchGroupKey(devices[i]) != group)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AllItemsInGroup(IReadOnlyList<(ResolvedDevice Device, object Value)> items, string group)
    {
        for (var i = 1; i < items.Count; i++)
        {
            if (GetBatchGroupKey(items[i].Device) != group)
            {
                return false;
            }
        }

        return true;
    }

    private object[] ReadIndividually(IReadOnlyList<ResolvedDevice> devices)
    {
        var values = new object[devices.Count];
        for (var i = 0; i < devices.Count; i++)
        {
            values[i] = ReadOne(devices[i]);
        }

        return values;
    }

    private object[] RelayReadIndividually(object hops, IReadOnlyList<ResolvedDevice> devices)
    {
        var values = new object[devices.Count];
        for (var i = 0; i < devices.Count; i++)
        {
            values[i] = RelayReadOne(hops, devices[i]);
        }

        return values;
    }

    private static int[] CollectBasicAddresses(IReadOnlyList<ResolvedDevice> devices)
    {
        var addresses = new int[devices.Count];
        for (var i = 0; i < devices.Count; i++)
        {
            addresses[i] = Require(devices[i].BasicAddress, "basic_addr");
        }

        return addresses;
    }

    private static int[] CollectAddress32Values(IReadOnlyList<ResolvedDevice> devices)
    {
        var addresses = new int[devices.Count];
        for (var i = 0; i < devices.Count; i++)
        {
            addresses[i] = Require(devices[i].Address32, "pc10 addr32");
        }

        return addresses;
    }

    private static (int No, int Address)[] CollectNoAddresses(IReadOnlyList<ResolvedDevice> devices)
    {
        var points = new (int No, int Address)[devices.Count];
        for (var i = 0; i < devices.Count; i++)
        {
            points[i] = (Require(devices[i].No, "extended number"), Require(devices[i].Address, "extended addr"));
        }

        return points;
    }

    private static (int No, int Bit, int Address)[] CollectNoBitAddresses(IReadOnlyList<ResolvedDevice> devices)
    {
        var points = new (int No, int Bit, int Address)[devices.Count];
        for (var i = 0; i < devices.Count; i++)
        {
            points[i] = (
                Require(devices[i].No, "extended number"),
                Require(devices[i].BitNo, "extended bit"),
                Require(devices[i].Address, "extended addr"));
        }

        return points;
    }

    private static (int Address, int Value)[] CollectBasicAddressValues(IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var values = new (int Address, int Value)[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            values[i] = (Require(items[i].Device.BasicAddress, "basic_addr"), ToInt32Invariant(items[i].Value));
        }

        return values;
    }

    private static (int No, int Address, int Value)[] CollectNoAddressValues(IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var values = new (int No, int Address, int Value)[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            values[i] = (
                Require(items[i].Device.No, "extended number"),
                Require(items[i].Device.Address, "extended addr"),
                ToInt32Invariant(items[i].Value));
        }

        return values;
    }

    private static (int No, int Bit, int Address, int Value)[] CollectNoBitAddressValues(IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var values = new (int No, int Bit, int Address, int Value)[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            values[i] = (
                Require(items[i].Device.No, "extended number"),
                Require(items[i].Device.BitNo, "extended bit"),
                Require(items[i].Device.Address, "extended addr"),
                ToBitInt(items[i].Value));
        }

        return values;
    }

    private static (int Address32, int Value)[] CollectAddress32WordValues(IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var values = new (int Address32, int Value)[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            values[i] = (Require(items[i].Device.Address32, "pc10 addr32"), ToInt32Invariant(items[i].Value));
        }

        return values;
    }

    private static (int Address32, int Value)[] CollectAddress32BitValues(IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var values = new (int Address32, int Value)[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            values[i] = (Require(items[i].Device.Address32, "pc10 addr32"), ToBitInt(items[i].Value));
        }

        return values;
    }

    private static int[] CollectIntValues(IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var values = new int[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            values[i] = ToInt32Invariant(items[i].Value);
        }

        return values;
    }

    private static byte[] CollectByteValues(IReadOnlyList<(ResolvedDevice Device, object Value)> items)
    {
        var values = new byte[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            values[i] = (byte)(ToInt32Invariant(items[i].Value) & 0xFF);
        }

        return values;
    }

    private static int[] SliceWordValues(IReadOnlyList<int> values, int offset, int count)
    {
        var slice = new int[count];
        for (var i = 0; i < count; i++)
        {
            slice[i] = values[offset + i];
        }

        return slice;
    }

    private static object[] BoxWords(IReadOnlyList<int> values)
    {
        var boxed = new object[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            boxed[i] = values[i];
        }

        return boxed;
    }

    private static object[] BoxBytes(IReadOnlyList<byte> values)
    {
        var boxed = new object[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            boxed[i] = values[i];
        }

        return boxed;
    }

    private static object[] BoxBooleans(IReadOnlyList<bool> values)
    {
        var boxed = new object[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            boxed[i] = values[i];
        }

        return boxed;
    }

    private static object[] BoxBooleanBits(IReadOnlyList<int> values)
    {
        var boxed = new object[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            boxed[i] = (values[i] & 0x01) != 0;
        }

        return boxed;
    }

    private static object[] BoxBooleanBytes(IReadOnlyList<byte> values)
    {
        var boxed = new object[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            boxed[i] = (values[i] & 0x01) != 0;
        }

        return boxed;
    }

    private static int[] ParsePc10MultiWordData(byte[] data, int count)
    {
        if (data.Length < 4 + (count * 2))
        {
            throw new ToyopucProtocolError("PC10 multi-word response too short");
        }

        var values = new int[count];
        for (var i = 0; i < count; i++)
        {
            var offset = 4 + (i * 2);
            values[i] = data[offset] | (data[offset + 1] << 8);
        }

        return values;
    }

    private static int[] ParsePc10MultiBitData(byte[] data, int count)
    {
        if (data.Length < 4 + ((count + 7) / 8))
        {
            throw new ToyopucProtocolError("PC10 multi-bit response too short");
        }

        var values = new int[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = (data[4 + (i / 8)] >> (i % 8)) & 0x01;
        }

        return values;
    }

    private static int[] ParseExtMultiBitData(byte[] data, int count)
    {
        if (data.Length < (count + 7) / 8)
        {
            throw new ToyopucProtocolError("Extended multi-bit response too short");
        }

        var values = new int[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = (data[i / 8] >> (i % 8)) & 0x01;
        }

        return values;
    }

    private static int ToBitInt(object value)
    {
        return ToBooleanInvariant(value) ? 1 : 0;
    }

    private static void RaiseGenericFrWriteError()
    {
        throw new ArgumentException(
            "Generic FR writes are disabled; use WriteFr(..., commit: false|true) or CommitFr() explicitly");
    }

    private int[] ReadFrWordsViaExt(int index, int count)
    {
        var values = new int[count];
        var offset = 0;
        foreach (var (number, address, chunkWords) in IterateFrExtSegments(index, count))
        {
            var chunk = ReadExtWords(number, address, chunkWords);
            Array.Copy(chunk, 0, values, offset, chunkWords);
            offset += chunkWords;
        }

        return values;
    }

    private int[] RelayReadFrWordsViaExt(object hops, int index, int count)
    {
        var values = new int[count];
        var offset = 0;
        foreach (var (number, address, chunkWords) in IterateFrExtSegments(index, count))
        {
            var response = SendViaRelay(hops, ToyopucProtocol.BuildExtWordRead(number, address, chunkWords));
            EnsureCommand(response, 0x94, "Unexpected CMD in relay ext word-read response");
            var chunk = ToyopucProtocol.UnpackU16LittleEndian(response.Data);
            Array.Copy(chunk, 0, values, offset, chunkWords);
            offset += chunkWords;
        }

        return values;
    }

    private int[] RelayReadFrWordsPc10(object hops, int index, int count)
    {
        var values = new int[count];
        var offset = 0;
        foreach (var (number, address, chunkWords) in IterateFrExtSegments(index, count))
        {
            var address32 = ToyopucAddress.EncodeExNoByteU32(number, address * 2);
            var response = SendViaRelay(hops, ToyopucProtocol.BuildPc10BlockRead(address32, chunkWords * 2));
            EnsureCommand(response, 0xC2, "Unexpected CMD in relay FR block-read response");
            var chunk = ToyopucProtocol.UnpackU16LittleEndian(response.Data);
            Array.Copy(chunk, 0, values, offset, chunkWords);
            offset += chunkWords;
        }

        return values;
    }

    private void WriteFrWordsViaExt(int index, IReadOnlyList<int> values)
    {
        var offset = 0;
        foreach (var (number, address, chunkWords) in IterateFrExtSegments(index, values.Count))
        {
            WriteExtWords(number, address, SliceWordValues(values, offset, chunkWords));
            offset += chunkWords;
        }
    }

    private void RelayWriteFrWordsViaExt(object hops, int index, IReadOnlyList<int> values)
    {
        var offset = 0;
        foreach (var (number, address, chunkWords) in IterateFrExtSegments(index, values.Count))
        {
            var response = SendViaRelay(
                hops,
                ToyopucProtocol.BuildExtWordWrite(number, address, SliceWordValues(values, offset, chunkWords)));
            EnsureCommand(response, 0x95, "Unexpected CMD in relay ext word-write response");
            offset += chunkWords;
        }
    }

    private static IEnumerable<(int No, int Address, int WordCount)> IterateFrExtSegments(int startIndex, int wordCount)
    {
        if (startIndex is < 0 or > 0x1FFFFF)
        {
            throw new ArgumentOutOfRangeException(nameof(startIndex), "FR index out of range (0x000000-0x1FFFFF)");
        }

        if (wordCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(wordCount), "wordCount must be >= 1");
        }

        var index = startIndex;
        var remaining = wordCount;
        while (remaining > 0)
        {
            var chunkWords = Math.Min(remaining, 0x8000 - (index % 0x8000));
            var fr = ToyopucAddress.EncodeExtNoAddress("FR", index, "word");
            yield return (fr.No, fr.Address, chunkWords);
            index += chunkWords;
            remaining -= chunkWords;
        }
    }

    private object ReadOne(ResolvedDevice resolved)
    {
        return resolved.Scheme switch
        {
            "basic-bit" => ReadBit(Require(resolved.BasicAddress, "basic_addr")),
            "basic-word" => ReadWords(Require(resolved.BasicAddress, "basic_addr"), 1)[0],
            "basic-byte" => ReadBytes(Require(resolved.BasicAddress, "basic_addr"), 1)[0],
            "program-bit" => (ReadExtMulti(
                new[] { (Require(resolved.No, "program number"), Require(resolved.BitNo, "program bit"), Require(resolved.Address, "program addr")) },
                Array.Empty<(int No, int Address)>(),
                Array.Empty<(int No, int Address)>())[0] & 0x01) != 0,
            "program-word" => ReadExtWords(Require(resolved.No, "program number"), Require(resolved.Address, "program addr"), 1)[0],
            "program-byte" => ReadExtBytes(Require(resolved.No, "program number"), Require(resolved.Address, "program addr"), 1)[0],
            "ext-bit" => (ReadExtMulti(
                new[] { (Require(resolved.No, "extended number"), Require(resolved.BitNo, "extended bit"), Require(resolved.Address, "extended addr")) },
                Array.Empty<(int No, int Address)>(),
                Array.Empty<(int No, int Address)>())[0] & 0x01) != 0,
            "ext-word" => ReadExtWords(Require(resolved.No, "extended number"), Require(resolved.Address, "extended addr"), 1)[0],
            "ext-byte" => ReadExtBytes(Require(resolved.No, "extended number"), Require(resolved.Address, "extended addr"), 1)[0],
            "pc10-bit" => ReadPc10MultiBits(this, new[] { Require(resolved.Address32, "pc10 addr32") })[0] != 0,
            "pc10-word" => ReadPc10BlockWord(this, Require(resolved.Address32, "pc10 addr32")),
            "pc10-byte" => Pc10BlockRead(Require(resolved.Address32, "pc10 addr32"), 1)[0],
            _ => throw new ArgumentException($"Unsupported resolved scheme: {resolved.Scheme}", nameof(resolved)),
        };
    }

    private object RelayReadOne(object hops, ResolvedDevice resolved)
    {
        switch (resolved.Scheme)
        {
            case "basic-bit":
                {
                    var response = SendViaRelay(hops, ToyopucProtocol.BuildBitRead(Require(resolved.BasicAddress, "basic_addr")));
                    EnsureCommand(response, 0x20, "Unexpected CMD in relay bit-read response");
                    if (response.Data.Length != 1)
                    {
                        throw new ToyopucProtocolError("Relay bit-read response must be 1 byte");
                    }

                    return (response.Data[0] & 0x01) != 0;
                }
            case "basic-word":
                {
                    var response = SendViaRelay(hops, ToyopucProtocol.BuildWordRead(Require(resolved.BasicAddress, "basic_addr"), 1));
                    EnsureCommand(response, 0x1C, "Unexpected CMD in relay word-read response");
                    return ToyopucProtocol.UnpackU16LittleEndian(response.Data)[0];
                }
            case "basic-byte":
                {
                    var response = SendViaRelay(hops, ToyopucProtocol.BuildByteRead(Require(resolved.BasicAddress, "basic_addr"), 1));
                    EnsureCommand(response, 0x1E, "Unexpected CMD in relay byte-read response");
                    if (response.Data.Length != 1)
                    {
                        throw new ToyopucProtocolError("Relay byte-read response must be 1 byte");
                    }

                    return response.Data[0];
                }
            case "program-bit":
                {
                    var response = SendViaRelay(
                        hops,
                        ToyopucProtocol.BuildExtMultiRead(
                            new[] { (Require(resolved.No, "program number"), Require(resolved.BitNo, "program bit"), Require(resolved.Address, "program addr")) },
                            Array.Empty<(int No, int Address)>(),
                            Array.Empty<(int No, int Address)>()));
                    EnsureCommand(response, 0x98, "Unexpected CMD in relay multi-read response");
                    if (response.Data.Length == 0)
                    {
                        throw new ToyopucProtocolError("Relay multi-read response missing bit payload");
                    }

                    return (response.Data[0] & 0x01) != 0;
                }
            case "program-word":
                {
                    var response = SendViaRelay(
                        hops,
                        ToyopucProtocol.BuildExtWordRead(Require(resolved.No, "program number"), Require(resolved.Address, "program addr"), 1));
                    EnsureCommand(response, 0x94, "Unexpected CMD in relay ext word-read response");
                    return ToyopucProtocol.UnpackU16LittleEndian(response.Data)[0];
                }
            case "program-byte":
                {
                    var response = SendViaRelay(
                        hops,
                        ToyopucProtocol.BuildExtByteRead(Require(resolved.No, "program number"), Require(resolved.Address, "program addr"), 1));
                    EnsureCommand(response, 0x96, "Unexpected CMD in relay ext byte-read response");
                    if (response.Data.Length != 1)
                    {
                        throw new ToyopucProtocolError("Relay ext byte-read response must be 1 byte");
                    }

                    return response.Data[0];
                }
            case "ext-bit":
                {
                    var response = SendViaRelay(
                        hops,
                        ToyopucProtocol.BuildExtMultiRead(
                            new[] { (Require(resolved.No, "extended number"), Require(resolved.BitNo, "extended bit"), Require(resolved.Address, "extended addr")) },
                            Array.Empty<(int No, int Address)>(),
                            Array.Empty<(int No, int Address)>()));
                    EnsureCommand(response, 0x98, "Unexpected CMD in relay multi-read response");
                    if (response.Data.Length == 0)
                    {
                        throw new ToyopucProtocolError("Relay multi-read response missing bit payload");
                    }

                    return (response.Data[0] & 0x01) != 0;
                }
            case "ext-word":
                {
                    var response = SendViaRelay(
                        hops,
                        ToyopucProtocol.BuildExtWordRead(Require(resolved.No, "extended number"), Require(resolved.Address, "extended addr"), 1));
                    EnsureCommand(response, 0x94, "Unexpected CMD in relay ext word-read response");
                    return ToyopucProtocol.UnpackU16LittleEndian(response.Data)[0];
                }
            case "ext-byte":
                {
                    var response = SendViaRelay(
                        hops,
                        ToyopucProtocol.BuildExtByteRead(Require(resolved.No, "extended number"), Require(resolved.Address, "extended addr"), 1));
                    EnsureCommand(response, 0x96, "Unexpected CMD in relay ext byte-read response");
                    if (response.Data.Length != 1)
                    {
                        throw new ToyopucProtocolError("Relay ext byte-read response must be 1 byte");
                    }

                    return response.Data[0];
                }
            case "pc10-bit":
                {
                    var response = SendViaRelay(
                        hops,
                        ToyopucProtocol.BuildPc10MultiRead(BuildPc10MultiBitReadPayload(new[] { Require(resolved.Address32, "pc10 addr32") })));
                    EnsureCommand(response, 0xC4, "Unexpected CMD in relay PC10 multi-read response");
                    if (response.Data.Length < 5)
                    {
                        throw new ToyopucProtocolError("Relay PC10 bit-read response too short");
                    }

                    return (response.Data[4] & 0x01) != 0;
                }
            case "pc10-word":
                {
                    var response = SendViaRelay(hops, ToyopucProtocol.BuildPc10BlockRead(Require(resolved.Address32, "pc10 addr32"), 2));
                    EnsureCommand(response, 0xC2, "Unexpected CMD in relay PC10 block-read response");
                    if (response.Data.Length < 2)
                    {
                        throw new ToyopucProtocolError("Relay PC10 word-read response too short");
                    }

                    return response.Data[0] | (response.Data[1] << 8);
                }
            case "pc10-byte":
                {
                    var response = SendViaRelay(hops, ToyopucProtocol.BuildPc10BlockRead(Require(resolved.Address32, "pc10 addr32"), 1));
                    EnsureCommand(response, 0xC2, "Unexpected CMD in relay PC10 block-read response");
                    if (response.Data.Length < 1)
                    {
                        throw new ToyopucProtocolError("Relay PC10 byte-read response too short");
                    }

                    return response.Data[0];
                }
            default:
                throw new ArgumentException($"Unsupported resolved scheme: {resolved.Scheme}", nameof(resolved));
        }
    }

    private void WriteOne(ResolvedDevice resolved, object value)
    {
        if (resolved.Area == "FR")
        {
            RaiseGenericFrWriteError();
        }

        switch (resolved.Scheme)
        {
            case "basic-bit":
                WriteBit(Require(resolved.BasicAddress, "basic_addr"), ToBooleanInvariant(value));
                return;
            case "basic-word":
                WriteWords(Require(resolved.BasicAddress, "basic_addr"), new[] { ToInt32Invariant(value) });
                return;
            case "basic-byte":
                WriteBytes(Require(resolved.BasicAddress, "basic_addr"), new[] { ToInt32Invariant(value) });
                return;
            case "program-bit":
                WriteExtMulti(
                    new[] { (Require(resolved.No, "program number"), Require(resolved.BitNo, "program bit"), Require(resolved.Address, "program addr"), ToInt32Invariant(value) & 0x01) },
                    Array.Empty<(int No, int Address, int Value)>(),
                    Array.Empty<(int No, int Address, int Value)>());
                return;
            case "program-word":
                WriteExtWords(Require(resolved.No, "program number"), Require(resolved.Address, "program addr"), new[] { ToInt32Invariant(value) });
                return;
            case "program-byte":
                WriteExtBytes(Require(resolved.No, "program number"), Require(resolved.Address, "program addr"), new[] { ToInt32Invariant(value) });
                return;
            case "ext-bit":
                WriteExtMulti(
                    new[] { (Require(resolved.No, "extended number"), Require(resolved.BitNo, "extended bit"), Require(resolved.Address, "extended addr"), ToInt32Invariant(value) & 0x01) },
                    Array.Empty<(int No, int Address, int Value)>(),
                    Array.Empty<(int No, int Address, int Value)>());
                return;
            case "ext-word":
                WriteExtWords(Require(resolved.No, "extended number"), Require(resolved.Address, "extended addr"), new[] { ToInt32Invariant(value) });
                return;
            case "ext-byte":
                WriteExtBytes(Require(resolved.No, "extended number"), Require(resolved.Address, "extended addr"), new[] { ToInt32Invariant(value) });
                return;
            case "pc10-bit":
                Pc10MultiWrite(PackPc10MultiBitPayload(new[] { (Require(resolved.Address32, "pc10 addr32"), ToInt32Invariant(value) & 0x01) }));
                return;
            case "pc10-word":
                WritePc10BlockWord(this, Require(resolved.Address32, "pc10 addr32"), ToInt32Invariant(value));
                return;
            case "pc10-byte":
                Pc10BlockWrite(Require(resolved.Address32, "pc10 addr32"), new[] { (byte)(ToInt32Invariant(value) & 0xFF) });
                return;
            default:
                throw new ArgumentException($"Unsupported resolved scheme: {resolved.Scheme}", nameof(resolved));
        }
    }

    private void RelayWriteOne(object hops, ResolvedDevice resolved, object value)
    {
        switch (resolved.Scheme)
        {
            case "basic-bit":
                {
                    var response = SendViaRelay(hops, ToyopucProtocol.BuildBitWrite(Require(resolved.BasicAddress, "basic_addr"), ToInt32Invariant(value) & 0x01));
                    EnsureCommand(response, 0x21, "Unexpected CMD in relay bit-write response");
                    return;
                }
            case "basic-word":
                {
                    var response = SendViaRelay(hops, ToyopucProtocol.BuildWordWrite(Require(resolved.BasicAddress, "basic_addr"), new[] { ToInt32Invariant(value) }));
                    EnsureCommand(response, 0x1D, "Unexpected CMD in relay word-write response");
                    return;
                }
            case "basic-byte":
                {
                    var response = SendViaRelay(hops, ToyopucProtocol.BuildByteWrite(Require(resolved.BasicAddress, "basic_addr"), new[] { ToInt32Invariant(value) }));
                    EnsureCommand(response, 0x1F, "Unexpected CMD in relay byte-write response");
                    return;
                }
            case "program-bit":
                {
                    var response = SendViaRelay(
                        hops,
                        ToyopucProtocol.BuildExtMultiWrite(
                            new[] { (Require(resolved.No, "program number"), Require(resolved.BitNo, "program bit"), Require(resolved.Address, "program addr"), ToInt32Invariant(value) & 0x01) },
                            Array.Empty<(int No, int Address, int Value)>(),
                            Array.Empty<(int No, int Address, int Value)>()));
                    EnsureCommand(response, 0x99, "Unexpected CMD in relay multi-write response");
                    return;
                }
            case "program-word":
                {
                    var response = SendViaRelay(
                        hops,
                        ToyopucProtocol.BuildExtWordWrite(Require(resolved.No, "program number"), Require(resolved.Address, "program addr"), new[] { ToInt32Invariant(value) }));
                    EnsureCommand(response, 0x95, "Unexpected CMD in relay ext word-write response");
                    return;
                }
            case "program-byte":
                {
                    var response = SendViaRelay(
                        hops,
                        ToyopucProtocol.BuildExtByteWrite(Require(resolved.No, "program number"), Require(resolved.Address, "program addr"), new[] { ToInt32Invariant(value) }));
                    EnsureCommand(response, 0x97, "Unexpected CMD in relay ext byte-write response");
                    return;
                }
            case "ext-bit":
                {
                    var response = SendViaRelay(
                        hops,
                        ToyopucProtocol.BuildExtMultiWrite(
                            new[] { (Require(resolved.No, "extended number"), Require(resolved.BitNo, "extended bit"), Require(resolved.Address, "extended addr"), ToInt32Invariant(value) & 0x01) },
                            Array.Empty<(int No, int Address, int Value)>(),
                            Array.Empty<(int No, int Address, int Value)>()));
                    EnsureCommand(response, 0x99, "Unexpected CMD in relay multi-write response");
                    return;
                }
            case "ext-word":
                {
                    var response = SendViaRelay(
                        hops,
                        ToyopucProtocol.BuildExtWordWrite(Require(resolved.No, "extended number"), Require(resolved.Address, "extended addr"), new[] { ToInt32Invariant(value) }));
                    EnsureCommand(response, 0x95, "Unexpected CMD in relay ext word-write response");
                    return;
                }
            case "ext-byte":
                {
                    var response = SendViaRelay(
                        hops,
                        ToyopucProtocol.BuildExtByteWrite(Require(resolved.No, "extended number"), Require(resolved.Address, "extended addr"), new[] { ToInt32Invariant(value) }));
                    EnsureCommand(response, 0x97, "Unexpected CMD in relay ext byte-write response");
                    return;
                }
            case "pc10-bit":
                {
                    var response = SendViaRelay(
                        hops,
                        ToyopucProtocol.BuildPc10MultiWrite(PackPc10MultiBitPayload(new[] { (Require(resolved.Address32, "pc10 addr32"), ToInt32Invariant(value) & 0x01) })));
                    EnsureCommand(response, 0xC5, "Unexpected CMD in relay PC10 multi-write response");
                    return;
                }
            case "pc10-word":
                {
                    var response = SendViaRelay(
                        hops,
                        ToyopucProtocol.BuildPc10BlockWrite(Require(resolved.Address32, "pc10 addr32"), ToyopucProtocol.PackU16LittleEndian(ToInt32Invariant(value) & 0xFFFF)));
                    EnsureCommand(response, 0xC3, "Unexpected CMD in relay PC10 block-write response");
                    return;
                }
            case "pc10-byte":
                {
                    var response = SendViaRelay(
                        hops,
                        ToyopucProtocol.BuildPc10BlockWrite(Require(resolved.Address32, "pc10 addr32"), new[] { (byte)(ToInt32Invariant(value) & 0xFF) }));
                    EnsureCommand(response, 0xC3, "Unexpected CMD in relay PC10 block-write response");
                    return;
                }
            default:
                throw new ArgumentException($"Unsupported resolved scheme: {resolved.Scheme}", nameof(resolved));
        }
    }

    private ResolvedDevice Offset(ResolvedDevice resolved, int delta)
    {
        if (delta == 0)
        {
            return resolved;
        }

        var nextIndex = checked(resolved.Index + delta);
        if (nextIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delta), "Offset would move address index below zero");
        }

        if (TryOffsetFast(resolved, nextIndex, out var next))
        {
            return next;
        }

        return ResolveDevice(BuildResolvedText(resolved, nextIndex));
    }

    private bool TryOffsetFast(ResolvedDevice resolved, int nextIndex, out ResolvedDevice next)
    {
        var nextText = BuildResolvedText(resolved, nextIndex);
        switch (resolved.Scheme)
        {
            case "basic-bit":
                {
                    if ((resolved.Area is "L" or "M") && nextIndex >= 0x1000)
                    {
                        break;
                    }

                    var parsed = new ParsedAddress(resolved.Area, nextIndex, "bit", Packed: resolved.Packed);
                    next = resolved with
                    {
                        Text = nextText,
                        Index = nextIndex,
                        BasicAddress = ToyopucAddress.EncodeBitAddress(parsed),
                    };
                    return true;
                }
            case "basic-word":
                {
                    var parsed = new ParsedAddress(resolved.Area, nextIndex, "word", Packed: resolved.Packed);
                    next = resolved with
                    {
                        Text = nextText,
                        Index = nextIndex,
                        BasicAddress = ToyopucAddress.EncodeWordAddress(parsed),
                    };
                    return true;
                }
            case "basic-byte":
                {
                    var parsed = new ParsedAddress(resolved.Area, nextIndex, "byte", resolved.High, resolved.Packed);
                    next = resolved with
                    {
                        Text = nextText,
                        Index = nextIndex,
                        BasicAddress = ToyopucAddress.EncodeByteAddress(parsed),
                    };
                    return true;
                }
            case "program-bit":
                {
                    if (resolved.Prefix is null || !ProgramPrefixExNo.TryGetValue(resolved.Prefix, out var exNo))
                    {
                        break;
                    }

                    var parsed = new ParsedAddress(resolved.Area, nextIndex, "bit", Packed: resolved.Packed);
                    var (bitNo, address) = ToyopucAddress.EncodeProgramBitAddress(parsed);
                    next = resolved with
                    {
                        Text = nextText,
                        Index = nextIndex,
                        Address = address,
                        BitNo = bitNo,
                        Address32 = ToyopucAddress.EncodePc10BitAddress(parsed) | (exNo << 19),
                    };
                    return true;
                }
            case "program-word":
                {
                    var parsed = new ParsedAddress(resolved.Area, nextIndex, "word", Packed: resolved.Packed);
                    next = resolved with
                    {
                        Text = nextText,
                        Index = nextIndex,
                        Address = ToyopucAddress.EncodeProgramWordAddress(parsed),
                    };
                    return true;
                }
            case "program-byte":
                {
                    var parsed = new ParsedAddress(resolved.Area, nextIndex, "byte", resolved.High, resolved.Packed);
                    next = resolved with
                    {
                        Text = nextText,
                        Index = nextIndex,
                        Address = ToyopucAddress.EncodeProgramByteAddress(parsed),
                    };
                    return true;
                }
            case "ext-word":
                {
                    if (resolved.Area == "U" && AddressingOptions.UseUpperUPc10 && nextIndex >= 0x08000)
                    {
                        break;
                    }

                    var ext = ToyopucAddress.EncodeExtNoAddress(resolved.Area, nextIndex, "word");
                    next = resolved with
                    {
                        Text = nextText,
                        Index = nextIndex,
                        No = ext.No,
                        Address = ext.Address,
                    };
                    return true;
                }
            case "ext-byte":
                {
                    if (resolved.Area == "FR")
                    {
                        break;
                    }

                    if (resolved.Area == "U" && AddressingOptions.UseUpperUPc10 && nextIndex >= 0x08000)
                    {
                        break;
                    }

                    var ext = ToyopucAddress.EncodeExtNoAddress(
                        resolved.Area,
                        checked((nextIndex * 2) + (resolved.High ? 1 : 0)),
                        "byte");
                    next = resolved with
                    {
                        Text = nextText,
                        Index = nextIndex,
                        No = ext.No,
                        Address = ext.Address,
                    };
                    return true;
                }
            case "pc10-bit":
                {
                    if (nextIndex < 0x1000)
                    {
                        break;
                    }

                    var parsed = new ParsedAddress(resolved.Area, nextIndex, "bit", Packed: resolved.Packed);
                    next = resolved with
                    {
                        Text = nextText,
                        Index = nextIndex,
                        Address32 = ToyopucAddress.EncodePc10BitAddress(parsed),
                    };
                    return true;
                }
            case "pc10-word":
                {
                    if (!TryEncodePc10WordAddress32(resolved.Area, nextIndex, out var address32))
                    {
                        break;
                    }

                    next = resolved with
                    {
                        Text = nextText,
                        Index = nextIndex,
                        Address32 = address32,
                    };
                    return true;
                }
            case "pc10-byte":
                {
                    if (!TryEncodePc10ByteAddress32(resolved.Area, nextIndex, resolved.High, out var address32))
                    {
                        break;
                    }

                    next = resolved with
                    {
                        Text = nextText,
                        Index = nextIndex,
                        Address32 = address32,
                    };
                    return true;
                }
        }

        next = null!;
        return false;
    }

    private bool TryEncodePc10WordAddress32(string area, int index, out int address32)
    {
        address32 = default;
        switch (area)
        {
            case "U" when AddressingOptions.UseUpperUPc10 && index >= 0x08000 && index <= 0x1FFFF:
                address32 = EncodePc10UAddress32(index);
                return true;
            case "EB" when AddressingOptions.UseEbPc10 && index >= 0x00000 && index <= 0x3FFFF:
                address32 = EncodePc10EbAddress32(index);
                return true;
            case "FR" when AddressingOptions.UseFrPc10 && index >= 0x000000 && index <= 0x1FFFFF:
                address32 = ToyopucAddress.EncodeFrWordAddr32(index);
                return true;
            default:
                return false;
        }
    }

    private bool TryEncodePc10ByteAddress32(string area, int index, bool high, out int address32)
    {
        address32 = default;
        switch (area)
        {
            case "U" when AddressingOptions.UseUpperUPc10 && index >= 0x08000 && index <= 0x1FFFF:
                address32 = EncodePc10UAddress32(index, byteAddress: true, high: high);
                return true;
            case "EB" when AddressingOptions.UseEbPc10 && index >= 0x00000 && index <= 0x3FFFF:
                address32 = EncodePc10EbAddress32(index, byteAddress: true, high: high);
                return true;
            default:
                return false;
        }
    }

    private static int EncodePc10UAddress32(int index, bool byteAddress = false, bool high = false)
    {
        var block = index / 0x8000;
        var exNo = 0x03 + block;
        var byteOffset = (index % 0x8000) * 2 + (byteAddress && high ? 1 : 0);
        return ToyopucAddress.EncodeExNoByteU32(exNo, byteOffset);
    }

    private static int EncodePc10EbAddress32(int index, bool byteAddress = false, bool high = false)
    {
        var block = index / 0x8000;
        var exNo = 0x10 + block;
        var byteOffset = (index % 0x8000) * 2 + (byteAddress && high ? 1 : 0);
        return ToyopucAddress.EncodeExNoByteU32(exNo, byteOffset);
    }

    private static string BuildResolvedText(ResolvedDevice resolved, int index)
    {
        return ToyopucAddress.Format(resolved, index);
    }
}
