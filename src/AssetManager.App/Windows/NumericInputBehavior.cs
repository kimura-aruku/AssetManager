using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AssetManager.App.Windows;

public static partial class NumericInputBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(NumericInputBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty IsIntegerProperty = DependencyProperty.RegisterAttached(
        "IsInteger",
        typeof(bool),
        typeof(NumericInputBehavior),
        new PropertyMetadata(false));

    public static bool GetIsEnabled(DependencyObject element)
    {
        return (bool)element.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(DependencyObject element, bool value)
    {
        element.SetValue(IsEnabledProperty, value);
    }

    public static bool GetIsInteger(DependencyObject element)
    {
        return (bool)element.GetValue(IsIntegerProperty);
    }

    public static void SetIsInteger(DependencyObject element, bool value)
    {
        element.SetValue(IsIntegerProperty, value);
    }

    private static void OnIsEnabledChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            textBox.PreviewTextInput += OnPreviewTextInput;
            textBox.PreviewKeyDown += OnPreviewKeyDown;
            DataObject.AddPastingHandler(textBox, OnPaste);
        }
        else
        {
            textBox.PreviewTextInput -= OnPreviewTextInput;
            textBox.PreviewKeyDown -= OnPreviewKeyDown;
            DataObject.RemovePastingHandler(textBox, OnPaste);
        }
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            e.Handled = !IsValid(CreateProposedText(textBox, e.Text), GetIsInteger(textBox));
        }
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            e.Handled = true;
        }
    }

    private static void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox textBox
            || !e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText)
            || e.SourceDataObject.GetData(DataFormats.UnicodeText) is not string text
            || !IsValid(CreateProposedText(textBox, text), GetIsInteger(textBox)))
        {
            e.CancelCommand();
        }
    }

    private static string CreateProposedText(TextBox textBox, string input)
    {
        var start = textBox.SelectionStart;
        var length = textBox.SelectionLength;
        return textBox.Text.Remove(start, length).Insert(start, input);
    }

    private static bool IsValid(string value, bool integerOnly)
    {
        return integerOnly
            ? IntegerPattern().IsMatch(value)
            : NumberPattern().IsMatch(value);
    }

    [GeneratedRegex(@"^-?\d*$")]
    private static partial Regex IntegerPattern();

    [GeneratedRegex(@"^-?\d*(?:[\.,]\d*)?$")]
    private static partial Regex NumberPattern();
}
