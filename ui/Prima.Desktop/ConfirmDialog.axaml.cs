using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Prima.Desktop;

/// <summary>Reusable Yes/No confirmation dialog (e.g. "discard unsaved changes?").</summary>
public partial class ConfirmDialog : Window
{
    private bool _confirmed;

    public ConfirmDialog()
    {
        InitializeComponent();
    }

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        _confirmed = true;
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        _confirmed = false;
        Close();
    }

    /// <summary>Shows the dialog and returns true if the user confirmed.</summary>
    public static async Task<bool> Show(
        Window owner, string title, string message, string confirmText = "Discard")
    {
        var dlg = new ConfirmDialog
        {
            Title = title,
            MessageText = { Text = message },
            ConfirmBtn = { Content = confirmText },
        };
        await dlg.ShowDialog(owner);
        return dlg._confirmed;
    }
}
