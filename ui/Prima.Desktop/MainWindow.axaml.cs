using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Prima.App;

#pragma warning disable CS0618  // OpenFileDialog/SaveFileDialog are deprecated but work fine.

namespace Prima.Desktop;

public partial class MainWindow : Window
{
    private const int CanvasWidth = 640;
    private const int CanvasHeight = 480;

    private SettingsWindow? _settingsWindow;
    private WindowState _preFullScreenState = WindowState.Maximized;

    public MainWindow()
    {
        InitializeComponent();

        WindowState = WindowState.Maximized;

        var doc = new Document(CanvasWidth, CanvasHeight);
        doc.Clear(Rgba.White);

        Canvas.Document = doc;
        BrushColorPicker.SelectedColor = Canvas.BrushColor;
    }

    private void OnBrushColorChanged(object? sender, Rgba color) => Canvas.BrushColor = color;

    private async void OnOpenFile(object? sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Open Image",
            Filters =
            {
                new FileDialogFilter
                {
                    Name = "Image Files",
                    Extensions = { "png", "jpg", "jpeg" }
                },
                new FileDialogFilter
                {
                    Name = "All Files",
                    Extensions = { "*" }
                }
            },
            AllowMultiple = false,
        };

        string[]? result = await dlg.ShowAsync(this);
        if (result is null || result.Length == 0) return;

        var doc = Document.LoadFromFile(result[0]);
        if (doc is null) return;

        Canvas.Document = doc;
    }

    private async void OnExportPng(object? sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Export as PNG",
            DefaultExtension = "png",
            Filters =
            {
                new FileDialogFilter { Name = "PNG Image", Extensions = { "png" } }
            },
        };

        string? path = await dlg.ShowAsync(this);
        if (path is null) return;

        Canvas.Document?.SaveAsPng(path);
    }

    private async void OnExportJpeg(object? sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Export as JPEG",
            DefaultExtension = "jpg",
            Filters =
            {
                new FileDialogFilter { Name = "JPEG Image", Extensions = { "jpg", "jpeg" } }
            },
        };

        string? path = await dlg.ShowAsync(this);
        if (path is null) return;

        Canvas.Document?.SaveAsJpeg(path);
    }

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
        Canvas.Document?.Dispose();
        base.OnClosed(e);
    }
}
