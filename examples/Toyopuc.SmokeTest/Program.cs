using System.Globalization;
using System.Text;
using Toyopuc;

ToyopucDeviceClient? plc = null;
SmokeLogger? logger = null;

try
{
    var options = SmokeTestOptions.Parse(args);
    if (options.ShowHelp)
    {
        SmokeTestOptions.PrintUsage();
        return 0;
    }

    logger = new SmokeLogger(options.LogPath);
    logger.Info($"connect : {options.Protocol}://{options.Host}:{options.Port}");
    if (options.LocalPort != 0)
    {
        logger.Info($"local   : {options.LocalPort}");
    }

    if (!string.IsNullOrWhiteSpace(options.Hops))
    {
        logger.Info($"relay   : {options.Hops}");
    }

    if (!string.IsNullOrWhiteSpace(options.LogPath))
    {
        logger.Info($"log     : {options.LogPath}");
    }

    var addressingOptions = options.BuildAddressingOptions();
    if (options.ShouldLogAddressingProfile)
    {
        logger.Info($"profile : {options.Profile}");
        logger.Info(
            $"pc10    : upper-u={(addressingOptions.UseUpperUPc10 ? "on" : "off")} eb={(addressingOptions.UseEbPc10 ? "on" : "off")} fr={(addressingOptions.UseFrPc10 ? "on" : "off")}");
    }

    plc = new ToyopucDeviceClient(
        options.Host,
        options.Port,
        localPort: options.LocalPort,
        protocol: options.Protocol,
        timeout: options.Timeout,
        retries: options.Retries,
        addressingOptions: addressingOptions,
        deviceProfile: options.Profile);
    plc.CaptureTraceFrames = options.Verbose;

    if (!options.SkipStatusRead)
    {
        PrepareTrace(plc);
        var status = string.IsNullOrWhiteSpace(options.Hops)
            ? plc.ReadCpuStatus()
            : plc.RelayReadCpuStatus(options.Hops);
        logger.Info($"status  : {status.RawBytesHex}");
        DumpFrames(logger, plc, options, "cpu-status");
    }

    if (!options.SkipClockRead)
    {
        PrepareTrace(plc);
        var clock = string.IsNullOrWhiteSpace(options.Hops)
            ? plc.ReadClock()
            : plc.RelayReadClock(options.Hops);
        logger.Info($"clock   : {clock.AsDateTime():yyyy-MM-dd HH:mm:ss}");
        DumpFrames(logger, plc, options, "clock-read");
    }

    if (!string.IsNullOrWhiteSpace(options.Suite))
    {
        return RunSuite(logger, plc, options);
    }

    if (options.IsManyDeviceRequested)
    {
        return RunManyDeviceAccess(logger, plc, options);
    }

    if (options.IsCountProbeRequested)
    {
        return RunCountProbe(logger, plc, options);
    }

    if (options.IsFrRangeDumpRequested)
    {
        return RunFrRangeDump(logger, plc, options);
    }

    if (options.IsFrRangeVerifyOnlyRequested)
    {
        return RunFrRangePatternVerify(logger, plc, options);
    }

    if (options.IsFrRangeWriteRequested)
    {
        return RunFrRangeWrite(logger, plc, options);
    }

    var runPrimaryDevice = options.DeviceRequested || string.IsNullOrWhiteSpace(options.FrDevice);
    if (runPrimaryDevice)
    {
        var primaryLabel = FormatPrimaryDeviceLabel(options.Device, options.Count);

        PrepareTrace(plc);
        var before = string.IsNullOrWhiteSpace(options.Hops)
            ? plc.Read(options.Device, options.Count)
            : plc.RelayRead(options.Hops, options.Device, options.Count);
        logger.Info($"read    : {primaryLabel} = {FormatValue(before)}");
        DumpFrames(logger, plc, options, $"read {primaryLabel}");

        if (TryResolvePrimaryWriteValue(before, options, out var writeValue))
        {
            if (string.IsNullOrWhiteSpace(options.Hops))
            {
                PrepareTrace(plc);
                plc.Write(options.Device, writeValue);
                logger.Info($"write   : {primaryLabel} <= {FormatValue(writeValue)}");
                DumpFrames(logger, plc, options, $"write {primaryLabel}");

                PrepareTrace(plc);
                var after = plc.Read(options.Device, options.Count);
                logger.Info($"verify  : {primaryLabel} = {FormatValue(after)}");
                DumpFrames(logger, plc, options, $"verify {primaryLabel}");
                EnsurePrimaryValueEqual(after, writeValue, primaryLabel, "verify");

                if (options.RestoreAfterWrite)
                {
                    PrepareTrace(plc);
                    plc.Write(options.Device, before);
                    logger.Info($"restore : {primaryLabel} <= {FormatValue(before)}");
                    DumpFrames(logger, plc, options, $"restore {primaryLabel}");

                    PrepareTrace(plc);
                    var restored = plc.Read(options.Device, options.Count);
                    logger.Info($"recheck : {primaryLabel} = {FormatValue(restored)}");
                    DumpFrames(logger, plc, options, $"recheck {primaryLabel}");
                    EnsurePrimaryValueEqual(restored, before, primaryLabel, "recheck");
                }
            }
            else
            {
                PrepareTrace(plc);
                plc.RelayWrite(options.Hops, options.Device, writeValue);
                logger.Info($"write   : {primaryLabel} <= {FormatValue(writeValue)}");
                DumpFrames(logger, plc, options, $"relay-write {primaryLabel}");

                PrepareTrace(plc);
                var after = plc.RelayRead(options.Hops, options.Device, options.Count);
                logger.Info($"verify  : {primaryLabel} = {FormatValue(after)}");
                DumpFrames(logger, plc, options, $"relay-verify {primaryLabel}");
                EnsurePrimaryValueEqual(after, writeValue, primaryLabel, "verify");

                if (options.RestoreAfterWrite)
                {
                    PrepareTrace(plc);
                    plc.RelayWrite(options.Hops, options.Device, before);
                    logger.Info($"restore : {primaryLabel} <= {FormatValue(before)}");
                    DumpFrames(logger, plc, options, $"relay-restore {primaryLabel}");

                    PrepareTrace(plc);
                    var restored = plc.RelayRead(options.Hops, options.Device, options.Count);
                    logger.Info($"recheck : {primaryLabel} = {FormatValue(restored)}");
                    DumpFrames(logger, plc, options, $"relay-recheck {primaryLabel}");
                    EnsurePrimaryValueEqual(restored, before, primaryLabel, "recheck");
                }
            }
        }
    }

    if (!string.IsNullOrWhiteSpace(options.FrDevice))
    {
        PrepareTrace(plc);
        var frBefore = string.IsNullOrWhiteSpace(options.Hops)
            ? plc.ReadFr(options.FrDevice)
            : plc.RelayReadFr(options.Hops, options.FrDevice);
        logger.Info($"fr-read : {options.FrDevice} = {FormatValue(frBefore)}");
        DumpFrames(logger, plc, options, $"fr-read {options.FrDevice}");

        if (options.FrWriteValue is not null)
        {
            if (string.IsNullOrWhiteSpace(options.Hops))
            {
                PrepareTrace(plc);
                plc.WriteFr(options.FrDevice, options.FrWriteValue.Value, commit: options.FrCommit);
                logger.Info($"fr-write: {options.FrDevice} <= {FormatValue(options.FrWriteValue.Value)} commit={options.FrCommit}");
                DumpFrames(logger, plc, options, $"fr-write {options.FrDevice}");

                PrepareTrace(plc);
                var frAfter = plc.ReadFr(options.FrDevice);
                logger.Info($"fr-verify: {options.FrDevice} = {FormatValue(frAfter)}");
                DumpFrames(logger, plc, options, $"fr-verify {options.FrDevice}");
                EnsurePrimaryValueEqual(frAfter, options.FrWriteValue.Value, options.FrDevice, "fr-verify");

                if (options.RestoreAfterWrite)
                {
                    PrepareTrace(plc);
                    plc.WriteFr(options.FrDevice, frBefore, commit: options.FrCommit);
                    logger.Info($"fr-restore: {options.FrDevice} <= {FormatValue(frBefore)} commit={options.FrCommit}");
                    DumpFrames(logger, plc, options, $"fr-restore {options.FrDevice}");

                    PrepareTrace(plc);
                    var frRestored = plc.ReadFr(options.FrDevice);
                    logger.Info($"fr-recheck: {options.FrDevice} = {FormatValue(frRestored)}");
                    DumpFrames(logger, plc, options, $"fr-recheck {options.FrDevice}");
                    EnsurePrimaryValueEqual(frRestored, frBefore, options.FrDevice, "fr-recheck");
                }
            }
            else
            {
                PrepareTrace(plc);
                plc.RelayWriteFr(options.Hops, options.FrDevice, options.FrWriteValue.Value, commit: options.FrCommit);
                logger.Info($"fr-write: {options.FrDevice} <= {FormatValue(options.FrWriteValue.Value)} commit={options.FrCommit}");
                DumpFrames(logger, plc, options, $"relay-fr-write {options.FrDevice}");

                PrepareTrace(plc);
                var frAfter = plc.RelayReadFr(options.Hops, options.FrDevice);
                logger.Info($"fr-verify: {options.FrDevice} = {FormatValue(frAfter)}");
                DumpFrames(logger, plc, options, $"relay-fr-verify {options.FrDevice}");
                EnsurePrimaryValueEqual(frAfter, options.FrWriteValue.Value, options.FrDevice, "fr-verify");

                if (options.RestoreAfterWrite)
                {
                    PrepareTrace(plc);
                    plc.RelayWriteFr(options.Hops, options.FrDevice, frBefore, commit: options.FrCommit);
                    logger.Info($"fr-restore: {options.FrDevice} <= {FormatValue(frBefore)} commit={options.FrCommit}");
                    DumpFrames(logger, plc, options, $"relay-fr-restore {options.FrDevice}");

                    PrepareTrace(plc);
                    var frRestored = plc.RelayReadFr(options.Hops, options.FrDevice);
                    logger.Info($"fr-recheck: {options.FrDevice} = {FormatValue(frRestored)}");
                    DumpFrames(logger, plc, options, $"relay-fr-recheck {options.FrDevice}");
                    EnsurePrimaryValueEqual(frRestored, frBefore, options.FrDevice, "fr-recheck");
                }
            }
        }
    }

    return 0;
}
catch (Exception exception)
{
    logger ??= new SmokeLogger(null);
    logger.Error($"error   : {exception.Message}");
    if (plc is not null)
    {
        DumpLastFramesOnError(logger, plc);
    }

    return 1;
}
finally
{
    plc?.Dispose();
    logger?.Dispose();
}

static string FormatValue(object value)
{
    return value switch
    {
        bool bit => bit ? "1" : "0",
        byte b => $"0x{b:X2}",
        int word => $"0x{word:X4}",
        Array array => "[" + string.Join(", ", array.Cast<object>().Select(FormatValue)) + "]",
        _ => value.ToString() ?? string.Empty,
    };
}

static bool TryResolvePrimaryWriteValue(object before, SmokeTestOptions options, out object writeValue)
{
    if (options.ToggleBitWrite)
    {
        if (before is bool bit)
        {
            writeValue = !bit;
            return true;
        }

        if (before is object[] bitValues)
        {
            var toggled = new bool[bitValues.Length];
            for (var i = 0; i < bitValues.Length; i++)
            {
                toggled[i] = !Convert.ToBoolean(bitValues[i], CultureInfo.InvariantCulture);
            }

            writeValue = toggled;
            return true;
        }

        throw new ArgumentException("--toggle-bit-write requires a bit device");
    }

    if (options.WriteValue is not null)
    {
        if (before is object[] sequence)
        {
            writeValue = BuildPrimaryWriteSequence(sequence, options.WriteValue.Value, options.WritePattern);
            return true;
        }

        writeValue = options.WriteValue.Value;
        return true;
    }

    writeValue = 0;
    return false;
}

static string FormatPrimaryDeviceLabel(string device, int count)
{
    return count <= 1 ? device : $"{device} count=0x{count:X}";
}

static object BuildPrimaryWriteSequence(object[] beforeValues, int baseValue, string pattern)
{
    if (beforeValues.Length == 0)
    {
        throw new ArgumentException("Sequence write requires at least one value", nameof(beforeValues));
    }

    return beforeValues[0] switch
    {
        byte => BuildNumericWriteSequence(beforeValues.Length, baseValue, pattern, 0xFF),
        int => BuildNumericWriteSequence(beforeValues.Length, baseValue, pattern, 0xFFFF),
        _ => throw new ArgumentException("--write-value with --count > 1 supports byte and word devices; use --toggle-bit-write for bit devices"),
    };
}

static int[] BuildNumericWriteSequence(int count, int baseValue, string pattern, int mask)
{
    var values = new int[count];
    for (var i = 0; i < count; i++)
    {
        values[i] = pattern switch
        {
            "fill" => baseValue & mask,
            "ramp" => (baseValue + i) & mask,
            _ => throw new ArgumentException($"Unsupported write pattern: {pattern}", nameof(pattern)),
        };
    }

    return values;
}

static int RunCountProbe(SmokeLogger logger, ToyopucDeviceClient plc, SmokeTestOptions options)
{
    var hadFailure = false;
    foreach (var count in options.ProbeCounts)
    {
        var label = FormatPrimaryDeviceLabel(options.Device, count);
        try
        {
            PrepareTrace(plc);
            _ = string.IsNullOrWhiteSpace(options.Hops)
                ? plc.Read(options.Device, count)
                : plc.RelayRead(options.Hops, options.Device, count);
            logger.Info($"probe-ok : {label}");
            DumpFrames(logger, plc, options, $"probe {label}");
        }
        catch (Exception exception)
        {
            hadFailure = true;
            logger.Error($"probe-ng : {label} => {exception.Message}");
            DumpLastFramesOnError(logger, plc);
        }

        if (options.ProbeDelayMs > 0)
        {
            Thread.Sleep(options.ProbeDelayMs);
        }
    }

    return hadFailure ? 1 : 0;
}

static int RunManyDeviceAccess(SmokeLogger logger, ToyopucDeviceClient plc, SmokeTestOptions options)
{
    var devices = options.ManyDevices.Select(static device => (object)device).ToArray();

    PrepareTrace(plc);
    var before = string.IsNullOrWhiteSpace(options.Hops)
        ? plc.ReadMany(devices)
        : plc.RelayReadMany(options.Hops, devices);
    logger.Info($"many-read   : {FormatManyResult(options.ManyDevices, before)}");
    DumpFrames(logger, plc, options, $"many-read {FormatManyDeviceLabel(options.ManyDevices)}");

    if (options.ManyWriteValues is null)
    {
        return 0;
    }

    var expected = BuildManyWriteValues(before, options.ManyWriteValues);
    var writeItems = BuildManyWriteItems(options.ManyDevices, expected);

    if (string.IsNullOrWhiteSpace(options.Hops))
    {
        PrepareTrace(plc);
        plc.WriteMany(writeItems);
        logger.Info($"many-write  : {FormatManyResult(options.ManyDevices, expected)}");
        DumpFrames(logger, plc, options, $"many-write {FormatManyDeviceLabel(options.ManyDevices)}");

        PrepareTrace(plc);
        var after = plc.ReadMany(devices);
        logger.Info($"many-verify : {FormatManyResult(options.ManyDevices, after)}");
        DumpFrames(logger, plc, options, $"many-verify {FormatManyDeviceLabel(options.ManyDevices)}");
        EnsureManyValuesEqual(after, expected, options.ManyDevices, "many-verify");

        if (options.RestoreAfterWrite)
        {
            PrepareTrace(plc);
            plc.WriteMany(BuildManyWriteItems(options.ManyDevices, before));
            logger.Info($"many-restore: {FormatManyResult(options.ManyDevices, before)}");
            DumpFrames(logger, plc, options, $"many-restore {FormatManyDeviceLabel(options.ManyDevices)}");

            PrepareTrace(plc);
            var restored = plc.ReadMany(devices);
            logger.Info($"many-recheck: {FormatManyResult(options.ManyDevices, restored)}");
            DumpFrames(logger, plc, options, $"many-recheck {FormatManyDeviceLabel(options.ManyDevices)}");
            EnsureManyValuesEqual(restored, before, options.ManyDevices, "many-recheck");
        }

        return 0;
    }

    PrepareTrace(plc);
    plc.RelayWriteMany(options.Hops, writeItems);
    logger.Info($"many-write  : {FormatManyResult(options.ManyDevices, expected)}");
    DumpFrames(logger, plc, options, $"relay-many-write {FormatManyDeviceLabel(options.ManyDevices)}");

    PrepareTrace(plc);
    var relayAfter = plc.RelayReadMany(options.Hops, devices);
    logger.Info($"many-verify : {FormatManyResult(options.ManyDevices, relayAfter)}");
    DumpFrames(logger, plc, options, $"relay-many-verify {FormatManyDeviceLabel(options.ManyDevices)}");
    EnsureManyValuesEqual(relayAfter, expected, options.ManyDevices, "many-verify");

    if (options.RestoreAfterWrite)
    {
        PrepareTrace(plc);
        plc.RelayWriteMany(options.Hops, BuildManyWriteItems(options.ManyDevices, before));
        logger.Info($"many-restore: {FormatManyResult(options.ManyDevices, before)}");
        DumpFrames(logger, plc, options, $"relay-many-restore {FormatManyDeviceLabel(options.ManyDevices)}");

        PrepareTrace(plc);
        var relayRestored = plc.RelayReadMany(options.Hops, devices);
        logger.Info($"many-recheck: {FormatManyResult(options.ManyDevices, relayRestored)}");
        DumpFrames(logger, plc, options, $"relay-many-recheck {FormatManyDeviceLabel(options.ManyDevices)}");
        EnsureManyValuesEqual(relayRestored, before, options.ManyDevices, "many-recheck");
    }

    return 0;
}

static string FormatManyDeviceLabel(IReadOnlyList<string> devices)
{
    return string.Join(", ", devices);
}

static string FormatManyResult(IReadOnlyList<string> devices, IReadOnlyList<object> values)
{
    if (devices.Count != values.Count)
    {
        throw new InvalidOperationException($"Device/value count mismatch: devices={devices.Count} values={values.Count}");
    }

    return string.Join(", ", devices.Select((device, index) => $"{device}={FormatValue(values[index])}"));
}

static object[] BuildManyWriteValues(IReadOnlyList<object> beforeValues, IReadOnlyList<int> requestedValues)
{
    if (beforeValues.Count != requestedValues.Count)
    {
        throw new InvalidOperationException($"Write value count mismatch: before={beforeValues.Count} requested={requestedValues.Count}");
    }

    var values = new object[requestedValues.Count];
    for (var i = 0; i < requestedValues.Count; i++)
    {
        values[i] = beforeValues[i] switch
        {
            bool => (requestedValues[i] & 0x01) != 0,
            byte => (byte)(requestedValues[i] & 0xFF),
            int => requestedValues[i] & 0xFFFF,
            _ => throw new ArgumentException($"Unsupported device type for --devices at index {i}: {beforeValues[i].GetType().Name}"),
        };
    }

    return values;
}

static KeyValuePair<object, object>[] BuildManyWriteItems(IReadOnlyList<string> devices, IReadOnlyList<object> values)
{
    if (devices.Count != values.Count)
    {
        throw new InvalidOperationException($"Device/value count mismatch: devices={devices.Count} values={values.Count}");
    }

    var items = new KeyValuePair<object, object>[devices.Count];
    for (var i = 0; i < devices.Count; i++)
    {
        items[i] = new KeyValuePair<object, object>(devices[i], values[i]);
    }

    return items;
}

static void EnsureManyValuesEqual(IReadOnlyList<object> actual, IReadOnlyList<object> expected, IReadOnlyList<string> devices, string label)
{
    if (actual.Count != expected.Count || actual.Count != devices.Count)
    {
        throw new InvalidOperationException(
            $"{label} count mismatch: actual={actual.Count} expected={expected.Count} devices={devices.Count}");
    }

    for (var i = 0; i < actual.Count; i++)
    {
        if (ManyValuesEqual(actual[i], expected[i]))
        {
            continue;
        }

        throw new InvalidOperationException(
            $"{label} mismatch at {devices[i]}: expected={FormatValue(expected[i])} actual={FormatValue(actual[i])}");
    }
}

static void EnsurePrimaryValueEqual(object actual, object expected, string device, string label)
{
    if (PrimaryValuesEqual(actual, expected))
    {
        return;
    }

    throw new InvalidOperationException(
        $"{label} mismatch at {device}: expected={FormatValue(expected)} actual={FormatValue(actual)}");
}

static bool PrimaryValuesEqual(object actual, object expected)
{
    if (actual is Array || expected is Array)
    {
        if (actual is not Array actualItems || expected is not Array expectedItems || actualItems.Length != expectedItems.Length)
        {
            return false;
        }

        for (var i = 0; i < actualItems.Length; i++)
        {
            if (!PrimaryValuesEqual(actualItems.GetValue(i)!, expectedItems.GetValue(i)!))
            {
                return false;
            }
        }

        return true;
    }

    if (actual is bool || expected is bool)
    {
        return Convert.ToBoolean(actual, CultureInfo.InvariantCulture)
            == Convert.ToBoolean(expected, CultureInfo.InvariantCulture);
    }

    if (actual is byte || expected is byte)
    {
        return Convert.ToByte(actual, CultureInfo.InvariantCulture)
            == Convert.ToByte(expected, CultureInfo.InvariantCulture);
    }

    return Convert.ToInt32(actual, CultureInfo.InvariantCulture)
        == Convert.ToInt32(expected, CultureInfo.InvariantCulture);
}

static bool ManyValuesEqual(object actual, object expected)
{
    if (actual is bool || expected is bool)
    {
        return Convert.ToBoolean(actual, CultureInfo.InvariantCulture)
            == Convert.ToBoolean(expected, CultureInfo.InvariantCulture);
    }

    if (actual is byte || expected is byte)
    {
        return Convert.ToByte(actual, CultureInfo.InvariantCulture)
            == Convert.ToByte(expected, CultureInfo.InvariantCulture);
    }

    return Convert.ToInt32(actual, CultureInfo.InvariantCulture)
        == Convert.ToInt32(expected, CultureInfo.InvariantCulture);
}

static void DumpFrames(SmokeLogger logger, ToyopucDeviceClient plc, SmokeTestOptions options, string label)
{
    if (!options.Verbose)
    {
        return;
    }

    logger.Info($"detail  : {label}");
    var traces = plc.TraceFrames;
    if (traces.Count == 0)
    {
        logger.Info($"tx      : {FormatFrame(plc.LastTx)}");
        logger.Info($"rx      : {FormatFrame(plc.LastRx)}");
        return;
    }

    if (traces.Count == 1)
    {
        logger.Info($"tx      : {FormatFrame(traces[0].Tx)}");
        logger.Info($"rx      : {FormatFrame(traces[0].Rx)}");
        return;
    }

    for (var i = 0; i < traces.Count; i++)
    {
        logger.Info($"trace   : {i + 1}/{traces.Count}");
        logger.Info($"tx      : {FormatFrame(traces[i].Tx)}");
        logger.Info($"rx      : {FormatFrame(traces[i].Rx)}");
    }
}

static void DumpLastFramesOnError(SmokeLogger logger, ToyopucDeviceClient plc)
{
    if (plc.TraceFrames.Count > 0)
    {
        logger.Error($"trace   : {plc.TraceFrames.Count} frame(s)");
        for (var i = 0; i < plc.TraceFrames.Count; i++)
        {
            logger.Error($"trace-tx[{i + 1}] : {FormatFrame(plc.TraceFrames[i].Tx)}");
            logger.Error($"trace-rx[{i + 1}] : {FormatFrame(plc.TraceFrames[i].Rx)}");
        }
    }

    logger.Error($"last-tx : {FormatFrame(plc.LastTx)}");
    logger.Error($"last-rx : {FormatFrame(plc.LastRx)}");
}

static void PrepareTrace(ToyopucDeviceClient plc)
{
    plc.ClearTraceFrames();
}

static string FormatFrame(byte[]? frame)
{
    if (frame is null || frame.Length == 0)
    {
        return "<none>";
    }

    return string.Join(" ", frame.Select(static b => b.ToString("X2", CultureInfo.InvariantCulture)));
}

static int RunSuite(SmokeLogger logger, ToyopucDeviceClient plc, SmokeTestOptions options)
{
    var probes = BuildSuiteProbes(options.Suite);
    var ok = 0;
    var skip = 0;
    var ng = 0;

    logger.Info($"suite   : {options.Suite} ({probes.Count} probes)");
    foreach (var probe in probes)
    {
        try
        {
            PrepareTrace(plc);
            var value = probe.IsFr
                ? string.IsNullOrWhiteSpace(options.Hops)
                    ? plc.ReadFr(probe.Device)
                    : plc.RelayReadFr(options.Hops, probe.Device)
                : string.IsNullOrWhiteSpace(options.Hops)
                    ? plc.Read(probe.Device)
                    : plc.RelayRead(options.Hops, probe.Device);

            var note = probe.ExpectUnsupported ? " supported" : string.Empty;
            logger.Info($"probe   : {probe.Device} = {FormatValue(value)} [OK{note}]");
            DumpFrames(logger, plc, options, $"suite {probe.Device}");
            ok++;
        }
        catch (Exception exception)
        {
            if (probe.ExpectUnsupported && IsOutOfRangeError(exception, plc))
            {
                logger.Info($"probe   : {probe.Device} unsupported [SKIP]");
                DumpFrames(logger, plc, options, $"suite-skip {probe.Device}");
                skip++;
                continue;
            }

            logger.Error($"probe   : {probe.Device} [NG] {exception.Message}");
            DumpLastFramesOnError(logger, plc);
            ng++;
        }
    }

    logger.Info($"summary : suite={options.Suite} ok={ok} skip={skip} ng={ng}");
    return ng == 0 ? 0 : 1;
}

static int RunFrRangeWrite(SmokeLogger logger, ToyopucDeviceClient plc, SmokeTestOptions options)
{
    const int frTotalWords = 0x200000;
    const int frBlockWords = 0x8000;
    const int frVerifyChunkWords = 0x0200;
    const int relayFrChunkWords = 0x0100;

    var resolved = plc.ResolveDevice(options.FrRangeDevice);
    if (resolved.Area != "FR" || resolved.Unit != "word")
    {
        throw new ArgumentException("--fr-range-device must be an FR word device such as FR000000");
    }

    var startIndex = resolved.Index;
    var count = options.FrRangeCount ?? (frTotalWords - startIndex);
    if (count < 1)
    {
        throw new ArgumentOutOfRangeException(nameof(options), "--fr-range-count must be >= 1");
    }

    var endExclusive = checked(startIndex + count);
    if (endExclusive > frTotalWords)
    {
        throw new ArgumentOutOfRangeException(
            nameof(options),
            $"FR range exceeds 0x{frTotalWords - 1:X6}: start=FR{startIndex:X6} count=0x{count:X}");
    }

    var useRelay = !string.IsNullOrWhiteSpace(options.Hops);
    var writeChunkWords = useRelay ? relayFrChunkWords : frBlockWords;
    var verifyChunkWords = useRelay ? relayFrChunkWords : frVerifyChunkWords;
    var segments = EnumerateFrSegments(startIndex, count, frBlockWords).ToArray();
    logger.Info(
        $"fr-range: start=FR{startIndex:X6} count=0x{count:X} pattern={options.FrRangePattern} seed=0x{options.FrRangeSeed:X4} verify={(options.FrRangeVerify ? "on" : "off")} blocks={segments.Length} write-chunk=0x{writeChunkWords:X} verify-chunk=0x{verifyChunkWords:X}");

    for (var i = 0; i < segments.Length; i++)
    {
        var segment = segments[i];
        var device = $"FR{segment.StartIndex:X6}";

        if (useRelay)
        {
            foreach (var writeChunk in EnumerateFrSegments(segment.StartIndex, segment.WordCount, writeChunkWords))
            {
                var chunkDevice = $"FR{writeChunk.StartIndex:X6}";
                var chunkValues = BuildFrPatternBlock(writeChunk.StartIndex, writeChunk.WordCount, options.FrRangePattern, options.FrRangeSeed);
                PrepareTrace(plc);
                plc.RelayWriteFr(options.Hops, chunkDevice, chunkValues, commit: false, wait: false);
            }

            PrepareTrace(plc);
            plc.RelayCommitFr(options.Hops, device, wait: true);
        }
        else
        {
            var values = BuildFrPatternBlock(segment.StartIndex, segment.WordCount, options.FrRangePattern, options.FrRangeSeed);
            PrepareTrace(plc);
            plc.WriteFr(device, values, commit: true);
        }

        logger.Info($"fr-write-block: {i + 1}/{segments.Length} {device} words=0x{segment.WordCount:X}");

        if (!options.FrRangeVerify)
        {
            continue;
        }

        foreach (var verifyChunk in EnumerateFrSegments(segment.StartIndex, segment.WordCount, verifyChunkWords))
        {
            var verifyDevice = $"FR{verifyChunk.StartIndex:X6}";
            PrepareTrace(plc);
            var actual = useRelay
                ? plc.RelayReadFr(options.Hops, verifyDevice, verifyChunk.WordCount)
                : plc.ReadFr(verifyDevice, verifyChunk.WordCount);
            var actualWords = NormalizeFrReadback(actual, verifyChunk.WordCount);
            for (var offset = 0; offset < actualWords.Length; offset++)
            {
                var absoluteIndex = verifyChunk.StartIndex + offset;
                var expected = BuildFrPatternValue(absoluteIndex, options.FrRangePattern, options.FrRangeSeed);
                if (actualWords[offset] != expected)
                {
                    throw new InvalidOperationException(
                        $"FR verify mismatch at FR{absoluteIndex:X6}: expected=0x{expected:X4} actual=0x{actualWords[offset]:X4}");
                }
            }
        }

        logger.Info($"fr-verify-block: {i + 1}/{segments.Length} {device} words=0x{segment.WordCount:X}");
    }

    logger.Info($"fr-range-done: start=FR{startIndex:X6} count=0x{count:X} blocks={segments.Length}");
    return 0;
}

static int RunFrRangePatternVerify(SmokeLogger logger, ToyopucDeviceClient plc, SmokeTestOptions options)
{
    const int frTotalWords = 0x200000;
    const int frBlockWords = 0x8000;
    const int localVerifyChunkWords = 0x0200;
    const int relayVerifyChunkWords = 0x0100;

    var resolved = plc.ResolveDevice(options.FrRangeDevice);
    if (resolved.Area != "FR" || resolved.Unit != "word")
    {
        throw new ArgumentException("--fr-range-device must be an FR word device such as FR000000");
    }

    var startIndex = resolved.Index;
    var count = options.FrRangeCount ?? (frTotalWords - startIndex);
    if (count < 1)
    {
        throw new ArgumentOutOfRangeException(nameof(options), "--fr-range-count must be >= 1");
    }

    var endExclusive = checked(startIndex + count);
    if (endExclusive > frTotalWords)
    {
        throw new ArgumentOutOfRangeException(
            nameof(options),
            $"FR range exceeds 0x{frTotalWords - 1:X6}: start=FR{startIndex:X6} count=0x{count:X}");
    }

    var useRelay = !string.IsNullOrWhiteSpace(options.Hops);
    var verifyChunkWords = useRelay ? relayVerifyChunkWords : localVerifyChunkWords;
    var blocks = EnumerateFrSegments(startIndex, count, frBlockWords).ToArray();

    logger.Info(
        $"fr-range-check: start=FR{startIndex:X6} count=0x{count:X} pattern={options.FrRangePattern} seed=0x{options.FrRangeSeed:X4} verify-chunk=0x{verifyChunkWords:X} blocks={blocks.Length}");

    for (var i = 0; i < blocks.Length; i++)
    {
        var block = blocks[i];
        foreach (var verifyChunk in EnumerateFrSegments(block.StartIndex, block.WordCount, verifyChunkWords))
        {
            var verifyDevice = $"FR{verifyChunk.StartIndex:X6}";
            PrepareTrace(plc);
            var actual = useRelay
                ? plc.RelayReadFr(options.Hops, verifyDevice, verifyChunk.WordCount)
                : plc.ReadFr(verifyDevice, verifyChunk.WordCount);
            var actualWords = NormalizeFrReadback(actual, verifyChunk.WordCount);
            for (var offset = 0; offset < actualWords.Length; offset++)
            {
                var absoluteIndex = verifyChunk.StartIndex + offset;
                var expected = BuildFrPatternValue(absoluteIndex, options.FrRangePattern, options.FrRangeSeed);
                if (actualWords[offset] != expected)
                {
                    throw new InvalidOperationException(
                        $"FR verify mismatch at FR{absoluteIndex:X6}: expected=0x{expected:X4} actual=0x{actualWords[offset]:X4}");
                }
            }
        }

        logger.Info($"fr-check-block: {i + 1}/{blocks.Length} FR{block.StartIndex:X6} words=0x{block.WordCount:X}");
    }

    logger.Info($"fr-range-check-done: start=FR{startIndex:X6} count=0x{count:X} blocks={blocks.Length}");
    return 0;
}

static int RunFrRangeDump(SmokeLogger logger, ToyopucDeviceClient plc, SmokeTestOptions options)
{
    const int frTotalWords = 0x200000;
    const int frBlockWords = 0x8000;
    const int localReadChunkWords = 0x0200;
    const int relayReadChunkWords = 0x0100;

    var resolved = plc.ResolveDevice(options.FrRangeDevice);
    if (resolved.Area != "FR" || resolved.Unit != "word")
    {
        throw new ArgumentException("--fr-range-device must be an FR word device such as FR000000");
    }

    var startIndex = resolved.Index;
    var count = options.FrRangeCount ?? (frTotalWords - startIndex);
    if (count < 1)
    {
        throw new ArgumentOutOfRangeException(nameof(options), "--fr-range-count must be >= 1");
    }

    var endExclusive = checked(startIndex + count);
    if (endExclusive > frTotalWords)
    {
        throw new ArgumentOutOfRangeException(
            nameof(options),
            $"FR range exceeds 0x{frTotalWords - 1:X6}: start=FR{startIndex:X6} count=0x{count:X}");
    }

    var useRelay = !string.IsNullOrWhiteSpace(options.Hops);
    var readChunkWords = useRelay ? relayReadChunkWords : localReadChunkWords;
    var blocks = EnumerateFrSegments(startIndex, count, frBlockWords).ToArray();
    var csvPath = Path.GetFullPath(options.FrRangeDumpCsvPath!);
    var csvDirectory = Path.GetDirectoryName(csvPath);
    if (!string.IsNullOrWhiteSpace(csvDirectory))
    {
        Directory.CreateDirectory(csvDirectory);
    }

    using var writer = new StreamWriter(csvPath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    writer.WriteLine("index_hex,device,value_hex,value_dec");

    logger.Info(
        $"fr-range-dump: start=FR{startIndex:X6} count=0x{count:X} read-chunk=0x{readChunkWords:X} blocks={blocks.Length} path={csvPath}");

    for (var i = 0; i < blocks.Length; i++)
    {
        var block = blocks[i];
        foreach (var readChunk in EnumerateFrSegments(block.StartIndex, block.WordCount, readChunkWords))
        {
            var readDevice = $"FR{readChunk.StartIndex:X6}";
            PrepareTrace(plc);
            var actual = useRelay
                ? plc.RelayReadFr(options.Hops, readDevice, readChunk.WordCount)
                : plc.ReadFr(readDevice, readChunk.WordCount);
            var actualWords = NormalizeFrReadback(actual, readChunk.WordCount);
            for (var offset = 0; offset < actualWords.Length; offset++)
            {
                var absoluteIndex = readChunk.StartIndex + offset;
                writer.WriteLine(
                    $"{absoluteIndex:X6},FR{absoluteIndex:X6},0x{actualWords[offset]:X4},{actualWords[offset].ToString(CultureInfo.InvariantCulture)}");
            }
        }

        logger.Info($"fr-dump-block: {i + 1}/{blocks.Length} FR{block.StartIndex:X6} words=0x{block.WordCount:X}");
    }

    logger.Info($"fr-range-dump-done: start=FR{startIndex:X6} count=0x{count:X} blocks={blocks.Length} path={csvPath}");
    return 0;
}

static IEnumerable<FrRangeSegment> EnumerateFrSegments(int startIndex, int wordCount, int maxSegmentWords = 0x8000)
{
    const int frTotalWords = 0x200000;

    if (startIndex < 0 || startIndex >= frTotalWords)
    {
        throw new ArgumentOutOfRangeException(nameof(startIndex), "FR start index must be within 0x000000-0x1FFFFF");
    }

    if (wordCount < 1)
    {
        throw new ArgumentOutOfRangeException(nameof(wordCount), "FR word count must be >= 1");
    }

    if (maxSegmentWords < 1)
    {
        throw new ArgumentOutOfRangeException(nameof(maxSegmentWords), "FR segment size must be >= 1");
    }

    var index = startIndex;
    var remaining = wordCount;
    while (remaining > 0)
    {
        var blockRemaining = 0x8000 - (index % 0x8000);
        var segmentWords = Math.Min(remaining, Math.Min(blockRemaining, maxSegmentWords));
        yield return new FrRangeSegment(index, segmentWords);
        index += segmentWords;
        remaining -= segmentWords;
    }
}

static int[] BuildFrPatternBlock(int startIndex, int count, string pattern, int seed)
{
    var values = new int[count];
    for (var i = 0; i < count; i++)
    {
        values[i] = BuildFrPatternValue(startIndex + i, pattern, seed);
    }

    return values;
}

static int BuildFrPatternValue(int index, string pattern, int seed)
{
    return pattern switch
    {
        "ramp16" => (index + seed) & 0xFFFF,
        "xor16" => (index ^ seed) & 0xFFFF,
        "fill16" => seed & 0xFFFF,
        _ => throw new ArgumentException($"Unsupported FR range pattern: {pattern}", nameof(pattern)),
    };
}

static int[] NormalizeFrReadback(object value, int expectedCount)
{
    if (expectedCount == 1)
    {
        return new[] { Convert.ToInt32(value, CultureInfo.InvariantCulture) & 0xFFFF };
    }

    if (value is not object[] values || values.Length != expectedCount)
    {
        throw new InvalidOperationException($"FR readback returned an unexpected payload shape for count={expectedCount}");
    }

    var normalized = new int[values.Length];
    for (var i = 0; i < values.Length; i++)
    {
        normalized[i] = Convert.ToInt32(values[i], CultureInfo.InvariantCulture) & 0xFFFF;
    }

    return normalized;
}

static IReadOnlyList<SuiteProbe> BuildSuiteProbes(string? suite)
{
    if (string.IsNullOrWhiteSpace(suite))
    {
        return Array.Empty<SuiteProbe>();
    }

    if (suite.Equals("TOYOPUC-Plus:Plus Extended mode", StringComparison.OrdinalIgnoreCase))
    {
        return new[]
        {
            new SuiteProbe("program-word-d", "P1-D0000"),
            new SuiteProbe("program-bit-m", "P1-M0000"),
            new SuiteProbe("program-byte-d", "P1-D0000L"),
            new SuiteProbe("program-word-s", "P1-S0000"),
            new SuiteProbe("ext-word", "U0000"),
            new SuiteProbe("ext-word-upper-edge", "U07FFF"),
            new SuiteProbe("upper-u", "U08000", ExpectUnsupported: true),
            new SuiteProbe("eb-word", "EB00000", ExpectUnsupported: true),
            new SuiteProbe("fr-word", "FR000000", IsFr: true, ExpectUnsupported: true),
        };
    }

    if (suite.Equals("Nano 10GX:Compatible mode", StringComparison.OrdinalIgnoreCase))
    {
        return new[]
        {
            new SuiteProbe("program-word-d", "P1-D0000"),
            new SuiteProbe("program-bit-m", "P1-M0000"),
            new SuiteProbe("program-byte-d", "P1-D0000L"),
            new SuiteProbe("program-word-s", "P1-S0000"),
            new SuiteProbe("ext-word", "U0000"),
            new SuiteProbe("ext-word-upper-edge", "U07FFF"),
            new SuiteProbe("upper-u", "U08000"),
            new SuiteProbe("eb-word", "EB00000"),
            new SuiteProbe("fr-word", "FR000000", IsFr: true),
        };
    }

    if (TryResolveGeneratedSuiteProfile(suite, out var profile))
    {
        return BuildGeneratedSuiteProbes(profile);
    }

    throw new ArgumentException($"Unknown suite: {suite}", nameof(suite));
}

static bool TryResolveGeneratedSuiteProfile(string suite, out string profile)
{
    const string fullPrefix = "full:";
    var candidate = suite;
    if (suite.StartsWith(fullPrefix, StringComparison.OrdinalIgnoreCase))
    {
        candidate = suite[fullPrefix.Length..].Trim();
    }

    try
    {
        profile = ToyopucDeviceProfiles.FromName(candidate).Name;
        return true;
    }
    catch
    {
        profile = string.Empty;
        return false;
    }
}

static IReadOnlyList<SuiteProbe> BuildGeneratedSuiteProbes(string profile)
{
    var options = ToyopucAddressingOptions.FromProfile(profile);
    var probes = new List<SuiteProbe>();
    var seen = new HashSet<string>(StringComparer.Ordinal);

    AddAccessProbes(prefixed: false, prefix: null);
    AddAccessProbes(prefixed: true, prefix: "P1");
    return probes;

    void AddAccessProbes(bool prefixed, string? prefix)
    {
        foreach (var descriptor in ToyopucDeviceCatalog.GetAreaDescriptors(profile))
        {
            var defaultRanges = descriptor.GetRanges(prefixed, descriptor.SupportsPackedWord ? "bit" : "word", packed: false);
            if (defaultRanges.Count == 0)
            {
                continue;
            }

            foreach (var range in defaultRanges)
            {
                TryAddProbe(descriptor, prefix, range.Start, unit: descriptor.SupportsPackedWord ? "bit" : "word", packed: false, suffix: null);
                if (range.End != range.Start)
                {
                    TryAddProbe(descriptor, prefix, range.End, unit: descriptor.SupportsPackedWord ? "bit" : "word", packed: false, suffix: null);
                }
            }

            if (descriptor.SupportsPackedWord)
            {
                var derivedRanges = descriptor.GetRanges(prefixed, unit: "word", packed: true);
                foreach (var range in derivedRanges)
                {
                    TryAddProbe(descriptor, prefix, range.Start, unit: "word", packed: true, suffix: "W");
                    if (range.End != range.Start)
                    {
                        TryAddProbe(descriptor, prefix, range.End, unit: "word", packed: true, suffix: "W");
                    }

                    TryAddProbe(descriptor, prefix, range.Start, unit: "byte", packed: false, suffix: "L");
                    if (range.End != range.Start)
                    {
                        TryAddProbe(descriptor, prefix, range.End, unit: "byte", packed: false, suffix: "L");
                    }
                }
            }
            else if (descriptor.Area != "FR")
            {
                foreach (var range in defaultRanges)
                {
                    TryAddProbe(descriptor, prefix, range.Start, unit: "byte", packed: false, suffix: "L");
                    if (range.End != range.Start)
                    {
                        TryAddProbe(descriptor, prefix, range.End, unit: "byte", packed: false, suffix: "L");
                    }
                }
            }
        }
    }

    void TryAddProbe(ToyopucAreaDescriptor descriptor, string? prefix, int index, string unit, bool packed, string? suffix)
    {
        var device = FormatSuiteDevice(descriptor, prefix, index, unit, packed, suffix);
        if (!seen.Add(device))
        {
            return;
        }

        if (!CanResolveSuiteDevice(device, options))
        {
            return;
        }

        var labelPrefix = prefix is null ? "direct" : prefix.ToLowerInvariant();
        var labelSuffix = suffix switch
        {
            "W" => "packed",
            "L" => "byte",
            _ => "default",
        };
        var width = descriptor.GetAddressWidth(unit, packed);
        probes.Add(new SuiteProbe(
            $"{labelPrefix}-{descriptor.Area.ToLowerInvariant()}-{index.ToString($"X{width}", CultureInfo.InvariantCulture).ToLowerInvariant()}-{labelSuffix}",
            device,
            IsFr: descriptor.Area == "FR"));
    }
}

static string FormatSuiteDevice(ToyopucAreaDescriptor descriptor, string? prefix, int index, string unit, bool packed, string? suffix)
{
    var width = descriptor.GetAddressWidth(unit, packed);
    var body = $"{descriptor.Area}{index.ToString($"X{width}", CultureInfo.InvariantCulture)}{suffix}";
    return prefix is null ? body : $"{prefix}-{body}";
}

static bool CanResolveSuiteDevice(string device, ToyopucAddressingOptions options)
{
    try
    {
        _ = ToyopucDeviceResolver.ResolveDevice(device, options);
        return true;
    }
    catch
    {
        return false;
    }
}

static bool IsOutOfRangeError(Exception exception, ToyopucDeviceClient plc)
{
    if (exception.Message.Contains("Address out of range for profile", StringComparison.OrdinalIgnoreCase)
        || exception.Message.Contains("Unknown area for profile", StringComparison.OrdinalIgnoreCase)
        || exception.Message.Contains("is not available for direct access in profile", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    if (exception.Message.Contains("error_code=0x40", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    if (plc.LastRx is null)
    {
        return false;
    }

    try
    {
        var response = ToyopucProtocol.ParseResponse(plc.LastRx);
        if (response.Rc != 0x10)
        {
            return TryExtractRelayNakErrorCode(response) == 0x40;
        }

        var errorCode = response.Data.Length > 0 ? response.Data[^1] : response.Cmd;
        return errorCode == 0x40;
    }
    catch
    {
        return false;
    }
}

static int? TryExtractRelayNakErrorCode(ResponseFrame response)
{
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

internal sealed record SuiteProbe(
    string Label,
    string Device,
    bool IsFr = false,
    bool ExpectUnsupported = false);

internal sealed record FrRangeSegment(int StartIndex, int WordCount);

internal sealed record SmokeTestOptions
{
    public string Host { get; private init; } = "127.0.0.1";
    public int Port { get; private init; } = 15000;
    public string Protocol { get; private init; } = "tcp";
    public int LocalPort { get; private init; }
    public double Timeout { get; private init; } = 3.0;
    public int Retries { get; private init; }
    public string Device { get; private init; } = "P1-D0000";
    public int Count { get; private init; } = 1;
    public bool DeviceRequested { get; private init; }
    public string[] ManyDevices { get; private init; } = Array.Empty<string>();
    public int[]? ManyWriteValues { get; private init; }
    public int[] ProbeCounts { get; private init; } = Array.Empty<int>();
    public int ProbeDelayMs { get; private init; } = 1000;
    public int? WriteValue { get; private init; }
    public string WritePattern { get; private init; } = "fill";
    public bool ToggleBitWrite { get; private init; }
    public string Hops { get; private init; } = string.Empty;
    public string Suite { get; private init; } = string.Empty;
    public string FrDevice { get; private init; } = string.Empty;
    public int? FrWriteValue { get; private init; }
    public bool FrCommit { get; private init; }
    public string FrRangeDevice { get; private init; } = string.Empty;
    public int? FrRangeCount { get; private init; }
    public string FrRangePattern { get; private init; } = "ramp16";
    public int FrRangeSeed { get; private init; } = 0x55AA;
    public bool FrRangeVerify { get; private init; }
    public bool FrRangeVerifyOnly { get; private init; }
    public string? FrRangeDumpCsvPath { get; private init; }
    public bool RestoreAfterWrite { get; private init; }
    public string Profile { get; private init; } = "Generic";
    public bool? UseUpperUPc10 { get; private init; }
    public bool? UseEbPc10 { get; private init; }
    public bool? UseFrPc10 { get; private init; }
    public bool SkipStatusRead { get; private init; }
    public bool SkipClockRead { get; private init; }
    public string? LogPath { get; private init; }
    public bool Verbose { get; private init; }
    public bool ShowHelp { get; private init; }

    public bool IsFrRangeWriteRequested => !string.IsNullOrWhiteSpace(FrRangeDevice);
    public bool IsFrRangeVerifyOnlyRequested => !string.IsNullOrWhiteSpace(FrRangeDevice) && FrRangeVerifyOnly;
    public bool IsFrRangeDumpRequested =>
        !string.IsNullOrWhiteSpace(FrRangeDevice) && !string.IsNullOrWhiteSpace(FrRangeDumpCsvPath);
    public bool IsManyDeviceRequested => ManyDevices.Length > 0;
    public bool IsCountProbeRequested => ProbeCounts.Length > 0;

    public bool ShouldLogAddressingProfile =>
        !string.Equals(Profile, "Generic", StringComparison.OrdinalIgnoreCase)
        || UseUpperUPc10 is not null
        || UseEbPc10 is not null
        || UseFrPc10 is not null;

    public static SmokeTestOptions Parse(string[] args)
    {
        var options = new SmokeTestOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--help":
                case "-h":
                    options = options with { ShowHelp = true };
                    break;
                case "--host":
                    options = options with { Host = ReadValue(args, ref i, arg) };
                    break;
                case "--port":
                    options = options with { Port = ParseInt32(ReadValue(args, ref i, arg)) };
                    break;
                case "--protocol":
                    options = options with { Protocol = ReadValue(args, ref i, arg).ToLowerInvariant() };
                    break;
                case "--local-port":
                    options = options with { LocalPort = ParseInt32(ReadValue(args, ref i, arg)) };
                    break;
                case "--timeout":
                    options = options with { Timeout = double.Parse(ReadValue(args, ref i, arg), CultureInfo.InvariantCulture) };
                    break;
                case "--retries":
                    options = options with { Retries = ParseInt32(ReadValue(args, ref i, arg)) };
                    break;
                case "--device":
                    options = options with { Device = ReadValue(args, ref i, arg).ToUpperInvariant(), DeviceRequested = true };
                    break;
                case "--devices":
                    options = options with { ManyDevices = ParseDeviceList(ReadValue(args, ref i, arg)) };
                    break;
                case "--probe-counts":
                    options = options with { ProbeCounts = ParseValueList(ReadValue(args, ref i, arg)), DeviceRequested = true };
                    break;
                case "--probe-delay-ms":
                    options = options with { ProbeDelayMs = ParseInt32(ReadValue(args, ref i, arg)) };
                    break;
                case "--count":
                    options = options with { Count = ParseInt32(ReadValue(args, ref i, arg)), DeviceRequested = true };
                    break;
                case "--write-value":
                    options = options with { WriteValue = ParseInt32(ReadValue(args, ref i, arg)), DeviceRequested = true };
                    break;
                case "--write-values":
                    options = options with { ManyWriteValues = ParseValueList(ReadValue(args, ref i, arg)) };
                    break;
                case "--write-pattern":
                    options = options with { WritePattern = ReadValue(args, ref i, arg).ToLowerInvariant(), DeviceRequested = true };
                    break;
                case "--toggle-bit-write":
                    options = options with { ToggleBitWrite = true, DeviceRequested = true };
                    break;
                case "--hops":
                    options = options with { Hops = ReadValue(args, ref i, arg) };
                    break;
                case "--suite":
                    options = options with { Suite = ReadValue(args, ref i, arg) };
                    break;
                case "--fr-device":
                    options = options with { FrDevice = ReadValue(args, ref i, arg).ToUpperInvariant() };
                    break;
                case "--fr-write-value":
                    options = options with { FrWriteValue = ParseInt32(ReadValue(args, ref i, arg)) };
                    break;
                case "--fr-commit":
                    options = options with { FrCommit = true };
                    break;
                case "--fr-range-device":
                    options = options with { FrRangeDevice = ReadValue(args, ref i, arg).ToUpperInvariant() };
                    break;
                case "--fr-range-count":
                    options = options with { FrRangeCount = ParseInt32(ReadValue(args, ref i, arg)) };
                    break;
                case "--fr-range-pattern":
                    options = options with { FrRangePattern = ReadValue(args, ref i, arg).ToLowerInvariant() };
                    break;
                case "--fr-range-seed":
                    options = options with { FrRangeSeed = ParseInt32(ReadValue(args, ref i, arg)) };
                    break;
                case "--fr-range-verify":
                    options = options with { FrRangeVerify = true };
                    break;
                case "--fr-range-verify-only":
                    options = options with { FrRangeVerifyOnly = true };
                    break;
                case "--fr-range-dump-csv":
                    options = options with { FrRangeDumpCsvPath = ReadValue(args, ref i, arg) };
                    break;
                case "--restore-after-write":
                    options = options with { RestoreAfterWrite = true };
                    break;
                case "--profile":
                    options = options with { Profile = ReadValue(args, ref i, arg) };
                    break;
                case "--skip-status-read":
                    options = options with { SkipStatusRead = true };
                    break;
                case "--skip-clock-read":
                    options = options with { SkipClockRead = true };
                    break;
                case "--enable-u-pc10":
                    options = options with { UseUpperUPc10 = true };
                    break;
                case "--disable-u-pc10":
                    options = options with { UseUpperUPc10 = false };
                    break;
                case "--enable-eb-pc10":
                    options = options with { UseEbPc10 = true };
                    break;
                case "--disable-eb-pc10":
                    options = options with { UseEbPc10 = false };
                    break;
                case "--enable-fr-pc10":
                    options = options with { UseFrPc10 = true };
                    break;
                case "--disable-fr-pc10":
                    options = options with { UseFrPc10 = false };
                    break;
                case "--log":
                    options = options with { LogPath = ReadValue(args, ref i, arg) };
                    break;
                case "--verbose":
                    options = options with { Verbose = true };
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {arg}");
            }
        }

        if (options.Protocol is not ("tcp" or "udp"))
        {
            throw new ArgumentException("--protocol must be tcp or udp");
        }

        if (options.Count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "--count must be >= 1");
        }

        if (options.ToggleBitWrite && options.WriteValue is not null)
        {
            throw new ArgumentException("--toggle-bit-write cannot be combined with --write-value");
        }

        if (options.WritePattern is not ("fill" or "ramp"))
        {
            throw new ArgumentException("--write-pattern must be fill or ramp");
        }

        if (options.IsManyDeviceRequested)
        {
            if (options.DeviceRequested)
            {
                throw new ArgumentException("--devices cannot be combined with --device, --count, --write-value, --write-pattern, or --toggle-bit-write");
            }

            if (!string.IsNullOrWhiteSpace(options.FrDevice) || options.IsFrRangeWriteRequested)
            {
                throw new ArgumentException("--devices cannot be combined with --fr-device or --fr-range-device");
            }

            if (options.ToggleBitWrite)
            {
                throw new ArgumentException("--toggle-bit-write cannot be combined with --devices");
            }

            if (options.ManyWriteValues is not null && options.ManyWriteValues.Length != options.ManyDevices.Length)
            {
                throw new ArgumentException("--write-values count must match --devices count");
            }
        }
        else if (options.ManyWriteValues is not null)
        {
            throw new ArgumentException("--write-values requires --devices");
        }

        if (options.IsCountProbeRequested)
        {
            if (options.IsManyDeviceRequested)
            {
                throw new ArgumentException("--probe-counts cannot be combined with --devices");
            }

            if (!string.IsNullOrWhiteSpace(options.FrDevice) || options.IsFrRangeWriteRequested)
            {
                throw new ArgumentException("--probe-counts cannot be combined with --fr-device or --fr-range-device");
            }

            if (options.WriteValue is not null || options.ToggleBitWrite || options.RestoreAfterWrite)
            {
                throw new ArgumentException("--probe-counts is read-only and cannot be combined with write options");
            }

            if (options.ProbeDelayMs < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(args), "--probe-delay-ms must be >= 0");
            }
        }

        if (options.IsFrRangeWriteRequested)
        {
            if (options.FrWriteValue is not null || !string.IsNullOrWhiteSpace(options.FrDevice))
            {
                throw new ArgumentException("--fr-range-device cannot be combined with --fr-device or --fr-write-value");
            }

            if (options.RestoreAfterWrite)
            {
                throw new ArgumentException("--restore-after-write cannot be combined with --fr-range-device");
            }

            if (options.FrCommit)
            {
                throw new ArgumentException("--fr-commit is implicit for --fr-range-device and must not be specified");
            }

            if (options.FrRangePattern is not ("ramp16" or "xor16" or "fill16"))
            {
                throw new ArgumentException("--fr-range-pattern must be ramp16, xor16, or fill16");
            }

            if (options.FrRangeVerifyOnly && options.FrRangeVerify)
            {
                throw new ArgumentException("--fr-range-verify and --fr-range-verify-only cannot be combined");
            }

            if (options.IsFrRangeDumpRequested)
            {
                if (options.FrRangeVerify || options.FrRangeVerifyOnly)
                {
                    throw new ArgumentException("--fr-range-dump-csv cannot be combined with --fr-range-verify or --fr-range-verify-only");
                }

                if (options.RestoreAfterWrite)
                {
                    throw new ArgumentException("--restore-after-write cannot be combined with --fr-range-dump-csv");
                }
            }
        }

        _ = options.BuildAddressingOptions();
        return options;
    }

    public ToyopucAddressingOptions BuildAddressingOptions()
    {
        var options = ToyopucAddressingOptions.FromProfile(Profile);
        return options with
        {
            UseUpperUPc10 = UseUpperUPc10 ?? options.UseUpperUPc10,
            UseEbPc10 = UseEbPc10 ?? options.UseEbPc10,
            UseFrPc10 = UseFrPc10 ?? options.UseFrPc10,
        };
    }

    public static void PrintUsage()
    {
        Console.WriteLine("Toyopuc smoke test");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --host <host>             default: 127.0.0.1");
        Console.WriteLine("  --port <port>             default: 15000");
        Console.WriteLine("  --protocol <tcp|udp>      default: tcp");
        Console.WriteLine("  --local-port <port>       default: 0");
        Console.WriteLine("  --timeout <seconds>       default: 3.0");
        Console.WriteLine("  --retries <count>         default: 0");
        Console.WriteLine("  --device <addr>           default: P1-D0000");
        Console.WriteLine("  --devices <a,b,c>         discrete-point read/write set");
        Console.WriteLine("  --probe-counts <...>      comma-separated sequential counts, read-only in one connection");
        Console.WriteLine("  --probe-delay-ms <ms>     delay between probe counts, default: 1000");
        Console.WriteLine("  --count <count>           sequential device count, default: 1");
        Console.WriteLine("  --write-value <value>     optional write/readback");
        Console.WriteLine("  --write-values <...>      comma-separated values for --devices");
        Console.WriteLine("  --write-pattern <name>    fill | ramp for sequential writes, default: fill");
        Console.WriteLine("  --toggle-bit-write        optional bit write using the inverse of the current value");
        Console.WriteLine("  --hops <relay hops>       optional relay path, e.g. P1-L2:N2");
        Console.WriteLine("  --suite <name>            read-only validation suite, e.g. \"TOYOPUC-Plus:Plus Extended mode\" or \"full:PC10G:PC10 mode\"");
        Console.WriteLine("  --fr-device <addr>        optional FR device, e.g. FR000000");
        Console.WriteLine("  --fr-write-value <value>  optional FR write value");
        Console.WriteLine("  --fr-commit               commit FR write");
        Console.WriteLine("  --fr-range-device <addr>  destructive FR range write start, e.g. FR000000");
        Console.WriteLine("  --fr-range-count <count>  FR words to write; default is from start to FR end");
        Console.WriteLine("  --fr-range-pattern <name> ramp16 | xor16 | fill16");
        Console.WriteLine("  --fr-range-seed <value>   FR range pattern seed, default: 0x55AA");
        Console.WriteLine("  --fr-range-verify         read back each chunk after commit");
        Console.WriteLine("  --fr-range-verify-only    read-only FR range verification against the pattern");
        Console.WriteLine("  --fr-range-dump-csv <path> read-only FR range dump to CSV");
        Console.WriteLine("  --restore-after-write     restore original value after verify");
        Console.WriteLine("  --profile <name>          \"Generic\" | \"TOYOPUC-Plus:Plus Standard mode\" | \"TOYOPUC-Plus:Plus Extended mode\" | \"Nano 10GX:Nano 10GX mode\" | \"Nano 10GX:Compatible mode\" | \"PC10G:PC10 standard/PC3JG mode\" | \"PC10G:PC10 mode\" | \"PC3JX:PC3 separate mode\" | \"PC3JX:Plus expansion mode\" | \"PC3JG:PC3JG mode\" | \"PC3JG:PC3 separate mode\"");
        Console.WriteLine("  --skip-status-read        skip initial CPU status read");
        Console.WriteLine("  --skip-clock-read         skip initial clock read");
        Console.WriteLine("  --enable-u-pc10           force PC10 for U08000+");
        Console.WriteLine("  --disable-u-pc10          disable PC10 for U08000+");
        Console.WriteLine("  --enable-eb-pc10          force PC10 for EB");
        Console.WriteLine("  --disable-eb-pc10         disable PC10 for EB");
        Console.WriteLine("  --enable-fr-pc10          force PC10 for FR");
        Console.WriteLine("  --disable-fr-pc10         disable PC10 for FR");
        Console.WriteLine("  --log <path>              optional log file");
        Console.WriteLine("  --verbose                 print TX/RX frames");
        Console.WriteLine("  --help                    show this help");
    }

    private static string ReadValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for {option}");
        }

        index++;
        return args[index];
    }

    private static int ParseInt32(string value)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.Parse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        return int.Parse(value, CultureInfo.InvariantCulture);
    }

    private static string[] ParseDeviceList(string value)
    {
        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static item => item.ToUpperInvariant())
            .ToArray();
    }

    private static int[] ParseValueList(string value)
    {
        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseInt32)
            .ToArray();
    }
}

internal sealed class SmokeLogger : IDisposable
{
    private readonly StreamWriter? _writer;

    public SmokeLogger(string? logPath)
    {
        if (string.IsNullOrWhiteSpace(logPath))
        {
            return;
        }

        var fullPath = Path.GetFullPath(logPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(fullPath, append: true, Encoding.UTF8)
        {
            AutoFlush = true,
        };
    }

    public void Info(string message)
    {
        WriteLine(message, isError: false);
    }

    public void Error(string message)
    {
        WriteLine(message, isError: true);
    }

    public void Dispose()
    {
        _writer?.Dispose();
    }

    private void WriteLine(string message, bool isError)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}";
        if (isError)
        {
            Console.Error.WriteLine(line);
        }
        else
        {
            Console.WriteLine(line);
        }

        _writer?.WriteLine(line);
    }
}
