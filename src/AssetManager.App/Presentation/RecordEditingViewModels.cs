using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
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

public sealed class FieldEditorViewModel : ObservableObject
{
    private string _text = string.Empty;
    private bool _booleanValue;
    private string? _selectedOptionId;
    private DateTime? _selectedDate;
    private TargetPathKind _targetPathKind = TargetPathKind.File;
    private string? _warning;
    private bool _isDirty;
    private bool _isLoading;

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

        Load(value);
    }

    public FieldDefinition Definition { get; }

    public string Label => Definition.Id == BuiltInFieldIds.TargetPath ? "パス" : Definition.Label;

    public string? Hint => Definition.Type switch
    {
        FieldType.Date => "YYYY/MM/DD",
        FieldType.StringList => "1行に1件",
        FieldType.TitledPathList => "1行に『タイトル | パス』",
        FieldType.TitledUrlList => "1行に『タイトル | URL』",
        _ => null,
    };

    public ObservableCollection<SelectableOptionViewModel> Options { get; }

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
    }

    public bool IsBoolean => Definition.Type == FieldType.Boolean;

    public bool IsSingleOption => Definition.Type is FieldType.SingleSelect or FieldType.RecordStatus;

    public bool IsMultiOption => Definition.Type is FieldType.MultiSelect
        or FieldType.AssetTypeSet
        or FieldType.TagSet;

    public bool IsTargetPath => Definition.Type == FieldType.TargetPath;

    public bool IsDate => Definition.Type == FieldType.Date;

    public bool IsNumber => Definition.Type == FieldType.Number;

    public bool IsCurrency => Definition.SystemRole == SystemRole.PriceJpy;

    public string? NumberSuffix => IsCurrency ? "円" : null;

    public bool IsText => !IsBoolean
        && !IsSingleOption
        && !IsMultiOption
        && !IsDate
        && !IsNumber;

    public bool AcceptsMultipleLines => Definition.Type is FieldType.MultilineText
        or FieldType.StringList
        or FieldType.TitledPathList
        or FieldType.TitledUrlList;

    public string Text
    {
        get => _text;
        set
        {
            if (SetProperty(ref _text, value))
            {
                MarkDirty();
                ValidatePreview();
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

    public bool IsDirty
    {
        get => _isDirty;
        private set => SetProperty(ref _isDirty, value);
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
            FieldType.StringList when Definition.SystemRole == SystemRole.Creators =>
                new CreatorListFieldValue(ParseLines(Text)),
            FieldType.StringList when Definition.SystemRole == SystemRole.Sellers =>
                new SellerListFieldValue(ParseLines(Text)),
            FieldType.TitledPathList => new RelatedDocumentListFieldValue(
                ParseTitledLines(Text).Select(item => new RelatedDocument(item.Title, item.Value))),
            FieldType.TitledUrlList => new RelatedUrlListFieldValue(
                ParseTitledLines(Text).Select(item => new RelatedUrl(item.Title, item.Value))),
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
        if (text.Length == 0)
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
        else if (Definition.Type is FieldType.Url or FieldType.TitledUrlList)
        {
            try
            {
                _ = CreateValue();
            }
            catch (Exception exception) when (exception is DomainValidationException or FormatException)
            {
                Warning = exception.Message;
            }
        }
        else if (Definition.Type is FieldType.FilePath or FieldType.FolderPath or FieldType.TargetPath
                 && !System.IO.Path.IsPathFullyQualified(text))
        {
            Warning = "ローカルドライブの絶対パスを入力してください。";
        }
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

    private static string[] ParseLines(string value)
    {
        return value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static IEnumerable<(string Title, string Value)> ParseTitledLines(string value)
    {
        foreach (var line in ParseLines(value))
        {
            var parts = line.Split('|', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || parts.Any(string.IsNullOrWhiteSpace))
            {
                throw new DomainValidationException("各行を『タイトル | 値』の形式で入力してください。", nameof(value));
            }

            yield return (parts[0], parts[1]);
        }
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
        var labels = new List<string>();
        AddFlag(BuiltInFieldIds.CommercialUseAllowed, "商用○");
        AddFlag(BuiltInFieldIds.ModificationAllowed, "改変○");
        AddFlag(BuiltInFieldIds.CreditRequired, "クレジット必須");
        AddFlag(BuiltInFieldIds.LinkRequired, "リンク必須");
        AddFlag(BuiltInFieldIds.LicenseUnknown, "条件不明");
        AddFlag(BuiltInFieldIds.LicenseNeedsReview, "要再確認");
        return labels.Count == 0 ? "—" : string.Join("  ", labels);

        void AddFlag(FieldId id, string label)
        {
            if (record.GetValue<BooleanFieldValue>(id)?.Value == true)
            {
                labels.Add(label);
            }
        }
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
