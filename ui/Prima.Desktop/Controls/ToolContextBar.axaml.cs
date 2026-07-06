using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Prima.App;

namespace Prima.Desktop.Controls;

public sealed partial class ToolContextBar : UserControl
{
    public event EventHandler<float>? BrushSizeChanged;
    public event EventHandler<float>? BrushHardnessChanged;
    public event EventHandler<float>? BrushOpacityChanged;
    public event EventHandler<float>? BrushFlowChanged;

    private readonly TextBlock? _toolLabel;
    private readonly StackPanel? _brushOptions;
    private readonly Slider? _sizeSlider;
    private readonly TextBlock? _sizeValue;
    private readonly Slider? _hardnessSlider;
    private readonly TextBlock? _hardnessValue;
    private readonly Slider? _opacitySlider;
    private readonly TextBlock? _opacityValue;
    private readonly Slider? _flowSlider;
    private readonly TextBlock? _flowValue;

    private bool _syncing;

    public ToolContextBar()
    {
        InitializeComponent();

        _toolLabel = this.FindControl<TextBlock>("PART_ToolLabel");
        _brushOptions = this.FindControl<StackPanel>("PART_BrushOptions");
        _sizeSlider = this.FindControl<Slider>("PART_SizeSlider");
        _sizeValue = this.FindControl<TextBlock>("PART_SizeValue");
        _hardnessSlider = this.FindControl<Slider>("PART_HardnessSlider");
        _hardnessValue = this.FindControl<TextBlock>("PART_HardnessValue");
        _opacitySlider = this.FindControl<Slider>("PART_OpacitySlider");
        _opacityValue = this.FindControl<TextBlock>("PART_OpacityValue");
        _flowSlider = this.FindControl<Slider>("PART_FlowSlider");
        _flowValue = this.FindControl<TextBlock>("PART_FlowValue");

        if (_sizeSlider is not null)
            _sizeSlider.ValueChanged += (_, e) =>
            {
                if (_syncing) return;
                float v = (float)Math.Round(e.NewValue, 1);
                if (_sizeValue is not null) _sizeValue.Text = v.ToString("F1");
                BrushSizeChanged?.Invoke(this, v);
            };

        if (_hardnessSlider is not null)
            _hardnessSlider.ValueChanged += (_, e) =>
            {
                if (_syncing) return;
                float v = (float)Math.Round(e.NewValue, 2);
                if (_hardnessValue is not null) _hardnessValue.Text = v.ToString("F2");
                BrushHardnessChanged?.Invoke(this, v);
            };

        if (_opacitySlider is not null)
            _opacitySlider.ValueChanged += (_, e) =>
            {
                if (_syncing) return;
                float v = (float)Math.Round(e.NewValue, 2);
                if (_opacityValue is not null) _opacityValue.Text = v.ToString("F2");
                BrushOpacityChanged?.Invoke(this, v);
            };

        if (_flowSlider is not null)
            _flowSlider.ValueChanged += (_, e) =>
            {
                if (_syncing) return;
                float v = (float)Math.Round(e.NewValue, 2);
                if (_flowValue is not null) _flowValue.Text = v.ToString("F2");
                BrushFlowChanged?.Invoke(this, v);
            };
    }

    public void SetTool(ToolType tool)
    {
        if (_toolLabel is not null)
            _toolLabel.Text = tool switch
            {
                ToolType.Brush => "Brush",
                ToolType.FloodFill => "Fill",
                _ => tool.ToString(),
            };

        if (_brushOptions is not null)
            _brushOptions.IsVisible = tool == ToolType.Brush;
    }

    public void SyncFromCanvas(CanvasControl canvas)
    {
        _syncing = true;
        try
        {
            if (_sizeSlider is not null) _sizeSlider.Value = canvas.BrushSize;
            if (_sizeValue is not null) _sizeValue.Text = canvas.BrushSize.ToString("F1");
            if (_hardnessSlider is not null) _hardnessSlider.Value = canvas.BrushHardness;
            if (_hardnessValue is not null) _hardnessValue.Text = canvas.BrushHardness.ToString("F2");
            if (_opacitySlider is not null) _opacitySlider.Value = canvas.BrushOpacity;
            if (_opacityValue is not null) _opacityValue.Text = canvas.BrushOpacity.ToString("F2");
            if (_flowSlider is not null) _flowSlider.Value = canvas.BrushFlow;
            if (_flowValue is not null) _flowValue.Text = canvas.BrushFlow.ToString("F2");
        }
        finally
        {
            _syncing = false;
        }
    }
}
