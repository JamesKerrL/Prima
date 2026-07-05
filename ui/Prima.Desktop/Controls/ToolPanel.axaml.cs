using System;
using Avalonia.Controls;
using Prima.App;

namespace Prima.Desktop.Controls;

public sealed partial class ToolPanel : UserControl
{
    public event EventHandler<ToolType>? ToolSelected;

    private readonly RadioButton? _brush;
    private readonly RadioButton? _floodFill;

    public ToolPanel()
    {
        InitializeComponent();
        _brush = this.FindControl<RadioButton>("PART_Brush");
        if (_brush is not null)
            _brush.IsCheckedChanged += (_, _) =>
            {
                if (_brush.IsChecked == true) ToolSelected?.Invoke(this, ToolType.Brush);
            };

        _floodFill = this.FindControl<RadioButton>("PART_FloodFill");
        if (_floodFill is not null)
            _floodFill.IsCheckedChanged += (_, _) =>
            {
                if (_floodFill.IsChecked == true)
                    ToolSelected?.Invoke(this, ToolType.FloodFill);
            };
    }
}
