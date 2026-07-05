using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Prima.App.Commands;
using Prima.Desktop.Controls;

namespace Prima.Desktop.Commands;

public static class CommandCatalog
{
    public static void Populate(
        MainWindow window,
        CommandRegistry registry,
        CommandTargetRegistry targets)
    {
        registry.Register(new CommandDescriptor("file.open", "Open File", "File",
            ["load", "import", "picture"], "Ctrl+O"));
        targets.Register(new CommandTarget
        {
            Id = "file.open",
            Locate = () => window.LocateMenuItem("_File", "_Open..."),
            Reveal = () => window.RevealMenuAsync("_File"),
        });

        registry.Register(new CommandDescriptor("file.export.png", "Export as PNG", "File",
            ["save"], "Ctrl+Shift+S"));
        targets.Register(new CommandTarget
        {
            Id = "file.export.png",
            Locate = () => window.LocateMenuItem("_File", "_Export", "As _PNG..."),
            Reveal = () => window.RevealMenuAsync("_File", "_Export"),
        });

        registry.Register(new CommandDescriptor("file.export.jpeg", "Export as JPEG", "File",
            ["save"]));
        targets.Register(new CommandTarget
        {
            Id = "file.export.jpeg",
            Locate = () => window.LocateMenuItem("_File", "_Export", "As _JPEG..."),
            Reveal = () => window.RevealMenuAsync("_File", "_Export"),
        });

        registry.Register(new CommandDescriptor("app.settings", "Settings", "File",
            ["preferences", "options"]));
        targets.Register(new CommandTarget
        {
            Id = "app.settings",
            Locate = () => window.LocateMenuItem("_File", "_Settings..."),
            Reveal = () => window.RevealMenuAsync("_File"),
        });

        registry.Register(new CommandDescriptor("view.fullscreen", "Toggle Fullscreen", "View",
            ["full", "screen"], "F11"));
        targets.Register(new CommandTarget
        {
            Id = "view.fullscreen",
            Locate = () => window.LocateMenuItem("_View", "Toggle _Fullscreen"),
            Reveal = () => window.RevealMenuAsync("_View"),
        });

        registry.Register(new CommandDescriptor("tool.brush", "Brush Tool", "Tools",
            ["brush", "paint"]));
        targets.Register(new CommandTarget
        {
            Id = "tool.brush",
            Locate = () => window.FindControl<ToolPanel>("PART_ToolPanel")
                           ?.FindControl<RadioButton>("PART_Brush"),
        });

        registry.Register(new CommandDescriptor("color.tab.rgb", "Color: RGB tab", "Color",
            ["rgb"]));
        targets.Register(new CommandTarget
        {
            Id = "color.tab.rgb",
            Locate = () => window.FindControl<ColorPickerControl>("BrushColorPicker")
                           ?.FindControl<ToggleButton>("PART_TabRgb"),
        });

        registry.Register(new CommandDescriptor("color.tab.hsv", "Color: HSV tab", "Color",
            ["hsv"]));
        targets.Register(new CommandTarget
        {
            Id = "color.tab.hsv",
            Locate = () => window.FindControl<ColorPickerControl>("BrushColorPicker")
                           ?.FindControl<ToggleButton>("PART_TabHsv"),
        });

        registry.Register(new CommandDescriptor("color.tab.hex", "Color: HEX tab", "Color",
            ["hex"]));
        targets.Register(new CommandTarget
        {
            Id = "color.tab.hex",
            Locate = () => window.FindControl<ColorPickerControl>("BrushColorPicker")
                           ?.FindControl<ToggleButton>("PART_TabHex"),
        });

        registry.Register(new CommandDescriptor("color.addswatch", "Add Swatch", "Color",
            ["swatch", "palette"]));
        targets.Register(new CommandTarget
        {
            Id = "color.addswatch",
            Locate = () => window.FindControl<ColorPickerControl>("BrushColorPicker")
                           ?.FindControl<Button>("PART_AddSwatch"),
        });

        registry.Register(new CommandDescriptor("color.wheel", "Color Wheel", "Color",
            ["wheel", "hue"]));
        targets.Register(new CommandTarget
        {
            Id = "color.wheel",
            Locate = () => window.FindControl<ColorPickerControl>("BrushColorPicker")
                           ?.FindControl<HueRingTriangleControl>("PART_Wheel"),
        });
    }
}
