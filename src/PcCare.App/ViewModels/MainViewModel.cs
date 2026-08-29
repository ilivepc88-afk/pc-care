using System.Collections.ObjectModel;
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
    private readonly WindowsCleanupCatalog _catalog;
    private readonly CleanupScanner _scanner;
    private readonly CleanupCoordinator _cleanupCoordinator;
    private readonly ReportWriter _reportWriter;
    private readonly OutputDirectoryResolver _outputDirectoryResolver;
    private readonly VisualEffectsService _visualEffectsService;
    private readonly IDialogService _dialogService;
    private CancellationTokenSource? _operationCancellation;
    private ScanReport? _report;
    private CleanupExecutionResult? _lastCleanup;
    private SystemSnapshot? _system;
    private bool _isBusy;
    private bool _hasVisualEffectsBackup;
    private string _statusText = "准备就绪。点击“开始体检”进行只读扫描。";
    private string _visualEffectsStatus;

    public MainViewModel(
        SystemInfoService systemInfoService,
        StartupService startupService,
        WindowsCleanupCatalog catalog,
        CleanupScanner scanner,
        CleanupCoordinator cleanupCoordinator,
        ReportWriter reportWriter,
        OutputDirectoryResolver outputDirectoryResolver,
        VisualEffectsService visualEffectsService,
        IDialogService dialogService)
    {
        _systemInfoService = systemInfoService;
        _startupService = startupService;
        _catalog = catalog;
        _scanner = scanner;
        _cleanupCoordinator = cleanupCoordinator;
        _reportWriter = reportWriter;
        _outputDirectoryResolver = outputDirectoryResolver;
        _visualEffectsService = visualEffectsService;
        _dialogService = dialogService;
        _hasVisualEffectsBackup = visualEffectsService.HasBackup;
        _visualEffectsStatus = _hasVisualEffectsBackup
            ? "已保存修改前配置，可随时恢复。"
            : "尚未应用视觉效果性能模式。";

        ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsBusy);
        CleanCommand = new AsyncRelayCommand(CleanAsync, () => !IsBusy && _report is not null);
        ExportCommand = new AsyncRelayCommand(ExportAsync, () => !IsBusy && _report is not null);
        OptimizeVisualEffectsCommand = new AsyncRelayCommand(OptimizeVisualEffectsAsync, () => !IsBusy);
        RestoreVisualEffectsCommand = new AsyncRelayCommand(
            RestoreVisualEffectsAsync,
            () => !IsBusy && _hasVisualEffectsBackup);
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
    }

    public ObservableCollection<CleanupCategoryItemViewModel> CleanupCategories { get; } = [];

    public ObservableCollection<StartupEntry> StartupEntries { get; } = [];

    public AsyncRelayCommand ScanCommand { get; }

    public AsyncRelayCommand CleanCommand { get; }

    public AsyncRelayCommand ExportCommand { get; }

    public AsyncRelayCommand OptimizeVisualEffectsCommand { get; }

    public AsyncRelayCommand RestoreVisualEffectsCommand { get; }

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

    public string VisualEffectsStatus
    {
        get => _visualEffectsStatus;
        private set => SetProperty(ref _visualEffectsStatus, value);
    }

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

    public string AdministratorText => _system is null ? "权限：尚未检查" : _system.IsAdministrator ? "权限：管理员" : "权限：普通用户（清理时按需提权）";

    public string ReclaimableSpace => ReportWriter.FormatBytes(CleanupCategories.Sum(category => category.SizeBytes));

    private async Task ScanAsync()
    {
        BeginOperation("正在进行只读体检……");
        try
        {
            CancellationToken token = _operationCancellation!.Token;
            Task<SystemSnapshot> systemTask = _systemInfoService.CaptureAsync(token);
            Task<List<StartupEntry>> startupTask = _startupService.ReadAsync(token);
            Task<List<CleanupCategoryScanResult>> cleanupTask = _scanner.ScanAsync(_catalog.GetAll(), DateTimeOffset.UtcNow, token);

            await Task.WhenAll(systemTask, startupTask, cleanupTask);
            _system = await systemTask;
            List<StartupEntry> startupEntries = await startupTask;
            List<CleanupCategoryScanResult> cleanupResults = await cleanupTask;

            _report = new ScanReport
            {
                System = _system,
                CleanupCategories = cleanupResults,
                StartupEntries = startupEntries,
                LastCleanup = _lastCleanup,
                ApplicationVersion = typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.2.0"
            };

            ReplaceCollections(cleanupResults, startupEntries);
            NotifySystemProperties();
            StatusText = $"体检完成：发现 {cleanupResults.Sum(item => item.Candidates.Count)} 个可清理文件、{startupEntries.Count} 个启动项。";
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

    private async Task CleanAsync()
    {
        if (_report is null)
        {
            return;
        }

        List<string> selected = CleanupCategories
            .Where(category => category.IsSelected && category.FileCount > 0)
            .Select(category => category.Id)
            .ToList();
        if (selected.Count == 0)
        {
            _dialogService.ShowInfo("没有选择包含可清理文件的分类。", "无需清理");
            return;
        }

        long selectedBytes = CleanupCategories.Where(category => selected.Contains(category.Id)).Sum(category => category.SizeBytes);
        if (!_dialogService.Confirm(
                $"将重新校验并清理所选分类，预计释放 {ReportWriter.FormatBytes(selectedBytes)}。\n\n清理的临时文件不可恢复，是否继续？",
                "确认执行清理"))
        {
            return;
        }

        CleanupExecutionResult? completedResult = null;
        BeginOperation("正在重新校验并执行清理……");
        try
        {
            CleanupExecutionResult result = await _cleanupCoordinator.ExecuteAsync(selected, _operationCancellation!.Token);
            completedResult = result;
            _lastCleanup = result;
            _report.LastCleanup = result;
            StatusText = $"清理完成：删除 {result.DeletedCount} 个文件，释放 {ReportWriter.FormatBytes(result.FreedBytes)}，失败 {result.FailedCount} 个。";
            _dialogService.ShowInfo(StatusText, "清理完成");
        }
        catch (OperationCanceledException)
        {
            StatusText = "清理已取消或未授予管理员权限。";
        }
        catch (Exception exception)
        {
            StatusText = "清理失败。";
            _dialogService.ShowError(exception.Message, "清理失败");
        }
        finally
        {
            EndOperation();
        }

        if (completedResult is not null)
        {
            await ScanAsync();
            StatusText = $"清理完成：删除 {completedResult.DeletedCount} 个文件，释放 {ReportWriter.FormatBytes(completedResult.FreedBytes)}，失败 {completedResult.FailedCount} 个。";
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
            (string htmlPath, string jsonPath) = await _reportWriter.WriteAsync(
                _report,
                outputDirectory,
                _operationCancellation!.Token);
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
            _dialogService.ShowError(exception.Message, "导出失败");
        }
        finally
        {
            EndOperation();
        }
    }

    private async Task OptimizeVisualEffectsAsync()
    {
        if (!_dialogService.Confirm(
                "将关闭当前用户的窗口动画、淡入淡出、阴影、任务栏动画、Peek、缩略图预览等视觉效果，仅保留字体平滑。\n\n修改前配置会保存在本机，可一键恢复。部分效果可能需要重新登录后完全生效，是否继续？",
                "确认应用视觉效果性能模式"))
        {
            return;
        }

        BeginOperation("正在调整视觉效果……");
        try
        {
            await _visualEffectsService.ApplyPerformanceProfileAsync(_operationCancellation!.Token);
            _hasVisualEffectsBackup = true;
            VisualEffectsStatus = "性能模式已应用：仅保留字体平滑；修改前配置已备份。";
            StatusText = "视觉效果性能模式已应用。";
            _dialogService.ShowInfo(
                "调整完成。字体平滑保持开启，其他视觉效果已关闭。部分程序或任务栏效果可能在重新登录后完全生效。",
                "性能优化完成");
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

    private async Task RestoreVisualEffectsAsync()
    {
        if (!_dialogService.Confirm(
                "将恢复应用性能模式之前保存的视觉效果配置。部分效果可能需要重新登录后完全生效，是否继续？",
                "确认恢复视觉效果"))
        {
            return;
        }

        BeginOperation("正在恢复视觉效果……");
        try
        {
            await _visualEffectsService.RestoreAsync(_operationCancellation!.Token);
            _hasVisualEffectsBackup = false;
            VisualEffectsStatus = "已恢复修改前的视觉效果配置。";
            StatusText = "视觉效果已恢复。";
            _dialogService.ShowInfo("已恢复修改前配置。部分程序或任务栏效果可能在重新登录后完全生效。", "恢复完成");
        }
        catch (OperationCanceledException)
        {
            StatusText = "视觉效果恢复已取消。";
        }
        catch (Exception exception)
        {
            StatusText = "视觉效果恢复失败。";
            _dialogService.ShowError(exception.Message, "恢复失败");
        }
        finally
        {
            EndOperation();
        }
    }

    private void ReplaceCollections(
        IEnumerable<CleanupCategoryScanResult> cleanupResults,
        IEnumerable<StartupEntry> startupEntries)
    {
        CleanupCategories.Clear();
        foreach (CleanupCategoryScanResult result in cleanupResults)
        {
            CleanupCategories.Add(new CleanupCategoryItemViewModel(result));
        }

        StartupEntries.Clear();
        foreach (StartupEntry entry in startupEntries)
        {
            StartupEntries.Add(entry);
        }

        OnPropertyChanged(nameof(ReclaimableSpace));
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
        CleanCommand.RaiseCanExecuteChanged();
        ExportCommand.RaiseCanExecuteChanged();
        OptimizeVisualEffectsCommand.RaiseCanExecuteChanged();
        RestoreVisualEffectsCommand.RaiseCanExecuteChanged();
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
