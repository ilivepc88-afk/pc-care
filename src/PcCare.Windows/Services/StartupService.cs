using Microsoft.Win32;
using PcCare.Core.Models;

namespace PcCare.Windows.Services;

public sealed class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public Task<List<StartupEntry>> ReadAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(Read, cancellationToken);
    }

    private static List<StartupEntry> Read()
    {
        var entries = new List<StartupEntry>();

        ReadRegistry(entries, RegistryHive.CurrentUser, RegistryView.Registry64, "当前用户", "HKCU Run (64位)");
        ReadRegistry(entries, RegistryHive.CurrentUser, RegistryView.Registry32, "当前用户", "HKCU Run (32位)");
        ReadRegistry(entries, RegistryHive.LocalMachine, RegistryView.Registry64, "所有用户", "HKLM Run (64位)");
        ReadRegistry(entries, RegistryHive.LocalMachine, RegistryView.Registry32, "所有用户", "HKLM Run (32位)");
        ReadStartupFolder(entries, Environment.SpecialFolder.Startup, "当前用户", "用户启动目录");
        ReadStartupFolder(entries, Environment.SpecialFolder.CommonStartup, "所有用户", "公共启动目录");

        return entries
            .DistinctBy(entry => $"{entry.Name}\0{entry.Command}\0{entry.Source}", StringComparer.OrdinalIgnoreCase)
            .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static void ReadRegistry(
        ICollection<StartupEntry> entries,
        RegistryHive hive,
        RegistryView view,
        string scope,
        string source)
    {
        try
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
            using RegistryKey? runKey = baseKey.OpenSubKey(RunKeyPath, writable: false);
            if (runKey is null)
            {
                return;
            }

            foreach (string valueName in runKey.GetValueNames())
            {
                string command = runKey.GetValue(valueName)?.ToString() ?? string.Empty;
                entries.Add(new StartupEntry(valueName, command, source, scope));
            }
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            // A missing or inaccessible view is treated as an empty source.
        }
    }

    private static void ReadStartupFolder(
        ICollection<StartupEntry> entries,
        Environment.SpecialFolder folder,
        string scope,
        string source)
    {
        string path = Environment.GetFolderPath(folder);
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return;
        }

        try
        {
            foreach (string file in Directory.EnumerateFiles(path))
            {
                entries.Add(new StartupEntry(Path.GetFileNameWithoutExtension(file), file, source, scope));
            }
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            // Keep the other startup sources available.
        }
    }
}
