using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using AssetManager.Application.Search;
using AssetManager.Application.Status;
using AssetManager.Domain.Catalog;
using AssetManager.Domain.Common;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Identifiers;
using AssetManager.Domain.Licensing;
using AssetManager.Domain.Records;
using AssetManager.Domain.Values;

namespace AssetManager.App.Presentation;

public sealed class SelectableOptionViewModel(string id, string label, bool isSelected = false)
    : ObservableObject
{
    private bool _isSelected = isSelected;

    public string Id { get; } = id;

    public string Label { get; } = label;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed class SearchFieldViewModel : ObservableObject
{
    private string _text = string.Empty;
    private int _booleanMode;

    public SearchFieldViewModel(
        FieldDefinition definition,
        IEnumerable<SelectableOptionViewModel>? options = null)
    {
        Definition = definition;
        Options = new ObservableCollection<SelectableOptionViewModel>(options ?? []);
    }

    public FieldDefinition Definition { get; }

    public string Label => Definition.Id == BuiltInFieldIds.TargetPath ? "パス" : Definition.Label;

    public ObservableCollection<SelectableOptionViewModel> Options { get; }

    public bool IsBoolean => Definition.Type == FieldType.Boolean;

    public bool IsOption => Definition.Type is FieldType.SingleSelect
        or FieldType.MultiSelect
        or FieldType.AssetTypeSet
        or FieldType.TagSet
        or FieldType.RecordStatus;

    public bool IsText => !IsBoolean && !IsOption;

    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }

    public int BooleanMode
    {
        get => _booleanMode;
        set => SetProperty(ref _booleanMode, value);
    }

    public FieldSearchCondition? CreateCondition()
    {
        if (IsBoolean)
        {
            return BooleanMode switch
            {
                1 => new BooleanEqualsCondition(true),
                2 => new BooleanEqualsCondition(false),
                _ => null,
            };
        }

        if (IsOption)
        {
            var selected = Options.Where(option => option.IsSelected).Select(option => option.Id).ToArray();
            return selected.Length == 0 ? null : new OptionAnyCondition(selected);
        }

        return string.IsNullOrWhiteSpace(Text) ? null : new TextContainsCondition(Text);
    }

    public void Clear()
    {
        Text = string.Empty;
        BooleanMode = 0;
        foreach (var option in Options)
        {
            option.IsSelected = false;
        }
    }

    public void Apply(FieldSearchCondition? condition)
    {
        Clear();
        switch (condition)
        {
            case TextContainsCondition text:
                Text = text.Query;
                break;
            case BooleanEqualsCondition boolean:
                BooleanMode = boolean.Value ? 1 : 2;
                break;
            case OptionAnyCondition options:
                foreach (var option in Options)
                {
                    option.IsSelected = options.OptionIds.Contains(option.Id, StringComparer.Ordinal);
                }
                break;
        }
    }
}

public sealed class FieldEditorEntryViewModel : ObservableObject
{
    private string _primaryText;
    private string _secondaryText;

    public FieldEditorEntryViewModel(string primaryText = "", string secondaryText = "")
    {
        _primaryText = primaryText;
        _secondaryText = secondaryText;
    }

    public string PrimaryText
    {
        get => _primaryText;
        set => SetProperty(ref _primaryText, value);
    }

    public string SecondaryText
    {
        get => _secondaryText;
        set => SetProperty(ref _secondaryText, value);
    }
}

public sealed class FieldEditorViewModel : ObservableObject
{
    private string _text = string.Empty;
    private bool _booleanValue;
    private string? _selectedOptionId;
    private DateTime? _selectedDate;
    private TargetPathKind _targetPathKind = TargetPathKind.File;
    private string? _warning;
    private bool? _isUrlFormatValid;
    private bool _isDirty;
    private bool _isLoading;
    private bool _showsLicenseConditionGroup;

    public FieldEditorViewModel(
        FieldDefinition definition,
        FieldValue? value,
        IEnumerable<SelectableOptionViewModel>? options = null)
    {
        Definition = definition;
        Options = new ObservableCollection<SelectableOptionViewModel>(options ?? []);
        foreach (var option in Options)
        {
            option.PropertyChanged += OnOptionPropertyChanged;
        }

        AddListEntryCommand = new RelayCommand(AddListEntry, () => IsList);
        RemoveListEntryCommand = new RelayCommand(RemoveListEntry, () => IsList && Entries.Count > 1);
        Load(value);
        ValidateUrlInput();
    }

    public FieldDefinition Definition { get; }

    public string Label => Definition.Id == BuiltInFieldIds.TargetPath ? "パス" : Definition.Label;

    public ObservableCollection<SelectableOptionViewModel> Options { get; }

    public ObservableCollection<FieldEditorEntryViewModel> Entries { get; } = [];

    public ICommand AddListEntryCommand { get; }

    public ICommand RemoveListEntryCommand { get; }

    public void ReplaceOptions(IEnumerable<SelectableOptionViewModel> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var selectedIds = Options
            .Where(option => option.IsSelected)
            .Select(option => option.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var option in Options)
        {
            option.PropertyChanged -= OnOptionPropertyChanged;
        }

        Options.Clear();
        foreach (var option in options)
        {
            option.IsSelected = selectedIds.Contains(option.Id);
            option.PropertyChanged += OnOptionPropertyChanged;
            Options.Add(option);
        }

        if (IsSingleOption
            && SelectedOptionId is { } selectedId
            && Options.All(option => option.Id != selectedId))
        {
            SelectedOptionId = null;
        }
    }

    public bool IsBoolean => Definition.Type == FieldType.Boolean;

    public bool IsSingleOption => Definition.Type is FieldType.SingleSelect or FieldType.RecordStatus;

    public bool IsMultiOption => Definition.Type is FieldType.MultiSelect
        or FieldType.AssetTypeSet
        or FieldType.TagSet;

    public bool IsAssetTypeSet => Definition.Type == FieldType.AssetTypeSet;

    public bool IsTagSet => Definition.Type == FieldType.TagSet;

    public bool IsOtherMultiOption => IsMultiOption && !IsAssetTypeSet && !IsTagSet;

    public LicenseConditionDefinition? LicenseCondition => LicenseConditionCatalog.Find(Definition.SystemRole);

    public bool IsLicenseCondition => LicenseCondition is not null;

    public string? LicenseConditionSummary => LicenseCondition?.Summary;

    public string? LicenseConditionDescription => LicenseCondition?.Description;

    public string? LicenseConditionToolTip => LicenseCondition?.ToolTip;

    public bool IsStandaloneBoolean => IsBoolean && !IsLicenseCondition;

    public bool ShowsLicenseConditionGroup => _showsLicenseConditionGroup;

    public bool IsDetailItemVisible => !IsLicenseCondition || ShowsLicenseConditionGroup;

    public bool IsTargetPath => Definition.Type == FieldType.TargetPath;

    public bool IsFilePath => Definition.Type == FieldType.FilePath;

    public bool IsFolderPath => Definition.Type == FieldType.FolderPath;

    public bool IsAuxiliaryPath => IsFilePath || IsFolderPath;

    public bool IsUrl => Definition.Type == FieldType.Url;

    public bool IsStringList => Definition.Type == FieldType.StringList;

    public bool IsTitledPathList => Definition.Type == FieldType.TitledPathList;

    public bool IsTitledUrlList => Definition.Type == FieldType.TitledUrlList;

    public bool IsTitledList => IsTitledPathList || IsTitledUrlList;

    public bool IsList => IsStringList || IsTitledList;

    public bool IsNonBoolean => !IsBoolean;

    public bool IsDate => Definition.Type == FieldType.Date;

    public bool IsNumber => Definition.Type == FieldType.Number;

    public bool IsCurrency => Definition.SystemRole == SystemRole.PriceJpy;

    public string? NumberSuffix => IsCurrency ? "円" : null;

    public bool IsText => !IsBoolean
        && !IsSingleOption
        && !IsMultiOption
        && !IsDate
        && !IsNumber
        && !IsList;

    public bool IsPlainText => IsText && !IsUrl && !IsAuxiliaryPath && !IsTargetPath;

    public bool AcceptsMultipleLines => Definition.Type == FieldType.MultilineText;

    public void ShowLicenseConditionGroup()
    {
        if (SetProperty(ref _showsLicenseConditionGroup, true, nameof(ShowsLicenseConditionGroup)))
        {
            OnPropertyChanged(nameof(IsDetailItemVisible));
        }
    }

    public string Text
    {
        get => _text;
        set
        {
            if (SetProperty(ref _text, value))
            {
                MarkDirty();
                if (IsUrl)
                {
                    ClearUrlValidation();
                }
                else
                {
                    ValidatePreview();
                }

                RelayCommand.RefreshCanExecute();
            }
        }
    }

    public bool BooleanValue
    {
        get => _booleanValue;
        set
        {
            if (SetProperty(ref _booleanValue, value))
            {
                MarkDirty();
            }
        }
    }

    public string? SelectedOptionId
    {
        get => _selectedOptionId;
        set
        {
            if (SetProperty(ref _selectedOptionId, value))
            {
                MarkDirty();
            }
        }
    }

    public DateTime? SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (SetProperty(ref _selectedDate, value))
            {
                MarkDirty();
            }
        }
    }

    public TargetPathKind TargetPathKind
    {
        get => _targetPathKind;
        set
        {
            if (SetProperty(ref _targetPathKind, value))
            {
                MarkDirty();
            }
        }
    }

    public string? Warning
    {
        get => _warning;
        private set => SetProperty(ref _warning, value);
    }

    public bool ShowsValidUrlIndicator => _isUrlFormatValid is true;

    public bool ShowsInvalidUrlIndicator => _isUrlFormatValid is false;

    public bool IsDirty
    {
        get => _isDirty;
        private set => SetProperty(ref _isDirty, value);
    }

    public void ValidateUrlInput()
    {
        if (!IsUrl && !IsTitledUrlList)
        {
            return;
        }

        Warning = null;
        var hasInput = IsUrl
            ? !string.IsNullOrWhiteSpace(Text)
            : Entries.Any(entry => !string.IsNullOrWhiteSpace(entry.PrimaryText)
                || !string.IsNullOrWhiteSpace(entry.SecondaryText));
        if (!hasInput)
        {
            SetUrlFormatValidity(null);
            return;
        }

        try
        {
            _ = CreateValue();
            SetUrlFormatValidity(true);
        }
        catch (Exception exception) when (exception is DomainValidationException or FormatException)
        {
            Warning = exception.Message;
            SetUrlFormatValidity(false);
        }
    }

    public FieldValue? CreateValue()
    {
        var trimmed = Text.Trim();
        if (IsBoolean)
        {
            return new BooleanFieldValue(BooleanValue);
        }

        if (IsSingleOption)
        {
            if (Definition.Type == FieldType.RecordStatus)
            {
                var status = Enum.Parse<RecordStatus>(SelectedOptionId ?? nameof(RecordStatus.Unchecked), true);
                return new RecordStatusFieldValue(status);
            }

            return string.IsNullOrWhiteSpace(SelectedOptionId)
                ? null
                : new SingleSelectionFieldValue(new SelectionOptionId(SelectedOptionId));
        }

        if (IsMultiOption)
        {
            var selected = Options.Where(option => option.IsSelected).Select(option => option.Id).ToArray();
            return Definition.Type switch
            {
                FieldType.MultiSelect => new MultiSelectionFieldValue(
                    selected.Select(id => new SelectionOptionId(id))),
                FieldType.AssetTypeSet => new AssetTypeSetFieldValue(
                    selected.Select(id => new AssetTypeId(id))),
                FieldType.TagSet => new TagSetFieldValue(
                    selected.Select(id => new TagId(id))),
                _ => throw new InvalidOperationException(),
            };
        }

        if (IsDate)
        {
            return SelectedDate is null
                ? null
                : new DateFieldValue(new AssetDate(DateOnly.FromDateTime(SelectedDate.Value)));
        }

        if (IsStringList)
        {
            var values = Entries
                .Select(entry => entry.PrimaryText.Trim())
                .Where(value => value.Length > 0)
                .ToArray();
            if (values.Length == 0)
            {
                return null;
            }

            return Definition.SystemRole switch
            {
                SystemRole.Creators => new CreatorListFieldValue(values),
                SystemRole.Sellers => new SellerListFieldValue(values),
                _ => throw new DomainValidationException($"カラム'{Label}'の入力形式に対応していません。"),
            };
        }

        if (IsTitledPathList)
        {
            var values = CreateTitledEntries();
            return values.Length == 0
                ? null
                : new RelatedDocumentListFieldValue(
                    values.Select(item => new RelatedDocument(item.Title, item.Value)));
        }

        if (IsTitledUrlList)
        {
            var values = CreateTitledEntries();
            return values.Length == 0
                ? null
                : new RelatedUrlListFieldValue(
                    values.Select(item => new RelatedUrl(item.Title, item.Value)));
        }

        if (trimmed.Length == 0)
        {
            return null;
        }

        return Definition.Type switch
        {
            FieldType.Text => new TextFieldValue(trimmed),
            FieldType.MultilineText => new MultilineTextFieldValue(Text),
            FieldType.Number => new NumberFieldValue(ParseNumber(trimmed)),
            FieldType.Url => new UrlFieldValue(trimmed),
            FieldType.FilePath => new FilePathFieldValue(trimmed),
            FieldType.FolderPath => new FolderPathFieldValue(trimmed),
            FieldType.TargetPath => new TargetPathFieldValue(TargetPathKind, trimmed),
            _ => throw new DomainValidationException($"カラム'{Label}'の入力形式に対応していません。"),
        };
    }

    private void Load(FieldValue? value)
    {
        _isLoading = true;
        try
        {
            Text = RecordValueFormatter.FormatForEditor(value);
            BooleanValue = value is BooleanFieldValue { Value: true };
            SelectedOptionId = value switch
            {
                SingleSelectionFieldValue single => single.Value.Value,
                RecordStatusFieldValue status => status.Value.ToString().ToLowerInvariant(),
                _ when Definition.Type == FieldType.RecordStatus => "unchecked",
                _ => null,
            };
            SelectedDate = value is DateFieldValue date
                ? date.Value.Value.ToDateTime(TimeOnly.MinValue)
                : null;
            TargetPathKind = value is TargetPathFieldValue target ? target.Kind : TargetPathKind.File;
            LoadListEntries(value);

            var selectedIds = value switch
            {
                MultiSelectionFieldValue multiple => multiple.Values.Select(id => id.Value),
                AssetTypeSetFieldValue types => types.Values.Select(id => id.Value),
                TagSetFieldValue tags => tags.Values.Select(id => id.Value),
                _ => [],
            };
            var selectedSet = selectedIds.ToHashSet(StringComparer.Ordinal);
            foreach (var option in Options)
            {
                option.IsSelected = selectedSet.Contains(option.Id);
            }
        }
        finally
        {
            _isLoading = false;
            IsDirty = false;
        }
    }

    private void OnOptionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectableOptionViewModel.IsSelected))
        {
            MarkDirty();
        }
    }

    private void OnEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        MarkDirty();
        if (IsTitledUrlList)
        {
            ClearUrlValidation();
        }
        else
        {
            ValidatePreview();
        }

        RelayCommand.RefreshCanExecute();
    }

    private void AddListEntry()
    {
        AddListEntryCore(new FieldEditorEntryViewModel());
        MarkDirty();
        RelayCommand.RefreshCanExecute();
    }

    private void RemoveListEntry()
    {
        if (Entries.Count <= 1)
        {
            return;
        }

        var entry = Entries[^1];
        entry.PropertyChanged -= OnEntryPropertyChanged;
        Entries.RemoveAt(Entries.Count - 1);
        MarkDirty();
        if (IsTitledUrlList)
        {
            ClearUrlValidation();
        }
        else
        {
            ValidatePreview();
        }

        RelayCommand.RefreshCanExecute();
    }

    private void LoadListEntries(FieldValue? value)
    {
        foreach (var entry in Entries)
        {
            entry.PropertyChanged -= OnEntryPropertyChanged;
        }

        Entries.Clear();
        switch (value)
        {
            case CreatorListFieldValue creators:
                foreach (var creator in creators.Values)
                {
                    AddListEntryCore(new FieldEditorEntryViewModel(creator));
                }
                break;
            case SellerListFieldValue sellers:
                foreach (var seller in sellers.Values)
                {
                    AddListEntryCore(new FieldEditorEntryViewModel(seller));
                }
                break;
            case RelatedDocumentListFieldValue documents:
                foreach (var document in documents.Values)
                {
                    AddListEntryCore(new FieldEditorEntryViewModel(document.Title, document.Path));
                }
                break;
            case RelatedUrlListFieldValue urls:
                foreach (var url in urls.Values)
                {
                    AddListEntryCore(new FieldEditorEntryViewModel(url.Title, url.Url));
                }
                break;
        }

        if (IsList && Entries.Count == 0)
        {
            AddListEntryCore(new FieldEditorEntryViewModel());
        }

        RelayCommand.RefreshCanExecute();
    }

    private void AddListEntryCore(FieldEditorEntryViewModel entry)
    {
        entry.PropertyChanged += OnEntryPropertyChanged;
        Entries.Add(entry);
    }

    private void MarkDirty()
    {
        if (!_isLoading)
        {
            IsDirty = true;
        }
    }

    private void ValidatePreview()
    {
        var text = Text.Trim();
        Warning = null;
        if (text.Length == 0 && !IsList)
        {
            return;
        }

        if (Definition.Type == FieldType.Date)
        {
            try
            {
                _ = AssetDate.ParseDisplay(text);
            }
            catch (DomainValidationException exception)
            {
                Warning = exception.Message;
            }
        }
        else if (Definition.Type is FieldType.FilePath or FieldType.FolderPath or FieldType.TargetPath
                 && !System.IO.Path.IsPathFullyQualified(text))
        {
            Warning = "ローカルドライブの絶対パスを入力してください。";
        }

        else if (IsTitledPathList
                 && Entries.Any(entry => !string.IsNullOrWhiteSpace(entry.SecondaryText)
                     && !System.IO.Path.IsPathFullyQualified(entry.SecondaryText.Trim())))
        {
            Warning = "各パスにはローカルドライブの絶対パスを入力してください。";
        }
    }

    private void ClearUrlValidation()
    {
        Warning = null;
        SetUrlFormatValidity(null);
    }

    private void SetUrlFormatValidity(bool? value)
    {
        if (_isUrlFormatValid == value)
        {
            return;
        }

        _isUrlFormatValid = value;
        OnPropertyChanged(nameof(ShowsValidUrlIndicator));
        OnPropertyChanged(nameof(ShowsInvalidUrlIndicator));
    }

    private static decimal ParseNumber(string value)
    {
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var result)
            || decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result))
        {
            return result;
        }

        throw new DomainValidationException("数値を入力してください。", nameof(value));
    }

    private (string Title, string Value)[] CreateTitledEntries()
    {
        var entries = Entries
            .Select(entry => (Title: entry.PrimaryText.Trim(), Value: entry.SecondaryText.Trim()))
            .Where(entry => entry.Title.Length > 0 || entry.Value.Length > 0)
            .ToArray();
        if (entries.Any(entry => entry.Title.Length == 0 || entry.Value.Length == 0))
        {
            throw new DomainValidationException("タイトルと値を両方入力してください。", nameof(Entries));
        }

        return entries;
    }
}

public static class RecordValueFormatter
{
    public static string FormatForEditor(FieldValue? value)
    {
        return value switch
        {
            TextFieldValue item => item.Value,
            MultilineTextFieldValue item => item.Value,
            NumberFieldValue item => item.Value.ToString(CultureInfo.CurrentCulture),
            DateFieldValue item => item.Value.ToDisplayString(),
            UrlFieldValue item => item.Value,
            FilePathFieldValue item => item.Value,
            FolderPathFieldValue item => item.Value,
            TargetPathFieldValue item => item.Path,
            CreatorListFieldValue item => string.Join(Environment.NewLine, item.Values),
            SellerListFieldValue item => string.Join(Environment.NewLine, item.Values),
            RelatedDocumentListFieldValue item => string.Join(
                Environment.NewLine,
                item.Values.Select(document => $"{document.Title} | {document.Path}")),
            RelatedUrlListFieldValue item => string.Join(
                Environment.NewLine,
                item.Values.Select(url => $"{url.Title} | {url.Url}")),
            _ => string.Empty,
        };
    }

    public static string FormatForGrid(FieldValue? value)
    {
        return value switch
        {
            BooleanFieldValue item => item.Value ? "✓" : string.Empty,
            RecordStatusFieldValue item => item.Value switch
            {
                RecordStatus.Available => "利用可能",
                RecordStatus.Unavailable => "利用不可",
                RecordStatus.Archived => "アーカイブ",
                _ => "未確認",
            },
            SingleSelectionFieldValue item => item.Value.Value,
            MultiSelectionFieldValue item => string.Join(", ", item.Values.Select(id => id.Value)),
            AssetTypeSetFieldValue item => string.Join(", ", item.Values.Select(id => id.Value)),
            TagSetFieldValue item => string.Join(", ", item.Values.Select(id => id.Value)),
            _ => FormatForEditor(value).Replace(Environment.NewLine, "; ", StringComparison.Ordinal),
        };
    }
}

public sealed class RecordRowViewModel
{
    public RecordRowViewModel(
        AssetRecord record,
        IReadOnlyDictionary<AssetTypeId, string> typeNames,
        IReadOnlyDictionary<TagId, string> tagNames,
        IReadOnlyDictionary<FieldId, FieldDefinition> definitions,
        AssetDate today,
        LicenseWarningPolicy warningPolicy,
        IReadOnlyDictionary<string, AssetManager.Application.Paths.PathCheckResult> pathResults)
    {
        Record = record;
        Name = record.GetValue<TextFieldValue>(BuiltInFieldIds.Name)?.Value ?? "（名称未設定）";
        IsFavorite = record.Favorite.IsFavorite;
        FavoriteGlyph = IsFavorite ? "★" : "☆";
        Types = record.GetValue<AssetTypeSetFieldValue>(BuiltInFieldIds.AssetTypes) is { } types
            ? string.Join(", ", types.Values.Select(id => typeNames.GetValueOrDefault(id, id.Value)))
            : string.Empty;
        TargetPath = record.TargetPath?.Path ?? string.Empty;
        LicenseLastCheckedDate = record
            .GetValue<DateFieldValue>(BuiltInFieldIds.LicenseLastCheckedDate)?
            .Value
            .ToDisplayString() ?? string.Empty;
        License = CreateLicenseSummary(record);
        LicenseBadges = LicenseBadgeEvaluator.Evaluate(LicenseTerms.FromRecord(record));
        StatusIndicators = RecordIndicatorEvaluator.Evaluate(
            record,
            today,
            warningPolicy,
            pathResults);
        DynamicValues = definitions.ToDictionary(
            pair => pair.Key.Value,
            pair => FormatDynamicValue(
                record.Values.GetValueOrDefault(pair.Key),
                pair.Value,
                typeNames,
                tagNames),
            StringComparer.Ordinal);
    }

    public AssetRecord Record { get; }

    public string FavoriteGlyph { get; }

    public bool IsFavorite { get; }

    public string Name { get; }

    public string Types { get; }

    public string TargetPath { get; }

    public string LicenseLastCheckedDate { get; }

    public string License { get; }

    public IReadOnlyList<LicenseBadge> LicenseBadges { get; }

    public IReadOnlyList<RecordIndicator> StatusIndicators { get; }

    public IReadOnlyDictionary<string, string> DynamicValues { get; }

    private static string FormatDynamicValue(
        FieldValue? value,
        FieldDefinition definition,
        IReadOnlyDictionary<AssetTypeId, string> typeNames,
        IReadOnlyDictionary<TagId, string> tagNames)
    {
        return value switch
        {
            AssetTypeSetFieldValue types => string.Join(", ", types.Values.Select(
                id => typeNames.GetValueOrDefault(id, id.Value))),
            TagSetFieldValue tags => string.Join(", ", tags.Values.Select(
                id => tagNames.GetValueOrDefault(id, id.Value))),
            SingleSelectionFieldValue single => definition.Options
                .FirstOrDefault(option => option.Id == single.Value)?.Label ?? single.Value.Value,
            MultiSelectionFieldValue multiple => string.Join(", ", multiple.Values.Select(id =>
                definition.Options.FirstOrDefault(option => option.Id == id)?.Label ?? id.Value)),
            _ => RecordValueFormatter.FormatForGrid(value),
        };
    }

    private static string CreateLicenseSummary(AssetRecord record)
    {
        var labels = LicenseConditionCatalog.All
            .Where(condition => record.GetValue<BooleanFieldValue>(condition.FieldId)?.Value == true)
            .Select(condition => condition.Label)
            .ToArray();
        return labels.Length == 0 ? "—" : string.Join("  ", labels);
    }
}

public sealed class DisplayColumnOptionViewModel : ObservableObject
{
    private bool _isVisible;

    public DisplayColumnOptionViewModel(FieldDefinition definition, bool isVisible)
    {
        Definition = definition;
        _isVisible = isVisible;
    }

    public event EventHandler? VisibilityChanged;

    public FieldDefinition Definition { get; }

    public string Label => Definition.Label;

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (SetProperty(ref _isVisible, value))
            {
                VisibilityChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
