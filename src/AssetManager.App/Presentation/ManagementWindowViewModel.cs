using System.Collections.ObjectModel;
using System.Windows.Input;
using AssetManager.Application.Catalog;
using AssetManager.Application.Data;
using AssetManager.Application.Fields;
using AssetManager.Domain.Catalog;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Licensing;

namespace AssetManager.App.Presentation;

public sealed class ManagementWindowViewModel : ObservableObject
{
    private readonly IAssetManagerDataStore _store;
    private readonly FieldApplicationService _fields;
    private readonly CatalogApplicationService _catalog;
    private readonly IUserDialogService _dialogs;
    private FieldDefinition? _selectedField;
    private AssetTypeDefinition? _selectedAssetType;
    private SelectionOption? _selectedAcquisitionSource;
    private LicensePresetDefinition? _selectedLicensePreset;
    private TagCategoryDefinition? _selectedCategory;
    private TagDefinition? _selectedTag;
    private string _fieldName = string.Empty;
    private FieldType _fieldType = FieldType.Text;
    private string _fieldOptions = string.Empty;
    private bool _fieldMainVisible;
    private bool _fieldDetailVisible = true;
    private string _assetTypeName = string.Empty;
    private string _assetTypeExtensions = string.Empty;
    private string _acquisitionSourceName = string.Empty;
    private string _licensePresetName = string.Empty;
    private string _categoryName = string.Empty;
    private string _tagName = string.Empty;
    private string _tagColor = "#4F7CAC";
    private TagCategoryDefinition? _tagCategory;

    public ManagementWindowViewModel(
        IAssetManagerDataStore store,
        FieldApplicationService fields,
        CatalogApplicationService catalog,
        IUserDialogService dialogs)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _fields = fields ?? throw new ArgumentNullException(nameof(fields));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        SaveFieldCommand = new AsyncRelayCommand(SaveFieldAsync, () => !string.IsNullOrWhiteSpace(FieldName));
        NewFieldCommand = new RelayCommand(ClearFieldForm);
        DeleteFieldCommand = new AsyncRelayCommand(DeleteFieldAsync, () => SelectedField is not null);
        SaveAssetTypeCommand = new AsyncRelayCommand(SaveAssetTypeAsync, () => !string.IsNullOrWhiteSpace(AssetTypeName));
        NewAssetTypeCommand = new RelayCommand(ClearAssetTypeForm);
        DeleteAssetTypeCommand = new AsyncRelayCommand(DeleteAssetTypeAsync, () => SelectedAssetType is not null);
        SaveAcquisitionSourceCommand = new AsyncRelayCommand(
            SaveAcquisitionSourceAsync,
            () => !string.IsNullOrWhiteSpace(AcquisitionSourceName));
        NewAcquisitionSourceCommand = new RelayCommand(ClearAcquisitionSourceForm);
        DeleteAcquisitionSourceCommand = new AsyncRelayCommand(
            DeleteAcquisitionSourceAsync,
            () => SelectedAcquisitionSource is not null);
        SaveLicensePresetCommand = new AsyncRelayCommand(
            SaveLicensePresetAsync,
            () => !string.IsNullOrWhiteSpace(LicensePresetName));
        NewLicensePresetCommand = new RelayCommand(ClearLicensePresetForm);
        DeleteLicensePresetCommand = new AsyncRelayCommand(
            DeleteLicensePresetAsync,
            () => SelectedLicensePreset is not null);
        SaveCategoryCommand = new AsyncRelayCommand(SaveCategoryAsync, () => !string.IsNullOrWhiteSpace(CategoryName));
        NewCategoryCommand = new RelayCommand(ClearCategoryForm);
        DeleteCategoryCommand = new AsyncRelayCommand(DeleteCategoryAsync, () => SelectedCategory is not null);
        SaveTagCommand = new AsyncRelayCommand(SaveTagAsync, () => !string.IsNullOrWhiteSpace(TagName));
        NewTagCommand = new RelayCommand(ClearTagForm);
        DeleteTagCommand = new AsyncRelayCommand(DeleteTagAsync, () => SelectedTag is not null);

    }

    public ObservableCollection<FieldDefinition> CustomFields { get; } = [];

    public ObservableCollection<AssetTypeDefinition> AssetTypes { get; } = [];

    public ObservableCollection<SelectionOption> AcquisitionSources { get; } = [];

    public ObservableCollection<LicensePresetDefinition> LicensePresets { get; } = [];

    public ObservableCollection<LicensePresetConditionViewModel> LicensePresetConditions { get; } =
        new(LicenseConditionCatalog.All.Select(condition => new LicensePresetConditionViewModel(condition)));

    public ObservableCollection<TagCategoryDefinition> Categories { get; } = [];

    public ObservableCollection<TagDefinition> Tags { get; } = [];

    public IReadOnlyList<FieldType> CustomFieldTypes { get; } =
    [
        FieldType.Text,
        FieldType.MultilineText,
        FieldType.Number,
        FieldType.Date,
        FieldType.Boolean,
        FieldType.Url,
        FieldType.SingleSelect,
        FieldType.MultiSelect,
        FieldType.FilePath,
        FieldType.FolderPath,
    ];

    public FieldDefinition? SelectedField
    {
        get => _selectedField;
        set
        {
            if (SetProperty(ref _selectedField, value) && value is not null)
            {
                FieldName = value.Label;
                FieldType = value.Type;
                FieldOptions = string.Join(", ", value.Options.Select(option => option.Label));
                FieldMainVisible = value.MainTableVisible;
                FieldDetailVisible = value.DetailVisible;
                RelayCommand.RefreshCanExecute();
            }
        }
    }

    public AssetTypeDefinition? SelectedAssetType
    {
        get => _selectedAssetType;
        set
        {
            if (SetProperty(ref _selectedAssetType, value) && value is not null)
            {
                AssetTypeName = value.Name;
                AssetTypeExtensions = string.Join(", ", value.Extensions);
                RelayCommand.RefreshCanExecute();
            }
        }
    }

    public SelectionOption? SelectedAcquisitionSource
    {
        get => _selectedAcquisitionSource;
        set
        {
            if (SetProperty(ref _selectedAcquisitionSource, value) && value is not null)
            {
                AcquisitionSourceName = value.Label;
                RelayCommand.RefreshCanExecute();
            }
        }
    }

    public LicensePresetDefinition? SelectedLicensePreset
    {
        get => _selectedLicensePreset;
        set
        {
            if (SetProperty(ref _selectedLicensePreset, value) && value is not null)
            {
                LicensePresetName = value.Name;
                ApplyPresetTerms(value.Terms);
                RelayCommand.RefreshCanExecute();
            }
        }
    }

    public TagCategoryDefinition? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value) && value is not null)
            {
                CategoryName = value.Name;
                RelayCommand.RefreshCanExecute();
            }
        }
    }

    public TagDefinition? SelectedTag
    {
        get => _selectedTag;
        set
        {
            if (SetProperty(ref _selectedTag, value) && value is not null)
            {
                TagName = value.Name;
                TagColor = value.Color.Value;
                TagCategory = Categories.FirstOrDefault(category => category.Id == value.CategoryId);
                RelayCommand.RefreshCanExecute();
            }
        }
    }

    public string FieldName
    {
        get => _fieldName;
        set { if (SetProperty(ref _fieldName, value)) RelayCommand.RefreshCanExecute(); }
    }

    public FieldType FieldType
    {
        get => _fieldType;
        set => SetProperty(ref _fieldType, value);
    }

    public string FieldOptions
    {
        get => _fieldOptions;
        set => SetProperty(ref _fieldOptions, value);
    }

    public bool FieldMainVisible
    {
        get => _fieldMainVisible;
        set => SetProperty(ref _fieldMainVisible, value);
    }

    public bool FieldDetailVisible
    {
        get => _fieldDetailVisible;
        set => SetProperty(ref _fieldDetailVisible, value);
    }

    public string AssetTypeName
    {
        get => _assetTypeName;
        set { if (SetProperty(ref _assetTypeName, value)) RelayCommand.RefreshCanExecute(); }
    }

    public string AssetTypeExtensions
    {
        get => _assetTypeExtensions;
        set => SetProperty(ref _assetTypeExtensions, value);
    }

    public string AcquisitionSourceName
    {
        get => _acquisitionSourceName;
        set { if (SetProperty(ref _acquisitionSourceName, value)) RelayCommand.RefreshCanExecute(); }
    }

    public string LicensePresetName
    {
        get => _licensePresetName;
        set { if (SetProperty(ref _licensePresetName, value)) RelayCommand.RefreshCanExecute(); }
    }

    public string CategoryName
    {
        get => _categoryName;
        set { if (SetProperty(ref _categoryName, value)) RelayCommand.RefreshCanExecute(); }
    }

    public string TagName
    {
        get => _tagName;
        set { if (SetProperty(ref _tagName, value)) RelayCommand.RefreshCanExecute(); }
    }

    public string TagColor
    {
        get => _tagColor;
        set => SetProperty(ref _tagColor, value);
    }

    public TagCategoryDefinition? TagCategory
    {
        get => _tagCategory;
        set => SetProperty(ref _tagCategory, value);
    }

    public ICommand SaveFieldCommand { get; }
    public ICommand NewFieldCommand { get; }
    public ICommand DeleteFieldCommand { get; }
    public ICommand SaveAssetTypeCommand { get; }
    public ICommand NewAssetTypeCommand { get; }
    public ICommand DeleteAssetTypeCommand { get; }
    public ICommand SaveAcquisitionSourceCommand { get; }
    public ICommand NewAcquisitionSourceCommand { get; }
    public ICommand DeleteAcquisitionSourceCommand { get; }
    public ICommand SaveLicensePresetCommand { get; }
    public ICommand NewLicensePresetCommand { get; }
    public ICommand DeleteLicensePresetCommand { get; }
    public ICommand SaveCategoryCommand { get; }
    public ICommand NewCategoryCommand { get; }
    public ICommand DeleteCategoryCommand { get; }
    public ICommand SaveTagCommand { get; }
    public ICommand NewTagCommand { get; }
    public ICommand DeleteTagCommand { get; }

    public async Task InitializeAsync()
    {
        try
        {
            await ReloadAsync();
        }
        catch (Exception exception)
        {
            _dialogs.ShowError($"管理データを読み込めませんでした。{Environment.NewLine}{exception.Message}", exception: exception);
        }
    }

    private async Task ReloadAsync()
    {
        var snapshot = await _store.LoadAsync();
        Replace(CustomFields, snapshot.FieldDefinitions.Where(field => field.Origin == FieldOrigin.Custom));
        Replace(AssetTypes, snapshot.AssetTypes);
        Replace(
            AcquisitionSources,
            snapshot.FieldDefinitions
                .Single(field => field.Id == BuiltInFieldIds.AcquisitionSource)
                .Options);
        Replace(LicensePresets, snapshot.LicensePresets);
        Replace(Categories, snapshot.TagCategories);
        Replace(Tags, snapshot.Tags);
    }

    private async Task SaveFieldAsync()
    {
        try
        {
            var options = CreateSelectionOptions();
            if (SelectedField is null)
            {
                await _fields.AddCustomAsync(
                    FieldName.Trim(),
                    FieldType,
                    FieldMainVisible,
                    FieldDetailVisible,
                    options);
            }
            else
            {
                var selectionOptionsChanged = FieldType is FieldType.SingleSelect or FieldType.MultiSelect
                    && !SelectedField.Options.Select(option => (option.Id, option.Label))
                        .SequenceEqual((options ?? []).Select(option => (option.Id, option.Label)));
                if (SelectedField.Type != FieldType || selectionOptionsChanged)
                {
                    var analysis = await _fields.AnalyzeTypeChangeAsync(SelectedField.Id, FieldType, options);
                    var clear = analysis.CanConvertAllValues || _dialogs.Confirm(
                        $"{analysis.IncompatibleRecordIds.Count}件の値を変換できません。全レコードのこのカラム値を空にして型を変更しますか？",
                        "AssetManager - カラム型変更");
                    if (!clear)
                    {
                        return;
                    }

                    _ = await _fields.ChangeTypeAsync(SelectedField.Id, FieldType, !analysis.CanConvertAllValues, options);
                }

                if (!string.Equals(SelectedField.Label, FieldName.Trim(), StringComparison.Ordinal))
                {
                    _ = await _fields.RenameAsync(SelectedField.Id, FieldName.Trim());
                }

                _ = await _fields.SetVisibilityAsync(
                    SelectedField.Id,
                    FieldMainVisible,
                    FieldDetailVisible);
            }

            await ReloadAsync();
            ClearFieldForm();
        }
        catch (Exception exception)
        {
            _dialogs.ShowError($"カスタムカラムを保存できませんでした。{Environment.NewLine}{exception.Message}", exception: exception);
        }
    }

    private async Task DeleteFieldAsync()
    {
        var selected = SelectedField;
        if (selected is null || !_dialogs.Confirm(
                $"カスタムカラム「{selected.Label}」と全レコードの同カラム値を削除しますか？",
                "AssetManager - カラム削除"))
        {
            return;
        }

        try
        {
            await _fields.DeleteCustomAsync(selected.Id);
            await ReloadAsync();
            ClearFieldForm();
        }
        catch (Exception exception)
        {
            _dialogs.ShowError($"カスタムカラムを削除できませんでした。{Environment.NewLine}{exception.Message}", exception: exception);
        }
    }

    private async Task SaveAssetTypeAsync()
    {
        try
        {
            var extensions = SplitCommaList(AssetTypeExtensions);
            _ = SelectedAssetType is null
                ? await _catalog.AddAssetTypeAsync(AssetTypeName.Trim(), extensions)
                : await _catalog.UpdateAssetTypeAsync(SelectedAssetType.Id, AssetTypeName.Trim(), extensions);
            await ReloadAsync();
            ClearAssetTypeForm();
        }
        catch (Exception exception)
        {
            _dialogs.ShowError($"種類を保存できませんでした。{Environment.NewLine}{exception.Message}", exception: exception);
        }
    }

    private async Task SaveAcquisitionSourceAsync()
    {
        try
        {
            _ = SelectedAcquisitionSource is null
                ? await _catalog.AddAcquisitionSourceAsync(AcquisitionSourceName)
                : await _catalog.UpdateAcquisitionSourceAsync(
                    SelectedAcquisitionSource.Id,
                    AcquisitionSourceName);
            await ReloadAsync();
            ClearAcquisitionSourceForm();
        }
        catch (Exception exception)
        {
            _dialogs.ShowError(
                $"購入／入手元を保存できませんでした。{Environment.NewLine}{exception.Message}",
                exception: exception);
        }
    }

    private async Task DeleteAcquisitionSourceAsync()
    {
        var selected = SelectedAcquisitionSource;
        if (selected is null
            || !_dialogs.Confirm(
                $"購入／入手元「{selected.Label}」を削除しますか？",
                "AssetManager - 購入／入手元削除"))
        {
            return;
        }

        try
        {
            await _catalog.DeleteAcquisitionSourceAsync(selected.Id);
            await ReloadAsync();
            ClearAcquisitionSourceForm();
        }
        catch (Exception exception)
        {
            _dialogs.ShowError(
                $"購入／入手元を削除できませんでした。{Environment.NewLine}{exception.Message}",
                exception: exception);
        }
    }

    private async Task SaveLicensePresetAsync()
    {
        try
        {
            var terms = CreatePresetTerms();
            _ = SelectedLicensePreset is null
                ? await _catalog.AddLicensePresetAsync(LicensePresetName, terms)
                : await _catalog.UpdateLicensePresetAsync(
                    SelectedLicensePreset.Id,
                    LicensePresetName,
                    terms);
            await ReloadAsync();
            ClearLicensePresetForm();
        }
        catch (Exception exception)
        {
            _dialogs.ShowError(
                $"定型ライセンスを保存できませんでした。{Environment.NewLine}{exception.Message}",
                exception: exception);
        }
    }

    private async Task DeleteLicensePresetAsync()
    {
        var selected = SelectedLicensePreset;
        if (selected is null
            || !_dialogs.Confirm(
                $"定型ライセンス「{selected.Name}」を削除しますか？",
                "AssetManager - 定型ライセンス削除"))
        {
            return;
        }

        try
        {
            await _catalog.DeleteLicensePresetAsync(selected.Id);
            await ReloadAsync();
            ClearLicensePresetForm();
        }
        catch (Exception exception)
        {
            _dialogs.ShowError(
                $"定型ライセンスを削除できませんでした。{Environment.NewLine}{exception.Message}",
                exception: exception);
        }
    }

    private async Task DeleteAssetTypeAsync()
    {
        var selected = SelectedAssetType;
        if (selected is null || !_dialogs.Confirm($"種類「{selected.Name}」を削除しますか？", "AssetManager - 種類削除"))
        {
            return;
        }

        try
        {
            await _catalog.DeleteAssetTypeAsync(selected.Id);
            await ReloadAsync();
            ClearAssetTypeForm();
        }
        catch (Exception exception)
        {
            _dialogs.ShowError($"種類を削除できませんでした。{Environment.NewLine}{exception.Message}", exception: exception);
        }
    }

    private async Task SaveCategoryAsync()
    {
        try
        {
            _ = SelectedCategory is null
                ? await _catalog.AddTagCategoryAsync(CategoryName.Trim())
                : await _catalog.UpdateTagCategoryAsync(SelectedCategory.Id, CategoryName.Trim());
            await ReloadAsync();
            ClearCategoryForm();
        }
        catch (Exception exception)
        {
            _dialogs.ShowError($"タグ分類を保存できませんでした。{Environment.NewLine}{exception.Message}", exception: exception);
        }
    }

    private async Task DeleteCategoryAsync()
    {
        var selected = SelectedCategory;
        if (selected is null || !_dialogs.Confirm($"タグ分類「{selected.Name}」を削除しますか？", "AssetManager - タグ分類削除"))
        {
            return;
        }

        try
        {
            await _catalog.DeleteTagCategoryAsync(selected.Id);
            await ReloadAsync();
            ClearCategoryForm();
        }
        catch (Exception exception)
        {
            _dialogs.ShowError($"タグ分類を削除できませんでした。{Environment.NewLine}{exception.Message}", exception: exception);
        }
    }

    private async Task SaveTagAsync()
    {
        try
        {
            var color = new TagColor(TagColor.Trim());
            _ = SelectedTag is null
                ? await _catalog.AddTagAsync(TagName.Trim(), color, TagCategory?.Id)
                : await _catalog.UpdateTagAsync(SelectedTag.Id, TagName.Trim(), color, TagCategory?.Id);
            await ReloadAsync();
            ClearTagForm();
        }
        catch (Exception exception)
        {
            _dialogs.ShowError($"タグを保存できませんでした。{Environment.NewLine}{exception.Message}", exception: exception);
        }
    }

    private async Task DeleteTagAsync()
    {
        var selected = SelectedTag;
        if (selected is null || !_dialogs.Confirm($"タグ「{selected.Name}」を削除しますか？", "AssetManager - タグ削除"))
        {
            return;
        }

        try
        {
            await _catalog.DeleteTagAsync(selected.Id);
            await ReloadAsync();
            ClearTagForm();
        }
        catch (Exception exception)
        {
            _dialogs.ShowError($"タグを削除できませんでした。{Environment.NewLine}{exception.Message}", exception: exception);
        }
    }

    private SelectionOption[]? CreateSelectionOptions()
    {
        if (FieldType is not FieldType.SingleSelect and not FieldType.MultiSelect)
        {
            return null;
        }

        var existingByLabel = SelectedField?.Options.ToDictionary(
            option => option.Label,
            StringComparer.OrdinalIgnoreCase) ?? [];
        return SplitCommaList(FieldOptions).Select(label =>
            existingByLabel.TryGetValue(label, out var existing)
                ? existing
                : new SelectionOption(
                    new SelectionOptionId($"option.{Guid.CreateVersion7():D}"),
                    label)).ToArray();
    }

    private void ClearFieldForm()
    {
        SelectedField = null;
        FieldName = string.Empty;
        FieldType = FieldType.Text;
        FieldOptions = string.Empty;
        FieldMainVisible = false;
        FieldDetailVisible = true;
    }

    private void ClearAssetTypeForm()
    {
        SelectedAssetType = null;
        AssetTypeName = string.Empty;
        AssetTypeExtensions = string.Empty;
    }

    private void ClearAcquisitionSourceForm()
    {
        SelectedAcquisitionSource = null;
        AcquisitionSourceName = string.Empty;
    }

    private void ClearLicensePresetForm()
    {
        SelectedLicensePreset = null;
        LicensePresetName = string.Empty;
        ApplyPresetTerms(new LicenseTerms());
    }

    private LicenseTerms CreatePresetTerms()
    {
        return new LicenseTerms(
            ReadPresetCondition(SystemRole.CommercialUseAllowed),
            ReadPresetCondition(SystemRole.ModificationAllowed),
            ReadPresetCondition(SystemRole.ProductEmbeddingAllowed),
            ReadPresetCondition(SystemRole.OriginalDataRedistributionAllowed),
            ReadPresetCondition(SystemRole.CreditDisplayRequired),
            ReadPresetCondition(SystemRole.CopyrightNoticeRetentionRequired),
            ReadPresetCondition(SystemRole.LicenseTextAttachmentRequired),
            ReadPresetCondition(SystemRole.SameLicenseRequired),
            ReadPresetCondition(SystemRole.AiTrainingAllowed),
            ReadPresetCondition(SystemRole.GenerativeAiInputAllowed),
            ReadPresetCondition(SystemRole.EngineRestrictionExists),
            ReadPresetCondition(SystemRole.LicenseReviewRequired));
    }

    private void ApplyPresetTerms(LicenseTerms terms)
    {
        foreach (var condition in LicensePresetConditions)
        {
            condition.IsChecked = terms.GetValue(condition.Definition.SystemRole);
        }
    }

    private bool ReadPresetCondition(SystemRole role)
    {
        return LicensePresetConditions.Single(condition => condition.Definition.SystemRole == role).IsChecked;
    }

    private void ClearCategoryForm()
    {
        SelectedCategory = null;
        CategoryName = string.Empty;
    }

    private void ClearTagForm()
    {
        SelectedTag = null;
        TagName = string.Empty;
        TagColor = "#4F7CAC";
        TagCategory = null;
    }

    private static string[] SplitCommaList(string value)
    {
        return value.Split([',', '、'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            target.Add(value);
        }
    }
}

public sealed class LicensePresetConditionViewModel(LicenseConditionDefinition definition) : ObservableObject
{
    private bool _isChecked;

    public LicenseConditionDefinition Definition { get; } = definition
        ?? throw new ArgumentNullException(nameof(definition));

    public string Label => Definition.Label;

    public string Summary => Definition.Summary;

    public string Description => Definition.Description;

    public string ToolTip => Definition.ToolTip;

    public bool IsChecked
    {
        get => _isChecked;
        set => SetProperty(ref _isChecked, value);
    }
}
