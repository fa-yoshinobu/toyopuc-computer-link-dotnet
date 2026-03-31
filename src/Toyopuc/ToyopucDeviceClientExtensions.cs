using System.Globalization;
using System.Runtime.CompilerServices;

namespace PlcComm.Toyopuc;

public static class ToyopucDeviceClientExtensions
{
    public static Task<object> ReadTypedAsync(this ToyopucDeviceClient client, string device, string dtype, CancellationToken ct = default)
        => ReadTypedCoreAsync(client, relayHops: null, device, dtype, ct);

    public static Task<object> ReadTypedAsync(this QueuedToyopucDeviceClient client, string device, string dtype, CancellationToken ct = default)
        => client.ExecuteAsync(inner => ReadTypedCoreAsync(inner, client.RelayHops, device, dtype, ct), ct);

    public static Task WriteTypedAsync(this ToyopucDeviceClient client, string device, string dtype, object value, CancellationToken ct = default)
        => WriteTypedCoreAsync(client, relayHops: null, device, dtype, value, ct);

    public static Task WriteTypedAsync(this QueuedToyopucDeviceClient client, string device, string dtype, object value, CancellationToken ct = default)
        => client.ExecuteAsync(inner => WriteTypedCoreAsync(inner, client.RelayHops, device, dtype, value, ct), ct);

    public static Task WriteBitInWordAsync(this ToyopucDeviceClient client, string device, int bitIndex, bool value, CancellationToken ct = default)
        => WriteBitInWordCoreAsync(client, relayHops: null, device, bitIndex, value, ct);

    public static Task WriteBitInWordAsync(this QueuedToyopucDeviceClient client, string device, int bitIndex, bool value, CancellationToken ct = default)
        => client.ExecuteAsync(inner => WriteBitInWordCoreAsync(inner, client.RelayHops, device, bitIndex, value, ct), ct);

    public static Task<IReadOnlyDictionary<string, object>> ReadNamedAsync(this ToyopucDeviceClient client, IEnumerable<string> addresses, CancellationToken ct = default)
        => ReadNamedCoreAsync(client, relayHops: null, addresses, ct);

    public static Task<IReadOnlyDictionary<string, object>> ReadNamedAsync(this QueuedToyopucDeviceClient client, IEnumerable<string> addresses, CancellationToken ct = default)
    {
        var addrList = addresses as IList<string> ?? addresses.ToList();
        return client.ExecuteAsync(inner => ReadNamedCoreAsync(inner, client.RelayHops, addrList, ct), ct);
    }

    public static async IAsyncEnumerable<IReadOnlyDictionary<string, object>> PollAsync(
        this ToyopucDeviceClient client,
        IEnumerable<string> addresses,
        TimeSpan interval,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var addrList = addresses as IList<string> ?? addresses.ToList();
        while (!ct.IsCancellationRequested)
        {
            yield return await ReadNamedCoreAsync(client, relayHops: null, addrList, ct).ConfigureAwait(false);
            await Task.Delay(interval, ct).ConfigureAwait(false);
        }
    }

    public static async IAsyncEnumerable<IReadOnlyDictionary<string, object>> PollAsync(
        this QueuedToyopucDeviceClient client,
        IEnumerable<string> addresses,
        TimeSpan interval,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var addrList = addresses as IList<string> ?? addresses.ToList();
        while (!ct.IsCancellationRequested)
        {
            var snapshot = await client.ExecuteAsync(inner => ReadNamedCoreAsync(inner, client.RelayHops, addrList, ct), ct).ConfigureAwait(false);
            yield return snapshot;
            await Task.Delay(interval, ct).ConfigureAwait(false);
        }
    }

    public static async Task<ushort[]> ReadWordsAsync(this ToyopucDeviceClient client, string device, int count, CancellationToken ct = default)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "count must be 1 or greater.");
        var raw = await client.ReadAsync(device, count, ct).ConfigureAwait(false);
        return ConvertWordReadResult(raw, count);
    }

    public static Task<ushort[]> ReadWordsAsync(this QueuedToyopucDeviceClient client, string device, int count, CancellationToken ct = default)
        => client.ExecuteAsync(inner => client.UsesRelay ? ReadWordsSemanticViaRelayCoreAsync(inner, client.RelayHops!, device, count, ct) : ToyopucDeviceClientExtensions.ReadWordsAsync(inner, device, count, ct), ct);

    public static Task<uint[]> ReadDWordsAsync(this ToyopucDeviceClient client, string device, int count, CancellationToken ct = default)
        => client.ReadDWordsAsync(device, count, atomicTransfer: false, cancellationToken: ct);

    public static Task<uint[]> ReadDWordsAsync(this QueuedToyopucDeviceClient client, string device, int count, CancellationToken ct = default)
        => client.ExecuteAsync(inner => client.UsesRelay ? inner.RelayReadDWordsAsync(client.RelayHops!, device, count, atomicTransfer: false, cancellationToken: ct) : inner.ReadDWordsAsync(device, count, atomicTransfer: false, cancellationToken: ct), ct);

    public static Task<ushort[]> ReadWordsSingleRequestAsync(this ToyopucDeviceClient client, string device, int count, CancellationToken ct = default)
        => ReadWordsSingleRequestCoreAsync(client, relayHops: null, device, count, ct);

    public static Task<ushort[]> ReadWordsSingleRequestAsync(this QueuedToyopucDeviceClient client, string device, int count, CancellationToken ct = default)
        => client.ExecuteAsync(inner => ReadWordsSingleRequestCoreAsync(inner, client.RelayHops, device, count, ct), ct);

    public static async Task<uint[]> ReadDWordsSingleRequestAsync(this ToyopucDeviceClient client, string device, int count, CancellationToken ct = default)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "count must be 1 or greater.");
        var words = await ReadWordsSingleRequestCoreAsync(client, relayHops: null, device, checked(count * 2), ct).ConfigureAwait(false);
        return PackDWords(words);
    }

    public static Task<uint[]> ReadDWordsSingleRequestAsync(this QueuedToyopucDeviceClient client, string device, int count, CancellationToken ct = default)
        => client.ExecuteAsync(inner => ReadDWordsSingleRequestCoreAsync(inner, client.RelayHops, device, count, ct), ct);

    public static Task WriteWordsSingleRequestAsync(this ToyopucDeviceClient client, string device, IReadOnlyList<ushort> values, CancellationToken ct = default)
        => WriteWordsSingleRequestCoreAsync(client, relayHops: null, device, values, ct);

    public static Task WriteWordsSingleRequestAsync(this QueuedToyopucDeviceClient client, string device, IReadOnlyList<ushort> values, CancellationToken ct = default)
        => client.ExecuteAsync(inner => WriteWordsSingleRequestCoreAsync(inner, client.RelayHops, device, values, ct), ct);

    public static Task WriteDWordsSingleRequestAsync(this ToyopucDeviceClient client, string device, IReadOnlyList<uint> values, CancellationToken ct = default)
        => WriteWordsSingleRequestCoreAsync(client, relayHops: null, device, ExpandDWords(values), ct);

    public static Task WriteDWordsSingleRequestAsync(this QueuedToyopucDeviceClient client, string device, IReadOnlyList<uint> values, CancellationToken ct = default)
        => client.ExecuteAsync(inner => WriteWordsSingleRequestCoreAsync(inner, client.RelayHops, device, ExpandDWords(values), ct), ct);

    public static async Task<ushort[]> ReadWordsChunkedAsync(this ToyopucDeviceClient client, string device, int count, int maxWordsPerRequest, CancellationToken ct = default)
    {
        ValidateChunkArguments(count, maxWordsPerRequest, nameof(count), nameof(maxWordsPerRequest));
        var start = client.ResolveDevice(device);
        EnsureWordResolved(start, nameof(device), "ReadWordsChunkedAsync()");
        var result = new ushort[count];
        var offset = 0;
        while (offset < count)
        {
            var chunkCount = Math.Min(maxWordsPerRequest, count - offset);
            var chunkDevice = ToyopucAddress.Format(start, checked(start.Index + offset));
            var chunk = await ReadWordsSingleRequestCoreAsync(client, relayHops: null, chunkDevice, chunkCount, ct).ConfigureAwait(false);
            Array.Copy(chunk, 0, result, offset, chunkCount);
            offset += chunkCount;
        }
        return result;
    }

    public static Task<ushort[]> ReadWordsChunkedAsync(this QueuedToyopucDeviceClient client, string device, int count, int maxWordsPerRequest, CancellationToken ct = default)
        => client.ExecuteAsync(inner => ReadWordsChunkedCoreAsync(inner, client.RelayHops, device, count, maxWordsPerRequest, ct), ct);

    public static async Task<uint[]> ReadDWordsChunkedAsync(this ToyopucDeviceClient client, string device, int count, int maxDwordsPerRequest, CancellationToken ct = default)
    {
        ValidateChunkArguments(count, maxDwordsPerRequest, nameof(count), nameof(maxDwordsPerRequest));
        var start = client.ResolveDevice(device);
        EnsureWordResolved(start, nameof(device), "ReadDWordsChunkedAsync()");
        var result = new uint[count];
        var offset = 0;
        while (offset < count)
        {
            var chunkCount = Math.Min(maxDwordsPerRequest, count - offset);
            var chunkDevice = ToyopucAddress.Format(start, checked(start.Index + (offset * 2)));
            var chunk = await client.ReadDWordsSingleRequestAsync(chunkDevice, chunkCount, ct).ConfigureAwait(false);
            Array.Copy(chunk, 0, result, offset, chunkCount);
            offset += chunkCount;
        }
        return result;
    }

    public static Task<uint[]> ReadDWordsChunkedAsync(this QueuedToyopucDeviceClient client, string device, int count, int maxDwordsPerRequest, CancellationToken ct = default)
        => client.ExecuteAsync(inner => ReadDWordsChunkedCoreAsync(inner, client.RelayHops, device, count, maxDwordsPerRequest, ct), ct);

    public static async Task WriteWordsChunkedAsync(this ToyopucDeviceClient client, string device, IReadOnlyList<ushort> values, int maxWordsPerRequest, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
            throw new ToyopucProtocolError("values must not be empty");
        ValidateChunkSize(maxWordsPerRequest, nameof(maxWordsPerRequest));
        var start = client.ResolveDevice(device);
        EnsureWordResolved(start, nameof(device), "WriteWordsChunkedAsync()");
        var offset = 0;
        while (offset < values.Count)
        {
            var chunkCount = Math.Min(maxWordsPerRequest, values.Count - offset);
            var chunkDevice = ToyopucAddress.Format(start, checked(start.Index + offset));
            await client.WriteWordsSingleRequestAsync(chunkDevice, values.Skip(offset).Take(chunkCount).ToArray(), ct).ConfigureAwait(false);
            offset += chunkCount;
        }
    }

    public static Task WriteWordsChunkedAsync(this QueuedToyopucDeviceClient client, string device, IReadOnlyList<ushort> values, int maxWordsPerRequest, CancellationToken ct = default)
        => client.ExecuteAsync(inner => WriteWordsChunkedCoreAsync(inner, client.RelayHops, device, values, maxWordsPerRequest, ct), ct);

    public static async Task WriteDWordsChunkedAsync(this ToyopucDeviceClient client, string device, IReadOnlyList<uint> values, int maxDwordsPerRequest, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
            throw new ToyopucProtocolError("values must not be empty");
        ValidateChunkSize(maxDwordsPerRequest, nameof(maxDwordsPerRequest));
        var start = client.ResolveDevice(device);
        EnsureWordResolved(start, nameof(device), "WriteDWordsChunkedAsync()");
        var offset = 0;
        while (offset < values.Count)
        {
            var chunkCount = Math.Min(maxDwordsPerRequest, values.Count - offset);
            var chunkDevice = ToyopucAddress.Format(start, checked(start.Index + (offset * 2)));
            await client.WriteDWordsSingleRequestAsync(chunkDevice, values.Skip(offset).Take(chunkCount).ToArray(), ct).ConfigureAwait(false);
            offset += chunkCount;
        }
    }

    public static Task WriteDWordsChunkedAsync(this QueuedToyopucDeviceClient client, string device, IReadOnlyList<uint> values, int maxDwordsPerRequest, CancellationToken ct = default)
        => client.ExecuteAsync(inner => WriteDWordsChunkedCoreAsync(inner, client.RelayHops, device, values, maxDwordsPerRequest, ct), ct);

    public static Task<QueuedToyopucDeviceClient> OpenAndConnectAsync(ToyopucConnectionOptions options, CancellationToken ct = default)
        => ToyopucDeviceClientFactory.OpenAndConnectAsync(options, ct);

    public static Task<QueuedToyopucDeviceClient> OpenAndConnectAsync(string host, int port = 1025, CancellationToken ct = default)
        => ToyopucDeviceClientFactory.OpenAndConnectAsync(new ToyopucConnectionOptions(host) { Port = port }, ct);

    private static async Task<object> ReadTypedCoreAsync(ToyopucDeviceClient client, object? relayHops, string device, string dtype, CancellationToken ct)
    {
        switch (NormalizeDType(dtype))
        {
            case "F":
                return BitConverter.Int32BitsToSingle(unchecked((int)(await ReadDWordsSingleRequestCoreAsync(client, relayHops, device, 1, ct).ConfigureAwait(false))[0]));
            case "D":
                return (await ReadDWordsSingleRequestCoreAsync(client, relayHops, device, 1, ct).ConfigureAwait(false))[0];
            case "L":
                return unchecked((int)(await ReadDWordsSingleRequestCoreAsync(client, relayHops, device, 1, ct).ConfigureAwait(false))[0]);
            case "S":
                return unchecked((short)(await ReadWordsSingleRequestCoreAsync(client, relayHops, device, 1, ct).ConfigureAwait(false))[0]);
            default:
                return (await ReadWordsSingleRequestCoreAsync(client, relayHops, device, 1, ct).ConfigureAwait(false))[0];
        }
    }

    private static Task WriteTypedCoreAsync(ToyopucDeviceClient client, object? relayHops, string device, string dtype, object value, CancellationToken ct)
    {
        switch (NormalizeDType(dtype))
        {
            case "F":
                {
                    float single = Convert.ToSingle(value, CultureInfo.InvariantCulture);
                    return WriteWordsSingleRequestCoreAsync(client, relayHops, device, ExpandDWords([unchecked((uint)BitConverter.SingleToInt32Bits(single))]), ct);
                }
            case "D":
                return WriteWordsSingleRequestCoreAsync(client, relayHops, device, ExpandDWords([Convert.ToUInt32(value, CultureInfo.InvariantCulture)]), ct);
            case "L":
                return WriteWordsSingleRequestCoreAsync(client, relayHops, device, ExpandDWords([unchecked((uint)Convert.ToInt32(value, CultureInfo.InvariantCulture))]), ct);
            case "S":
                return WriteWordsSingleRequestCoreAsync(client, relayHops, device, [unchecked((ushort)Convert.ToInt16(value, CultureInfo.InvariantCulture))], ct);
            default:
                return WriteWordsSingleRequestCoreAsync(client, relayHops, device, [Convert.ToUInt16(value, CultureInfo.InvariantCulture)], ct);
        }
    }

    private static async Task WriteBitInWordCoreAsync(ToyopucDeviceClient client, object? relayHops, string device, int bitIndex, bool value, CancellationToken ct)
    {
        if (bitIndex is < 0 or > 15)
            throw new ArgumentOutOfRangeException(nameof(bitIndex), "bitIndex must be 0-15.");
        ushort current = (await ReadWordsSingleRequestCoreAsync(client, relayHops, device, 1, ct).ConfigureAwait(false))[0];
        int raw = value ? current | (1 << bitIndex) : current & ~(1 << bitIndex);
        await WriteWordsSingleRequestCoreAsync(client, relayHops, device, [(ushort)(raw & 0xFFFF)], ct).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyDictionary<string, object>> ReadNamedCoreAsync(ToyopucDeviceClient client, object? relayHops, IEnumerable<string> addresses, CancellationToken ct)
    {
        var result = new Dictionary<string, object>();
        foreach (var address in addresses)
        {
            var (baseAddr, dtype, bitIdx) = ParseLogicalAddress(address);
            if (dtype == "BIT_IN_WORD")
            {
                var raw = (await ReadWordsSingleRequestCoreAsync(client, relayHops, baseAddr, 1, ct).ConfigureAwait(false))[0];
                result[address] = ((raw >> (bitIdx ?? 0)) & 1) != 0;
            }
            else
            {
                result[address] = await ReadTypedCoreAsync(client, relayHops, baseAddr, dtype, ct).ConfigureAwait(false);
            }
        }
        return result;
    }

    private static async Task<ushort[]> ReadWordsSemanticViaRelayCoreAsync(ToyopucDeviceClient client, object relayHops, string device, int count, CancellationToken ct)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "count must be 1 or greater.");
        var raw = await client.RelayReadAsync(relayHops, device, count, ct).ConfigureAwait(false);
        return ConvertWordReadResult(raw, count);
    }

    private static async Task<ushort[]> ReadWordsSingleRequestCoreAsync(ToyopucDeviceClient client, object? relayHops, string device, int count, CancellationToken ct)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "count must be 1 or greater.");

        var devices = BuildSequentialWordDevices(client, device, count);
        var group = GetBatchGroupKey(devices[0]);
        if (group is null || !AllDevicesInGroup(devices, group))
            throw new ToyopucProtocolError("Single-request word access cannot cross incompatible protocol groups.");

        return relayHops is null
            ? await ReadWordsSingleRequestDirectAsync(client, devices, group, ct).ConfigureAwait(false)
            : await ReadWordsSingleRequestViaRelayAsync(client, relayHops, devices, group, ct).ConfigureAwait(false);
    }

    private static async Task<uint[]> ReadDWordsSingleRequestCoreAsync(ToyopucDeviceClient client, object? relayHops, string device, int count, CancellationToken ct)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "count must be 1 or greater.");
        var words = await ReadWordsSingleRequestCoreAsync(client, relayHops, device, checked(count * 2), ct).ConfigureAwait(false);
        return PackDWords(words);
    }

    private static async Task WriteWordsSingleRequestCoreAsync(ToyopucDeviceClient client, object? relayHops, string device, IReadOnlyList<ushort> values, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
            throw new ToyopucProtocolError("values must not be empty");

        var devices = BuildSequentialWordDevices(client, device, values.Count);
        var group = GetBatchGroupKey(devices[0]);
        if (group is null || !AllDevicesInGroup(devices, group))
            throw new ToyopucProtocolError("Single-request word write cannot cross incompatible protocol groups.");

        if (relayHops is null)
        {
            await WriteWordsSingleRequestDirectAsync(client, devices, group, values, ct).ConfigureAwait(false);
            return;
        }

        await WriteWordsSingleRequestViaRelayAsync(client, relayHops, devices, group, values, ct).ConfigureAwait(false);
    }

    private static async Task<ushort[]> ReadWordsChunkedCoreAsync(ToyopucDeviceClient client, object? relayHops, string device, int count, int maxWordsPerRequest, CancellationToken ct)
    {
        ValidateChunkArguments(count, maxWordsPerRequest, nameof(count), nameof(maxWordsPerRequest));
        var start = client.ResolveDevice(device);
        EnsureWordResolved(start, nameof(device), "ReadWordsChunkedAsync()");
        var result = new ushort[count];
        var offset = 0;
        while (offset < count)
        {
            var chunkCount = Math.Min(maxWordsPerRequest, count - offset);
            var chunkDevice = ToyopucAddress.Format(start, checked(start.Index + offset));
            var chunk = await ReadWordsSingleRequestCoreAsync(client, relayHops, chunkDevice, chunkCount, ct).ConfigureAwait(false);
            Array.Copy(chunk, 0, result, offset, chunkCount);
            offset += chunkCount;
        }
        return result;
    }

    private static async Task<uint[]> ReadDWordsChunkedCoreAsync(ToyopucDeviceClient client, object? relayHops, string device, int count, int maxDwordsPerRequest, CancellationToken ct)
    {
        ValidateChunkArguments(count, maxDwordsPerRequest, nameof(count), nameof(maxDwordsPerRequest));
        var start = client.ResolveDevice(device);
        EnsureWordResolved(start, nameof(device), "ReadDWordsChunkedAsync()");
        var result = new uint[count];
        var offset = 0;
        while (offset < count)
        {
            var chunkCount = Math.Min(maxDwordsPerRequest, count - offset);
            var chunkDevice = ToyopucAddress.Format(start, checked(start.Index + (offset * 2)));
            var chunk = await ReadDWordsSingleRequestCoreAsync(client, relayHops, chunkDevice, chunkCount, ct).ConfigureAwait(false);
            Array.Copy(chunk, 0, result, offset, chunkCount);
            offset += chunkCount;
        }
        return result;
    }

    private static async Task WriteWordsChunkedCoreAsync(ToyopucDeviceClient client, object? relayHops, string device, IReadOnlyList<ushort> values, int maxWordsPerRequest, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
            throw new ToyopucProtocolError("values must not be empty");
        ValidateChunkSize(maxWordsPerRequest, nameof(maxWordsPerRequest));

        var start = client.ResolveDevice(device);
        EnsureWordResolved(start, nameof(device), "WriteWordsChunkedAsync()");
        var offset = 0;
        while (offset < values.Count)
        {
            var chunkCount = Math.Min(maxWordsPerRequest, values.Count - offset);
            var chunkDevice = ToyopucAddress.Format(start, checked(start.Index + offset));
            await WriteWordsSingleRequestCoreAsync(client, relayHops, chunkDevice, values.Skip(offset).Take(chunkCount).ToArray(), ct).ConfigureAwait(false);
            offset += chunkCount;
        }
    }

    private static Task WriteDWordsChunkedCoreAsync(ToyopucDeviceClient client, object? relayHops, string device, IReadOnlyList<uint> values, int maxDwordsPerRequest, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
            throw new ToyopucProtocolError("values must not be empty");
        ValidateChunkSize(maxDwordsPerRequest, nameof(maxDwordsPerRequest));
        return WriteWordsChunkedCoreAsync(client, relayHops, device, ExpandDWords(values), checked(maxDwordsPerRequest * 2), ct);
    }

    private static async Task<ushort[]> ReadWordsSingleRequestDirectAsync(ToyopucDeviceClient client, IReadOnlyList<ResolvedDevice> devices, string group, CancellationToken ct)
    {
        switch (group)
        {
            case "basic-word":
                if (TryGetConsecutiveStart(devices, static device => device.BasicAddress, 1, out var basicStart))
                    return ToUShortArray(await client.ReadWordsAsync(basicStart, devices.Count, ct).ConfigureAwait(false));
                return ToUShortArray(await client.ReadWordsMultiAsync(CollectBasicAddresses(devices), ct).ConfigureAwait(false));
            case "ext-word":
                if (TryGetUniformNumber(devices, out var number) && TryGetConsecutiveStart(devices, static device => device.Address, 1, out var extStart))
                    return ToUShortArray(await client.ReadExtWordsAsync(number, extStart, devices.Count, ct).ConfigureAwait(false));
                var extData = await client.ReadExtMultiAsync(Array.Empty<(int No, int Bit, int Address)>(), Array.Empty<(int No, int Address)>(), CollectNoAddresses(devices), ct).ConfigureAwait(false);
                return ToUShortArray(ToyopucProtocol.UnpackU16LittleEndian(extData));
            case "pc10-word":
                if (TryGetConsecutivePc10BlockStart(devices, 2, out var pc10Start))
                    return ToUShortArray(ToyopucProtocol.UnpackU16LittleEndian(await client.Pc10BlockReadAsync(pc10Start, devices.Count * 2, ct).ConfigureAwait(false)));
                var multiData = await client.Pc10MultiReadAsync(BuildPc10MultiWordReadPayload(CollectAddress32Values(devices)), ct).ConfigureAwait(false);
                return ToUShortArray(ParsePc10MultiWordData(multiData, devices.Count));
            default:
                throw new ToyopucProtocolError($"Single-request word access does not support group '{group}'.");
        }
    }

    private static async Task<ushort[]> ReadWordsSingleRequestViaRelayAsync(ToyopucDeviceClient client, object relayHops, IReadOnlyList<ResolvedDevice> devices, string group, CancellationToken ct)
    {
        switch (group)
        {
            case "basic-word":
                if (TryGetConsecutiveStart(devices, static device => device.BasicAddress, 1, out var basicStart))
                {
                    var response = await client.SendViaRelayAsync(relayHops, ToyopucProtocol.BuildWordRead(basicStart, devices.Count), ct).ConfigureAwait(false);
                    EnsureRelayCommand(response, 0x1C, "Unexpected CMD in relay word-read response");
                    return ToUShortArray(ToyopucProtocol.UnpackU16LittleEndian(response.Data));
                }
                var multiBasic = await client.SendViaRelayAsync(relayHops, ToyopucProtocol.BuildMultiWordRead(CollectBasicAddresses(devices)), ct).ConfigureAwait(false);
                EnsureRelayCommand(multiBasic, 0x22, "Unexpected CMD in relay multi-word-read response");
                return ToUShortArray(ToyopucProtocol.UnpackU16LittleEndian(multiBasic.Data));
            case "ext-word":
                if (TryGetUniformNumber(devices, out var number) && TryGetConsecutiveStart(devices, static device => device.Address, 1, out var extStart))
                {
                    var response = await client.SendViaRelayAsync(relayHops, ToyopucProtocol.BuildExtWordRead(number, extStart, devices.Count), ct).ConfigureAwait(false);
                    EnsureRelayCommand(response, 0x94, "Unexpected CMD in relay ext word-read response");
                    return ToUShortArray(ToyopucProtocol.UnpackU16LittleEndian(response.Data));
                }
                var multiExt = await client.SendViaRelayAsync(relayHops, ToyopucProtocol.BuildExtMultiRead(Array.Empty<(int No, int Bit, int Address)>(), Array.Empty<(int No, int Address)>(), CollectNoAddresses(devices)), ct).ConfigureAwait(false);
                EnsureRelayCommand(multiExt, 0x98, "Unexpected CMD in relay ext multi-read response");
                return ToUShortArray(ToyopucProtocol.UnpackU16LittleEndian(multiExt.Data));
            case "pc10-word":
                if (TryGetConsecutivePc10BlockStart(devices, 2, out var pc10Start))
                {
                    var response = await client.SendViaRelayAsync(relayHops, ToyopucProtocol.BuildPc10BlockRead(pc10Start, devices.Count * 2), ct).ConfigureAwait(false);
                    EnsureRelayCommand(response, 0xC2, "Unexpected CMD in relay PC10 block-read response");
                    return ToUShortArray(ToyopucProtocol.UnpackU16LittleEndian(response.Data));
                }
                var multiPc10 = await client.SendViaRelayAsync(relayHops, ToyopucProtocol.BuildPc10MultiRead(BuildPc10MultiWordReadPayload(CollectAddress32Values(devices))), ct).ConfigureAwait(false);
                EnsureRelayCommand(multiPc10, 0xC4, "Unexpected CMD in relay PC10 multi-read response");
                return ToUShortArray(ParsePc10MultiWordData(multiPc10.Data, devices.Count));
            default:
                throw new ToyopucProtocolError($"Single-request word access does not support group '{group}'.");
        }
    }

    private static async Task WriteWordsSingleRequestDirectAsync(ToyopucDeviceClient client, IReadOnlyList<ResolvedDevice> devices, string group, IReadOnlyList<ushort> values, CancellationToken ct)
    {
        switch (group)
        {
            case "basic-word":
                if (TryGetConsecutiveStart(devices, static device => device.BasicAddress, 1, out var basicStart))
                {
                    await client.WriteWordsAsync(basicStart, ToIntArray(values), ct).ConfigureAwait(false);
                    return;
                }
                await client.WriteWordsMultiAsync(CollectBasicAddressValues(devices, values), ct).ConfigureAwait(false);
                return;
            case "ext-word":
                if (TryGetUniformNumber(devices, out var number) && TryGetConsecutiveStart(devices, static device => device.Address, 1, out var extStart))
                {
                    await client.WriteExtWordsAsync(number, extStart, ToIntArray(values), ct).ConfigureAwait(false);
                    return;
                }
                await client.WriteExtMultiAsync(Array.Empty<(int No, int Bit, int Address, int Value)>(), Array.Empty<(int No, int Address, int Value)>(), CollectNoAddressValues(devices, values), ct).ConfigureAwait(false);
                return;
            case "pc10-word":
                if (TryGetConsecutivePc10BlockStart(devices, 2, out var pc10Start))
                {
                    await client.Pc10BlockWriteAsync(pc10Start, PackWordValues(values), ct).ConfigureAwait(false);
                    return;
                }
                await client.Pc10MultiWriteAsync(PackPc10MultiWordPayload(CollectAddress32WordValues(devices, values)), ct).ConfigureAwait(false);
                return;
            default:
                throw new ToyopucProtocolError($"Single-request word write does not support group '{group}'.");
        }
    }

    private static async Task WriteWordsSingleRequestViaRelayAsync(ToyopucDeviceClient client, object relayHops, IReadOnlyList<ResolvedDevice> devices, string group, IReadOnlyList<ushort> values, CancellationToken ct)
    {
        switch (group)
        {
            case "basic-word":
                if (TryGetConsecutiveStart(devices, static device => device.BasicAddress, 1, out var basicStart))
                {
                    var response = await client.SendViaRelayAsync(relayHops, ToyopucProtocol.BuildWordWrite(basicStart, ToIntArray(values)), ct).ConfigureAwait(false);
                    EnsureRelayCommand(response, 0x1D, "Unexpected CMD in relay word-write response");
                    return;
                }
                var multiBasic = await client.SendViaRelayAsync(relayHops, ToyopucProtocol.BuildMultiWordWrite(CollectBasicAddressValues(devices, values)), ct).ConfigureAwait(false);
                EnsureRelayCommand(multiBasic, 0x23, "Unexpected CMD in relay multi-word-write response");
                return;
            case "ext-word":
                if (TryGetUniformNumber(devices, out var number) && TryGetConsecutiveStart(devices, static device => device.Address, 1, out var extStart))
                {
                    var response = await client.SendViaRelayAsync(relayHops, ToyopucProtocol.BuildExtWordWrite(number, extStart, ToIntArray(values)), ct).ConfigureAwait(false);
                    EnsureRelayCommand(response, 0x95, "Unexpected CMD in relay ext word-write response");
                    return;
                }
                var multiExt = await client.SendViaRelayAsync(relayHops, ToyopucProtocol.BuildExtMultiWrite(Array.Empty<(int No, int Bit, int Address, int Value)>(), Array.Empty<(int No, int Address, int Value)>(), CollectNoAddressValues(devices, values)), ct).ConfigureAwait(false);
                EnsureRelayCommand(multiExt, 0x99, "Unexpected CMD in relay ext multi-write response");
                return;
            case "pc10-word":
                if (TryGetConsecutivePc10BlockStart(devices, 2, out var pc10Start))
                {
                    var response = await client.SendViaRelayAsync(relayHops, ToyopucProtocol.BuildPc10BlockWrite(pc10Start, PackWordValues(values)), ct).ConfigureAwait(false);
                    EnsureRelayCommand(response, 0xC3, "Unexpected CMD in relay PC10 block-write response");
                    return;
                }
                var multiPc10 = await client.SendViaRelayAsync(relayHops, ToyopucProtocol.BuildPc10MultiWrite(PackPc10MultiWordPayload(CollectAddress32WordValues(devices, values))), ct).ConfigureAwait(false);
                EnsureRelayCommand(multiPc10, 0xC5, "Unexpected CMD in relay PC10 multi-write response");
                return;
            default:
                throw new ToyopucProtocolError($"Single-request word write does not support group '{group}'.");
        }
    }

    private static ResolvedDevice[] BuildSequentialWordDevices(ToyopucDeviceClient client, string device, int count)
    {
        var start = client.ResolveDevice(device);
        EnsureWordResolved(start, nameof(device), "Word access");
        var devices = new ResolvedDevice[count];
        devices[0] = start;
        for (var i = 1; i < count; i++)
        {
            devices[i] = client.ResolveDevice(ToyopucAddress.Format(start, checked(start.Index + i)));
        }
        return devices;
    }

    private static void EnsureWordResolved(ResolvedDevice resolved, string paramName, string methodName)
    {
        if (resolved.Unit != "word")
            throw new ArgumentException($"{methodName} requires a word device", paramName);
    }

    private static string? GetBatchGroupKey(ResolvedDevice device) => device.Scheme switch
    {
        "basic-word" => "basic-word",
        "ext-word" or "program-word" => "ext-word",
        "pc10-word" => "pc10-word",
        _ => null,
    };

    private static bool AllDevicesInGroup(IReadOnlyList<ResolvedDevice> devices, string group)
    {
        for (var i = 1; i < devices.Count; i++)
        {
            if (GetBatchGroupKey(devices[i]) != group)
                return false;
        }
        return true;
    }

    private static bool TryGetConsecutiveStart(IReadOnlyList<ResolvedDevice> devices, Func<ResolvedDevice, int?> selector, int step, out int start)
    {
        start = default;
        if (devices.Count == 0)
            return false;
        var first = selector(devices[0]);
        if (first is null)
            return false;
        start = first.Value;
        for (var i = 1; i < devices.Count; i++)
        {
            var current = selector(devices[i]);
            if (current != start + (i * step))
                return false;
        }
        return true;
    }

    private static bool TryGetConsecutivePc10BlockStart(IReadOnlyList<ResolvedDevice> devices, int step, out int start)
    {
        start = default;
        if (!TryGetConsecutiveStart(devices, static device => device.Address32, step, out start))
            return false;
        var block = devices[0].Address32!.Value >> 16;
        for (var i = 1; i < devices.Count; i++)
        {
            if ((devices[i].Address32!.Value >> 16) != block)
                return false;
        }
        return true;
    }

    private static bool TryGetUniformNumber(IReadOnlyList<ResolvedDevice> devices, out int number)
    {
        number = default;
        if (devices.Count == 0)
            return false;
        var firstNo = devices[0].No;
        if (!firstNo.HasValue)
            return false;
        number = firstNo.Value;
        for (var i = 1; i < devices.Count; i++)
        {
            if (devices[i].No != number)
                return false;
        }
        return true;
    }

    private static int[] CollectBasicAddresses(IReadOnlyList<ResolvedDevice> devices)
        => devices.Select(static device => device.BasicAddress ?? throw new ToyopucProtocolError("Missing basic address")).ToArray();

    private static (int Address, int Value)[] CollectBasicAddressValues(IReadOnlyList<ResolvedDevice> devices, IReadOnlyList<ushort> values)
    {
        var result = new (int Address, int Value)[devices.Count];
        for (var i = 0; i < devices.Count; i++)
            result[i] = (devices[i].BasicAddress ?? throw new ToyopucProtocolError("Missing basic address"), values[i]);
        return result;
    }

    private static (int No, int Address)[] CollectNoAddresses(IReadOnlyList<ResolvedDevice> devices)
    {
        var result = new (int No, int Address)[devices.Count];
        for (var i = 0; i < devices.Count; i++)
            result[i] = (devices[i].No ?? throw new ToyopucProtocolError("Missing extended number"), devices[i].Address ?? throw new ToyopucProtocolError("Missing extended address"));
        return result;
    }

    private static (int No, int Address, int Value)[] CollectNoAddressValues(IReadOnlyList<ResolvedDevice> devices, IReadOnlyList<ushort> values)
    {
        var result = new (int No, int Address, int Value)[devices.Count];
        for (var i = 0; i < devices.Count; i++)
            result[i] = (devices[i].No ?? throw new ToyopucProtocolError("Missing extended number"), devices[i].Address ?? throw new ToyopucProtocolError("Missing extended address"), values[i]);
        return result;
    }

    private static int[] CollectAddress32Values(IReadOnlyList<ResolvedDevice> devices)
        => devices.Select(static device => device.Address32 ?? throw new ToyopucProtocolError("Missing PC10 address")).ToArray();

    private static (int Address32, int Value)[] CollectAddress32WordValues(IReadOnlyList<ResolvedDevice> devices, IReadOnlyList<ushort> values)
    {
        var result = new (int Address32, int Value)[devices.Count];
        for (var i = 0; i < devices.Count; i++)
            result[i] = (devices[i].Address32 ?? throw new ToyopucProtocolError("Missing PC10 address"), values[i]);
        return result;
    }

    private static ushort[] ConvertWordReadResult(object raw, int count)
    {
        if (count == 1)
            return [Convert.ToUInt16(raw, CultureInfo.InvariantCulture)];
        var array = (object[])raw;
        var result = new ushort[array.Length];
        for (var i = 0; i < array.Length; i++)
            result[i] = Convert.ToUInt16(array[i], CultureInfo.InvariantCulture);
        return result;
    }

    private static ushort[] ToUShortArray(IReadOnlyList<int> values)
    {
        var result = new ushort[values.Count];
        for (var i = 0; i < values.Count; i++)
            result[i] = unchecked((ushort)(values[i] & 0xFFFF));
        return result;
    }

    private static int[] ToIntArray(IReadOnlyList<ushort> values)
    {
        var result = new int[values.Count];
        for (var i = 0; i < values.Count; i++)
            result[i] = values[i];
        return result;
    }

    private static uint[] PackDWords(IReadOnlyList<ushort> words)
    {
        if ((words.Count & 1) != 0)
            throw new ToyopucProtocolError("Expected an even number of words for DWord conversion.");
        var result = new uint[words.Count / 2];
        for (var i = 0; i < result.Length; i++)
            result[i] = (uint)(words[i * 2] | (words[(i * 2) + 1] << 16));
        return result;
    }

    private static ushort[] ExpandDWords(IReadOnlyList<uint> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
            throw new ToyopucProtocolError("values must not be empty");
        var result = new ushort[values.Count * 2];
        for (var i = 0; i < values.Count; i++)
        {
            result[i * 2] = unchecked((ushort)(values[i] & 0xFFFF));
            result[(i * 2) + 1] = unchecked((ushort)((values[i] >> 16) & 0xFFFF));
        }
        return result;
    }

    private static byte[] PackWordValues(IReadOnlyList<ushort> values)
    {
        var data = new byte[values.Count * 2];
        for (var i = 0; i < values.Count; i++)
            WriteU16LittleEndian(data, i * 2, values[i]);
        return data;
    }

    private static byte[] BuildPc10MultiWordReadPayload(IEnumerable<int> addresses32)
    {
        var items = addresses32.ToArray();
        var payload = new byte[4 + (items.Length * 4)];
        payload[2] = (byte)(items.Length & 0xFF);
        for (var i = 0; i < items.Length; i++)
            WriteAddress32LittleEndian(payload, 4 + (i * 4), items[i]);
        return payload;
    }

    private static byte[] PackPc10MultiWordPayload(IEnumerable<(int Address32, int Value)> addressValues)
    {
        var items = addressValues.ToArray();
        var payload = new byte[4 + (items.Length * 4) + (items.Length * 2)];
        payload[2] = (byte)(items.Length & 0xFF);
        for (var i = 0; i < items.Length; i++)
            WriteAddress32LittleEndian(payload, 4 + (i * 4), items[i].Address32);
        var valuesOffset = 4 + (items.Length * 4);
        for (var i = 0; i < items.Length; i++)
            WriteU16LittleEndian(payload, valuesOffset + (i * 2), items[i].Value);
        return payload;
    }

    private static int[] ParsePc10MultiWordData(byte[] data, int count)
    {
        if (data.Length < 4 + (count * 2))
            throw new ToyopucProtocolError("PC10 multi-word response too short");
        var values = new int[count];
        for (var i = 0; i < count; i++)
        {
            var offset = 4 + (i * 2);
            values[i] = data[offset] | (data[offset + 1] << 8);
        }
        return values;
    }

    private static void WriteU16LittleEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    private static void WriteAddress32LittleEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    private static void EnsureRelayCommand(ResponseFrame response, int expectedCommand, string message)
    {
        if (response.Cmd != expectedCommand)
            throw new ToyopucProtocolError(message);
    }

    private static string NormalizeDType(string text)
    {
        var dtype = text.Trim().TrimStart('.').ToUpperInvariant();
        return dtype switch
        {
            "U" => "U",
            "S" => "S",
            "D" => "D",
            "L" => "L",
            "F" => "F",
            _ => throw new ToyopucProtocolError($"Unsupported logical data type '{text}'."),
        };
    }

    private static (string Base, string DType, int? BitIdx) ParseLogicalAddress(string address)
    {
        if (address.Contains(':'))
        {
            var index = address.IndexOf(':');
            return (address[..index], NormalizeDType(address[(index + 1)..]), null);
        }
        if (address.Contains('.'))
        {
            var index = address.LastIndexOf('.');
            if (int.TryParse(address[(index + 1)..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var bitIndex) && bitIndex is >= 0 and <= 15)
                return (address[..index], "BIT_IN_WORD", bitIndex);
        }
        return (address, "U", null);
    }

    private static void ValidateChunkArguments(int count, int maxPerRequest, string countParam, string maxParam)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(countParam, "count must be 1 or greater.");
        ValidateChunkSize(maxPerRequest, maxParam);
    }

    private static void ValidateChunkSize(int maxPerRequest, string paramName)
    {
        if (maxPerRequest < 1)
            throw new ArgumentOutOfRangeException(paramName, "Chunk size must be 1 or greater.");
    }
}
