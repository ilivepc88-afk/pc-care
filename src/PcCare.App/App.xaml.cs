using System.Windows;
using PcCare.App.Services;
using PcCare.App.ViewModels;
using PcCare.Windows.Services;

namespace PcCare.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (StartupOperationCommandLine.TryParse(e.Args, out var operation) && operation is not null)
        {
            var result = new StartupManager().ApplyAsync(operation).GetAwaiter().GetResult();
            Shutdown(result.Succeeded ? 0 : 1);
            return;
        }

        if (StartupOperationCommandLine.TryParseBackgroundOptimization(e.Args, out var backgroundOperation) && backgroundOperation is not null)
        {
            var result = new BackgroundOptimizationManager().ApplyAsync(backgroundOperation).GetAwaiter().GetResult();
            Shutdown(result.Succeeded ? 0 : 1);
            return;
        }

        var dialogService = new DialogService();
        var visualEffectsService = new VisualEffectsService();
        var viewModel = new MainViewModel(
            new SystemInfoService(),
            new StartupService(),
            new ElevatedStartupOperationRunner(new StartupManager()),
            new BackgroundOptimizationService(),
            new ElevatedBackgroundOptimizationRunner(new BackgroundOptimizationManager()),
            visualEffectsService,
            new HardwarePowerService(),
            new PowerOptimizationManager(),
            dialogService);

        var window = new MainWindow { DataContext = viewModel };
        MainWindow = window;
        window.Show();
    }
}
