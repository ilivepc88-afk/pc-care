using PcCare.App.Infrastructure;
using PcCare.Core.Models;
using PcCare.Core.Services;

namespace PcCare.App.ViewModels;

public sealed class CleanupCategoryItemViewModel : ObservableObject
{
    private bool _isSelected;

    public CleanupCategoryItemViewModel(CleanupCategoryScanResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Id = result.Rule.Id;
        DisplayName = result.Rule.DisplayName;
        FileCount = result.Candidates.Count;
        SizeBytes = result.TotalBytes;
        RequiresAdministrator = result.Rule.RequiresAdministrator;
        RiskDescription = result.Rule.RiskDescription;
        _isSelected = result.Rule.DefaultSelected;
    }

    public string Id { get; }

    public string DisplayName { get; }

    public int FileCount { get; }

    public long SizeBytes { get; }

    public string SizeText => ReportWriter.FormatBytes(SizeBytes);

    public bool RequiresAdministrator { get; }

    public string RiskDescription { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
