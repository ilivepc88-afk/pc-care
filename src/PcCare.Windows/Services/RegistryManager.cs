using Microsoft.Win32;

namespace PcCare.Windows.Services;

public sealed record RegistryValueLocation(
    RegistryHive Hive,
    string SubKeyPath,
    string ValueName,
    RegistryView View = RegistryView.Registry64);

public sealed record RegistryValueState(bool Exists, int? DwordValue, string? StringValue = null);

public sealed class RegistryManager
{
    public RegistryValueState Read(RegistryValueLocation location)
    {
        using RegistryKey baseKey = RegistryKey.OpenBaseKey(location.Hive, location.View);
        using RegistryKey? key = baseKey.OpenSubKey(location.SubKeyPath, writable: false);
        if (key is null || !key.GetValueNames().Contains(location.ValueName, StringComparer.OrdinalIgnoreCase))
        {
            return new RegistryValueState(false, null);
        }

        object? value = key.GetValue(location.ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return value switch
        {
            int dword => new RegistryValueState(true, dword),
            string text => new RegistryValueState(true, null, text),
            _ => new RegistryValueState(true, null, value?.ToString())
        };
    }

    public bool KeyExists(RegistryHive hive, string subKeyPath, RegistryView view = RegistryView.Registry64)
    {
        using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
        using RegistryKey? key = baseKey.OpenSubKey(subKeyPath, writable: false);
        return key is not null;
    }

    public bool AnySubKeyStartsWith(RegistryHive hive, string subKeyPath, string prefix, RegistryView view = RegistryView.Registry64)
    {
        try
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
            using RegistryKey? key = baseKey.OpenSubKey(subKeyPath, writable: false);
            return key?.GetSubKeyNames().Any(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) == true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void SetDword(RegistryValueLocation location, int value)
    {
        using RegistryKey baseKey = RegistryKey.OpenBaseKey(location.Hive, location.View);
        using RegistryKey key = baseKey.CreateSubKey(location.SubKeyPath, writable: true)
            ?? throw new IOException($"无法创建注册表路径：{location.SubKeyPath}。");
        key.SetValue(location.ValueName, value, RegistryValueKind.DWord);
    }

    public void DeleteValue(RegistryValueLocation location)
    {
        using RegistryKey baseKey = RegistryKey.OpenBaseKey(location.Hive, location.View);
        using RegistryKey? key = baseKey.OpenSubKey(location.SubKeyPath, writable: true);
        key?.DeleteValue(location.ValueName, throwOnMissingValue: false);
    }
}
