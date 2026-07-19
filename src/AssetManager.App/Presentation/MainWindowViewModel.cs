using System.Collections.ObjectModel;
using System.Windows.Input;
using AssetManager.App.Composition;
using AssetManager.Application.Data;
using AssetManager.Application.Paths;
using AssetManager.Application.Search;
using AssetManager.Application.Startup;
using AssetManager.Domain.Catalog;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Records;
using AssetManager.Domain.Values;

namespace AssetManager.App.Presentation;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly AppRuntimeServices _runtime;
    private readonly IUserDialogService _dialogs;
    private readonly List<AssetRecord> _allRecords = [];
    private readonly Dictionary<FieldId, FieldDefinition> _definitions = [];
    private readonly Dictionary<AssetTypeId, string> _typeNames = [];
    private readonly Dictionary<TagId, string> _tagNames = [];
    private RecordSearchSession? _searchSession;
    private RecordRowViewModel? _selectedRecord;
    private SavedSearch? _selectedSavedSearch;
    private string _savedSearchName = string.Empty;
    private string _statusMessage;
    private CancellationTokenSource? _pathCheckCancellation;
    private bool _isCheckingPaths;
    private bool _isDraft;
    private int _totalMatchCount;

    internal MainWindowViewModel(
        StartupResult startupResult,
        AppRuntimeServices runtime,
        IUserDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(startupResult);
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        DataRoot = startupResult.DataRoot;
        StartupSummary = startupResult.CreatedInitialData
            ? "初期データを作成し、利用準備が整いました。"
            : "既存の管理データを読み込みました。";
        _statusMessage = CreateStatusMessage(startupResult);

        ApplySearchCommand = new RelayCommand(ApplySearch);
        ClearSearchCommand = new RelayCommand(ClearSearch);
        LoadMoreCommand = new RelayCommand(LoadMore, () => HasMoreRecords);
        NewRecordCommand = new RelayCommand(BeginNewRecord);
        SaveRecordCommand = new AsyncRelayCommand(SaveRecordAsync, () => DetailFields.Count > 0);
        DeleteRecordCommand = new AsyncRelayCommand(DeleteSelectedRecordAsync, () => SelectedRecord is not null);
        ToggleFavoriteCommand = new AsyncRelayCommand(ToggleFavoriteAsync, () => SelectedRecord is not null);
        UndoCommand = new AsyncRelayCommand(UndoAsync, () => _runtime.UndoRedo.State.CanUndo);
        RedoCommand = new AsyncRelayCommand(RedoAsync, () => _runtime.UndoRedo.State.CanRedo);
        PickTargetFileCommand = new RelayCommand(() => PickTarget(isFolder: false));
        PickTargetFolderCommand = new RelayCommand(() => PickTarget(isFolder: true));
        OpenTargetCommand = new RelayCommand(OpenTarget, HasSelectedTarget);
        ShowTargetInExplorerCommand = new RelayCommand(ShowTargetInExplorer, HasSelectedTarget);
        ShowManagementCommand = new AsyncRelayCommand(ShowManagementAsync);
        SaveSearchCommand = new AsyncRelayCommand(SaveSearchAsync, () => !string.IsNullOrWhiteSpace(SavedSearchName));
        LoadSavedSearchCommand = new RelayCommand(LoadSavedSearch, () => SelectedSavedSearch is not null);
        DeleteSavedSearchCommand = new AsyncRelayCommand(DeleteSavedSearchAsync, () => SelectedSavedSearch is not null);
        CheckAllPathsCommand = new AsyncRelayCommand(
            () => CheckAllPathsAsync(refreshCachedResults: true),
            () => !IsCheckingPaths);
        CancelPathCheckCommand = new RelayCommand(CancelPathCheck, () => IsCheckingPaths);
    }

    public event EventHandler? GridColumnsChanged;

    public string DataRoot { get; }

    public string StartupSummary { get; }

    public ObservableCollection<RecordRowViewModel> Records { get; } = [];

    public ObservableCollection<SearchFieldViewModel> SearchFields { get; } = [];

    public ObservableCollection<FieldEditorViewModel> DetailFields { get; } = [];

    public ObservableCollection<DisplayColumnOptionViewModel> DisplayColumns { get; } = [];

    public ObservableCollection<SavedSearch> SavedSearches { get; } = [];

    public RecordRowViewModel? SelectedRecord
    {
        get => _selectedRecord;
        set
        {
            if (SetProperty(ref _selectedRecord, value))
            {
                _isDraft = false;
                LoadEditors(value?.Record);
                OnPropertyChanged(nameof(EditorTitle));
                OnPropertyChanged(nameof(IsEditorVisible));
                RelayCommand.RefreshCanExecute();
            }
        }
    }

    public SavedSearch? SelectedSavedSearch
    {
        get => _selectedSavedSearch;
        set
        {
            if (SetProperty(ref _selectedSavedSearch, value))
            {
                RelayCommand.RefreshCanExecute();
            }
        }
    }

    public string SavedSearchName
    {
        get => _savedSearchName;
        set
        {
            if (SetProperty(ref _savedSearchName, value))
            {
                RelayCommand.RefreshCanExecute();
            }
        }
    }

    public string EditorTitle => _isDraft
        ? "新しい素材"
        : SelectedRecord?.Name ?? "素材を選択してください";

    public bool IsEditorVisible => _isDraft || SelectedRecord is not null;

    public int TotalMatchCount
    {
        get => _totalMatchCount;
        private set => SetProperty(ref _totalMatchCount, value);
    }

    public bool HasMoreRecords => _searchSession?.HasMore == true;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ICommand ApplySearchCommand { get; }

    public ICommand ClearSearchCommand { get; }

    public ICommand LoadMoreCommand { get; }

    public ICommand NewRecordCommand { get; }

    public ICommand SaveRecordCommand { get; }

    public ICommand DeleteRecordCommand { get; }

    public ICommand ToggleFavoriteCommand { get; }

    public ICommand UndoCommand { get; }

    public ICommand RedoCommand { get; }

    public ICommand PickTargetFileCommand { get; }

    public ICommand PickTargetFolderCommand { get; }

    public ICommand OpenTargetCommand { get; }

    public ICommand ShowTargetInExplorerCommand { get; }

    public ICommand ShowManagementCommand { get; }

    public ICommand SaveSearchCommand { get; }

    public ICommand LoadSavedSearchCommand { get; }

    public ICommand DeleteSavedSearchCommand { get; }

    public ICommand CheckAllPathsCommand { get; }

    public ICommand CancelPathCheckCommand { get; }

    public bool IsCheckingPaths
    {
        get => _isCheckingPaths;
        private set
        {
            if (SetProperty(ref _isCheckingPaths, value))
            {
                RelayCommand.RefreshCanExecute();
            }
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await ReloadSnapshotAsync(cancellationToken);
        var savedSearches = await _runtime.SearchConfiguration.LoadSavedSearchesAsync(cancellationToken);
        SavedSearches.Clear();
        foreach (var search in savedSearches)
        {
            SavedSearches.Add(search);
        }

        ApplySearch();
    }

    public Task StartInitialPathCheckAsync()
    {
        return CheckAllPathsAsync(refreshCachedResults: false);
    }

    public void Dispose()
    {
        _pathCheckCancellation?.Cancel();
        _pathCheckCancellation?.Dispose();
        _pathCheckCancellation = null;
    }

    public IReadOnlyList<DisplayColumnOptionViewModel> GetVisibleDynamicColumns()
    {
        return DisplayColumns.Where(column => column.IsVisible).ToArray();
    }

    private async Task ReloadSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _runtime.Store.LoadAsync(cancellationToken);
        _allRecords.Clear();
        _allRecords.AddRange(snapshot.Records);
        _definitions.Clear();
        foreach (var definition in snapshot.FieldDefinitions)
        {
            _definitions[definition.Id] = definition;
        }

        _typeNames.Clear();
        foreach (var type in snapshot.AssetTypes)
        {
            _typeNames[type.Id] = type.Name;
        }

        _tagNames.Clear();
        foreach (var tag in snapshot.Tags)
        {
            _tagNames[tag.Id] = tag.Name;
        }

        BuildSearchFields(snapshot);
        await BuildDisplayColumnsAsync(snapshot, cancellationToken);
    }

    private void BuildSearchFields(AssetManagerDataSnapshot snapshot)
    {
        SearchFields.Clear();
        foreach (var definition in snapshot.FieldDefinitions)
        {
            var options = CreateOptions(definition, snapshot);
            SearchFields.Add(new SearchFieldViewModel(definition, options));
        }
    }

    private async Task BuildDisplayColumnsAsync(
        AssetManagerDataSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var settings = await _runtime.SearchConfiguration.LoadViewColumnsAsync(cancellationToken);
        var settingMap = settings.ToDictionary(setting => setting.Id, StringComparer.Ordinal);
        DisplayColumns.Clear();
        foreach (var definition in snapshot.FieldDefinitions.Where(definition => !definition.MainTableRequired))
        {
            var visible = settingMap.TryGetValue(definition.Id.Value, out var setting)
                ? setting.Visible
                : definition.MainTableVisible;
            var option = new DisplayColumnOptionViewModel(definition, visible);
            option.VisibilityChanged += OnDisplayColumnVisibilityChanged;
            DisplayColumns.Add(option);
        }

        GridColumnsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static IEnumerable<SelectableOptionViewModel> CreateOptions(
        FieldDefinition definition,
        AssetManagerDataSnapshot snapshot)
    {
        return definition.Type switch
        {
            FieldType.SingleSelect or FieldType.MultiSelect => definition.Options.Select(
                option => new SelectableOptionViewModel(option.Id.Value, option.Label)),
            FieldType.AssetTypeSet => snapshot.AssetTypes.Select(
                type => new SelectableOptionViewModel(type.Id.Value, type.Name)),
            FieldType.TagSet => snapshot.Tags.Select(
                tag => new SelectableOptionViewModel(tag.Id.Value, tag.Name)),
            FieldType.RecordStatus => new[]
            {
                new SelectableOptionViewModel("unchecked", "未確認"),
                new SelectableOptionViewModel("available", "利用可能"),
                new SelectableOptionViewModel("unavailable", "利用不可"),
                new SelectableOptionViewModel("archived", "アーカイブ"),
            },
            _ => [],
        };
    }

    private SearchQuery CreateSearchQuery()
    {
        return new SearchQuery(SearchFields
            .Select(field => new KeyValuePair<FieldId, FieldSearchCondition?>(
                field.Definition.Id,
                field.CreateCondition()))
            .Where(pair => pair.Value is not null)
            .Select(pair => new KeyValuePair<FieldId, FieldSearchCondition>(pair.Key, pair.Value!)));
    }

    private void ApplySearch()
    {
        _searchSession = new RecordSearchSession(_allRecords, CreateSearchQuery());
        Records.Clear();
        foreach (var record in _searchSession.VisibleRecords)
        {
            Records.Add(CreateRow(record));
        }

        TotalMatchCount = _searchSession.TotalCount;
        OnPropertyChanged(nameof(HasMoreRecords));
        StatusMessage = $"{TotalMatchCount} 件中 {Records.Count} 件を表示しています。";
        RelayCommand.RefreshCanExecute();
    }

    private void ClearSearch()
    {
        foreach (var field in SearchFields)
        {
            field.Clear();
        }

        ApplySearch();
    }

    private void LoadMore()
    {
        if (_searchSession is null)
        {
            return;
        }

        foreach (var record in _searchSession.LoadMore())
        {
            Records.Add(CreateRow(record));
        }

        OnPropertyChanged(nameof(HasMoreRecords));
        StatusMessage = $"{TotalMatchCount} 件中 {Records.Count} 件を表示しています。";
        RelayCommand.RefreshCanExecute();
    }

    private RecordRowViewModel CreateRow(AssetRecord record)
    {
        return new RecordRowViewModel(record, _typeNames, _tagNames, _definitions);
    }

    private void BeginNewRecord()
    {
        SelectedRecord = null;
        _isDraft = true;
        LoadEditors(null);
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(IsEditorVisible));
        StatusMessage = "新しい素材の情報を入力してください。";
        RelayCommand.RefreshCanExecute();
    }

    private void LoadEditors(AssetRecord? record)
    {
        DetailFields.Clear();
        if (record is null && !_isDraft)
        {
            return;
        }

        foreach (var definition in _definitions.Values.Where(definition => definition.DetailVisible))
        {
            var searchField = SearchFields.FirstOrDefault(field => field.Definition.Id == definition.Id);
            var options = searchField?.Options.Select(option => new SelectableOptionViewModel(option.Id, option.Label));
            DetailFields.Add(new FieldEditorViewModel(
                definition,
                record?.Values.GetValueOrDefault(definition.Id),
                options));
        }
    }

    private async Task SaveRecordAsync()
    {
        try
        {
            var values = DetailFields.ToDictionary(
                field => field.Definition.Id,
                CreateEditorValue);
            AssetRecord saved;
            if (_isDraft)
            {
                saved = await _runtime.Records.CreateAsync(values);
            }
            else if (SelectedRecord is not null)
            {
                saved = await _runtime.Records.UpdateAsync(SelectedRecord.Record.Id, values);
            }
            else
            {
                return;
            }

            await ReloadAndSelectAsync(saved.Id);
            StatusMessage = $"「{CreateRow(saved).Name}」を保存しました。";
        }
        catch (Exception exception)
        {
            _dialogs.ShowError($"レコードを保存できませんでした。{Environment.NewLine}{exception.Message}");
            StatusMessage = "保存に失敗しました。";
        }
    }

    private FieldValue? CreateEditorValue(FieldEditorViewModel editor)
    {
        var value = editor.CreateValue();
        if (value is not TargetPathFieldValue target)
        {
            return value;
        }

        var current = SelectedRecord?.Record.TargetPath;
        if (!_isDraft
            && current is not null
            && current.Kind == target.Kind
            && string.Equals(current.Path, target.Path, StringComparison.OrdinalIgnoreCase))
        {
            return target;
        }

        return _runtime.PathRegistration.RegisterTarget(target.Path);
    }

    private async Task DeleteSelectedRecordAsync()
    {
        var selected = SelectedRecord;
        if (selected is null
            || !_dialogs.Confirm(
                $"「{selected.Name}」の管理レコードを削除しますか？{Environment.NewLine}関連付けた実ファイルは削除されません。",
                "AssetManager - レコード削除"))
        {
            return;
        }

        try
        {
            await _runtime.Records.DeleteAsync(selected.Record.Id);
            await ReloadSnapshotAsync();
            SelectedRecord = null;
            ApplySearch();
            StatusMessage = "管理レコードを削除しました。実ファイルは変更していません。";
        }
        catch (Exception exception)
        {
            _dialogs.ShowError($"レコードを削除できませんでした。{Environment.NewLine}{exception.Message}");
        }
    }

    private async Task ToggleFavoriteAsync()
    {
        if (SelectedRecord is null)
        {
            return;
        }

        var id = SelectedRecord.Record.Id;
        var favorite = !SelectedRecord.IsFavorite;
        await _runtime.Records.UpdateAsync(
            id,
            new Dictionary<FieldId, FieldValue?>
            {
                [BuiltInFieldIds.Favorite] = new BooleanFieldValue(favorite),
            });
        await ReloadAndSelectAsync(id);
        StatusMessage = favorite ? "お気に入りに追加しました。" : "お気に入りを解除しました。";
    }

    private async Task UndoAsync()
    {
        try
        {
            if (await _runtime.UndoRedo.UndoAsync())
            {
                await ReloadSnapshotAsync();
                SelectedRecord = null;
                ApplySearch();
                StatusMessage = "操作を元に戻しました。";
            }
        }
        catch (Exception exception)
        {
            _dialogs.ShowError($"操作を元に戻せませんでした。{Environment.NewLine}{exception.Message}");
        }
    }

    private async Task RedoAsync()
    {
        try
        {
            if (await _runtime.UndoRedo.RedoAsync())
            {
                await ReloadSnapshotAsync();
                SelectedRecord = null;
                ApplySearch();
                StatusMessage = "操作をやり直しました。";
            }
        }
        catch (Exception exception)
        {
            _dialogs.ShowError($"操作をやり直せませんでした。{Environment.NewLine}{exception.Message}");
        }
    }

    private void PickTarget(bool isFolder)
    {
        try
        {
            var path = isFolder
                ? _runtime.PathRegistration.PickTargetFolder()
                : _runtime.PathRegistration.PickTargetFile();
            if (path is null)
            {
                return;
            }

            var editor = DetailFields.Single(field => field.Definition.Id == BuiltInFieldIds.TargetPath);
            editor.TargetPathKind = path.Kind;
            editor.Text = path.Path;
        }
        catch (Exception exception)
        {
            _dialogs.ShowError($"対象パスを登録できませんでした。{Environment.NewLine}{exception.Message}");
        }
    }

    private bool HasSelectedTarget()
    {
        return SelectedRecord?.Record.TargetPath is not null;
    }

    private void OpenTarget()
    {
        if (SelectedRecord?.Record.TargetPath is { } target)
        {
            TryShellAction(() => _runtime.Shell.Open(target.Path));
        }
    }

    private void ShowTargetInExplorer()
    {
        if (SelectedRecord?.Record.TargetPath is { } target)
        {
            var kind = target.Kind == TargetPathKind.File ? PathEntryKind.File : PathEntryKind.Folder;
            TryShellAction(() => _runtime.Shell.ShowInExplorer(target.Path, kind));
        }
    }

    private void TryShellAction(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            _dialogs.ShowError($"Windowsでパスを開けませんでした。{Environment.NewLine}{exception.Message}");
        }
    }

    private async Task ShowManagementAsync()
    {
        _runtime.ShowManagementWindow();
        try
        {
            await ReloadSnapshotAsync();
            SelectedRecord = null;
            ApplySearch();
        }
        catch (Exception exception)
        {
            _dialogs.ShowError($"管理画面の変更を再読み込みできませんでした。{Environment.NewLine}{exception.Message}");
        }
    }

    private async Task SaveSearchAsync()
    {
        try
        {
            var saved = await _runtime.SearchConfiguration.SaveSearchAsync(
                SavedSearchName.Trim(),
                CreateSearchQuery());
            SavedSearches.Add(saved);
            SelectedSavedSearch = saved;
            SavedSearchName = string.Empty;
            StatusMessage = "検索条件を保存しました。";
        }
        catch (Exception exception)
        {
            _dialogs.ShowError($"検索条件を保存できませんでした。{Environment.NewLine}{exception.Message}");
        }
    }

    private void LoadSavedSearch()
    {
        var selected = SelectedSavedSearch;
        if (selected is null)
        {
            return;
        }

        foreach (var field in SearchFields)
        {
            selected.Query.Conditions.TryGetValue(field.Definition.Id, out var condition);
            field.Apply(condition);
        }

        ApplySearch();
        StatusMessage = $"検索条件「{selected.Name}」を読み込みました。";
    }

    private async Task DeleteSavedSearchAsync()
    {
        var selected = SelectedSavedSearch;
        if (selected is null
            || !_dialogs.Confirm($"検索条件「{selected.Name}」を削除しますか？", "AssetManager - 検索条件削除"))
        {
            return;
        }

        await _runtime.SearchConfiguration.DeleteSearchAsync(selected.Id);
        _ = SavedSearches.Remove(selected);
        SelectedSavedSearch = null;
        StatusMessage = "保存済み検索条件を削除しました。";
    }

    private async void OnDisplayColumnVisibilityChanged(object? sender, EventArgs e)
    {
        GridColumnsChanged?.Invoke(this, EventArgs.Empty);
        try
        {
            var settings = DisplayColumns.Select((column, index) => new ViewColumnSetting(
                column.Definition.Id.Value,
                index,
                140,
                column.IsVisible)).ToArray();
            await _runtime.SearchConfiguration.SaveViewColumnsAsync(settings);
        }
        catch (Exception exception)
        {
            _dialogs.ShowError($"表示カラム設定を保存できませんでした。{Environment.NewLine}{exception.Message}");
        }
    }

    private async Task ReloadAndSelectAsync(RecordId id)
    {
        await ReloadSnapshotAsync();
        ApplySearch();
        SelectedRecord = Records.FirstOrDefault(row => row.Record.Id == id);
        _isDraft = false;
        OnPropertyChanged(nameof(EditorTitle));
    }

    private async Task CheckAllPathsAsync(bool refreshCachedResults)
    {
        if (IsCheckingPaths)
        {
            return;
        }

        _pathCheckCancellation?.Dispose();
        _pathCheckCancellation = new CancellationTokenSource();
        IsCheckingPaths = true;
        var progress = new Progress<PathCheckProgress>(item =>
        {
            StatusMessage = item.IsCanceled
                ? $"パス確認をキャンセルしました（未確認 {item.UncheckedCount} 件）。"
                : $"パスを確認しています（{item.CheckedCount} / {item.TotalCount}）。";
        });
        try
        {
            var result = await _runtime.PathChecks.CheckAllRecordsAsync(
                refreshCachedResults,
                progress,
                _pathCheckCancellation.Token);
            StatusMessage = result.IsCanceled
                ? $"パス確認をキャンセルしました（未確認 {result.TotalCount - result.CheckedCount} 件）。"
                : $"パス確認が完了しました（{result.CheckedCount} 件）。";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "パス確認をキャンセルしました。";
        }
        catch (Exception exception)
        {
            _dialogs.ShowError($"パス確認に失敗しました。{Environment.NewLine}{exception.Message}");
            StatusMessage = "パス確認に失敗しました。";
        }
        finally
        {
            IsCheckingPaths = false;
        }
    }

    private void CancelPathCheck()
    {
        _pathCheckCancellation?.Cancel();
    }

    private static string CreateStatusMessage(StartupResult result)
    {
        if (result.ExcludedRecordCount > 0 || result.RepairedValueCount > 0)
        {
            return $"読み込み完了: 修復 {result.RepairedValueCount} 件 / 除外 {result.ExcludedRecordCount} 件";
        }

        return "管理データの読み込みが完了しました。";
    }
}
