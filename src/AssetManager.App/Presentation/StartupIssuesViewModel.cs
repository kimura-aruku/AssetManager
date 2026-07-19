using AssetManager.Application.Startup;

namespace AssetManager.App.Presentation;

public sealed class StartupIssuesViewModel
{
    public StartupIssuesViewModel(StartupResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Repairs = result.Repairs;
        ExcludedRecords = result.ExcludedRecords;
    }

    public IReadOnlyList<StartupRepairDetail> Repairs { get; }

    public IReadOnlyList<StartupExcludedRecordDetail> ExcludedRecords { get; }
}
