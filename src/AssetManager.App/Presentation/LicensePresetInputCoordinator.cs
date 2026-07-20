using System.ComponentModel;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Licensing;

namespace AssetManager.App.Presentation;

public sealed class LicensePresetInputCoordinator : IDisposable
{
    private readonly FieldEditorViewModel _presetEditor;
    private readonly Dictionary<string, LicensePresetDefinition> _presets;
    private readonly IReadOnlyList<FieldEditorViewModel> _conditionEditors;
    private bool _isApplyingPreset;

    public LicensePresetInputCoordinator(
        FieldEditorViewModel presetEditor,
        IEnumerable<FieldEditorViewModel> conditionEditors,
        IEnumerable<LicensePresetDefinition> presets)
    {
        _presetEditor = presetEditor ?? throw new ArgumentNullException(nameof(presetEditor));
        if (_presetEditor.Definition.SystemRole != SystemRole.LicensePreset)
        {
            throw new ArgumentException("定型ライセンス用のエディターを指定してください。", nameof(presetEditor));
        }

        _conditionEditors = conditionEditors?.ToArray()
            ?? throw new ArgumentNullException(nameof(conditionEditors));
        _presets = presets?.ToDictionary(preset => preset.Id.Value, StringComparer.Ordinal)
            ?? throw new ArgumentNullException(nameof(presets));
        _presetEditor.PropertyChanged += OnPresetPropertyChanged;
        foreach (var editor in _conditionEditors)
        {
            editor.PropertyChanged += OnConditionPropertyChanged;
        }
    }

    public void Dispose()
    {
        _presetEditor.PropertyChanged -= OnPresetPropertyChanged;
        foreach (var editor in _conditionEditors)
        {
            editor.PropertyChanged -= OnConditionPropertyChanged;
        }
    }

    private void OnPresetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FieldEditorViewModel.SelectedOptionId)
            || _presetEditor.SelectedOptionId is not { } selectedId
            || !_presets.TryGetValue(selectedId, out var preset))
        {
            return;
        }

        _isApplyingPreset = true;
        try
        {
            foreach (var editor in _conditionEditors)
            {
                editor.BooleanValue = ReadTerm(preset.Terms, editor.Definition.SystemRole);
            }
        }
        finally
        {
            _isApplyingPreset = false;
        }
    }

    private void OnConditionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isApplyingPreset && e.PropertyName == nameof(FieldEditorViewModel.BooleanValue))
        {
            _presetEditor.SelectedOptionId = null;
        }
    }

    private static bool ReadTerm(LicenseTerms terms, SystemRole? role)
    {
        return role switch
        {
            SystemRole.CreditRequired => terms.CreditRequired,
            SystemRole.LinkRequired => terms.LinkRequired,
            SystemRole.LogoRequired => terms.LogoRequired,
            SystemRole.CommercialUseAllowed => terms.CommercialUseAllowed,
            SystemRole.ModificationAllowed => terms.ModificationAllowed,
            SystemRole.RedistributionAllowed => terms.RedistributionAllowed,
            SystemRole.AdultUseAllowed => terms.AdultUseAllowed,
            SystemRole.GenerativeAiUseAllowed => terms.GenerativeAiUseAllowed,
            SystemRole.LicenseUnknown => terms.ConditionsUnknown,
            SystemRole.LicenseNeedsReview => terms.NeedsReview,
            _ => throw new ArgumentException("ライセンス条件以外のエディターが含まれています。", nameof(role)),
        };
    }
}
