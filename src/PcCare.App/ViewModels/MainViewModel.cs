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
    private readonly ReportWriter _reportWriter;
    private readonly OutputDirectoryResolver _outputDirectoryResolver;
    private readonly VisualEffectsService _visualEffectsService;
    private readonly IDialogService _dialogService;
    private CancellationTokenSource? _operationCancellation;
    private ScanReport? _report;
    private SystemSnapshot? _system;
    private bool _isBusy;
    private string _statusText = "准备就绪。点击“开始体检”进行只读扫描。";

    public MainViewModel(
        SystemInfoService systemInfoService,
        StartupService startupService,
        ReportWriter reportWriter,
        OutputDirectoryResolver outputDirectoryResolver,
        VisualEffectsService visualEffectsService,
        IDialogService dialogService)
    {
        _systemInfoService = systemInfoService;
        _startupService = startupService;
        _reportWriter = reportWriter;
        _outputDirectoryResolver = outputDirectoryResolver;
        _visualEffectsService = visualEffectsService;
        _dialogService = dialogService;

        ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsBusy);
        ExportCommand = new AsyncRelayCommand(ExportAsync, () => !IsBusy && _report is not null);
        OptimizeVisualEffectsCommand = new AsyncRelayCommand(OptimizeVisualEffectsAsync, () => !IsBusy);
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
    }

    public ObservableCollection<StartupEntry> StartupEntries { get; } = [];

    public AsyncRelayCommand ScanCommand { get; }

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
        BeginOperation("正在进行只读体检……");
        try
        {
            CancellationToken token = _operationCancellation!.Token;
            Task<SystemSnapshot> systemTask = _systemInfoService.CaptureAsync(token);
            Task<List<StartupEntry>> startupTask = _startupService.ReadAsync(token);

            await Task.WhenAll(systemTask, startupTask);
            _system = await systemTask;
            List<StartupEntry> startupEntries = await startupTask;

            _report = new ScanReport
            {
                System = _system,
                StartupEntries = startupEntries,
                ApplicationVersion = typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.3.0"
            };

            ReplaceStartupEntries(startupEntries);
            NotifySystemProperties();
            StatusText = $"体检完成：发现 {startupEntries.Count} 个启动项。";
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
            _dialogService.ShowInfo(
                "调整完成。部分程序或任务栏效果可能在重新登录后完全生效。",
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

    private void ReplaceStartupEntries(IEnumerable<StartupEntry> startupEntries)
    {
        StartupEntries.Clear();
        foreach (StartupEntry entry in startupEntries)
        {
            StartupEntries.Add(entry);
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
