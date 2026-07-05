using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Prima.Desktop.Controls;

/// <summary>
/// One reusable labeled text field, instanced per HEX/R/G/B/H/S/V row in the
/// color picker's numeric panel. Holds no color-parsing logic itself - it just
/// surfaces the label, the raw text, and a commit event; the parent decides
/// how to interpret the text (hex string vs. an int/double channel).
/// </summary>
public sealed partial class LabeledColorField : UserControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<LabeledColorField, string>(nameof(Label), defaultValue: "");

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<LabeledColorField, string>(nameof(Text), defaultValue: "");

    /// <summary>Raised when the user commits an edit (Enter or focus loss).
    /// Does not fire for programmatic <see cref="Text"/> updates.</summary>
    public event EventHandler<string>? Committed;

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private readonly TextBlock? _label;
    private readonly TextBox? _input;

    public LabeledColorField()
    {
        InitializeComponent();
        _label = this.FindControl<TextBlock>("PART_Label");
        _input = this.FindControl<TextBox>("PART_Input");
        Sync();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty || change.Property == TextProperty)
            Sync();
    }

    private void Sync()
    {
        if (_label is not null) _label.Text = Label;
        if (_input is not null && _input.Text != Text) _input.Text = Text;
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Commit();
    }

    private void OnInputLostFocus(object? sender, RoutedEventArgs e) => Commit();

    private void Commit()
    {
        string text = _input?.Text ?? "";
        Text = text;
        Committed?.Invoke(this, text);
    }
}
