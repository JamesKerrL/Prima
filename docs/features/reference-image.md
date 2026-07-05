# Reference Image Panel

Status: **planned — not yet implemented.** A togglable panel that displays
reference images alongside the main canvas, so the user can load PNG or JPEG
files as visual reference while painting.

## Requirements

1. **Load and display reference images** — PNG and JPEG at minimum, from a file
   dialog.
2. **Toggle visibility** — a View menu item to show/hide the reference panel.
3. **Pan/zoom** — the reference image gets its own viewport (independent of the
   main canvas pan/zoom), so users can examine details.
4. **Replace images** — load a new image into the panel at any time (replaces the
   current one).
5. **Headless-capable** — the `ReferenceImage` model lives in the app layer and
   is loadable without a display.
6. **Minimal new dependencies** — one new NuGet package for image decoding in the
   app layer; no engine or interop changes.

### Out of scope (Phase 1)

- Multiple simultaneous reference images (deferred — a tabbed or strip
  collection belongs in a later phase).
- Image drag-and-drop from the OS.
- Image transformations (rotate, flip, color adjustments).
- Saving/restoring reference image state across sessions.

## Architecture

The feature touches only the app layer and the UI layer — no engine or interop
changes. The pattern mirrors how the main canvas renders:

1. **App layer** (`Prima.App`): `ReferenceImage` loads a file via `ImageSharp`,
   decodes it to RGBA8, and exposes the pixel span + dimensions. Headless-usable
   and unit-testable.
2. **UI layer** (`Prima.Desktop`): a new `ReferencePanel` control displays the
   reference image in its own `WriteableBitmap` with independent pan/zoom. The
   panel lives in a new right-side area of `MainWindow`.

### App layer: `Prima.App`

**NuGet addition** — `SixLabors.ImageSharp` (pure C#, cross-platform, no native
deps). Added to `app/Prima.App/Prima.App.csproj`.

**`app/Prima.App/ReferenceImage.cs`** (new):

```csharp
namespace Prima.App;

public sealed class ReferenceImage : IDisposable
{
    public int Width { get; }
    public int Height { get; }
    public ReadOnlySpan<byte> Pixels { get; }  // RGBA8, row-major

    public static ReferenceImage? LoadFromFile(string path);

    public void Dispose();  // returns the pixel buffer
}
```

- `LoadFromFile` uses `ImageSharp` to decode the file, converts to RGBA8 (always
  straight RGBA8 to match the canvas convention), and stores in a `byte[]`.
- Returns `null` if the file can't be decoded.
- `Dispose` releases the buffer (for now, just clears the reference; buffer
  pooling is a later concern).

### UI layer: `Prima.Desktop`

**`ui/Prima.Desktop/Controls/ReferencePanel.axaml` + `.cs`** (new):

A custom `UserControl` containing:
- A toolbar row: **"Load Image"** button (opens `OpenFileDialog` filter: PNG,
  JPEG) and a **"Close"** button to unload the image.
- A custom drawing surface that renders the loaded reference image into a
  `WriteableBitmap` with its own pan/zoom state (two floats + zoom factor,
  separate from the main `Viewport`).
- When no image is loaded, show a placeholder text ("Load a reference image…")
  centered in the panel.
- Pointer input: left-drag to pan, mouse wheel to zoom (same 1.1x factor,
  clamped to [0.05, 64.0]). Auto-fit on load.

Follows the same pixel-blit pattern as `CanvasControl`: on render, lock the
`WriteableBitmap`, copy the reference image pixels into the framebuffer with
viewport offset/zoom applied (bilinear sampling deferred — nearest-neighbor is
fine for Phase 1).

**`ui/Prima.Desktop/MainWindow.axaml`** — layout changes:

Replace the current flat `DockPanel` with a `Grid` that has two columns:

```xml
<Grid>
  <Grid.ColumnDefinitions>
    <ColumnDefinition Width="*" />
    <ColumnDefinition Width="Auto" />
  </Grid.ColumnDefinitions>

  <!-- existing DockPanel (Menu + Toolbar + CanvasControl) -->
  <DockPanel Grid.Column="0" LastChildFill="True">
    <Menu DockPanel.Dock="Top">...</Menu>
    <Border DockPanel.Dock="Top">...</Border>
    <controls:CanvasControl x:Name="Canvas" />
  </DockPanel>

  <!-- reference panel, togglable -->
  <controls:ReferencePanel
    Grid.Column="1"
    IsVisible="{Binding ShowReferencePanel, ElementName=MainWindow}" />
</Grid>
```

The `ReferencePanel` has a fixed initial width (~280 px) and resizes with the
window. The column width could also use `*` with `MinWidth`/`MaxWidth` for a
draggable splitter later.

**`ui/Prima.Desktop/MainWindow.axaml.cs`** — additions:

- A `bool ShowReferencePanel` property (or field-backed bindable property) that
  controls the panel's visibility.
- View menu: add a "Reference Image" menu item below "Toggle Fullscreen":
  ```xml
  <MenuItem Header="_Reference Image" IsChecked="False"
            Click="OnToggleReferencePanel" />
  ```
- `OnToggleReferencePanel` toggles `ShowReferencePanel` and updates the menu
  item's `IsChecked` state.
- The reference panel's ImageLoaded/ImageClosed events could update a status
  indicator, but no immediate need.

**Theme tokens** — the panel background should use `PrimaSurface` or a new
`PrimaPanelBackground` resource. Add to `Themes/Theme.axaml` if needed.

## Testing

### App-layer tests (`tests/app/Prima.App.Tests/`)

- `ReferenceImage` loads a known PNG/JPEG from a test fixture directory and
  reports the correct `Width`, `Height`, and non-zero pixels.
- `LoadFromFile` on a non-existent or corrupt file returns `null`.
- Disposal: pixels are inaccessible after `Dispose` (access throws / returns
  empty).

### UI tests

Manual verification in Phase 1 (no automated UI testing rig yet):
1. `dotnet run` in `ui/Prima.Desktop`
2. View → Reference Image → panel appears on the right, showing the placeholder
   text.
3. Click "Load Image" → file dialog opens → select a PNG → image appears in the
   panel with auto-fit.
4. Pan with left-drag, zoom with mouse wheel.
5. Click "Close" → panel returns to placeholder state.
6. Toggle View → Reference Image off → panel hides; toggle on → panel reappears
   with the same image still loaded (persist the reference across show/hide).

## File-by-file change list

| File | Change |
|---|---|
| `app/Prima.App/Prima.App.csproj` | add `SixLabors.ImageSharp` package reference |
| `app/Prima.App/ReferenceImage.cs` | **new** — load decode, pixel buffer, dispose |
| `tests/app/Prima.App.Tests/ReferenceImageTests.cs` | **new** — load, corrupt file, dispose |
| `ui/Prima.Desktop/Controls/ReferencePanel.axaml` | **new** — panel layout (toolbar + image surface) |
| `ui/Prima.Desktop/Controls/ReferencePanel.axaml.cs` | **new** — load, render, pan, zoom, close |
| `ui/Prima.Desktop/MainWindow.axaml` | Grid layout with reference panel column; updated View menu |
| `ui/Prima.Desktop/MainWindow.axaml.cs` | `ShowReferencePanel` toggle, menu click handler |
| `ui/Prima.Desktop/Themes/Theme.axaml` | optional new tokens for panel styling |

## Task breakdown

| # | Task | Files | Notes |
|---|---|---|---|
| 1 | Add `SixLabors.ImageSharp` to app layer project | `app/Prima.App/Prima.App.csproj` | Mechanical — one package line |
| 2 | `ReferenceImage` class — load, decode RGBA8, dispose | `app/Prima.App/ReferenceImage.cs` | Straightforward; ImageSharp API maps 1:1 |
| 3 | xUnit tests — load, corrupt, dispose | `tests/app/Prima.App.Tests/ReferenceImageTests.cs` | Small test fixture; create a test PNG bytes-in-memory approach to avoid external assets |
| 4 | `ReferencePanel` control — layout (axaml) | `ui/Prima.Desktop/Controls/ReferencePanel.axaml` | Toolbar + border + placeholder text or image surface |
| 5 | `ReferencePanel` code-behind — load, blit, pan, zoom | `ui/Prima.Desktop/Controls/ReferencePanel.axaml.cs` | The bulk of the work: file dialog, `WriteableBitmap`, pointer handling, viewport math |
| 6 | MainWindow layout → Grid with reference column | `ui/Prima.Desktop/MainWindow.axaml` | Wrap existing DockPanel in a Grid; add reference panel column |
| 7 | View menu + toggle logic | `ui/Prima.Desktop/MainWindow.axaml` + `.cs` | New menu item, `ShowReferencePanel` toggle |

**Sequencing**: app layer + tests (tasks 1–3) → UI (4–7). No engine or interop
work.

## Performance conformance

- The reference panel is **not on the hot path** — it does not affect stroke
  input or canvas rendering. Its `WriteableBitmap` is re-rendered only when the
  image first loads, the user pans/zooms, or the panel resizes.
- Image data is loaded once and kept in memory as raw RGBA8 (4 bytes/px). A
  4K reference image (3840×2160) is ~33 MB — acceptable for a single reference.
  Multiple simultaneous reference images (future) would need a strategy
  (mipmapped texture, tile-on-demand), but that is deferred.
- No allocation on pan/zoom: the viewport state is two floats + a zoom float,
  and the render loop reuses the same `WriteableBitmap` (sized to the panel's
  pixel dimensions, recreated on resize).
