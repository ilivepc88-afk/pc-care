using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using PcCare.Core.Models;
using PcCare.Core.Services;

namespace PcCare.Windows.Services;

internal sealed class ScheduledTaskStartupScanner
{
    private const int TaskEnumHidden = 1;

    public IReadOnlyList<StartupItem> Scan(bool includeMicrosoftSystemTasks, CancellationToken cancellationToken)
    {
        var items = new List<StartupItem>();
        object? service = null;
        try
        {
            Type? schedulerType = Type.GetTypeFromProgID("Schedule.Service", throwOnError: false);
            if (schedulerType is null)
            {
                return items;
            }

            dynamic scheduler = Activator.CreateInstance(schedulerType)!;
            service = scheduler;
            scheduler.Connect();
            dynamic root = scheduler.GetFolder("\\");
            ScanFolder(root, items, includeMicrosoftSystemTasks, cancellationToken);
            ReleaseComObject(root);
        }
        catch (Exception exception) when (exception is COMException or UnauthorizedAccessException or SecurityException)
        {
            // Task Scheduler can be unavailable or protected; other startup sources remain usable.
        }
        finally
        {
            ReleaseComObject(service);
        }

        return items;
    }

    private static void ScanFolder(
        dynamic folder,
        ICollection<StartupItem> items,
        bool includeMicrosoftSystemTasks,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        dynamic tasks = folder.GetTasks(TaskEnumHidden);
        try
        {
            foreach (dynamic task in tasks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddIfLoginOrStartupTask(task, items, includeMicrosoftSystemTasks);
                ReleaseComObject(task);
            }
        }
        finally
        {
            ReleaseComObject(tasks);
        }

        dynamic folders = folder.GetFolders(0);
        try
        {
            foreach (dynamic child in folders)
            {
                try
                {
                    ScanFolder(child, items, includeMicrosoftSystemTasks, cancellationToken);
                }
                finally
                {
                    ReleaseComObject(child);
                }
            }
        }
        finally
        {
            ReleaseComObject(folders);
        }
    }

    private static void AddIfLoginOrStartupTask(dynamic task, ICollection<StartupItem> items, bool includeMicrosoftSystemTasks)
    {
        string taskPath = task.Path?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(taskPath) ||
            (!includeMicrosoftSystemTasks && taskPath.StartsWith(@"\Microsoft\Windows\", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        dynamic definition = task.Definition;
        try
        {
            if (!HasLogonOrBootTrigger(definition))
            {
                return;
            }

            (string executable, string arguments, string actionType) = GetExecutable((object)definition);
            dynamic registration = definition.RegistrationInfo;
            string author = registration.Author?.ToString() ?? string.Empty;
            string description = registration.Description?.ToString() ?? string.Empty;
            ReleaseComObject(registration);

            items.Add(new StartupItem
            {
                Id = CreateId(taskPath),
                Name = GetTaskName(taskPath),
                SourceType = StartupSourceType.ScheduledTask,
                SourcePath = taskPath,
                Command = string.IsNullOrWhiteSpace(executable) ? taskPath : $"{executable} {arguments}".TrimEnd(),
                ExecutablePath = executable,
                Arguments = arguments,
                Description = description,
                ActionType = actionType,
                Enabled = task.Enabled,
                Scope = StartupScope.System,
                User = author,
                RequiresAdministrator = true,
                OperationIdentity = taskPath
            });
        }
        finally
        {
            ReleaseComObject(definition);
        }
    }

    private static bool HasLogonOrBootTrigger(dynamic definition)
    {
        dynamic triggers = definition.Triggers;
        try
        {
            foreach (dynamic trigger in triggers)
            {
                try
                {
                    int type = Convert.ToInt32(trigger.Type, System.Globalization.CultureInfo.InvariantCulture);
                    if (StartupTaskTriggerClassifier.IsLoginOrStartupTrigger(type))
                    {
                        return true;
                    }
                }
                finally
                {
                    ReleaseComObject(trigger);
                }
            }

            return false;
        }
        finally
        {
            ReleaseComObject(triggers);
        }
    }

    private static (string Executable, string Arguments, string ActionType) GetExecutable(dynamic definition)
    {
        dynamic actions = definition.Actions;
        try
        {
            foreach (dynamic action in actions)
            {
                try
                {
                    int actionType = Convert.ToInt32(action.Type, System.Globalization.CultureInfo.InvariantCulture);
                    if (actionType != 0)
                    {
                        return (string.Empty, string.Empty, $"任务操作类型 {actionType}");
                    }

                    return (action.Path?.ToString() ?? string.Empty, action.Arguments?.ToString() ?? string.Empty, "Exec");
                }
                finally
                {
                    ReleaseComObject(action);
                }
            }

            return (string.Empty, string.Empty, "未识别");
        }
        finally
        {
            ReleaseComObject(actions);
        }
    }

    private static string GetTaskName(string taskPath)
    {
        int separator = taskPath.LastIndexOf('\\');
        return separator >= 0 && separator < taskPath.Length - 1 ? taskPath[(separator + 1)..] : taskPath;
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }

    private static string CreateId(string path)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(path));
        return Convert.ToHexString(hash)[..20];
    }
}
