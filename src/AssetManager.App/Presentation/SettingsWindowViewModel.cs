using System.Windows.Input;
using AssetManager.Application.Settings;
using AssetManager.Infrastructure.Operations;

namespace AssetManager.App.Presentation;

public sealed class SettingsWindowViewModel : ObservableObject
{
    private readonly AppSettingsService _settings;
    private readonly DataRootMigrationService _migration;
    private readonly Func<string?> _pickFolder;
    private readonly IUserDialogService _dialogs;
    private readonly Action _requestShutdown;
    private bool _checkPathsOnStartup;
    private string _licenseWarningDays = "365";
    private string _destinationRoot = string.Empty;
    private string _statusMessage = "設定を読み込んでいます。";

    public SettingsWindowViewModel(
        AppSettingsService settings,
        DataRootMigrationService migration,
        string currentDataRoot,
        Func<string?> pickFolder,
        IUserDialogService dialogs,
        Action requestShutdown)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _migration = migration ?? throw new ArgumentNullException(nameof(migration));
        CurrentDataRoot = currentDataRoot;
        _pickFolder = pickFolder ?? throw new ArgumentNullException(nameof(pickFolder));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _requestShutdown = requestShutdown ?? throw new ArgumentNullException(nameof(requestShutdown));
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        BrowseDestinationCommand = new RelayCommand(BrowseDestination);
        ChangeDataRootCommand = new AsyncRelayCommand(
            ChangeDataRootAsync,
            () => !string.IsNullOrWhiteSpace(DestinationRoot));
    }

    public string CurrentDataRoot { get; }

    public bool CheckPathsOnStartup
    {
        get => _checkPathsOnStartup;
        set => SetProperty(ref _checkPathsOnStartup, value);
    }

    public string LicenseWarningDays
    {
        get => _licenseWarningDays;
        set => SetProperty(ref _licenseWarningDays, value);
    }

    public string DestinationRoot
    {
        get => _destinationRoot;
        set
        {
            if (SetProperty(ref _destinationRoot, value))
            {
                RelayCommand.RefreshCanExecute();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ICommand SaveSettingsCommand { get; }

    public ICommand BrowseDestinationCommand { get; }

    public ICommand ChangeDataRootCommand { get; }

    public async Task InitializeAsync()
    {
        try
        {
            var settings = await _settings.LoadAsync();
            CheckPathsOnStartup = settings.CheckPathsOnStartup;
            LicenseWarningDays = settings.LicenseWarningDays.ToString(System.Globalization.CultureInfo.InvariantCulture);
            StatusMessage = "設定を読み込みました。";
        }
        catch (Exception exception)
        {
            StatusMessage = "設定を読み込めませんでした。";
            _dialogs.ShowError($"設定を読み込めませんでした。{Environment.NewLine}{exception.Message}", exception: exception);
        }
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            if (!int.TryParse(
                    LicenseWarningDays,
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var warningDays))
            {
                throw new ArgumentException("ライセンス警告日数は1以上の整数で入力してください。");
            }

            await _settings.SaveAsync(new AppSettings(CheckPathsOnStartup, warningDays));
            StatusMessage = "設定を保存しました。表示への反映は次回起動時からです。";
        }
        catch (Exception exception)
        {
            _dialogs.ShowError($"設定を保存できませんでした。{Environment.NewLine}{exception.Message}", exception: exception);
        }
    }

    private void BrowseDestination()
    {
        var selected = _pickFolder();
        if (!string.IsNullOrWhiteSpace(selected))
        {
            DestinationRoot = selected;
        }
    }

    private async Task ChangeDataRootAsync()
    {
        if (!_dialogs.Confirm(
                $"管理データを次の空フォルダーへコピーして保存先を切り替えます。{Environment.NewLine}{DestinationRoot}{Environment.NewLine}{Environment.NewLine}素材の実ファイルは移動しません。続行しますか？",
                "AssetManager - データ保存先変更"))
        {
            return;
        }

        try
        {
            StatusMessage = "管理データをコピーして全件検証しています。";
            var result = await _migration.ChangeAsync(CurrentDataRoot, DestinationRoot);
            var deleteSource = _dialogs.Confirm(
                $"{result.RecordCount}件の管理データを検証し、保存先を切り替えました。{Environment.NewLine}{Environment.NewLine}元の管理データフォルダーを削除しますか？{Environment.NewLine}{result.SourceRoot}{Environment.NewLine}{Environment.NewLine}登録素材の実ファイルは削除されません。「いいえ」を選ぶと元の管理データを残します。",
                "AssetManager - 元データの確認");
            if (deleteSource)
            {
                await _migration.DeleteSourceAsync(result);
            }

            _dialogs.ShowInformation(
                "データ保存先を変更しました。新しい保存先を使用するため、AssetManagerを終了します。",
                "AssetManager - 再起動が必要です");
            _requestShutdown();
        }
        catch (Exception exception)
        {
            StatusMessage = "保存先を変更できませんでした。元の保存先を引き続き使用します。";
            _dialogs.ShowError($"データ保存先を変更できませんでした。{Environment.NewLine}{exception.Message}", exception: exception);
        }
    }
}
