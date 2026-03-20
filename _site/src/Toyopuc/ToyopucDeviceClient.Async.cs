using System.Threading;
using System.Threading.Tasks;

namespace Toyopuc;

public partial class ToyopucDeviceClient
{
    public uint ReadDWord(object device)
    {
        return ReadDWords(device, 1)[0];
    }

    public void WriteDWord(object device, uint value)
    {
        WriteDWords(device, new[] { value });
    }

    public uint[] ReadDWords(object device, int count)
    {
        var resolved = ResolveDeviceObject(device);
        EnsureWordResolvedDevice(resolved, nameof(device), "ReadDWords()");
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "count must be >= 1");
        }

        return UnpackUInt32LowWordFirst(ReadResolvedWordValues(resolved, checked(count * 2)));
    }

    public void WriteDWords(object device, IEnumerable<uint> values)
    {
        var resolved = ResolveDeviceObject(device);
        EnsureWordResolvedDevice(resolved, nameof(device), "WriteDWords()");
        WriteResolvedWordValues(resolved, PackUInt32LowWordFirstToWords(values));
    }

    public float ReadFloat32(object device)
    {
        return ReadFloat32s(device, 1)[0];
    }

    public void WriteFloat32(object device, float value)
    {
        WriteFloat32s(device, new[] { value });
    }

    public float[] ReadFloat32s(object device, int count)
    {
        var resolved = ResolveDeviceObject(device);
        EnsureWordResolvedDevice(resolved, nameof(device), "ReadFloat32s()");
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "count must be >= 1");
        }

        return UnpackFloat32LowWordFirst(ReadResolvedWordValues(resolved, checked(count * 2)));
    }

    public void WriteFloat32s(object device, IEnumerable<float> values)
    {
        var resolved = ResolveDeviceObject(device);
        EnsureWordResolvedDevice(resolved, nameof(device), "WriteFloat32s()");
        WriteResolvedWordValues(resolved, PackFloat32LowWordFirstToWords(values));
    }

    public uint RelayReadDWord(object hops, object device)
    {
        return RelayReadDWords(hops, device, 1)[0];
    }

    public void RelayWriteDWord(object hops, object device, uint value)
    {
        RelayWriteDWords(hops, device, new[] { value });
    }

    public uint[] RelayReadDWords(object hops, object device, int count)
    {
        var resolved = ResolveDeviceObject(device);
        EnsureWordResolvedDevice(resolved, nameof(device), "RelayReadDWords()");
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "count must be >= 1");
        }

        return UnpackUInt32LowWordFirst(RelayReadResolvedWordValues(hops, resolved, checked(count * 2)));
    }

    public void RelayWriteDWords(object hops, object device, IEnumerable<uint> values)
    {
        var resolved = ResolveDeviceObject(device);
        EnsureWordResolvedDevice(resolved, nameof(device), "RelayWriteDWords()");
        RelayWriteResolvedWordValues(hops, resolved, PackUInt32LowWordFirstToWords(values));
    }

    public float RelayReadFloat32(object hops, object device)
    {
        return RelayReadFloat32s(hops, device, 1)[0];
    }

    public void RelayWriteFloat32(object hops, object device, float value)
    {
        RelayWriteFloat32s(hops, device, new[] { value });
    }

    public float[] RelayReadFloat32s(object hops, object device, int count)
    {
        var resolved = ResolveDeviceObject(device);
        EnsureWordResolvedDevice(resolved, nameof(device), "RelayReadFloat32s()");
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "count must be >= 1");
        }

        return UnpackFloat32LowWordFirst(RelayReadResolvedWordValues(hops, resolved, checked(count * 2)));
    }

    public void RelayWriteFloat32s(object hops, object device, IEnumerable<float> values)
    {
        var resolved = ResolveDeviceObject(device);
        EnsureWordResolvedDevice(resolved, nameof(device), "RelayWriteFloat32s()");
        RelayWriteResolvedWordValues(hops, resolved, PackFloat32LowWordFirstToWords(values));
    }

    public Task<ResolvedDevice> ResolveDeviceAsync(string device, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ResolveDevice(device));
    }

    public Task<object> RelayReadAsync(object hops, object device, int count = 1, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => RelayRead(hops, device, count), cancellationToken);
    }

    public Task RelayWriteAsync(object hops, object device, object value, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => RelayWrite(hops, device, value), cancellationToken);
    }

    public Task<object> RelayReadWordsAsync(object hops, object device, int count = 1, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => RelayReadWords(hops, device, count), cancellationToken);
    }

    public Task RelayWriteWordsAsync(object hops, object device, object value, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => RelayWriteWords(hops, device, value), cancellationToken);
    }

    public Task<object[]> RelayReadManyAsync(object hops, IEnumerable<object> devices, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => RelayReadMany(hops, devices), cancellationToken);
    }

    public Task RelayWriteManyAsync(object hops, IEnumerable<KeyValuePair<object, object>> items, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => RelayWriteMany(hops, items), cancellationToken);
    }

    public Task<object> ReadFrAsync(object device, int count = 1, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => ReadFr(device, count), cancellationToken);
    }

    public Task<object> RelayReadFrAsync(object hops, object device, int count = 1, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => RelayReadFr(hops, device, count), cancellationToken);
    }

    public Task WriteFrAsync(
        object device,
        object value,
        bool commit = false,
        bool? wait = null,
        double timeout = 30.0,
        double pollInterval = 0.2,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => WriteFr(device, value, commit, wait, timeout, pollInterval), cancellationToken);
    }

    public Task RelayWriteFrAsync(
        object hops,
        object device,
        object value,
        bool commit = false,
        bool? wait = null,
        double timeout = 30.0,
        double pollInterval = 0.2,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => RelayWriteFr(hops, device, value, commit, wait, timeout, pollInterval), cancellationToken);
    }

    public Task CommitFrAsync(
        object device,
        int count = 1,
        bool wait = false,
        double timeout = 30.0,
        double pollInterval = 0.2,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => CommitFr(device, count, wait, timeout, pollInterval), cancellationToken);
    }

    public Task RelayCommitFrAsync(
        object hops,
        object device,
        int count = 1,
        bool wait = false,
        double timeout = 30.0,
        double pollInterval = 0.2,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => RelayCommitFr(hops, device, count, wait, timeout, pollInterval), cancellationToken);
    }

    public Task<object> ReadAsync(object device, int count = 1, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Read(device, count), cancellationToken);
    }

    public Task WriteAsync(object device, object value, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Write(device, value), cancellationToken);
    }

    public Task<object[]> ReadManyAsync(IEnumerable<object> devices, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => ReadMany(devices), cancellationToken);
    }

    public Task WriteManyAsync(IEnumerable<KeyValuePair<object, object>> items, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => WriteMany(items), cancellationToken);
    }

    public Task<uint> ReadDWordAsync(object device, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => ReadDWord(device), cancellationToken);
    }

    public Task WriteDWordAsync(object device, uint value, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => WriteDWord(device, value), cancellationToken);
    }

    public Task<uint[]> ReadDWordsAsync(object device, int count, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => ReadDWords(device, count), cancellationToken);
    }

    public Task WriteDWordsAsync(object device, IEnumerable<uint> values, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => WriteDWords(device, values), cancellationToken);
    }

    public Task<float> ReadFloat32Async(object device, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => ReadFloat32(device), cancellationToken);
    }

    public Task WriteFloat32Async(object device, float value, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => WriteFloat32(device, value), cancellationToken);
    }

    public Task<float[]> ReadFloat32sAsync(object device, int count, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => ReadFloat32s(device, count), cancellationToken);
    }

    public Task WriteFloat32sAsync(object device, IEnumerable<float> values, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => WriteFloat32s(device, values), cancellationToken);
    }

    public Task<uint> RelayReadDWordAsync(object hops, object device, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => RelayReadDWord(hops, device), cancellationToken);
    }

    public Task RelayWriteDWordAsync(object hops, object device, uint value, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => RelayWriteDWord(hops, device, value), cancellationToken);
    }

    public Task<uint[]> RelayReadDWordsAsync(object hops, object device, int count, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => RelayReadDWords(hops, device, count), cancellationToken);
    }

    public Task RelayWriteDWordsAsync(object hops, object device, IEnumerable<uint> values, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => RelayWriteDWords(hops, device, values), cancellationToken);
    }

    public Task<float> RelayReadFloat32Async(object hops, object device, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => RelayReadFloat32(hops, device), cancellationToken);
    }

    public Task RelayWriteFloat32Async(object hops, object device, float value, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => RelayWriteFloat32(hops, device, value), cancellationToken);
    }

    public Task<float[]> RelayReadFloat32sAsync(object hops, object device, int count, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => RelayReadFloat32s(hops, device, count), cancellationToken);
    }

    public Task RelayWriteFloat32sAsync(object hops, object device, IEnumerable<float> values, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => RelayWriteFloat32s(hops, device, values), cancellationToken);
    }

    private int[] ReadResolvedWordValues(ResolvedDevice resolved, int wordCount)
    {
        if (wordCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(wordCount), "wordCount must be >= 1");
        }

        if (wordCount == 1)
        {
            return new[] { ToInt32Invariant(ReadOne(resolved)) & 0xFFFF };
        }

        var values = ReadRuns(ResolveSequentialDevices(resolved, wordCount), splitPc10BlockBoundaries: true);
        var words = new int[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            words[i] = ToInt32Invariant(values[i]) & 0xFFFF;
        }

        return words;
    }

    private int[] RelayReadResolvedWordValues(object hops, ResolvedDevice resolved, int wordCount)
    {
        if (wordCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(wordCount), "wordCount must be >= 1");
        }

        if (wordCount == 1)
        {
            return new[] { ToInt32Invariant(RelayReadOne(hops, resolved)) & 0xFFFF };
        }

        var values = RelayReadRuns(hops, ResolveSequentialDevices(resolved, wordCount), splitPc10BlockBoundaries: true);
        var words = new int[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            words[i] = ToInt32Invariant(values[i]) & 0xFFFF;
        }

        return words;
    }

    private void WriteResolvedWordValues(ResolvedDevice resolved, IReadOnlyList<int> wordValues)
    {
        if (wordValues.Count < 1)
        {
            throw new ArgumentException("values must not be empty", nameof(wordValues));
        }

        if (resolved.Area == "FR")
        {
            WriteFr(resolved, wordValues.ToArray());
            return;
        }

        var boxedValues = new object[wordValues.Count];
        for (var i = 0; i < wordValues.Count; i++)
        {
            boxedValues[i] = wordValues[i];
        }

        WriteRuns(ResolveSequentialWriteItems(resolved, boxedValues), splitPc10BlockBoundaries: true);
    }

    private void RelayWriteResolvedWordValues(object hops, ResolvedDevice resolved, IReadOnlyList<int> wordValues)
    {
        if (wordValues.Count < 1)
        {
            throw new ArgumentException("values must not be empty", nameof(wordValues));
        }

        if (resolved.Area == "FR")
        {
            RelayWriteFr(hops, resolved, wordValues.ToArray());
            return;
        }

        var boxedValues = new object[wordValues.Count];
        for (var i = 0; i < wordValues.Count; i++)
        {
            boxedValues[i] = wordValues[i];
        }

        RelayWriteRuns(hops, ResolveSequentialWriteItems(resolved, boxedValues), splitPc10BlockBoundaries: true);
    }

    private static void EnsureWordResolvedDevice(ResolvedDevice resolved, string paramName, string methodName)
    {
        if (resolved.Unit != "word")
        {
            throw new ArgumentException($"{methodName} requires a word device", paramName);
        }
    }
}
