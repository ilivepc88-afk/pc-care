using Microsoft.Win32;

namespace PcCare.Windows.Services;

internal sealed class WindowsFeatureDetector
{
    private const string AppModelPackagesPath = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";
    private readonly RegistryManager _registry;

    public WindowsFeatureDetector(RegistryManager registry)
    {
        _registry = registry;
    }

    public int WindowsBuild
    {
        get
        {
            RegistryValueState build = _registry.Read(new RegistryValueLocation(
                RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                "CurrentBuildNumber"));
            return int.TryParse(build.StringValue, out int value) ? value : 0;
        }
    }

    public bool IsBusinessEdition
    {
        get
        {
            RegistryValueState edition = _registry.Read(new RegistryValueLocation(
                RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                "EditionID"));
            string value = edition.StringValue ?? string.Empty;
            return value.Contains("Enterprise", StringComparison.OrdinalIgnoreCase) ||
                   value.Contains("Education", StringComparison.OrdinalIgnoreCase) ||
                   value.Contains("Professional", StringComparison.OrdinalIgnoreCase);
        }
    }

    public bool HasAppPackage(string packagePrefix) =>
        _registry.AnySubKeyStartsWith(RegistryHive.CurrentUser, AppModelPackagesPath, packagePrefix);

    public bool HasExecutableRegistration(string executableName)
    {
        return _registry.Read(new RegistryValueLocation(
            RegistryHive.LocalMachine,
            $@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{executableName}",
            string.Empty,
            RegistryView.Registry64)).Exists ||
            _registry.Read(new RegistryValueLocation(
                RegistryHive.LocalMachine,
                $@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{executableName}",
                string.Empty,
                RegistryView.Registry32)).Exists;
    }

    public bool HasWindowsFeedsState() => _registry.KeyExists(
        RegistryHive.CurrentUser,
        @"Software\Microsoft\Windows\CurrentVersion\Feeds");

    public bool HasWidgetsPolicyState() => _registry.KeyExists(
        RegistryHive.LocalMachine,
        @"SOFTWARE\Policies\Microsoft\Dsh");

    public bool HasPolicyValue(string subKeyPath, string valueName) =>
        _registry.Read(new RegistryValueLocation(RegistryHive.CurrentUser, subKeyPath, valueName)).Exists ||
        _registry.Read(new RegistryValueLocation(RegistryHive.LocalMachine, subKeyPath, valueName)).Exists;
}
