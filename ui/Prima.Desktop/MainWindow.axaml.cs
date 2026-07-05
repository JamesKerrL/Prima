using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Prima.App;
using Prima.App.Commands;
using Prima.Desktop.Commands;
using Prima.Desktop.Controls;

#pragma warning disable CS0618  // OpenFileDialog/SaveFileDialog are deprecated but work fine.

namespace Prima.Desktop;

public partial class MainWindow : Window
{
    private const int CanvasWidth = 640;
    private const int CanvasHeight = 480;
    private const string BaseTitle = "Prima";

    private SettingsWindow? _settingsWindow;
    private WindowState _preFullScreenState = WindowState.Maximized;

    private readonly CommandRegistry _commandRegistry = new();
    private readonly CommandTargetRegistry _targetRegistry = new();

    public MainWindow()
    {
        InitializeComponent();

        WindowState = WindowState.Maximized;

        var doc = new Document(CanvasWidth, CanvasHeight);
        doc.Clear(Rgba.White);
        doc.History.Clear(); // the initial fill is setup, not an undoable edit

        SetDocument(doc);
        BrushColorPicker.SelectedColor = Canvas.BrushColor;
    }

    private void SetDocument(Document doc)
    {
        if (Canvas.Document is { } old)
            old.History.Changed -= OnHistoryChanged;

        Canvas.Document = doc;
        doc.History.Changed += OnHistoryChanged;
        UpdateTitle();

        BrushColorPicker.SelectedColor = Canvas.BrushColor;

        CommandCatalog.Populate(this, _commandRegistry, _targetRegistry);

        Palette.CommandChosen += OnPaletteCommandChosen;
        Palette.Dismissed += (_, _) => Palette.Hide();
    }

    private void OnHistoryChanged() => UpdateTitle();

    private void UpdateTitle()
    {
        bool modified = Canvas.Document?.IsModified ?? false;
        Title = modified ? $"{BaseTitle} •" : BaseTitle;
    }

    /// <summary>Prompts to discard unsaved changes if the current document is modified.
    /// Returns true if it's safe to proceed (no unsaved changes, or the user confirmed discard).</summary>
    private async System.Threading.Tasks.Task<bool> ConfirmDiscardIfModifiedAsync()
    {
        if (Canvas.Document is not { IsModified: true }) return true;
        return await ConfirmDialog.Show(
            this, "Unsaved Changes",
            "This document has unsaved changes. Discard them?");
    }

    private void OnBrushColorChanged(object? sender, Rgba color) => Canvas.BrushColor = color;

    private void OnToolSelected(object? sender, ToolType tool) => Canvas.CurrentTool = tool;

    private async void OnOpenFile(object? sender, RoutedEventArgs e)
    {
        if (!await ConfirmDiscardIfModifiedAsync()) return;

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
        doc.MarkSaved(); // freshly loaded from disk — starts clean, empty history

        SetDocument(doc);
    }

    private void OnUndoClick(object? sender, RoutedEventArgs e) => Canvas.Undo();

    private void OnRedoClick(object? sender, RoutedEventArgs e) => Canvas.Redo();

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

    private void OnFindCommand(object? sender, RoutedEventArgs e)
    {
        Palette.Show(_commandRegistry);
    }

    private async void OnPaletteCommandChosen(object? sender, string id)
    {
        try
        {
            Palette.Hide();

            var target = _targetRegistry.Get(id);
            if (target?.Reveal is { } reveal)
                await reveal();

            await ControlHighlighter.FlashAsync(target?.Locate());
        }
        catch
        {
            // Prevent async void crashes from silently breaking the feature
        }
    }

    public async System.Threading.Tasks.Task RevealMenuAsync(params string[] menuPath)
    {
        var menu = this.FindControl<Menu>("PART_MainMenu");
        if (menu is null) return;

        ItemsControl? current = menu;
        foreach (var header in menuPath)
        {
            MenuItem? found = null;
            foreach (var item in current.Items)
            {
                if (item is MenuItem mi && string.Equals(
                        mi.Header?.ToString(), header, StringComparison.Ordinal))
                {
                    found = mi;
                    break;
                }
            }

            if (found is null) return;

            found.IsSubMenuOpen = true;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            current = found;
        }
    }

    public MenuItem? LocateMenuItem(params string[] headers)
    {
        var menu = this.FindControl<Menu>("PART_MainMenu");
        if (menu is null) return null;

        ItemsControl? current = menu;
        foreach (var header in headers)
        {
            MenuItem? found = null;
            foreach (var item in current.Items)
            {
                if (item is MenuItem mi && string.Equals(
                        mi.Header?.ToString(), header, StringComparison.Ordinal))
                {
                    found = mi;
                    break;
                }
            }
            current = found;
            if (current is null) return null;
        }
        return current as MenuItem;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            ToggleFullScreen();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.K && e.KeyModifiers == KeyModifiers.Control)
        {
            Palette.Show(_commandRegistry);
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

    private bool _closeConfirmed;

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (!_closeConfirmed && Canvas.Document is { IsModified: true })
        {
            e.Cancel = true;
            base.OnClosing(e);

            if (await ConfirmDiscardIfModifiedAsync())
            {
                _closeConfirmed = true;
                Close();
            }
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        Canvas.Document?.Dispose();
        base.OnClosed(e);
    }
}
