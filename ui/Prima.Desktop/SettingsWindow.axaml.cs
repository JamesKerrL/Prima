using Avalonia.Controls;

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
}
