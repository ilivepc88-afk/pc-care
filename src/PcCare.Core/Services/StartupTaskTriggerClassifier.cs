namespace PcCare.Core.Services;

public static class StartupTaskTriggerClassifier
{
    public const int BootTriggerType = 8;
    public const int LogonTriggerType = 9;

    public static bool IsLoginOrStartupTrigger(int triggerType) =>
        triggerType is BootTriggerType or LogonTriggerType;
}
