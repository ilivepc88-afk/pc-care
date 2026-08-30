using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using PcCare.App.Infrastructure;
using PcCare.App.Services;
using PcCare.Core.Models;
using PcCare.Windows.Services;

namespace PcCare.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly SystemInfoService _systemInfoService;
    private readonly StartupService _startupService;
    private readonly ElevatedStartupOperationRunner _startupOperationRunner;
    private readonly BackgroundOptimizationService _backgroundOptimizationService;
    private readonly ElevatedBackgroundOptimizationRunner _backgroundOptimizationRunner;
    private readonly VisualEffectsService _visualEffectsService;
    private readonly IDialogService _dialogService;
    private CancellationTokenSource? _operationCancellation;
    private SystemSnapshot? _system;
    private bool _isBusy;
    private bool _showMicrosoftSystemTasks;
    private string _startupFilter = "全部";
    private string _startupSearchText = string.Empty;
    private string _backgroundFilter = "全部";
    private string _backgroundSearchText = string.Empty;
    private string _statusText = "准备就绪。点击“开始检查”读取本机状态。";

    public MainViewModel(
        SystemInfoService systemInfoService,
        StartupService startupService,
        ElevatedStartupOperationRunner startupOperationRunner,
        BackgroundOptimizationService backgroundOptimizationService,
        ElevatedBackgroundOptimizationRunner backgroundOptimizationRunner,
        VisualEffectsService visualEffectsService,
        IDialogService dialogService)
    {
        _systemInfoService = systemInfoService;
        _startupService = startupService;
        _startupOperationRunner = startupOperationRunner;
        _backgroundOptimizationService = backgroundOptimizationService;
        _backgroundOptimizationRunner = backgroundOptimizationRunner;
        _visualEffectsService = visualEffectsService;
        _dialogService = dialogService;

        StartupItemsView = CollectionViewSource.GetDefaultView(StartupItems);
        StartupItemsView.Filter = FilterStartupItem;
        BackgroundOptimizationItemsView = CollectionViewSource.GetDefaultView(BackgroundOptimizationItems);
        BackgroundOptimizationItemsView.Filter = FilterBackgroundOptimizationItem;

        ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsBusy);
        ScanStartupCommand = new AsyncRelayCommand(ScanStartupAsync, () => !IsBusy);
        OptimizeStartupCommand = new AsyncRelayCommand(OptimizeStartupAsync, () => !IsBusy);
        ToggleStartupItemCommand = new AsyncParameterRelayCommand(ToggleStartupItemAsync, CanToggleStartupItem);
        ScanBackgroundOptimizationCommand = new AsyncRelayCommand(ScanBackgroundOptimizationAsync, () => !IsBusy);
        OptimizeBackgroundCommand = new AsyncRelayCommand(OptimizeBackgroundAsync, () => !IsBusy);
        ToggleBackgroundOptimizationItemCommand = new AsyncParameterRelayCommand(ToggleBackgroundOptimizationItemAsync, CanToggleBackgroundOptimizationItem);
        OptimizeVisualEffectsCommand = new AsyncRelayCommand(OptimizeVisualEffectsAsync, () => !IsBusy);
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
    }

    public ObservableCollection<StartupItem> StartupItems { get; } = [];

    public ObservableCollection<OptimizationItem> BackgroundOptimizationItems { get; } = [];

    public ObservableCollection<VisualEffectSettingStatus> VisualEffectSettings { get; } = [];

    public ICollectionView StartupItemsView { get; }

    public ICollectionView BackgroundOptimizationItemsView { get; }

    public IReadOnlyList<string> StartupFilters { get; } = ["全部", "已启用", "已禁用", "建议优化", "系统/重要", "注册表", "启动文件夹", "计划任务", "未知"];

    public IReadOnlyList<string> BackgroundOptimizationFilters { get; } = ["全部", "Windows 内容", "Widgets", "Copilot", "浏览器后台", "隐私与推荐", "界面体验", "建议优化", "可选", "已优化", "组织策略", "系统不支持"];

    public AsyncRelayCommand ScanCommand { get; }

    public AsyncRelayCommand ScanStartupCommand { get; }

    public AsyncRelayCommand OptimizeStartupCommand { get; }

    public AsyncParameterRelayCommand ToggleStartupItemCommand { get; }

    public AsyncRelayCommand ScanBackgroundOptimizationCommand { get; }

    public AsyncRelayCommand OptimizeBackgroundCommand { get; }

    public AsyncParameterRelayCommand ToggleBackgroundOptimizationItemCommand { get; }

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

    public string BackgroundFilter
    {
        get => _backgroundFilter;
        set
        {
            if (SetProperty(ref _backgroundFilter, value))
            {
                BackgroundOptimizationItemsView.Refresh();
            }
        }
    }

    public string BackgroundSearchText
    {
        get => _backgroundSearchText;
        set
        {
            if (SetProperty(ref _backgroundSearchText, value))
            {
                BackgroundOptimizationItemsView.Refresh();
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

    public int OptimizedBackgroundItemCount => BackgroundOptimizationItems.Count(item => item.CurrentState == OptimizationState.Disabled);

    public int RecommendedBackgroundItemCount => BackgroundOptimizationItems.Count(item => item.CanOptimize && item.Recommendation == OptimizationRecommendation.Recommended && item.RiskLevel == OptimizationRiskLevel.Low);

    public int OptionalBackgroundItemCount => BackgroundOptimizationItems.Count(item => item.Recommendation == OptimizationRecommendation.Optional);

    public int UnsupportedBackgroundItemCount => BackgroundOptimizationItems.Count(item => item.CurrentState == OptimizationState.Unsupported);

    public string OptimizedBackgroundItemCountText => $"已优化 {OptimizedBackgroundItemCount} 项";

    public string RecommendedBackgroundItemCountText => $"建议优化 {RecommendedBackgroundItemCount} 项";

    public string OptionalBackgroundItemCountText => $"可选 {OptionalBackgroundItemCount} 项";

    public string UnsupportedBackgroundItemCountText => $"系统不支持 {UnsupportedBackgroundItemCount} 项";

    public string ComputerName => _system?.ComputerName ?? "尚未检查";

    public string WindowsSummary => _system is null ? "尚未检查" : $"{_system.WindowsEdition} {_system.WindowsVersion}";

    public string WindowsDetails => _system is null ? "尚未检查" : $"{_system.WindowsEdition} {_system.WindowsVersion}，Build {_system.WindowsBuild}";

    public string CpuName => _system?.CpuName ?? "尚未检查";

    public string MemorySummary => _system is null
        ? "尚未检查"
        : $"{FormatBytes((long)_system.AvailableMemoryBytes)} 可用 / {FormatBytes((long)_system.TotalMemoryBytes)}";

    public string SystemDriveFree => _system is null ? "尚未检查" : FormatBytes(_system.SystemDriveFreeBytes);

    public string DriveSummary => _system is null
        ? "尚未检查"
        : $"{FormatBytes(_system.SystemDriveFreeBytes)} 可用 / {FormatBytes(_system.SystemDriveTotalBytes)}";

    public string DiskMediaType => _system?.DiskMediaType ?? "尚未检查";

    public string UptimeSummary => _system is null ? "尚未检查" : $"{_system.Uptime.Days}天 {_system.Uptime.Hours}小时";

    public string RebootPendingText => _system is null ? "尚未检查" : _system.RebootPending ? "是，建议安排重启" : "否";

    public string AdministratorText => _system is null ? "权限：尚未检查" : _system.IsAdministrator ? "权限：管理员" : "权限：普通用户";

    private async Task ScanAsync()
    {
        BeginOperation("正在进行离线检查和优化项扫描……");
        try
        {
            CancellationToken token = _operationCancellation!.Token;
            Task<SystemSnapshot> systemTask = _systemInfoService.CaptureAsync(token);
            Task<List<StartupItem>> startupTask = _startupService.ScanAsync(ShowMicrosoftSystemTasks, token);
            Task<List<OptimizationItem>> backgroundTask = _backgroundOptimizationService.ScanAsync(token);
            Task<List<VisualEffectSettingStatus>> visualEffectsTask = _visualEffectsService.ReadPerformanceProfileAsync(token);

            await Task.WhenAll(systemTask, startupTask, backgroundTask, visualEffectsTask);
            _system = await systemTask;
            List<StartupItem> startupItems = await startupTask;
            List<OptimizationItem> backgroundItems = await backgroundTask;
            ReplaceStartupItems(startupItems);
            ReplaceBackgroundOptimizationItems(backgroundItems);
            ReplaceVisualEffectSettings(await visualEffectsTask);
            NotifySystemProperties();
            StatusText = $"检查完成：发现 {startupItems.Count} 个启动项，已读取视觉效果配置。";
        }
        catch (OperationCanceledException)
        {
            StatusText = "检查已取消。";
        }
        catch (Exception exception)
        {
            StatusText = "检查失败。";
            _dialogService.ShowError(exception.Message, "检查失败");
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
                if (result.Succeeded)
                {
                    succeeded++;
                }
            }

            List<StartupItem> refreshed = await _startupService.ScanAsync(ShowMicrosoftSystemTasks, _operationCancellation!.Token);
            ReplaceStartupItems(refreshed);
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
            if (!result.Succeeded)
            {
                StatusText = $"{actionText}失败：{result.Message}";
                _dialogService.ShowError(result.Message, $"{actionText}启动项失败");
                return;
            }

            List<StartupItem> refreshed = await _startupService.ScanAsync(ShowMicrosoftSystemTasks, _operationCancellation.Token);
            ReplaceStartupItems(refreshed);
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

    private async Task ScanBackgroundOptimizationAsync()
    {
        BeginOperation("正在读取后台优化状态……");
        try
        {
            List<OptimizationItem> items = await _backgroundOptimizationService.ScanAsync(_operationCancellation!.Token);
            ReplaceBackgroundOptimizationItems(items);
            StatusText = $"后台优化扫描完成：发现 {items.Count} 项。";
        }
        catch (OperationCanceledException)
        {
            StatusText = "后台优化扫描已取消。";
        }
        catch (Exception exception)
        {
            StatusText = "后台优化扫描失败。";
            _dialogService.ShowError(exception.Message, "后台优化扫描失败");
        }
        finally
        {
            EndOperation();
        }
    }

    private async Task OptimizeBackgroundAsync()
    {
        List<OptimizationItem> candidates = BackgroundOptimizationItems
            .Where(item => item.CanOptimize && item.Supported && item.RiskLevel == OptimizationRiskLevel.Low && item.Recommendation == OptimizationRecommendation.Recommended)
            .ToList();
        if (candidates.Count == 0)
        {
            _dialogService.ShowInfo("当前没有符合“低风险且建议优化”条件的后台设置。可选项和组织策略项不会自动处理。", "无需一键优化");
            return;
        }

        string itemNames = string.Join("、", candidates.Select(item => item.Name));
        if (!_dialogService.Confirm(
                $"即将优化 {candidates.Count} 项设置：{itemNames}\n\n这些操作不会卸载 Windows 组件、浏览器或应用，也不会修改 Defender、Firewall、Windows Update 或服务。是否继续？",
                "确认一键优化后台"))
        {
            return;
        }

        BeginOperation("正在应用低风险后台优化……");
        try
        {
            int succeeded = 0;
            foreach (OptimizationItem item in candidates)
            {
                _operationCancellation!.Token.ThrowIfCancellationRequested();
                OptimizationOperationResult result = await _backgroundOptimizationRunner.ApplyAsync(item, _operationCancellation.Token);
                if (result.Succeeded)
                {
                    succeeded++;
                }
            }

            List<OptimizationItem> refreshed = await _backgroundOptimizationService.ScanAsync(_operationCancellation!.Token);
            ReplaceBackgroundOptimizationItems(refreshed);
            StatusText = $"后台优化完成：成功 {succeeded} 项，失败 {candidates.Count - succeeded} 项。{GetBackgroundCompletionHint(refreshed)}";
        }
        catch (OperationCanceledException)
        {
            StatusText = "后台优化已取消。";
        }
        catch (Exception exception)
        {
            StatusText = "后台优化失败。";
            _dialogService.ShowError(exception.Message, "后台优化失败");
        }
        finally
        {
            EndOperation();
        }
    }

    private async Task ToggleBackgroundOptimizationItemAsync(object? parameter)
    {
        if (parameter is not OptimizationItem item || !(item.CanOptimize || item.CanRestore))
        {
            return;
        }

        bool restoring = item.CurrentState == OptimizationState.Disabled;
        string actionText = restoring ? "恢复默认" : "优化";
        string message = restoring
            ? $"确定要将“{item.Name}”恢复为 Windows 默认的未配置状态吗？"
            : $"确定要优化“{item.Name}”吗？\n\n{item.Impact}";
        if (!_dialogService.Confirm(message, $"确认{actionText}"))
        {
            return;
        }

        BeginOperation($"正在{actionText}后台设置……");
        try
        {
            OptimizationOperationResult result = await _backgroundOptimizationRunner.ApplyAsync(item, _operationCancellation!.Token);
            if (!result.Succeeded)
            {
                StatusText = $"{actionText}失败：{result.Message}";
                _dialogService.ShowError(result.Message, $"{actionText}失败");
                return;
            }

            List<OptimizationItem> refreshed = await _backgroundOptimizationService.ScanAsync(_operationCancellation.Token);
            ReplaceBackgroundOptimizationItems(refreshed);
            StatusText = $"{actionText}完成：{item.Name}。{GetBackgroundCompletionHint(refreshed)}";
        }
        catch (OperationCanceledException)
        {
            StatusText = "操作已取消。";
        }
        catch (Exception exception)
        {
            StatusText = $"{actionText}失败。";
            _dialogService.ShowError(exception.Message, $"{actionText}失败");
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
            List<VisualEffectSettingStatus> settings = await _visualEffectsService.ReadPerformanceProfileAsync(_operationCancellation.Token);
            ReplaceVisualEffectSettings(settings);
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

    private bool FilterBackgroundOptimizationItem(object value)
    {
        if (value is not OptimizationItem item)
        {
            return false;
        }

        bool filterMatches = BackgroundFilter switch
        {
            "Windows 内容" => item.Category == OptimizationCategory.WindowsContent,
            "Widgets" => item.Category == OptimizationCategory.Widgets,
            "Copilot" => item.Category == OptimizationCategory.Copilot,
            "浏览器后台" => item.Category == OptimizationCategory.BrowserBackground,
            "隐私与推荐" => item.Category == OptimizationCategory.PrivacyAndRecommendations,
            "界面体验" => item.Category == OptimizationCategory.InterfaceExperience,
            "建议优化" => item.Recommendation == OptimizationRecommendation.Recommended,
            "可选" => item.Recommendation == OptimizationRecommendation.Optional,
            "已优化" => item.CurrentState == OptimizationState.Disabled,
            "组织策略" => item.CurrentState == OptimizationState.OrganizationManaged,
            "系统不支持" => item.CurrentState == OptimizationState.Unsupported,
            _ => true
        };
        if (!filterMatches || string.IsNullOrWhiteSpace(BackgroundSearchText))
        {
            return filterMatches;
        }

        string search = BackgroundSearchText.Trim();
        return new[] { item.Name, item.Description, item.Reason, item.Impact, item.RegistryPath, item.RegistryName }
            .Any(text => text.Contains(search, StringComparison.CurrentCultureIgnoreCase));
    }

    private bool CanToggleStartupItem(object? parameter) => !IsBusy && parameter is StartupItem item && (item.CanDisable || item.CanEnable);

    private bool CanToggleBackgroundOptimizationItem(object? parameter) => !IsBusy && parameter is OptimizationItem item && (item.CanOptimize || item.CanRestore);

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

    private void ReplaceBackgroundOptimizationItems(IEnumerable<OptimizationItem> items)
    {
        BackgroundOptimizationItems.Clear();
        foreach (OptimizationItem item in items)
        {
            BackgroundOptimizationItems.Add(item);
        }

        BackgroundOptimizationItemsView.Refresh();
        OnPropertyChanged(nameof(OptimizedBackgroundItemCount));
        OnPropertyChanged(nameof(RecommendedBackgroundItemCount));
        OnPropertyChanged(nameof(OptionalBackgroundItemCount));
        OnPropertyChanged(nameof(UnsupportedBackgroundItemCount));
        OnPropertyChanged(nameof(OptimizedBackgroundItemCountText));
        OnPropertyChanged(nameof(RecommendedBackgroundItemCountText));
        OnPropertyChanged(nameof(OptionalBackgroundItemCountText));
        OnPropertyChanged(nameof(UnsupportedBackgroundItemCountText));
        OptimizeBackgroundCommand.RaiseCanExecuteChanged();
        ToggleBackgroundOptimizationItemCommand.RaiseCanExecuteChanged();
    }

    private void ReplaceVisualEffectSettings(IEnumerable<VisualEffectSettingStatus> settings)
    {
        VisualEffectSettings.Clear();
        foreach (VisualEffectSettingStatus setting in settings)
        {
            VisualEffectSettings.Add(setting);
        }
    }

    private static string GetBackgroundCompletionHint(IEnumerable<OptimizationItem> items)
    {
        return items.Any(item => item.CurrentState == OptimizationState.Disabled && (item.RequiresLogoff || item.RequiresExplorerRestart || item.RequiresRestart))
            ? "部分设置需要重新登录后完全生效。"
            : string.Empty;
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
        ScanBackgroundOptimizationCommand.RaiseCanExecuteChanged();
        OptimizeBackgroundCommand.RaiseCanExecuteChanged();
        ToggleBackgroundOptimizationItemCommand.RaiseCanExecuteChanged();
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

    private static string FormatBytes(long bytes)
    {
        if (bytes < 0)
        {
            return "0 B";
        }

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }
}
