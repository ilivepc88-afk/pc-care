using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using PcCare.App.Infrastructure;
using PcCare.App.Services;
using PcCare.Core.Models;
using PcCare.Core.Services;
using PcCare.Windows.Services;

namespace PcCare.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly SystemInfoService _systemInfoService;
    private readonly StartupService _startupService;
    private readonly ElevatedStartupOperationRunner _startupOperationRunner;
    private readonly ReportWriter _reportWriter;
    private readonly OutputDirectoryResolver _outputDirectoryResolver;
    private readonly VisualEffectsService _visualEffectsService;
    private readonly IDialogService _dialogService;
    private readonly List<StartupOperationLogEntry> _startupOperationLog = [];
    private CancellationTokenSource? _operationCancellation;
    private ScanReport? _report;
    private SystemSnapshot? _system;
    private bool _isBusy;
    private bool _showMicrosoftSystemTasks;
    private string _startupFilter = "全部";
    private string _startupSearchText = string.Empty;
    private string _statusText = "准备就绪。点击“开始体检”进行离线扫描。";

    public MainViewModel(
        SystemInfoService systemInfoService,
        StartupService startupService,
        ElevatedStartupOperationRunner startupOperationRunner,
        ReportWriter reportWriter,
        OutputDirectoryResolver outputDirectoryResolver,
        VisualEffectsService visualEffectsService,
        IDialogService dialogService)
    {
        _systemInfoService = systemInfoService;
        _startupService = startupService;
        _startupOperationRunner = startupOperationRunner;
        _reportWriter = reportWriter;
        _outputDirectoryResolver = outputDirectoryResolver;
        _visualEffectsService = visualEffectsService;
        _dialogService = dialogService;

        StartupItemsView = CollectionViewSource.GetDefaultView(StartupItems);
        StartupItemsView.Filter = FilterStartupItem;

        ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsBusy);
        ScanStartupCommand = new AsyncRelayCommand(ScanStartupAsync, () => !IsBusy);
        OptimizeStartupCommand = new AsyncRelayCommand(OptimizeStartupAsync, () => !IsBusy);
        ToggleStartupItemCommand = new AsyncParameterRelayCommand(ToggleStartupItemAsync, CanToggleStartupItem);
        ExportCommand = new AsyncRelayCommand(ExportAsync, () => !IsBusy && _report is not null);
        OptimizeVisualEffectsCommand = new AsyncRelayCommand(OptimizeVisualEffectsAsync, () => !IsBusy);
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
    }

    public ObservableCollection<StartupItem> StartupItems { get; } = [];

    public ICollectionView StartupItemsView { get; }

    public IReadOnlyList<string> StartupFilters { get; } = ["全部", "已启用", "已禁用", "建议优化", "系统/重要", "注册表", "启动文件夹", "计划任务", "未知"];

    public AsyncRelayCommand ScanCommand { get; }

    public AsyncRelayCommand ScanStartupCommand { get; }

    public AsyncRelayCommand OptimizeStartupCommand { get; }

    public AsyncParameterRelayCommand ToggleStartupItemCommand { get; }

    public AsyncRelayCommand ExportCommand { get; }

    public AsyncRelayCommand OptimizeVisualEffectsCommand { get; }

    public RelayCommand CancelCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool ShowMicrosoftSystemTasks
    {
        get => _showMicrosoftSystemTasks;
        set => SetProperty(ref _showMicrosoftSystemTasks, value);
    }

    public string StartupFilter
    {
        get => _startupFilter;
        set
        {
            if (SetProperty(ref _startupFilter, value))
            {
                StartupItemsView.Refresh();
            }
        }
    }

    public string StartupSearchText
    {
        get => _startupSearchText;
        set
        {
            if (SetProperty(ref _startupSearchText, value))
            {
                StartupItemsView.Refresh();
            }
        }
    }

    public int StartupItemCount => StartupItems.Count;

    public int EnabledStartupItemCount => StartupItems.Count(item => item.Enabled);

    public int RecommendDisableCount => StartupItems.Count(item => item.Enabled && item.Recommendation == StartupRecommendation.RecommendDisable && item.RiskLevel == StartupRiskLevel.Low && item.CanDisable);

    public int ProtectedStartupItemCount => StartupItems.Count(item => item.IsSystemComponent);

    public string StartupItemCountText => $"发现 {StartupItemCount} 项";

    public string EnabledStartupItemCountText => $"已启用 {EnabledStartupItemCount} 项";

    public string RecommendDisableCountText => $"建议优化 {RecommendDisableCount} 项";

    public string ProtectedStartupItemCountText => $"系统/重要 {ProtectedStartupItemCount} 项";

    public string ComputerName => _system?.ComputerName ?? "尚未体检";

    public string WindowsSummary => _system is null ? "尚未体检" : $"{_system.WindowsEdition} {_system.WindowsVersion}";

    public string WindowsDetails => _system is null ? "尚未体检" : $"{_system.WindowsEdition} {_system.WindowsVersion}，Build {_system.WindowsBuild}";

    public string CpuName => _system?.CpuName ?? "尚未体检";

    public string MemorySummary => _system is null
        ? "尚未体检"
        : $"{ReportWriter.FormatBytes((long)_system.AvailableMemoryBytes)} 可用 / {ReportWriter.FormatBytes((long)_system.TotalMemoryBytes)}";

    public string SystemDriveFree => _system is null ? "尚未体检" : ReportWriter.FormatBytes(_system.SystemDriveFreeBytes);

    public string DriveSummary => _system is null
        ? "尚未体检"
        : $"{ReportWriter.FormatBytes(_system.SystemDriveFreeBytes)} 可用 / {ReportWriter.FormatBytes(_system.SystemDriveTotalBytes)}";

    public string DiskMediaType => _system?.DiskMediaType ?? "尚未体检";

    public string UptimeSummary => _system is null ? "尚未体检" : $"{_system.Uptime.Days}天 {_system.Uptime.Hours}小时";

    public string RebootPendingText => _system is null ? "尚未体检" : _system.RebootPending ? "是，建议安排重启" : "否";

    public string AdministratorText => _system is null ? "权限：尚未检查" : _system.IsAdministrator ? "权限：管理员" : "权限：普通用户";

    private async Task ScanAsync()
    {
        BeginOperation("正在进行离线体检和启动项扫描……");
        try
        {
            CancellationToken token = _operationCancellation!.Token;
            Task<SystemSnapshot> systemTask = _systemInfoService.CaptureAsync(token);
            Task<List<StartupItem>> startupTask = _startupService.ScanAsync(ShowMicrosoftSystemTasks, token);

            await Task.WhenAll(systemTask, startupTask);
            _system = await systemTask;
            List<StartupItem> startupItems = await startupTask;
            ReplaceStartupItems(startupItems);
            _report = CreateReport(startupItems);
            NotifySystemProperties();
            StatusText = $"体检完成：发现 {startupItems.Count} 个启动项。";
        }
        catch (OperationCanceledException)
        {
            StatusText = "体检已取消。";
        }
        catch (Exception exception)
        {
            StatusText = "体检失败。";
            _dialogService.ShowError(exception.Message, "体检失败");
        }
        finally
        {
            EndOperation();
        }
    }

    private async Task ScanStartupAsync()
    {
        BeginOperation("正在扫描安全启动项……");
        try
        {
            List<StartupItem> items = await _startupService.ScanAsync(ShowMicrosoftSystemTasks, _operationCancellation!.Token);
            ReplaceStartupItems(items);
            UpdateReportStartupItems(items);
            StatusText = $"启动项扫描完成：发现 {items.Count} 项。";
        }
        catch (OperationCanceledException)
        {
            StatusText = "启动项扫描已取消。";
        }
        catch (Exception exception)
        {
            StatusText = "启动项扫描失败。";
            _dialogService.ShowError(exception.Message, "启动项扫描失败");
        }
        finally
        {
            EndOperation();
        }
    }

    private async Task OptimizeStartupAsync()
    {
        List<StartupItem> candidates = StartupItems
            .Where(item => item.Enabled && item.Recommendation == StartupRecommendation.RecommendDisable && item.RiskLevel == StartupRiskLevel.Low && item.CanDisable)
            .ToList();
        if (candidates.Count == 0)
        {
            _dialogService.ShowInfo("当前没有符合“低风险且建议优化”条件的启动项。未知项、企业安全项、驱动项和系统项不会自动处理。", "无需一键优化");
            return;
        }

        string names = string.Join("、", candidates.Take(8).Select(item => item.Name));
        string suffix = candidates.Count > 8 ? " 等" : string.Empty;
        if (!_dialogService.Confirm(
                $"将禁用 {candidates.Count} 个低风险启动项：{names}{suffix}\n\n不会删除原始注册表值、文件或计划任务；下次需要时可在本页重新启用。是否继续？",
                "确认一键优化启动项"))
        {
            return;
        }

        BeginOperation("正在按保守规则优化启动项……");
        try
        {
            int succeeded = 0;
            foreach (StartupItem item in candidates)
            {
                _operationCancellation!.Token.ThrowIfCancellationRequested();
                StartupOperationResult result = await _startupOperationRunner.ApplyAsync(item, _operationCancellation.Token);
                AddOperationLog(item, result);
                if (result.Succeeded)
                {
                    succeeded++;
                }
            }

            List<StartupItem> refreshed = await _startupService.ScanAsync(ShowMicrosoftSystemTasks, _operationCancellation!.Token);
            ReplaceStartupItems(refreshed);
            UpdateReportStartupItems(refreshed);
            StatusText = $"一键优化完成：成功 {succeeded} 项，失败 {candidates.Count - succeeded} 项。";
        }
        catch (OperationCanceledException)
        {
            StatusText = "启动项优化已取消。";
        }
        catch (Exception exception)
        {
            StatusText = "启动项优化失败。";
            _dialogService.ShowError(exception.Message, "启动项优化失败");
        }
        finally
        {
            EndOperation();
        }
    }

    private async Task ToggleStartupItemAsync(object? parameter)
    {
        if (parameter is not StartupItem item || !(item.CanDisable || item.CanEnable))
        {
            return;
        }

        StartupOperationAction action = item.Enabled ? StartupOperationAction.Disable : StartupOperationAction.Enable;
        string actionText = action == StartupOperationAction.Disable ? "禁用" : "启用";
        string warning = item.Recommendation == StartupRecommendation.Unknown
            ? "\n\n该项用途未知，不会被一键优化；请确认这是您希望手动调整的程序。"
            : string.Empty;
        if (!_dialogService.Confirm(
                $"确定要{actionText}“{item.Name}”吗？\n\n原始启动项不会被删除。{warning}",
                $"确认{actionText}启动项"))
        {
            return;
        }

        BeginOperation($"正在{actionText}启动项……");
        try
        {
            StartupOperationResult result = await _startupOperationRunner.ApplyAsync(item, _operationCancellation!.Token);
            AddOperationLog(item, result);
            if (!result.Succeeded)
            {
                StatusText = $"{actionText}失败：{result.Message}";
                _dialogService.ShowError(result.Message, $"{actionText}启动项失败");
                return;
            }

            List<StartupItem> refreshed = await _startupService.ScanAsync(ShowMicrosoftSystemTasks, _operationCancellation.Token);
            ReplaceStartupItems(refreshed);
            UpdateReportStartupItems(refreshed);
            StatusText = $"{actionText}完成：{item.Name}。";
        }
        catch (OperationCanceledException)
        {
            StatusText = "操作已取消。";
        }
        catch (Exception exception)
        {
            StatusText = $"{actionText}失败。";
            _dialogService.ShowError(exception.Message, $"{actionText}启动项失败");
        }
        finally
        {
            EndOperation();
        }
    }

    private async Task ExportAsync()
    {
        if (_report is null)
        {
            return;
        }

        BeginOperation("正在生成离线报告……");
        try
        {
            string outputDirectory = _outputDirectoryResolver.ResolveReportsDirectory();
            (string htmlPath, string jsonPath) = await _reportWriter.WriteAsync(_report, outputDirectory, _operationCancellation!.Token);
            StatusText = $"报告已保存：{htmlPath}";
            _dialogService.ShowInfo($"HTML：{htmlPath}\nJSON：{jsonPath}", "报告已导出");
        }
        catch (OperationCanceledException)
        {
            StatusText = "导出已取消。";
        }
        catch (Exception exception)
        {
            StatusText = "报告导出失败。";
            _dialogService.ShowError(exception.Message, "报告导出失败");
        }
        finally
        {
            EndOperation();
        }
    }

    private async Task OptimizeVisualEffectsAsync()
    {
        if (!_dialogService.Confirm(
                "将调整当前用户的视觉效果，仅保留平滑屏幕字体边缘，其他项目全部关闭。部分效果可能需要重新登录后完全生效，是否继续？",
                "确认应用视觉效果性能模式"))
        {
            return;
        }

        BeginOperation("正在调整视觉效果……");
        try
        {
            await _visualEffectsService.ApplyPerformanceProfileAsync(_operationCancellation!.Token);
            StatusText = "视觉效果性能模式已应用。";
            _dialogService.ShowInfo("调整完成。部分程序或任务栏效果可能在重新登录后完全生效。", "性能优化完成");
        }
        catch (OperationCanceledException)
        {
            StatusText = "视觉效果调整已取消。";
        }
        catch (Exception exception)
        {
            StatusText = "视觉效果调整失败。";
            _dialogService.ShowError(exception.Message, "调整失败");
        }
        finally
        {
            EndOperation();
        }
    }

    private bool FilterStartupItem(object value)
    {
        if (value is not StartupItem item)
        {
            return false;
        }

        bool filterMatches = StartupFilter switch
        {
            "已启用" => item.Enabled,
            "已禁用" => !item.Enabled,
            "建议优化" => item.Recommendation == StartupRecommendation.RecommendDisable,
            "系统/重要" => item.IsSystemComponent,
            "注册表" => item.SourceType is StartupSourceType.RegistryRun or StartupSourceType.RegistryRunOnce,
            "启动文件夹" => item.SourceType == StartupSourceType.StartupFolder,
            "计划任务" => item.SourceType == StartupSourceType.ScheduledTask,
            "未知" => item.Recommendation == StartupRecommendation.Unknown,
            _ => true
        };
        if (!filterMatches || string.IsNullOrWhiteSpace(StartupSearchText))
        {
            return filterMatches;
        }

        string search = StartupSearchText.Trim();
        return new[] { item.Name, item.Command, item.SourcePath, item.Publisher, item.Description, item.ProductName }
            .Any(text => text.Contains(search, StringComparison.CurrentCultureIgnoreCase));
    }

    private bool CanToggleStartupItem(object? parameter) => !IsBusy && parameter is StartupItem item && (item.CanDisable || item.CanEnable);

    private void ReplaceStartupItems(IEnumerable<StartupItem> startupItems)
    {
        StartupItems.Clear();
        foreach (StartupItem item in startupItems)
        {
            StartupItems.Add(item);
        }

        StartupItemsView.Refresh();
        OnPropertyChanged(nameof(StartupItemCount));
        OnPropertyChanged(nameof(EnabledStartupItemCount));
        OnPropertyChanged(nameof(RecommendDisableCount));
        OnPropertyChanged(nameof(ProtectedStartupItemCount));
        OnPropertyChanged(nameof(StartupItemCountText));
        OnPropertyChanged(nameof(EnabledStartupItemCountText));
        OnPropertyChanged(nameof(RecommendDisableCountText));
        OnPropertyChanged(nameof(ProtectedStartupItemCountText));
        OptimizeStartupCommand.RaiseCanExecuteChanged();
        ToggleStartupItemCommand.RaiseCanExecuteChanged();
    }

    private void AddOperationLog(StartupItem item, StartupOperationResult result)
    {
        bool currentEnabled = result.Action == StartupOperationAction.Enable;
        _startupOperationLog.Add(new StartupOperationLogEntry(
            DateTimeOffset.UtcNow,
            item.Name,
            item.SourceType,
            result.Action,
            item.Enabled,
            result.Succeeded ? currentEnabled : item.Enabled,
            result.Succeeded,
            result.Message));
    }

    private ScanReport CreateReport(IReadOnlyCollection<StartupItem> startupItems)
    {
        return new ScanReport
        {
            System = _system ?? throw new InvalidOperationException("系统信息尚未获取。"),
            StartupItems = startupItems.ToList(),
            StartupOperationLog = _startupOperationLog.ToList(),
            ApplicationVersion = typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.4.0"
        };
    }

    private void UpdateReportStartupItems(IReadOnlyCollection<StartupItem> startupItems)
    {
        if (_system is not null)
        {
            _report = CreateReport(startupItems);
            ExportCommand.RaiseCanExecuteChanged();
        }
    }

    private void BeginOperation(string status)
    {
        _operationCancellation?.Dispose();
        _operationCancellation = new CancellationTokenSource();
        StatusText = status;
        IsBusy = true;
    }

    private void EndOperation()
    {
        IsBusy = false;
        _operationCancellation?.Dispose();
        _operationCancellation = null;
    }

    private void Cancel() => _operationCancellation?.Cancel();

    private void RaiseCommandStates()
    {
        ScanCommand.RaiseCanExecuteChanged();
        ScanStartupCommand.RaiseCanExecuteChanged();
        OptimizeStartupCommand.RaiseCanExecuteChanged();
        ToggleStartupItemCommand.RaiseCanExecuteChanged();
        ExportCommand.RaiseCanExecuteChanged();
        OptimizeVisualEffectsCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
    }

    private void NotifySystemProperties()
    {
        OnPropertyChanged(nameof(ComputerName));
        OnPropertyChanged(nameof(WindowsSummary));
        OnPropertyChanged(nameof(WindowsDetails));
        OnPropertyChanged(nameof(CpuName));
        OnPropertyChanged(nameof(MemorySummary));
        OnPropertyChanged(nameof(SystemDriveFree));
        OnPropertyChanged(nameof(DriveSummary));
        OnPropertyChanged(nameof(DiskMediaType));
        OnPropertyChanged(nameof(UptimeSummary));
        OnPropertyChanged(nameof(RebootPendingText));
        OnPropertyChanged(nameof(AdministratorText));
    }
}
