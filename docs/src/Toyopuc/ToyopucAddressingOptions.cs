namespace Toyopuc;

public sealed record ToyopucAddressingOptions(
    bool UseUpperUPc10 = true,
    bool UseEbPc10 = true,
    bool UseFrPc10 = true,
    bool UseUpperBitPc10 = true,
    bool UseUpperMBitPc10 = true)
{
    public static ToyopucAddressingOptions Default { get; } = new();

    public static ToyopucAddressingOptions Generic { get; } = Default;

    public static ToyopucAddressingOptions ToyopucPlusStandard { get; } = new(
        UseUpperUPc10: false,
        UseEbPc10: false,
        UseFrPc10: false,
        UseUpperBitPc10: false,
        UseUpperMBitPc10: false);

    public static ToyopucAddressingOptions ToyopucPlusExtended { get; } = ToyopucPlusStandard;

    public static ToyopucAddressingOptions Nano10GxMode { get; } = new();

    public static ToyopucAddressingOptions Nano10GxCompatible { get; } = new();

    public static ToyopucAddressingOptions Pc10GStandardPc3Jg { get; } = new(
        UseUpperUPc10: false,
        UseEbPc10: true,
        UseFrPc10: false,
        UseUpperBitPc10: false,
        UseUpperMBitPc10: false);

    public static ToyopucAddressingOptions Pc10GMode { get; } = new();

    public static ToyopucAddressingOptions Pc3JxPc3Separate { get; } = new(
        UseUpperUPc10: false,
        UseEbPc10: false,
        UseFrPc10: false,
        UseUpperBitPc10: false,
        UseUpperMBitPc10: false);

    public static ToyopucAddressingOptions Pc3JxPlusExpansion { get; } = new(
        UseUpperUPc10: false,
        UseEbPc10: false,
        UseFrPc10: false,
        UseUpperBitPc10: false,
        UseUpperMBitPc10: false);

    public static ToyopucAddressingOptions Pc3JgMode { get; } = Pc10GStandardPc3Jg;

    public static ToyopucAddressingOptions Pc3JgPc3Separate { get; } = Pc3JxPc3Separate;

    public static ToyopucAddressingOptions FromProfile(string? profile)
    {
        return ToyopucDeviceProfiles.FromName(profile).AddressingOptions;
    }
}
