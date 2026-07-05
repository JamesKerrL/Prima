using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Prima.App;

namespace Prima.Desktop;

public partial class MainWindow : Window
{
    private const int CanvasWidth = 640;
    private const int CanvasHeight = 480;

    private readonly Document _document;
    private SettingsWindow? _settingsWindow;
    private WindowState _preFullScreenState = WindowState.Maximized;

    public MainWindow()
    {
        InitializeComponent();

        WindowState = WindowState.Maximized;

        _document = new Document(CanvasWidth, CanvasHeight);
        _document.Clear(Rgba.White);

        Canvas.Document = _document;
        BrushColorPicker.SelectedColor = Canvas.BrushColor;
    }

    private void OnBrushColorChanged(object? sender, Rgba color) => Canvas.BrushColor = color;

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(this);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }

        _settingsWindow.Show(this);
        _settingsWindow.Activate();
    }

    private void OnToggleFullScreenClick(object? sender, RoutedEventArgs e) => ToggleFullScreen();

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            ToggleFullScreen();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    public void ToggleFullScreen()
    {
        if (WindowState == WindowState.FullScreen)
        {
            WindowState = _preFullScreenState;
        }
        else
        {
            _preFullScreenState = WindowState;
            WindowState = WindowState.FullScreen;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _document.Dispose();
        base.OnClosed(e);
    }
}
