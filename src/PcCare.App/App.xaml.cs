using System.Windows;
using PcCare.App.Services;
using PcCare.App.ViewModels;
using PcCare.Core.Services;
using PcCare.Windows.Services;

namespace PcCare.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var outputResolver = new OutputDirectoryResolver();
        var dialogService = new DialogService();
        var visualEffectsService = new VisualEffectsService();
        var viewModel = new MainViewModel(
            new SystemInfoService(),
            new StartupService(),
            new ReportWriter(),
            outputResolver,
            visualEffectsService,
            dialogService);

        var window = new MainWindow { DataContext = viewModel };
        MainWindow = window;
        window.Show();
    }
}
