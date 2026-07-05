using System;
using Avalonia.Controls;
using Prima.App;

namespace Prima.Desktop.Controls;

public sealed partial class ToolPanel : UserControl
{
    public event EventHandler<ToolType>? ToolSelected;

    private readonly RadioButton? _brush;

    public ToolPanel()
    {
        InitializeComponent();
        _brush = this.FindControl<RadioButton>("PART_Brush");
        if (_brush is not null)
            _brush.IsCheckedChanged += (_, _) => ToolSelected?.Invoke(this, ToolType.Brush);
    }
}
