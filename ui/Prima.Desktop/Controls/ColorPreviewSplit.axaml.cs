using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Prima.App;

namespace Prima.Desktop.Controls;

/// <summary>
/// A static (non-interactive) split swatch: left half shows the previous
/// color, right half shows the color currently being targeted. Used so the
/// user can compare before committing a pick.
/// </summary>
public sealed partial class ColorPreviewSplit : UserControl
{
    public static readonly StyledProperty<Rgba> PreviousColorProperty =
        AvaloniaProperty.Register<ColorPreviewSplit, Rgba>(nameof(PreviousColor), defaultValue: Rgba.White);

    public static readonly StyledProperty<Rgba> CurrentColorProperty =
        AvaloniaProperty.Register<ColorPreviewSplit, Rgba>(nameof(CurrentColor), defaultValue: Rgba.White);

    public Rgba PreviousColor
    {
        get => GetValue(PreviousColorProperty);
        set => SetValue(PreviousColorProperty, value);
    }

    public Rgba CurrentColor
    {
        get => GetValue(CurrentColorProperty);
        set => SetValue(CurrentColorProperty, value);
    }

    private readonly Border? _previous;
    private readonly Border? _current;

    public ColorPreviewSplit()
    {
        InitializeComponent();
        _previous = this.FindControl<Border>("PART_Previous");
        _current = this.FindControl<Border>("PART_Current");
        UpdateSwatches();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PreviousColorProperty || change.Property == CurrentColorProperty)
            UpdateSwatches();
    }

    private void UpdateSwatches()
    {
        if (_previous is not null) _previous.Background = ToBrush(PreviousColor);
        if (_current is not null) _current.Background = ToBrush(CurrentColor);
    }

    private static IBrush ToBrush(Rgba c) => new SolidColorBrush(new Color(c.A, c.R, c.G, c.B));
}
