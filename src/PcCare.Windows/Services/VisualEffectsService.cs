using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;

namespace PcCare.Windows.Services;

public sealed class VisualEffectsService(OutputDirectoryResolver outputDirectoryResolver)
{
    private const int BackupSchemaVersion = 1;
    private const uint SpiGetDragFullWindows = 0x0026;
    private const uint SpiSetDragFullWindows = 0x0025;
    private const uint SpiGetAnimation = 0x0048;
    private const uint SpiSetAnimation = 0x0049;
    private const uint SpiGetFontSmoothing = 0x004A;
    private const uint SpiSetFontSmoothing = 0x004B;
    private const uint SpiGetFontSmoothingType = 0x200A;
    private const uint SpiSetFontSmoothingType = 0x200B;
    private const uint SpiGetUiEffects = 0x103E;
    private const uint SpiSetUiEffects = 0x103F;
    private const uint SpifUpdateIniFile = 0x0001;
    private const uint SpifSendChange = 0x0002;
    private const uint UpdateFlags = SpifUpdateIniFile | SpifSendChange;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly EffectParameter[] EffectParameters =
    [
        new("clientAreaAnimation", 0x1042, 0x1043),
        new("comboBoxAnimation", 0x1004, 0x1005),
        new("cursorShadow", 0x101A, 0x101B),
        new("dropShadow", 0x1024, 0x1025),
        new("gradientCaptions", 0x1008, 0x1009),
        new("hotTracking", 0x100E, 0x100F),
        new("listBoxSmoothScrolling", 0x1006, 0x1007),
        new("menuAnimation", 0x1002, 0x1003),
        new("menuFade", 0x1012, 0x1013),
        new("selectionFade", 0x1014, 0x1015),
        new("toolTipAnimation", 0x1016, 0x1017),
        new("toolTipFade", 0x1018, 0x1019)
    ];

    private static readonly RegistrySetting[] RegistrySettings =
    [
        new(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ListviewAlphaSelect", 0),
        new(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ListviewShadow", 0),
        new(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations", 0),
        new(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "IconsOnly", 1),
        new(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "DisablePreviewDesktop", 1),
        new(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 3),
        new(@"Software\Microsoft\Windows\DWM", "EnableAeroPeek", 0),
        new(@"Software\Microsoft\Windows\DWM", "AlwaysHibernateThumbnails", 0)
    ];

    private readonly OutputDirectoryResolver _outputDirectoryResolver = outputDirectoryResolver ?? throw new ArgumentNullException(nameof(outputDirectoryResolver));

    public bool HasBackup
    {
        get
        {
            try
            {
                return File.Exists(GetBackupPath());
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
            {
                return false;
            }
        }
    }

    public Task ApplyPerformanceProfileAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(ApplyPerformanceProfile, CancellationToken.None);
    }

    public Task RestoreAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(RestoreFromBackup, CancellationToken.None);
    }

    private void ApplyPerformanceProfile()
    {
        VisualEffectsSnapshot rollbackSnapshot = CaptureSnapshot();
        string backupPath = GetBackupPath();
        if (!File.Exists(backupPath))
        {
            WriteBackupAtomically(backupPath, rollbackSnapshot);
        }

        try
        {
            SetAnimation(enabled: false);
            SetUiParameter(SpiSetDragFullWindows, enabled: false, "拖动时显示窗口内容");
            foreach (EffectParameter effect in EffectParameters)
            {
                SetPointerBoolean(effect.SetAction, enabled: false, effect.Name);
            }

            SetPointerBoolean(SpiSetUiEffects, enabled: false, "界面视觉效果");
            SetUiParameter(SpiSetFontSmoothing, enabled: true, "字体平滑");
            ApplyRegistryProfile();
        }
        catch
        {
            TryRestoreRollback(rollbackSnapshot);
            throw;
        }
    }

    private void RestoreFromBackup()
    {
        string backupPath = GetBackupPath();
        if (!File.Exists(backupPath))
        {
            throw new InvalidOperationException("没有找到可恢复的视觉效果备份。");
        }

        VisualEffectsSnapshot targetSnapshot = ReadBackup(backupPath);
        VisualEffectsSnapshot rollbackSnapshot = CaptureSnapshot();
        try
        {
            RestoreSnapshot(targetSnapshot);
            File.Delete(backupPath);
        }
        catch
        {
            TryRestoreRollback(rollbackSnapshot);
            throw;
        }
    }

    private static VisualEffectsSnapshot CaptureSnapshot()
    {
        var effects = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (EffectParameter effect in EffectParameters)
        {
            effects.Add(effect.Name, GetBoolean(effect.GetAction, effect.Name));
        }

        var registryValues = new Dictionary<string, RegistryDwordSnapshot>(StringComparer.Ordinal);
        foreach (RegistrySetting setting in RegistrySettings)
        {
            registryValues.Add(setting.Key, CaptureRegistryValue(setting));
        }

        return new VisualEffectsSnapshot
        {
            SchemaVersion = BackupSchemaVersion,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Effects = effects,
            UiEffectsEnabled = GetBoolean(SpiGetUiEffects, "界面视觉效果"),
            MinimizeAnimationEnabled = GetAnimation(),
            DragFullWindowsEnabled = GetBoolean(SpiGetDragFullWindows, "拖动时显示窗口内容"),
            FontSmoothingEnabled = GetBoolean(SpiGetFontSmoothing, "字体平滑"),
            FontSmoothingType = GetUnsignedInteger(SpiGetFontSmoothingType, "字体平滑类型"),
            RegistryValues = registryValues
        };
    }

    private static void RestoreSnapshot(VisualEffectsSnapshot snapshot)
    {
        ValidateSnapshot(snapshot);

        SetAnimation(snapshot.MinimizeAnimationEnabled);
        SetUiParameter(SpiSetDragFullWindows, snapshot.DragFullWindowsEnabled, "拖动时显示窗口内容");
        foreach (EffectParameter effect in EffectParameters)
        {
            SetPointerBoolean(effect.SetAction, snapshot.Effects[effect.Name], effect.Name);
        }

        SetPointerBoolean(SpiSetUiEffects, snapshot.UiEffectsEnabled, "界面视觉效果");

        // The smoothing type API requires font smoothing to be enabled first.
        SetUiParameter(SpiSetFontSmoothing, enabled: true, "字体平滑");
        SetPointerValue(SpiSetFontSmoothingType, snapshot.FontSmoothingType, "字体平滑类型");
        SetUiParameter(SpiSetFontSmoothing, snapshot.FontSmoothingEnabled, "字体平滑");

        foreach (RegistrySetting setting in RegistrySettings)
        {
            RestoreRegistryValue(setting, snapshot.RegistryValues[setting.Key]);
        }
    }

    private static void ValidateSnapshot(VisualEffectsSnapshot snapshot)
    {
        if (snapshot.SchemaVersion != BackupSchemaVersion)
        {
            throw new InvalidDataException("视觉效果备份版本不受支持。");
        }

        foreach (EffectParameter effect in EffectParameters)
        {
            if (!snapshot.Effects.ContainsKey(effect.Name))
            {
                throw new InvalidDataException($"视觉效果备份缺少参数：{effect.Name}。");
            }
        }

        foreach (RegistrySetting setting in RegistrySettings)
        {
            if (!snapshot.RegistryValues.ContainsKey(setting.Key))
            {
                throw new InvalidDataException($"视觉效果备份缺少注册表参数：{setting.Name}。");
            }
        }
    }

    private static void ApplyRegistryProfile()
    {
        foreach (RegistrySetting setting in RegistrySettings)
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(setting.SubKeyPath, writable: true)
                ?? throw new IOException($"无法打开当前用户注册表路径：{setting.SubKeyPath}。");
            key.SetValue(setting.Name, setting.OptimizedValue, RegistryValueKind.DWord);
        }
    }

    private static RegistryDwordSnapshot CaptureRegistryValue(RegistrySetting setting)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(setting.SubKeyPath, writable: false);
        object? value = key?.GetValue(setting.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (value is null)
        {
            return new RegistryDwordSnapshot { Exists = false };
        }

        if (key!.GetValueKind(setting.Name) != RegistryValueKind.DWord)
        {
            throw new InvalidDataException($"注册表值类型异常，已拒绝修改：{setting.SubKeyPath}\\{setting.Name}。");
        }

        return new RegistryDwordSnapshot
        {
            Exists = true,
            Value = Convert.ToInt32(value, CultureInfo.InvariantCulture)
        };
    }

    private static void RestoreRegistryValue(RegistrySetting setting, RegistryDwordSnapshot snapshot)
    {
        if (snapshot.Exists)
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(setting.SubKeyPath, writable: true)
                ?? throw new IOException($"无法打开当前用户注册表路径：{setting.SubKeyPath}。");
            key.SetValue(setting.Name, snapshot.Value, RegistryValueKind.DWord);
            return;
        }

        using RegistryKey? existingKey = Registry.CurrentUser.OpenSubKey(setting.SubKeyPath, writable: true);
        existingKey?.DeleteValue(setting.Name, throwOnMissingValue: false);
    }

    private string GetBackupPath()
    {
        return Path.Combine(_outputDirectoryResolver.ResolveBackupsDirectory(), "visual-effects.json");
    }

    private static void WriteBackupAtomically(string backupPath, VisualEffectsSnapshot snapshot)
    {
        string temporaryPath = backupPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(snapshot, JsonOptions));
            File.Move(temporaryPath, backupPath, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static VisualEffectsSnapshot ReadBackup(string backupPath)
    {
        string json = File.ReadAllText(backupPath);
        VisualEffectsSnapshot? snapshot = JsonSerializer.Deserialize<VisualEffectsSnapshot>(json, JsonOptions);
        if (snapshot is null)
        {
            throw new InvalidDataException("视觉效果备份内容无效。");
        }

        ValidateSnapshot(snapshot);
        return snapshot;
    }

    private static void TryRestoreRollback(VisualEffectsSnapshot snapshot)
    {
        try
        {
            RestoreSnapshot(snapshot);
        }
        catch (Exception exception) when (exception is Win32Exception or UnauthorizedAccessException or IOException)
        {
            // Preserve the original error. The on-disk backup remains available for manual retry.
        }
    }

    private static bool GetBoolean(uint action, string settingName)
    {
        int value = 0;
        if (!SystemParametersInfoInteger(action, 0, ref value, 0))
        {
            ThrowSystemParametersError(settingName);
        }

        return value != 0;
    }

    private static uint GetUnsignedInteger(uint action, string settingName)
    {
        int value = 0;
        if (!SystemParametersInfoInteger(action, 0, ref value, 0))
        {
            ThrowSystemParametersError(settingName);
        }

        return unchecked((uint)value);
    }

    private static bool GetAnimation()
    {
        var information = AnimationInfo.Create();
        if (!SystemParametersInfoAnimation(SpiGetAnimation, information.Size, ref information, 0))
        {
            ThrowSystemParametersError("最小化和还原动画");
        }

        return information.MinimizeAnimation != 0;
    }

    private static void SetAnimation(bool enabled)
    {
        var information = AnimationInfo.Create();
        information.MinimizeAnimation = enabled ? 1 : 0;
        if (!SystemParametersInfoAnimation(SpiSetAnimation, information.Size, ref information, UpdateFlags))
        {
            ThrowSystemParametersError("最小化和还原动画");
        }
    }

    private static void SetUiParameter(uint action, bool enabled, string settingName)
    {
        if (!SystemParametersInfoPointer(action, enabled ? 1u : 0u, IntPtr.Zero, UpdateFlags))
        {
            ThrowSystemParametersError(settingName);
        }
    }

    private static void SetPointerBoolean(uint action, bool enabled, string settingName)
    {
        if (!SystemParametersInfoPointer(action, 0, enabled ? new IntPtr(1) : IntPtr.Zero, UpdateFlags))
        {
            ThrowSystemParametersError(settingName);
        }
    }

    private static void SetPointerValue(uint action, uint value, string settingName)
    {
        if (!SystemParametersInfoPointer(action, 0, new IntPtr(unchecked((long)value)), UpdateFlags))
        {
            ThrowSystemParametersError(settingName);
        }
    }

    private static void ThrowSystemParametersError(string settingName)
    {
        int error = Marshal.GetLastWin32Error();
        throw new Win32Exception(error, $"无法调整系统参数：{settingName}。");
    }

    private readonly record struct EffectParameter(string Name, uint GetAction, uint SetAction);

    private readonly record struct RegistrySetting(string SubKeyPath, string Name, int OptimizedValue)
    {
        public string Key => $"{SubKeyPath}|{Name}";
    }

    private sealed class RegistryDwordSnapshot
    {
        public RegistryDwordSnapshot()
        {
        }

        public bool Exists { get; init; }

        public int Value { get; init; }
    }

    private sealed class VisualEffectsSnapshot
    {
        public VisualEffectsSnapshot()
        {
        }

        public int SchemaVersion { get; init; }

        public DateTimeOffset CreatedAtUtc { get; init; }

        public Dictionary<string, bool> Effects { get; init; } = new(StringComparer.Ordinal);

        public bool UiEffectsEnabled { get; init; }

        public bool MinimizeAnimationEnabled { get; init; }

        public bool DragFullWindowsEnabled { get; init; }

        public bool FontSmoothingEnabled { get; init; }

        public uint FontSmoothingType { get; init; }

        public Dictionary<string, RegistryDwordSnapshot> RegistryValues { get; init; } = new(StringComparer.Ordinal);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AnimationInfo
    {
        public uint Size;
        public int MinimizeAnimation;

        public static AnimationInfo Create()
        {
            return new AnimationInfo { Size = (uint)Marshal.SizeOf<AnimationInfo>() };
        }
    }

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfoInteger(uint action, uint parameter, ref int value, uint flags);

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfoPointer(uint action, uint parameter, IntPtr value, uint flags);

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfoAnimation(uint action, uint parameter, ref AnimationInfo value, uint flags);
}
