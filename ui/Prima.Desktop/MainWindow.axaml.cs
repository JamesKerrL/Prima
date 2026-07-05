using System;
using Avalonia.Controls;
using Prima.App;

namespace Prima.Desktop;

public partial class MainWindow : Window
{
    private const int CanvasWidth = 640;
    private const int CanvasHeight = 480;

    private readonly Document _document;

    public MainWindow()
    {
        InitializeComponent();

        _document = new Document(CanvasWidth, CanvasHeight);
        _document.Clear(Rgba.White);

        Canvas.Document = _document;
    }

    protected override void OnClosed(EventArgs e)
    {
        _document.Dispose();
        base.OnClosed(e);
    }
}
