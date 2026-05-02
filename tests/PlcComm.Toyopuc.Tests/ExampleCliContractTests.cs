using System.Diagnostics;
using System.Text;

namespace PlcComm.Toyopuc.Tests;

public sealed class ExampleCliContractTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private const string BuildConfiguration = "Debug";

    private static readonly SemaphoreSlim BuildGate = new(1, 1);
    private static readonly HashSet<string> BuiltProjects = new(StringComparer.OrdinalIgnoreCase);

    [Theory]
    [InlineData(
        @"examples\PlcComm.Toyopuc.HighLevelSample\PlcComm.Toyopuc.HighLevelSample.csproj",
        "Toyopuc high-level sample",
        "ReadTypedAsync")]
    [InlineData(
        @"examples\PlcComm.Toyopuc.MinimalRead\PlcComm.Toyopuc.MinimalRead.csproj",
        "Toyopuc minimal read example",
        "P1-D0000")]
    [InlineData(
        @"examples\PlcComm.Toyopuc.SmokeTest\PlcComm.Toyopuc.SmokeTest.csproj",
        "Toyopuc smoke test",
        "default: P1-D0000")]
    [InlineData(
        @"examples\PlcComm.Toyopuc.SoakMonitor\PlcComm.Toyopuc.SoakMonitor.csproj",
        "Toyopuc soak monitor",
        "P1-D0000,P1-M0000,U08000")]
    [InlineData(
        @"examples\PlcComm.Toyopuc.WriteLimitProbe\PlcComm.Toyopuc.WriteLimitProbe.csproj",
        "Toyopuc safe write-limit confirmation",
        "required; no device profile is inferred")]
    [InlineData(
        @"examples\PlcComm.Toyopuc.BitPatternProbe\PlcComm.Toyopuc.BitPatternProbe.csproj",
        "Toyopuc bit-to-packed readback probe",
        "required; no device profile is inferred")]
    public async Task ExampleCli_HelpOutput_ContainsStableUsage(
        string projectPath,
        string expectedHeader,
        string expectedContract)
    {
        var result = await RunProjectAsync(projectPath, "--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(expectedHeader, result.Stdout, StringComparison.Ordinal);
        Assert.Contains(expectedContract, result.Stdout, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(@"examples\PlcComm.Toyopuc.HighLevelSample\PlcComm.Toyopuc.HighLevelSample.csproj")]
    [InlineData(@"examples\PlcComm.Toyopuc.MinimalRead\PlcComm.Toyopuc.MinimalRead.csproj")]
    [InlineData(@"examples\PlcComm.Toyopuc.WriteLimitProbe\PlcComm.Toyopuc.WriteLimitProbe.csproj")]
    [InlineData(@"examples\PlcComm.Toyopuc.BitPatternProbe\PlcComm.Toyopuc.BitPatternProbe.csproj")]
    public async Task ProfileSpecificExamples_RejectOmittedProfile(string projectPath)
    {
        var result = await RunProjectAsync(projectPath);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("profile is required", result.AllOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SmokeTest_RejectsConflictingDeviceModes()
    {
        var result = await RunProjectAsync(
            @"examples\PlcComm.Toyopuc.SmokeTest\PlcComm.Toyopuc.SmokeTest.csproj",
            "--devices",
            "P1-D0000",
            "--device",
            "P1-D0001");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains(
            "--devices cannot be combined with --device, --count, --write-value, --write-pattern, or --toggle-bit-write",
            result.AllOutput,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task SmokeTest_RejectsReadOnlyProbeWithWriteArguments()
    {
        var result = await RunProjectAsync(
            @"examples\PlcComm.Toyopuc.SmokeTest\PlcComm.Toyopuc.SmokeTest.csproj",
            "--probe-counts",
            "1,2",
            "--write-value",
            "0x1234");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains(
            "--probe-counts is read-only and cannot be combined with write options",
            result.AllOutput,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task SoakMonitor_RequiresDeviceList()
    {
        var result = await RunProjectAsync(
            @"examples\PlcComm.Toyopuc.SoakMonitor\PlcComm.Toyopuc.SoakMonitor.csproj",
            "--profile",
            "PC10G:PC10 mode");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("--devices is required", result.AllOutput, StringComparison.Ordinal);
    }

    private static async Task<CliResult> RunProjectAsync(string relativeProjectPath, params string[] appArgs)
    {
        var normalizedPath = NormalizeRelativePath(relativeProjectPath);
        await EnsureProjectBuiltAsync(normalizedPath);

        var args = new List<string>
        {
            "run",
            "--no-build",
            "--project",
            normalizedPath,
            "--",
        };
        args.AddRange(appArgs);

        return await RunDotnetAsync(args);
    }

    private static async Task EnsureProjectBuiltAsync(string relativeProjectPath)
    {
        var normalizedPath = NormalizeRelativePath(relativeProjectPath);
        if (BuiltProjects.Contains(normalizedPath))
        {
            return;
        }

        await BuildGate.WaitAsync();
        try
        {
            if (BuiltProjects.Contains(normalizedPath))
            {
                return;
            }

            var result = await RunDotnetAsync(
                "build",
                "--no-restore",
                "--nologo",
                "-v",
                "quiet",
                "-c",
                BuildConfiguration,
                normalizedPath);

            Assert.True(
                result.ExitCode == 0,
                $"Failed to build example project before CLI test.{Environment.NewLine}{result.FormatForFailure()}");

            BuiltProjects.Add(normalizedPath);
        }
        finally
        {
            BuildGate.Release();
        }
    }

    private static async Task<CliResult> RunDotnetAsync(params string[] args)
    {
        return await RunDotnetAsync((IReadOnlyList<string>)args);
    }

    private static async Task<CliResult> RunDotnetAsync(IReadOnlyList<string> args)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        process.StartInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        process.StartInfo.Environment["DOTNET_NOLOGO"] = "1";
        process.StartInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";

        foreach (var arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                stdout.AppendLine(eventArgs.Data);
            }
        };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                stderr.AppendLine(eventArgs.Data);
            }
        };

        Assert.True(process.Start(), $"Failed to start dotnet for arguments: {string.Join(" ", args)}");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            process.WaitForExit();
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
            }

            throw new TimeoutException($"Timed out running: dotnet {string.Join(" ", args)}");
        }

        return new CliResult(args.ToArray(), process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private sealed record CliResult(IReadOnlyList<string> Args, int ExitCode, string Stdout, string Stderr)
    {
        public string AllOutput => string.Concat(Stdout, Environment.NewLine, Stderr);

        public string FormatForFailure()
        {
            return string.Join(
                Environment.NewLine,
                $"Command : dotnet {string.Join(" ", Args)}",
                $"ExitCode: {ExitCode}",
                "Stdout:",
                Stdout,
                "Stderr:",
                Stderr);
        }
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        var segments = relativePath
            .Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);

        return Path.Combine(segments);
    }
}
