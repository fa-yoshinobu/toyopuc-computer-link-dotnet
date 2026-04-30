namespace PlcComm.Toyopuc.Tests;

public class AddressAndResolverTests
{
    [Fact]
    public void ResolveDevice_UnprefixedBasicWordAddress_RequiresProgramPrefix()
    {
        var ex = Assert.Throws<ArgumentException>(() => ToyopucDeviceResolver.ResolveDevice("D0100"));

        Assert.Contains("requires P1-/P2-/P3- prefix", ex.Message);
    }

    [Fact]
    public void ResolveDevice_ProgramAndFrAddresses_SelectExpectedSchemes()
    {
        var program = ToyopucDeviceResolver.ResolveDevice("P1-D0100");
        var fr = ToyopucDeviceResolver.ResolveDevice("FR000000");

        Assert.Equal("program-word", program.Scheme);
        Assert.Equal(0x01, program.No);
        Assert.Equal(0x1100, program.Address);

        Assert.Equal("pc10-word", fr.Scheme);
        Assert.Equal(0x00400000, fr.Address32);
    }

    [Fact]
    public void ResolveDevice_SingleLetterAreaCanBeFollowedByHexDigitF()
    {
        const string profile = "Nano 10GX:Compatible mode";
        var options = ToyopucAddressingOptions.FromProfile(profile);

        var parsed = ToyopucAddress.ParsePrefixedAddress("P1-DFFFF", "word").Address;
        var upperU = ToyopucDeviceResolver.ResolveDevice("U0FFFF", options, profile);
        var outOfRange = Assert.Throws<ArgumentException>(
            () => ToyopucDeviceResolver.ResolveDevice("P1-DFFFF", options, profile));

        Assert.Equal("D", parsed.Area);
        Assert.Equal(0xFFFF, parsed.Index);
        Assert.Equal("U", upperU.Area);
        Assert.Equal(0x0FFFF, upperU.Index);
        Assert.Equal("pc10-word", upperU.Scheme);
        Assert.Contains("Address out of range", outOfRange.Message);
        Assert.DoesNotContain("Unknown area", outOfRange.Message);
    }

    [Fact]
    public void ParseAddress_UnknownAreaIsRejected()
    {
        var exception = Assert.Throws<ArgumentException>(() => ToyopucAddress.ParseAddress("QF00", "word"));

        Assert.Contains("Unknown device area", exception.Message);
    }

    [Fact]
    public void ResolveDevice_UpperBitAddresses_SelectExpectedSchemes()
    {
        var prefixed = ToyopucDeviceResolver.ResolveDevice("P1-P1000", ToyopucAddressingOptions.Default);

        Assert.Throws<ArgumentException>(() => ToyopucDeviceResolver.ResolveDevice("P1000", ToyopucAddressingOptions.Default));

        Assert.Equal("program-bit", prefixed.Scheme);
        Assert.Equal(0x01, prefixed.No);
        Assert.Equal(0xC000, prefixed.Address);
        Assert.Equal(0x681000, prefixed.Address32);
    }

    [Fact]
    public void ResolveDevice_ToyopucPlusProfile_FallsBackFromPc10Families()
    {
        var options = ToyopucAddressingOptions.ToyopucPlusExtended;
        var upperU = ToyopucDeviceResolver.ResolveDevice("U08000", options);
        var eb = ToyopucDeviceResolver.ResolveDevice("EB00000", options);
        var fr = ToyopucDeviceResolver.ResolveDevice("FR000000", options);

        Assert.Equal("ext-word", upperU.Scheme);
        Assert.Equal(0x08, upperU.No);
        Assert.Equal(0x8000, upperU.Address);

        Assert.Equal("ext-word", eb.Scheme);
        Assert.Equal(0x09, eb.No);
        Assert.Equal(0x0000, eb.Address);

        Assert.Equal("ext-word", fr.Scheme);
        Assert.Equal(0x40, fr.No);
        Assert.Equal(0x0000, fr.Address);
    }

    [Fact]
    public void AddressingOptions_FromProfile_ToyopucPlusModes_DisablePc10Switches()
    {
        var standard = ToyopucAddressingOptions.FromProfile("TOYOPUC-Plus:Plus Standard mode");
        var extended = ToyopucAddressingOptions.FromProfile("TOYOPUC-Plus:Plus Extended mode");

        Assert.False(standard.UseUpperUPc10);
        Assert.False(standard.UseEbPc10);
        Assert.False(standard.UseFrPc10);

        Assert.False(extended.UseUpperUPc10);
        Assert.False(extended.UseEbPc10);
        Assert.False(extended.UseFrPc10);
    }

    [Fact]
    public void AddressingOptions_FromProfile_Nano10GxProfiles_MapExpectedFlags()
    {
        var mode = ToyopucAddressingOptions.FromProfile("Nano 10GX:Nano 10GX mode");
        var compatible = ToyopucAddressingOptions.FromProfile("Nano 10GX:Compatible mode");

        Assert.True(mode.UseUpperUPc10);
        Assert.True(mode.UseEbPc10);
        Assert.True(mode.UseFrPc10);
        Assert.True(mode.UseUpperBitPc10);
        Assert.True(mode.UseUpperMBitPc10);

        Assert.True(compatible.UseUpperUPc10);
        Assert.True(compatible.UseEbPc10);
        Assert.True(compatible.UseFrPc10);
        Assert.True(compatible.UseUpperBitPc10);
        Assert.True(compatible.UseUpperMBitPc10);
    }

    [Fact]
    public void AddressingOptions_FromProfile_Pc10GStandard_DisablesFrPc10Only()
    {
        var options = ToyopucAddressingOptions.FromProfile("PC10G:PC10 standard/PC3JG mode");

        Assert.False(options.UseUpperUPc10);
        Assert.True(options.UseEbPc10);
        Assert.False(options.UseFrPc10);
        Assert.False(options.UseUpperBitPc10);
        Assert.False(options.UseUpperMBitPc10);
    }

    [Fact]
    public void AddressingOptions_FromProfile_Pc10GMode_EnablesFrPc10()
    {
        var options = ToyopucAddressingOptions.FromProfile("PC10G:PC10 mode");

        Assert.True(options.UseUpperUPc10);
        Assert.True(options.UseEbPc10);
        Assert.True(options.UseFrPc10);
        Assert.True(options.UseUpperBitPc10);
        Assert.True(options.UseUpperMBitPc10);
    }

    [Fact]
    public void ResolveDevice_WithProfile_AllowsUpperMOnPc10GMode()
    {
        var options = ToyopucAddressingOptions.FromProfile("PC10G:PC10 mode");
        var prefixed = ToyopucDeviceResolver.ResolveDevice("P1-M1000", options, "PC10G:PC10 mode");

        Assert.Throws<ArgumentException>(() => ToyopucDeviceResolver.ResolveDevice("M1000", options, "PC10G:PC10 mode"));
        Assert.Equal("program-bit", prefixed.Scheme);
        Assert.Equal(0x01, prefixed.No);
    }

    [Fact]
    public void ResolveDevice_WithOptions_RejectsUnprefixedDerivedBasicBitAddresses()
    {
        var options = ToyopucAddressingOptions.FromProfile("PC10G:PC10 mode");

        Assert.Throws<ArgumentException>(() => ToyopucDeviceResolver.ResolveDevice("M100W", options));
        Assert.Throws<ArgumentException>(() => ToyopucDeviceResolver.ResolveDevice("M100L", options));
        Assert.Throws<ArgumentException>(() => ToyopucDeviceResolver.ResolveDevice("P17FW", options));
    }

    [Fact]
    public void ResolveDevice_WithProfile_RejectsUnprefixedBasicAddresses()
    {
        var options = ToyopucAddressingOptions.FromProfile("PC10G:PC10 mode");

        Assert.Throws<ArgumentException>(() => ToyopucDeviceResolver.ResolveDevice("D0000", options, "PC10G:PC10 mode"));
        Assert.Throws<ArgumentException>(() => ToyopucDeviceResolver.ResolveDevice("M100W", options, "PC10G:PC10 mode"));
    }

    [Fact]
    public void ResolveDevice_WithProfile_AllowsPrefixedAccessOnPlusStandard()
    {
        var options = ToyopucAddressingOptions.FromProfile("TOYOPUC-Plus:Plus Standard mode");
        var resolved = ToyopucDeviceResolver.ResolveDevice("P1-D0000", options, "TOYOPUC-Plus:Plus Standard mode");

        Assert.Equal("program-word", resolved.Scheme);
        Assert.Equal(0x01, resolved.No);
    }

    [Fact]
    public void AddressingOptions_FromProfile_Pc3JxProfiles_MapExpectedFlags()
    {
        var pc3Mode = ToyopucAddressingOptions.FromProfile("PC3JX:PC3 separate mode");
        var plusMode = ToyopucAddressingOptions.FromProfile("PC3JX:Plus expansion mode");

        Assert.False(pc3Mode.UseUpperUPc10);
        Assert.False(pc3Mode.UseEbPc10);
        Assert.False(pc3Mode.UseFrPc10);

        Assert.False(plusMode.UseUpperUPc10);
        Assert.False(plusMode.UseEbPc10);
        Assert.False(plusMode.UseFrPc10);
        Assert.False(plusMode.UseUpperBitPc10);
        Assert.False(plusMode.UseUpperMBitPc10);
    }

    [Fact]
    public void AddressingOptions_FromProfile_Pc3JgProfiles_MapExpectedFlags()
    {
        var pc3jgMode = ToyopucAddressingOptions.FromProfile("PC3JG:PC3JG mode");
        var pc3SeparateMode = ToyopucAddressingOptions.FromProfile("PC3JG:PC3 separate mode");

        Assert.False(pc3jgMode.UseUpperUPc10);
        Assert.True(pc3jgMode.UseEbPc10);
        Assert.False(pc3jgMode.UseFrPc10);
        Assert.False(pc3jgMode.UseUpperBitPc10);
        Assert.False(pc3jgMode.UseUpperMBitPc10);

        Assert.False(pc3SeparateMode.UseUpperUPc10);
        Assert.False(pc3SeparateMode.UseEbPc10);
        Assert.False(pc3SeparateMode.UseFrPc10);
        Assert.False(pc3SeparateMode.UseUpperBitPc10);
        Assert.False(pc3SeparateMode.UseUpperMBitPc10);
    }

    [Fact]
    public void EncodeAddressHelpers_ReturnExpectedValues()
    {
        var byteAddress = ToyopucAddress.EncodeByteAddress(ToyopucAddress.ParseAddress("D0100L", "byte"));
        var bitAddress = ToyopucAddress.EncodeBitAddress(ToyopucAddress.ParseAddress("M0001", "bit"));
        var frAddress = ToyopucAddress.EncodeFrWordAddr32(0x8000);

        Assert.Equal(0x2200, byteAddress);
        Assert.Equal(0x1801, bitAddress);
        Assert.Equal(0x00410000, frAddress);
    }

    [Fact]
    public void EncodeExtNoAddress_UsesExplicitGxAndGyAreas()
    {
        var gxWord = ToyopucAddress.EncodeExtNoAddress("GX", 0x0123, "word");
        var gyByte = ToyopucAddress.EncodeExtNoAddress("GY", 0x0456, "byte");

        Assert.Equal(0x07, gxWord.No);
        Assert.Equal(0x0123, gxWord.Address);
        Assert.Equal(0x07, gyByte.No);
        Assert.Equal(0x0456, gyByte.Address);
    }

    [Fact]
    public void ParseRelayHops_ParsesPreferredSyntax()
    {
        var hops = ToyopucRelay.ParseRelayHops("P1-L2:N2,P3-L4:N10");

        Assert.Collection(
            hops,
            hop =>
            {
                Assert.Equal(0x12, hop.LinkNo);
                Assert.Equal(2, hop.StationNo);
            },
            hop =>
            {
                Assert.Equal(0x34, hop.LinkNo);
                Assert.Equal(10, hop.StationNo);
            });
    }

    [Fact]
    public void DeviceCatalog_ReturnsExpectedAreaMetadata()
    {
        var directAreas = ToyopucDeviceCatalog.GetAreas(prefixed: false);
        var prefixedAreas = ToyopucDeviceCatalog.GetAreas(prefixed: true);
        var fr = ToyopucDeviceCatalog.GetAreaDescriptor("FR");

        Assert.Contains("FR", directAreas);
        Assert.DoesNotContain("FR", prefixedAreas);
        Assert.Equal(6, fr.AddressWidth);
        Assert.False(fr.SupportsPackedWord);
        Assert.Equal(0x1000, fr.SuggestedStartStep);
    }

    [Fact]
    public void DeviceCatalog_GetSupportedRanges_ReturnsDisjointSegments()
    {
        var genericPrefixedP = ToyopucDeviceCatalog.GetSupportedRanges("P", prefixed: true, "Generic");
        var genericDirectAreas = ToyopucDeviceCatalog.GetAreas(prefixed: false, "Generic");
        var plusStandardPrefixed = ToyopucDeviceCatalog.GetAreas(prefixed: true, "TOYOPUC-Plus:Plus Standard mode");

        Assert.DoesNotContain("P", genericDirectAreas);
        Assert.Collection(
            genericPrefixedP,
            range =>
            {
                Assert.Equal(0x0000, range.Start);
                Assert.Equal(0x01FF, range.End);
            },
            range =>
            {
                Assert.Equal(0x1000, range.Start);
                Assert.Equal(0x17FF, range.End);
            });
        Assert.Contains("D", plusStandardPrefixed);
    }

    [Fact]
    public void DeviceCatalog_FormatAddressRanges_UsesExplicitRangeSeparator()
    {
        var ranges = ToyopucDeviceCatalog.GetSupportedRanges("P", prefixed: true, "Generic");

        var text = ToyopucDeviceCatalog.FormatAddressRanges("P1-P", ranges, width: 4);

        Assert.Equal("P1-P0000..P1-P01FF, P1-P1000..P1-P17FF", text);
    }

    [Fact]
    public void DeviceProfiles_ToyopucPlusModes_ExposeExpectedRanges()
    {
        var standard = ToyopucDeviceProfiles.FromName("TOYOPUC-Plus:Plus Standard mode");
        var extended = ToyopucDeviceProfiles.FromName("TOYOPUC-Plus:Plus Extended mode");

        var standardDirectAreas = ToyopucDeviceCatalog.GetAreas(prefixed: false, standard.Name);
        var standardPrefixedAreas = ToyopucDeviceCatalog.GetAreas(prefixed: true, standard.Name);
        var extendedDirectAreas = ToyopucDeviceCatalog.GetAreas(prefixed: false, extended.Name);
        var extendedPrefixedAreas = ToyopucDeviceCatalog.GetAreas(prefixed: true, extended.Name);
        var standardPrefixedDRange = ToyopucDeviceCatalog.GetSupportedRange("D", prefixed: true, standard.Name);
        var prefixedMRange = ToyopucDeviceCatalog.GetSupportedRange("M", prefixed: true, extended.Name);
        var extendedURange = ToyopucDeviceCatalog.GetSupportedRange("U", prefixed: false, extended.Name);

        Assert.Equal(ToyopucAddressingOptions.ToyopucPlusStandard, standard.AddressingOptions);
        Assert.DoesNotContain("D", standardDirectAreas);
        Assert.DoesNotContain("U", standardDirectAreas);
        Assert.DoesNotContain("EB", standardDirectAreas);
        Assert.DoesNotContain("FR", standardDirectAreas);
        Assert.DoesNotContain("B", standardDirectAreas);
        Assert.DoesNotContain("GM", standardDirectAreas);
        Assert.Contains("D", standardPrefixedAreas);
        Assert.Contains("M", standardPrefixedAreas);
        Assert.Equal(0x0FFF, standardPrefixedDRange.End);

        Assert.Equal(ToyopucAddressingOptions.ToyopucPlusExtended, extended.AddressingOptions);
        Assert.DoesNotContain("D", extendedDirectAreas);
        Assert.Contains("U", extendedDirectAreas);
        Assert.DoesNotContain("EB", extendedDirectAreas);
        Assert.DoesNotContain("FR", extendedDirectAreas);
        Assert.DoesNotContain("B", extendedDirectAreas);
        Assert.Contains("GM", extendedDirectAreas);
        Assert.Contains("D", extendedPrefixedAreas);
        Assert.Equal(0x07FF, prefixedMRange.End);
        Assert.Equal(0x07FFF, extendedURange.End);
    }

    [Fact]
    public void DeviceProfiles_Nano10GxModes_ExposeExpectedRanges()
    {
        var mode = ToyopucDeviceProfiles.FromName("Nano 10GX:Nano 10GX mode");
        var compatible = ToyopucDeviceProfiles.FromName("Nano 10GX:Compatible mode");

        var modeDirectAreas = ToyopucDeviceCatalog.GetAreas(prefixed: false, mode.Name);
        var modePrefixedAreas = ToyopucDeviceCatalog.GetAreas(prefixed: true, mode.Name);
        var compatibleDirectAreas = ToyopucDeviceCatalog.GetAreas(prefixed: false, compatible.Name);
        var compatiblePrefixedAreas = ToyopucDeviceCatalog.GetAreas(prefixed: true, compatible.Name);
        var modeURange = ToyopucDeviceCatalog.GetSupportedRange("U", prefixed: false, mode.Name);

        Assert.Equal(ToyopucAddressingOptions.Nano10GxMode, mode.AddressingOptions);
        Assert.DoesNotContain("D", modeDirectAreas);
        Assert.Contains("U", modeDirectAreas);
        Assert.Contains("EB", modeDirectAreas);
        Assert.Contains("FR", modeDirectAreas);
        Assert.DoesNotContain("B", modeDirectAreas);
        Assert.Contains("D", modePrefixedAreas);
        Assert.Equal(0x1FFFF, modeURange.End);

        Assert.Equal(ToyopucAddressingOptions.Nano10GxCompatible, compatible.AddressingOptions);
        Assert.DoesNotContain("D", compatibleDirectAreas);
        Assert.Contains("U", compatibleDirectAreas);
        Assert.Contains("EB", compatibleDirectAreas);
        Assert.Contains("FR", compatibleDirectAreas);
        Assert.Contains("D", compatiblePrefixedAreas);
    }

    [Fact]
    public void DeviceProfiles_Pc10StandardAndPc10Mode_ExposeExpectedRanges()
    {
        var standardProfile = ToyopucDeviceProfiles.FromName("PC10G:PC10 standard/PC3JG mode");
        var pc10ModeProfile = ToyopucDeviceProfiles.FromName("PC10G:PC10 mode");

        var standardDirectAreas = ToyopucDeviceCatalog.GetAreas(prefixed: false, standardProfile.Name);
        var standardPrefixedAreas = ToyopucDeviceCatalog.GetAreas(prefixed: true, standardProfile.Name);
        var pc10ModeDirectAreas = ToyopucDeviceCatalog.GetAreas(prefixed: false, pc10ModeProfile.Name);
        var pc10ModePrefixedAreas = ToyopucDeviceCatalog.GetAreas(prefixed: true, pc10ModeProfile.Name);
        var ebRange = ToyopucDeviceCatalog.GetSupportedRange("EB", prefixed: false, standardProfile.Name);

        Assert.Equal(ToyopucAddressingOptions.Pc10GStandardPc3Jg, standardProfile.AddressingOptions);
        Assert.DoesNotContain("D", standardDirectAreas);
        Assert.Contains("EB", standardDirectAreas);
        Assert.DoesNotContain("FR", standardDirectAreas);
        Assert.Contains("B", standardDirectAreas);
        Assert.Contains("D", standardPrefixedAreas);
        Assert.Equal(0x1FFFF, ebRange.End);

        Assert.Equal(ToyopucAddressingOptions.Pc10GMode, pc10ModeProfile.AddressingOptions);
        Assert.DoesNotContain("D", pc10ModeDirectAreas);
        Assert.Contains("EB", pc10ModeDirectAreas);
        Assert.Contains("FR", pc10ModeDirectAreas);
        Assert.Contains("B", pc10ModeDirectAreas);
        Assert.Contains("D", pc10ModePrefixedAreas);
    }

    [Fact]
    public void DeviceProfiles_Pc10Mode_GmDerivedAccess_FollowsManual()
    {
        const string profile = "PC10G:PC10 mode";
        var options = ToyopucAddressingOptions.FromProfile(profile);

        var gmBitRange = ToyopucDeviceCatalog.GetSupportedRange("GM", prefixed: false, profile);
        var gmDerivedRange = ToyopucDeviceCatalog.GetSupportedRange("GM", prefixed: false, unit: "word", packed: true, profile: profile);

        Assert.Equal(0xFFFF, gmBitRange.End);
        Assert.Equal(0x0FFF, gmDerivedRange.End);

        var packed = ToyopucDeviceResolver.ResolveDevice("GMFFFW", options, profile);
        Assert.Equal("GM", packed.Area);
        Assert.Equal(0x0FFF, packed.Index);
        Assert.True(packed.Packed);

        var packedShort = ToyopucDeviceResolver.ResolveDevice("GM40W", options, profile);
        Assert.Equal("GM", packedShort.Area);
        Assert.Equal(0x0040, packedShort.Index);
        Assert.True(packedShort.Packed);
        Assert.Equal(0x07, packedShort.No);
        Assert.Equal(0x1040, packedShort.Address);

        var lowByte = ToyopucDeviceResolver.ResolveDevice("GMFFFL", options, profile);
        Assert.Equal("GM", lowByte.Area);
        Assert.Equal(0x0FFF, lowByte.Index);
        Assert.Equal("byte", lowByte.Unit);
        Assert.False(lowByte.High);

        var lowByteShort = ToyopucDeviceResolver.ResolveDevice("GM40L", options, profile);
        Assert.Equal("GM", lowByteShort.Area);
        Assert.Equal(0x0040, lowByteShort.Index);
        Assert.Equal("byte", lowByteShort.Unit);
        Assert.False(lowByteShort.High);
        Assert.Equal(0x07, lowByteShort.No);
        Assert.Equal(0x2080, lowByteShort.Address);

        var highByteShort = ToyopucDeviceResolver.ResolveDevice("GM40H", options, profile);
        Assert.Equal("GM", highByteShort.Area);
        Assert.Equal(0x0040, highByteShort.Index);
        Assert.Equal("byte", highByteShort.Unit);
        Assert.True(highByteShort.High);
        Assert.Equal(0x07, highByteShort.No);
        Assert.Equal(0x2081, highByteShort.Address);

        Assert.Throws<ArgumentException>(() => ToyopucDeviceResolver.ResolveDevice("GM1000W", options, profile));
        Assert.Throws<ArgumentException>(() => ToyopucDeviceResolver.ResolveDevice("GM1000L", options, profile));

        var genericLowByte = ToyopucDeviceResolver.ResolveDevice("P1-M17FL");
        Assert.Equal("M", genericLowByte.Area);
        Assert.Equal(0x17F, genericLowByte.Index);
        Assert.Equal("byte", genericLowByte.Unit);
        Assert.False(genericLowByte.High);

        Assert.Throws<ArgumentException>(() => ToyopucDeviceResolver.ResolveDevice("M0000L"));
    }

    [Fact]
    public void DeviceProfiles_Pc3JxPresets_ExposeModeSpecificRanges()
    {
        var pc3Mode = ToyopucDeviceProfiles.FromName("PC3JX:PC3 separate mode");
        var plusMode = ToyopucDeviceProfiles.FromName("PC3JX:Plus expansion mode");

        var pc3DirectAreas = ToyopucDeviceCatalog.GetAreas(prefixed: false, pc3Mode.Name);
        var pc3PrefixedAreas = ToyopucDeviceCatalog.GetAreas(prefixed: true, pc3Mode.Name);
        var plusDirectAreas = ToyopucDeviceCatalog.GetAreas(prefixed: false, plusMode.Name);
        var plusPrefixedAreas = ToyopucDeviceCatalog.GetAreas(prefixed: true, plusMode.Name);
        var pc3DRange = ToyopucDeviceCatalog.GetSupportedRange("D", prefixed: true, pc3Mode.Name);
        var pc3PrefixedMRange = ToyopucDeviceCatalog.GetSupportedRange("M", prefixed: true, pc3Mode.Name);
        var pc3URange = ToyopucDeviceCatalog.GetSupportedRange("U", prefixed: false, pc3Mode.Name);
        var plusURange = ToyopucDeviceCatalog.GetSupportedRange("U", prefixed: false, plusMode.Name);

        Assert.Equal(ToyopucAddressingOptions.Pc3JxPc3Separate, pc3Mode.AddressingOptions);
        Assert.DoesNotContain("D", pc3DirectAreas);
        Assert.Contains("U", pc3DirectAreas);
        Assert.DoesNotContain("EB", pc3DirectAreas);
        Assert.DoesNotContain("FR", pc3DirectAreas);
        Assert.Contains("B", pc3DirectAreas);
        Assert.DoesNotContain("GM", pc3DirectAreas);
        Assert.Contains("D", pc3PrefixedAreas);
        Assert.Equal(0x2FFF, pc3DRange.End);
        Assert.Equal(0x07FF, pc3PrefixedMRange.End);
        Assert.Equal(0x07FFF, pc3URange.End);

        Assert.Equal(ToyopucAddressingOptions.Pc3JxPlusExpansion, plusMode.AddressingOptions);
        Assert.DoesNotContain("D", plusDirectAreas);
        Assert.Contains("U", plusDirectAreas);
        Assert.DoesNotContain("EB", plusDirectAreas);
        Assert.DoesNotContain("FR", plusDirectAreas);
        Assert.DoesNotContain("B", plusDirectAreas);
        Assert.Contains("GM", plusDirectAreas);
        Assert.Contains("D", plusPrefixedAreas);
        Assert.Equal(0x07FFF, plusURange.End);
    }

    [Fact]
    public void DeviceProfiles_Pc3JgPresets_ExposeModeSpecificRanges()
    {
        var pc3jgMode = ToyopucDeviceProfiles.FromName("PC3JG:PC3JG mode");
        var pc3SeparateMode = ToyopucDeviceProfiles.FromName("PC3JG:PC3 separate mode");

        var pc3jgDirectAreas = ToyopucDeviceCatalog.GetAreas(prefixed: false, pc3jgMode.Name);
        var pc3jgPrefixedAreas = ToyopucDeviceCatalog.GetAreas(prefixed: true, pc3jgMode.Name);
        var pc3SeparateDirectAreas = ToyopucDeviceCatalog.GetAreas(prefixed: false, pc3SeparateMode.Name);
        var pc3SeparatePrefixedAreas = ToyopucDeviceCatalog.GetAreas(prefixed: true, pc3SeparateMode.Name);
        var pc3jgURange = ToyopucDeviceCatalog.GetSupportedRange("U", prefixed: false, pc3jgMode.Name);
        var pc3SeparateURange = ToyopucDeviceCatalog.GetSupportedRange("U", prefixed: false, pc3SeparateMode.Name);
        var pc3SeparateEbRange = ToyopucDeviceCatalog.GetSupportedRange("EB", prefixed: false, pc3SeparateMode.Name);

        Assert.Equal(ToyopucAddressingOptions.Pc3JgMode, pc3jgMode.AddressingOptions);
        Assert.DoesNotContain("D", pc3jgDirectAreas);
        Assert.Contains("U", pc3jgDirectAreas);
        Assert.Contains("EB", pc3jgDirectAreas);
        Assert.DoesNotContain("FR", pc3jgDirectAreas);
        Assert.Contains("B", pc3jgDirectAreas);
        Assert.Contains("D", pc3jgPrefixedAreas);
        Assert.Equal(0x07FFF, pc3jgURange.End);

        Assert.Equal(ToyopucAddressingOptions.Pc3JgPc3Separate, pc3SeparateMode.AddressingOptions);
        Assert.DoesNotContain("D", pc3SeparateDirectAreas);
        Assert.Contains("U", pc3SeparateDirectAreas);
        Assert.Contains("EB", pc3SeparateDirectAreas);
        Assert.DoesNotContain("FR", pc3SeparateDirectAreas);
        Assert.Contains("B", pc3SeparateDirectAreas);
        Assert.Contains("D", pc3SeparatePrefixedAreas);
        Assert.Equal(0x07FFF, pc3SeparateURange.End);
        Assert.Equal(0x1FFFF, pc3SeparateEbRange.End);
    }

    [Fact]
    public void DeviceProfiles_GetNames_IncludesRequestedProfiles()
    {
        var names = ToyopucDeviceProfiles.GetNames();

        Assert.Contains("Generic", names);
        Assert.Contains("TOYOPUC-Plus:Plus Standard mode", names);
        Assert.Contains("TOYOPUC-Plus:Plus Extended mode", names);
        Assert.Contains("Nano 10GX:Nano 10GX mode", names);
        Assert.Contains("Nano 10GX:Compatible mode", names);
        Assert.Contains("PC10G:PC10 standard/PC3JG mode", names);
        Assert.Contains("PC10G:PC10 mode", names);
        Assert.Contains("PC3JX:PC3 separate mode", names);
        Assert.Contains("PC3JX:Plus expansion mode", names);
        Assert.Contains("PC3JG:PC3JG mode", names);
        Assert.Contains("PC3JG:PC3 separate mode", names);
    }

    [Fact]
    public void DeviceProfiles_NormalizeName_ReturnsCanonicalName()
    {
        var normalized = ToyopucDeviceProfiles.NormalizeName("PC10G:PC10 standard/PC3JG mode");

        Assert.Equal("PC10G:PC10 standard/PC3JG mode", normalized);
    }

    [Fact]
    public void DeviceProfiles_NormalizeName_ReturnsGenericName()
    {
        var normalized = ToyopucDeviceProfiles.NormalizeName("Generic");

        Assert.Equal("Generic", normalized);
    }

    [Fact]
    public void DeviceCatalog_StartAddresses_ContainOnlyResolvableCandidates()
    {
        var frStarts = ToyopucDeviceCatalog.GetSuggestedStartAddresses("FR", options: ToyopucAddressingOptions.Nano10GxCompatible);
        var prefixedMStarts = ToyopucDeviceCatalog.GetSuggestedStartAddresses("M", "P1", ToyopucAddressingOptions.Default);
        var prefixedMWordStarts = ToyopucDeviceCatalog.GetSuggestedStartAddresses("M", "P1", unit: "word", packed: true, profile: "Generic");
        var plusPrefixedDStarts = ToyopucDeviceCatalog.GetSuggestedStartAddresses("D", "P1", "TOYOPUC-Plus:Plus Extended mode");

        Assert.Contains("000000", frStarts);
        Assert.Contains("1FF000", frStarts);
        Assert.Contains("0000", prefixedMStarts);
        Assert.Contains("1000", prefixedMStarts);
        Assert.Contains("000", prefixedMWordStarts);
        Assert.Contains("100", prefixedMWordStarts);
        Assert.DoesNotContain("0000", prefixedMWordStarts);
        Assert.DoesNotContain("1010", plusPrefixedDStarts);
        Assert.Contains("0FF0", plusPrefixedDStarts);
    }
}
