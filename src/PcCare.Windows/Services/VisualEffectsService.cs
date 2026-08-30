using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PcCare.Windows.Services;

public sealed class VisualEffectsService
{
    private const uint SpiSetDragFullWindows = 0x0025;
    private const uint SpiSetAnimation = 0x0049;
    private const uint SpiSetFontSmoothing = 0x004B;
    private const uint SpiSetUiEffects = 0x103F;
    private const uint SpifUpdateIniFile = 0x0001;
    private const uint SpifSendChange = 0x0002;
    private const uint UpdateFlags = SpifUpdateIniFile | SpifSendChange;

    private static readonly EffectParameter[] EffectParameters =
    [
        new("工作区动画", 0x1043),
        new("组合框动画", 0x1005),
        new("鼠标指针阴影", 0x101B),
        new("窗口阴影", 0x1025),
        new("渐变标题栏", 0x1009),
        new("热点跟踪", 0x100F),
        new("列表框平滑滚动", 0x1007),
        new("菜单动画", 0x1003),
        new("菜单淡出", 0x1013),
        new("选择淡出", 0x1015),
        new("工具提示动画", 0x1017),
        new("工具提示淡出", 0x1019)
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

    public Task ApplyPerformanceProfileAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(ApplyPerformanceProfile, CancellationToken.None);
    }

    private static void ApplyPerformanceProfile()
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

    private static void ApplyRegistryProfile()
    {
        foreach (RegistrySetting setting in RegistrySettings)
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(setting.SubKeyPath, writable: true)
                ?? throw new IOException($"无法打开当前用户注册表路径：{setting.SubKeyPath}。");
            key.SetValue(setting.Name, setting.OptimizedValue, RegistryValueKind.DWord);
        }
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

    private static void ThrowSystemParametersError(string settingName)
    {
        int error = Marshal.GetLastWin32Error();
        throw new Win32Exception(error, $"无法调整系统参数：{settingName}。");
    }

    private readonly record struct EffectParameter(string Name, uint SetAction);

    private readonly record struct RegistrySetting(string SubKeyPath, string Name, int OptimizedValue);

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
    private static extern bool SystemParametersInfoPointer(uint action, uint parameter, IntPtr value, uint flags);

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfoAnimation(uint action, uint parameter, ref AnimationInfo value, uint flags);
}
