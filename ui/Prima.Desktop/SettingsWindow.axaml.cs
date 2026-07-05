using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Prima.App;

namespace Prima.Desktop;

public partial class SettingsWindow : Window
{
    private static readonly (int Width, int Height)[] Resolutions =
    {
        (1280, 720),
        (1600, 900),
        (1920, 1080),
        (2560, 1440),
    };

    private readonly MainWindow? _owner;

    public SettingsWindow()
    {
        InitializeComponent();
    }

    public SettingsWindow(MainWindow owner) : this()
    {
        _owner = owner;

        PalettesDirText.Text = AppPaths.Palettes;
        OpenPalettesDirBtn.Click += OnOpenPalettesDir;
    }

    private void OnResolutionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var index = ResolutionComboBox.SelectedIndex;
        if (index < 0 || _owner is null)
        {
            return;
        }

        if (index == 0)
        {
            _owner.WindowState = WindowState.Maximized;
            return;
        }

        var (width, height) = Resolutions[index - 1];
        _owner.WindowState = WindowState.Normal;
        _owner.Width = width;
        _owner.Height = height;
    }

    private void OnOpenPalettesDir(object? sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(AppPaths.Palettes) { UseShellExecute = true });
        }
        catch
        {
            // Fallback: silently ignore if we can't open the folder
        }
    }
}
