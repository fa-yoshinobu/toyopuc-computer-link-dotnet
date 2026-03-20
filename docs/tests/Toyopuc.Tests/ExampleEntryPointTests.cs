namespace Toyopuc.Tests;

public sealed class ExampleEntryPointTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Theory]
    [InlineData("README.md")]
    [InlineData(@"examples\README.md")]
    [InlineData(@"examples\run_validation.ps1")]
    [InlineData(@"examples\Toyopuc.MinimalRead\Program.cs")]
    [InlineData(@"examples\Toyopuc.SmokeTest\Program.cs")]
    [InlineData(@"examples\Toyopuc.SoakMonitor\Program.cs")]
    [InlineData(@"examples\Toyopuc.WriteLimitProbe\Program.cs")]
    public void EntryPointFiles_DoNotContainSyntheticGxyAlias(string relativePath)
    {
        var text = ReadRepoFile(relativePath);
        Assert.DoesNotContain("GXY", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ExampleReadmes_UsePrefixedBasicDevicesWhenProfileIsSupplied()
    {
        var rootReadme = ReadRepoFile("README.md");
        var examplesReadme = ReadRepoFile(@"examples\README.md");

        Assert.DoesNotContain("--device D0000", rootReadme, StringComparison.Ordinal);
        Assert.DoesNotContain("--device D0000", examplesReadme, StringComparison.Ordinal);
        Assert.DoesNotContain("--device M0000", rootReadme, StringComparison.Ordinal);
        Assert.DoesNotContain("--device M0000", examplesReadme, StringComparison.Ordinal);
        Assert.DoesNotContain("--devices D0000,M0000,U08000", rootReadme, StringComparison.Ordinal);
        Assert.DoesNotContain("--devices D0000,M0000,U08000", examplesReadme, StringComparison.Ordinal);

        Assert.Contains("P1-D0000", rootReadme, StringComparison.Ordinal);
        Assert.Contains("P1-M0000", rootReadme, StringComparison.Ordinal);
        Assert.Contains("P1-D0000", examplesReadme, StringComparison.Ordinal);
        Assert.Contains("P1-M0000", examplesReadme, StringComparison.Ordinal);
    }

    [Fact]
    public void EntryPointFiles_UseShortDerivedNotation()
    {
        var combined = string.Join(
            Environment.NewLine,
            ReadRepoFile(@"examples\run_validation.ps1"),
            ReadRepoFile(@"examples\README.md"),
            ReadRepoFile("README.md"),
            ReadRepoFile(@"examples\Toyopuc.SmokeTest\Program.cs"));

        Assert.DoesNotContain("M0000W", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("EP0000W", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("EK0000W", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("EV0000W", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("ET0000W", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("EC0000W", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("EL0000W", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("EM0000W", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("EX0000W", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("EY0000W", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("GM0000W", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("GX0000W", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("GY0000W", combined, StringComparison.Ordinal);

        Assert.Contains("P1-M000W", combined, StringComparison.Ordinal);
        Assert.Contains("EP000W", combined, StringComparison.Ordinal);
        Assert.Contains("GM000W", combined, StringComparison.Ordinal);
        Assert.Contains("GX000W", combined, StringComparison.Ordinal);
        Assert.Contains("GY000W", combined, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeTestAndHelpers_DefaultToProfileSafeDevices()
    {
        var smokeTest = ReadRepoFile(@"examples\Toyopuc.SmokeTest\Program.cs");
        var minimalRead = ReadRepoFile(@"examples\Toyopuc.MinimalRead\Program.cs");
        var soakMonitor = ReadRepoFile(@"examples\Toyopuc.SoakMonitor\Program.cs");
        var writeLimitProbe = ReadRepoFile(@"examples\Toyopuc.WriteLimitProbe\Program.cs");

        Assert.Contains("public string Device { get; private init; } = \"P1-D0000\";", smokeTest, StringComparison.Ordinal);
        Assert.Contains("default: P1-D0000", smokeTest, StringComparison.Ordinal);
        Assert.Contains("P1-D0000", minimalRead, StringComparison.Ordinal);
        Assert.Contains("P1-D0000,P1-M0000,U08000", soakMonitor, StringComparison.Ordinal);
        Assert.Contains("P1-D0000:622:623:0x4100", writeLimitProbe, StringComparison.Ordinal);
    }

    [Fact]
    public void RunValidation_UsesPrefixedBasicFamiliesForProfileBoundCases()
    {
        var text = ReadRepoFile(@"examples\run_validation.ps1");

        Assert.DoesNotContain("-Profile $profile -Device \"D0000\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("-Profile $profile -Device \"M0000\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("-Profile $profile -Device \"M1000\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"--device\", \"D0000\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"--device\", \"M0000\"", text, StringComparison.Ordinal);

        Assert.Contains("-Profile $profile -Device \"P1-D0000\"", text, StringComparison.Ordinal);
        Assert.Contains("-Profile $profile -Device \"P1-M0000\"", text, StringComparison.Ordinal);
        Assert.Contains("\"--device\", \"P1-D0000\"", text, StringComparison.Ordinal);
        Assert.Contains("\"--device\", \"P1-M0000\"", text, StringComparison.Ordinal);
        Assert.Contains("\"--devices\", \"P1-D0000,U08000,EB00000,P2-D0002\"", text, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(string relativePath)
    {
        var fullPath = Path.Combine(RepoRoot, relativePath);
        return File.ReadAllText(fullPath);
    }
}
