namespace PcCare.Core.Services;

public static class StartupApprovedState
{
    public static bool IsKnown(byte[]? value) => value is { Length: > 0 } &&
                                                 value[0] is 0x01 or 0x02 or 0x03 or 0x06 or 0x07 or 0x08 or 0x09;

    public static bool IsEnabled(byte[]? value)
    {
        return value is not { Length: > 0 } || value[0] is 0x02 or 0x06 or 0x08 ||
               value[0] is not (0x01 or 0x03 or 0x07 or 0x09);
    }
}
