using System.Windows;
using PcCare.App.Services;
using PcCare.App.ViewModels;
using PcCare.Core.Services;
using PcCare.Windows.Services;

namespace PcCare.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var catalog = new WindowsCleanupCatalog();
        var scanner = new CleanupScanner();
        var executor = new CleanupExecutor(scanner);
        var outputResolver = new OutputDirectoryResolver();
        var jobStore = new CleanupJobStore(outputResolver);
        CommandLineOptions options = CommandLineOptions.Parse(e.Args);

        if (options.IsElevatedCleanup)
        {
            int exitCode = await ElevatedCleanupRunner.RunAsync(options, catalog, executor, jobStore);
            Shutdown(exitCode);
            return;
        }

        var coordinator = new CleanupCoordinator(catalog, executor, jobStore);
        var dialogService = new DialogService();
        var visualEffectsService = new VisualEffectsService(outputResolver);
        var viewModel = new MainViewModel(
            new SystemInfoService(),
            new StartupService(),
            catalog,
            scanner,
            coordinator,
            new ReportWriter(),
            outputResolver,
            visualEffectsService,
            dialogService);

        var window = new MainWindow { DataContext = viewModel };
        MainWindow = window;
        window.Show();
    }
}
