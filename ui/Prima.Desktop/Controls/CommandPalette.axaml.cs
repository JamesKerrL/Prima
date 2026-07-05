using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Prima.App.Commands;

namespace Prima.Desktop.Controls;

public sealed partial class CommandPalette : UserControl
{
    public event EventHandler<string>? CommandChosen;
    public event EventHandler? Dismissed;

    private CommandRegistry? _registry;
    private IReadOnlyList<CommandMatch> _matches = [];

    public CommandPalette()
    {
        InitializeComponent();

        var searchBox = this.FindControl<TextBox>("PART_SearchBox");
        if (searchBox is not null)
        {
            searchBox.TextChanged += OnSearchTextChanged;
            searchBox.KeyDown += OnSearchKeyDown;
        }

        var results = this.FindControl<ListBox>("PART_Results");
        if (results is not null)
        {
            results.AddHandler(PointerPressedEvent, OnResultPointerPressed, RoutingStrategies.Tunnel);
        }

        AddHandler(KeyDownEvent, OnPaletteKeyDown, RoutingStrategies.Tunnel);
    }

    public void Show(CommandRegistry registry)
    {
        _registry = registry;
        _matches = registry.Search("");
        UpdateResults();
        IsVisible = true;

        var searchBox = this.FindControl<TextBox>("PART_SearchBox");
        if (searchBox is not null)
        {
            searchBox.Text = "";
            Dispatcher.UIThread.Post(() => searchBox.Focus(), DispatcherPriority.Input);
        }
    }

    public void Hide()
    {
        IsVisible = false;
        _registry = null;
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        var searchBox = (TextBox?)sender;
        if (_registry is null || searchBox is null) return;

        _matches = _registry.Search(searchBox.Text ?? "");
        UpdateResults();
    }

    private void UpdateResults()
    {
        var results = this.FindControl<ListBox>("PART_Results");
        if (results is null) return;

        var items = new List<ListBoxItem>();
        foreach (var match in _matches)
        {
            var item = CreateResultItem(match);
            items.Add(item);
        }

        results.ItemsSource = items;

        if (items.Count > 0)
            results.SelectedIndex = 0;
    }

    private static ListBoxItem CreateResultItem(CommandMatch match)
    {
        var cmd = match.Command;
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };

        var titleBlock = new TextBlock
        {
            Text = cmd.Title,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var categoryBlock = new TextBlock
        {
            Text = cmd.Category,
            Foreground = new SolidColorBrush(Color.FromArgb(160, 237, 237, 240)),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };

        panel.Children.Add(titleBlock);
        panel.Children.Add(categoryBlock);

        if (cmd.Shortcut is not null)
        {
            var spacer = new TextBlock { Text = "—", FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            var shortcutBlock = new TextBlock
            {
                Text = cmd.Shortcut,
                Foreground = new SolidColorBrush(Color.FromArgb(120, 237, 237, 240)),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
            };
            panel.Children.Add(spacer);
            panel.Children.Add(shortcutBlock);
        }

        return new ListBoxItem
        {
            Content = panel,
            Tag = cmd.Id,
        };
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        var results = this.FindControl<ListBox>("PART_Results");
        if (results is null) return;

        if (e.Key == Key.Down)
        {
            int next = Math.Min(results.SelectedIndex + 1, _matches.Count - 1);
            results.SelectedIndex = next;
            results.ScrollIntoView(next);
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            int prev = Math.Max(results.SelectedIndex - 1, 0);
            results.SelectedIndex = prev;
            results.ScrollIntoView(prev);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            CommitSelected();
        }
    }

    private void OnPaletteKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Dismissed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnResultPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        CommitSelected();
    }

    private void CommitSelected()
    {
        var results = this.FindControl<ListBox>("PART_Results");
        if (results?.SelectedItem is ListBoxItem item && item.Tag is string id)
        {
            CommandChosen?.Invoke(this, id);
        }
    }
}
