using System.Windows;

namespace PcCare.App.Services;

public interface IDialogService
{
    bool Confirm(string message, string title);

    void ShowInfo(string message, string title);

    void ShowError(string message, string title);
}

public sealed class DialogService : IDialogService
{
    public bool Confirm(string message, string title)
    {
        return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    public void ShowInfo(string message, string title)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void ShowError(string message, string title)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
