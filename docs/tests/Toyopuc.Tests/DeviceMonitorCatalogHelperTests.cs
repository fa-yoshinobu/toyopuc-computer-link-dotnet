using Toyopuc.DeviceMonitor;

namespace Toyopuc.Tests;

public sealed class DeviceMonitorCatalogHelperTests
{
    [Fact]
    public void NormalizeProfile_InvalidName_FallsBackToGeneric()
    {
        var normalized = DeviceMonitorCatalogHelper.NormalizeProfile("not-a-profile");

        Assert.Equal("Generic", normalized);
    }

    [Fact]
    public void GetProgramChoices_InvalidCurrentProgram_FallsBackToDirect()
    {
        var selection = DeviceMonitorCatalogHelper.GetProgramChoices("PC10G:PC10 mode", "P9");

        Assert.Equal(new[] { "P1", "P2", "P3", "Direct" }, selection.Choices);
        Assert.Equal("Direct", selection.Selected);
        Assert.True(selection.IsEnabled);
    }

    [Fact]
    public void GetAreaChoices_Pc10Mode_KeepsPrefixedAndDirectAreasSeparate()
    {
        var direct = DeviceMonitorCatalogHelper.GetAreaChoices("PC10G:PC10 mode", "Direct");
        var prefixed = DeviceMonitorCatalogHelper.GetAreaChoices("PC10G:PC10 mode", "P1");

        Assert.DoesNotContain("D", direct.Areas);
        Assert.Contains("GX", direct.Areas);
        Assert.Contains("GY", direct.Areas);
        Assert.DoesNotContain("GXY", direct.Areas);

        Assert.Contains("D", prefixed.Areas);
    }

    [Fact]
    public void GetStartAddressChoices_PackedWordArea_UsesShortDerivedWidth()
    {
        var selection = DeviceMonitorCatalogHelper.GetStartAddressChoices(
            "PC10G:PC10 mode",
            "Direct",
            "GM",
            "0x40");

        Assert.True(selection.UsesPackedWord);
        Assert.Equal(3, selection.Width);
        Assert.Equal("040", selection.Selected);
        Assert.Contains("000", selection.Candidates);
        Assert.Contains("040", selection.Candidates);
    }

    [Fact]
    public void BuildSelectionRequest_PrefixedBasicWord_RequiresExplicitPrefix()
    {
        var request = DeviceMonitorCatalogHelper.BuildSelectionRequest(
            "PC10G:PC10 mode",
            "P2",
            "D",
            "0000");

        Assert.Equal("P2-", request.Prefix);
        Assert.Equal("D", request.Area);
        Assert.Equal(4, request.Width);
        Assert.Equal(string.Empty, request.Suffix);
        Assert.Equal(0x0000, request.Ranges[0].Start);
    }

    [Fact]
    public void BuildSelectionRequest_DirectBasicWord_IsRejectedWhenProfileRequiresPrefix()
    {
        Assert.Throws<ArgumentException>(() =>
            DeviceMonitorCatalogHelper.BuildSelectionRequest(
                "PC10G:PC10 mode",
                "Direct",
                "D",
                "0000"));
    }

    [Fact]
    public void BuildSelectionRequest_GxPackedWord_PreservesExplicitAreaName()
    {
        var request = DeviceMonitorCatalogHelper.BuildSelectionRequest(
            "PC10G:PC10 mode",
            "Direct",
            "GX",
            "000");

        Assert.Equal(string.Empty, request.Prefix);
        Assert.Equal("GX", request.Area);
        Assert.Equal(3, request.Width);
        Assert.Equal("W", request.Suffix);
        Assert.Equal(0x0000, request.Ranges[0].Start);
    }
}
