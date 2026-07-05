# Command / Function Search

Status: **planned — not yet implemented.** A menu-launched search box that lets
the user type the name of any function and then *reveals and flashes* the actual
UI control for it — opening the containing menu or panel first when the control
is nested — so the user learns where the control lives.

This is a **discoverability** feature, not a command executor. Picking a result
does **not** run the action; it only shows the user *where to look*.

## Requirements

1. **Search any function by name or keyword** — a text input filters a catalog of
   every user-facing function (menu commands, tools, color-picker actions, …).
2. **Reveal & flash the control** — selecting a result highlights the real UI
   control with a brief flash animation so the user can see where it is.
3. **Open the container first** — if the control lives in a menu (or a collapsed
   panel), open that menu/panel before flashing the control.
4. **Menu-bar entry point** — the search is launched from a menu-bar item
   (`Help → Find a Command…`); an optional `Ctrl+K` accelerator on that same item
   is a cheap add.
5. **Catalog + search are headless** — the searchable catalog and fuzzy-match
   logic live in the app layer (`Prima.App`) and are unit-testable without a
   display. Only the reveal/flash/menu-open is UI-layer.
6. **Reuse the shared theme** — the overlay and the flash highlight use the
   centralized tokens in `Themes/Theme.axaml` (accent, surface, text, spacing).
   No screen-local one-off colors.
7. **No engine or interop changes** — this is an app-layer + UI-layer feature only.

### Out of scope (Phase 1)

- **Executing the action.** Selecting a result reveals and flashes only. Wiring
  each function to an invokable command is a later phase (and pairs naturally with
  undo/redo).
- Highlighting matched characters within the result rows.
- Flashing a whole group/path of controls (only the single target control flashes;
  its containing menu opens).
- Persisting anything (recent searches, catalog to disk).
- Editing keybindings — a separate `UX features.md` item that this catalog will
  later feed.

## Architecture

The feature splits cleanly along Prima's layering. The **catalog and search** are
app-layer concerns (headless, unit-tested); the **reveal, menu-open, and flash**
are inherently UI. The two are kept in sync by registering each function's search
metadata and its UI locator together in one place.

1. **App layer (`Prima.App`)** — `CommandDescriptor` (data), `FuzzyMatcher`
   (scoring), `CommandRegistry` (register + ranked search). Pure, no UI deps.
2. **UI layer (`Prima.Desktop`)** — `CommandTarget`/`CommandTargetRegistry`
   (how to reveal a control), `ControlHighlighter` (the flash), `CommandPalette`
   (the overlay), and `CommandCatalog` (registers descriptors + targets for the
   controls that exist today). `MainWindow` hosts the overlay and the menu entry.

This app-layer `CommandRegistry` is the intended foundation for the
adjustable-keybinds and undo/redo command items also listed in
[UX features.md](UX%20features.md).

**Interpretation of "all controls flash":** the *target* control flashes; when it
lives in a menu, the menu opens first and the target menu item flashes. Flashing a
whole group or path is deferred (see Out of scope).

## Implementation details

### App layer: `Prima.App`

No new NuGet dependencies. New files under `app/Prima.App/Commands/`.

**`CommandDescriptor.cs`** (new) — the searchable metadata for one function:

```csharp
namespace Prima.App.Commands;

public sealed record CommandDescriptor(
    string Id,                         // stable, e.g. "file.open"
    string Title,                      // "Open File"
    string Category,                   // "File", "View", "Tools", "Color"
    IReadOnlyList<string> Keywords,    // synonyms: "load", "import", "picture"
    string? Shortcut = null);          // display hint, e.g. "Ctrl+O"

public sealed record CommandMatch(CommandDescriptor Command, int Score);
```

**`FuzzyMatcher.cs`** (new) — pure subsequence scorer:

```csharp
namespace Prima.App.Commands;

public static class FuzzyMatcher
{
    // Returns a score if every char of `query` appears in order within
    // `candidate` (case-insensitive); returns null if not a subsequence.
    // Higher = better: bonus for contiguous runs and start-of-word matches.
    public static int? Score(string candidate, string query);
}
```

- Empty/whitespace query returns a neutral score (everything matches).
- Case-insensitive. Contiguous matched chars and matches at word boundaries
  (start, after space/`.`/`-`) score higher.

**`CommandRegistry.cs`** (new):

```csharp
namespace Prima.App.Commands;

public sealed class CommandRegistry
{
    public void Register(CommandDescriptor descriptor);
    public IReadOnlyList<CommandDescriptor> All { get; }

    // Ranked matches. Scores Title, each Keyword, and Category; keeps the best
    // score per command. Empty query returns All (ordered by Category, Title).
    public IReadOnlyList<CommandMatch> Search(string query, int max = 20);
}
```

### UI layer: `Prima.Desktop`

New files under `ui/Prima.Desktop/Commands/` and `ui/Prima.Desktop/Controls/`.

**`Commands/CommandTarget.cs`** (new) — how to reveal one command's control:

```csharp
namespace Prima.Desktop.Commands;

public sealed class CommandTarget
{
    public required string Id { get; init; }
    public required Func<Control?> Locate { get; init; }  // control to flash
    public Func<Task>? Reveal { get; init; }              // open menu/panel first
}

public sealed class CommandTargetRegistry
{
    public void Register(CommandTarget target);
    public CommandTarget? Get(string id);
}
```

- `Reveal` is awaitable so a just-opened menu item is realized in the visual tree
  before `Locate` runs.

**`Commands/ControlHighlighter.cs`** (new) — the flash:

```csharp
namespace Prima.Desktop.Commands;

public static class ControlHighlighter
{
    // Places an accent-colored highlight over the control's bounds using the
    // AdornerLayer (fallback: a Border in the window's overlay positioned via
    // TranslatePoint), pulses opacity ~3x over ~1.2s, then removes it.
    public static Task FlashAsync(Control? control);
}
```

- Uses `AdornerLayer.GetAdornerLayer(control)`; the highlight `Border` uses
  `PrimaAccentBrush` and a rounded outline. No-op if `control` is null.
- Fully transient: created on flash, removed when the pulse completes.

**`Controls/CommandPalette.axaml` + `.cs`** (new) — the overlay:

- A full-window scrim (`Panel`, semi-transparent `PrimaBackground`) with a
  centered card (`Border`, `PrimaSurfaceBrush`, `PrimaPadLarge`) near the top.
- A search `TextBox` (auto-focused on open) and a results `ListBox` showing each
  match's `Title`, `Category`, and `Shortcut` (styled with theme tokens).
- Live filter: on text change, call `CommandRegistry.Search` and rebind results;
  keep the first row selected.
- Keyboard: `Up`/`Down` move selection, `Enter` (or click) chooses, `Esc` closes.
- Raises `event EventHandler<string>? CommandChosen` with the selected `Id`, and
  `event EventHandler? Dismissed`.

**`Commands/CommandCatalog.cs`** (new) — the single place descriptors and targets
are declared together (keeps names and locators in sync):

```csharp
public static class CommandCatalog
{
    public static void Populate(
        MainWindow window,
        CommandRegistry registry,
        CommandTargetRegistry targets);
}
```

Registers (at minimum) the functions that exist today:

| Id | Title / Category | Locate → | Reveal |
|---|---|---|---|
| `file.open` | Open File / File | File menu's Open item | open `File` menu |
| `file.export.png` | Export as PNG / File | Export→PNG item | open `File` menu |
| `file.export.jpeg` | Export as JPEG / File | Export→JPEG item | open `File` menu |
| `app.settings` | Settings / File | Settings item | open `File` menu |
| `view.fullscreen` | Toggle Fullscreen / View | Fullscreen item | open `View` menu |
| `tool.brush` | Brush Tool / Tools | `PART_Brush` in ToolPanel | — |
| `color.tab.rgb` | Color: RGB tab / Color | `PART_TabRgb` | — |
| `color.tab.hsv` | Color: HSV tab / Color | `PART_TabHsv` | — |
| `color.tab.hex` | Color: HEX tab / Color | `PART_TabHex` | — |
| `color.addswatch` | Add Swatch / Color | `PART_AddSwatch` | — |
| `color.wheel` | Color Wheel / Color | `PART_Wheel` | — |

Locators use `window.FindControl<T>("PART_…")` on the relevant control (naming
menu items with `x:Name` so they can be found). New commands are added here as new
controls land.

**`MainWindow.axaml.cs`** — additions:

- Build a `CommandRegistry` + `CommandTargetRegistry` on construction and call
  `CommandCatalog.Populate(this, registry, targets)`.
- Helper `Task OpenMenuAsync(string header)`: find the top-level `MenuItem` by
  header and open it (`menuItem.Open()`), then `await Dispatcher.UIThread` at
  background priority so the submenu items are realized before flashing.
- `OnFindCommand`: show the `CommandPalette` overlay (build lazily on first use).
- On `CommandPalette.CommandChosen(id)`: hide the palette, then
  ```csharp
  var target = _targets.Get(id);
  if (target?.Reveal is { } reveal) await reveal();
  await ControlHighlighter.FlashAsync(target?.Locate());
  ```
- On `Dismissed`: hide the palette.

**`MainWindow.axaml`** — layout + menu:

- Wrap the current root `DockPanel` in a single-cell `Panel` (or `Grid`) so the
  `CommandPalette` overlay can be the last child (top of the z-order, hidden by
  default):
  ```xml
  <Panel>
    <DockPanel LastChildFill="True"> ... existing content ... </DockPanel>
    <controls:CommandPalette x:Name="Palette" IsVisible="False" />
  </Panel>
  ```
- Add a `Help` top-level menu with the entry point:
  ```xml
  <MenuItem Header="_Help">
    <MenuItem Header="_Find a Command…" InputGesture="Ctrl+K"
              Click="OnFindCommand" />
  </MenuItem>
  ```
- Give the existing menu items `x:Name`s (e.g. `MenuItemOpen`, `MenuItemFullscreen`)
  so the catalog's locators can find them.

**Theme** — the flash should use `PrimaAccent`/`PrimaAccentBrush`. Add a
`PrimaFlashBrush` token to `Themes/Theme.axaml` only if the accent alone reads
poorly over some surfaces.

## Testing

### App-layer tests (`tests/app/Prima.App.Tests/`)

- **`FuzzyMatcherTests`** — `Score` returns null for a non-subsequence; ranks a
  contiguous/prefix match above a scattered one; is case-insensitive; empty query
  matches everything.
- **`CommandRegistryTests`** — `Search("open")` ranks `file.open` first;
  keyword-only match (e.g. "import" → Export) is found; category match works;
  empty query returns `All` ordered by Category then Title; `max` caps results.

### UI tests

Manual verification (no automated UI rig yet):

1. `dotnet run` in `ui/Prima.Desktop`.
2. `Help → Find a Command…` (or `Ctrl+K`) → overlay opens with the search box
   focused.
3. Type `open` → `Open File` is the top result → `Enter` → the **File menu opens**
   and the **Open item flashes**.
4. Type `brush` → `Enter` → the brush tool button (`PART_Brush`) flashes.
5. Type `hex` → `Enter` → the color picker's HEX tab flashes.
6. `Esc` closes the overlay without revealing anything.

## File-by-file change list

| File | Change |
|---|---|
| `app/Prima.App/Commands/CommandDescriptor.cs` | **new** — descriptor + match records |
| `app/Prima.App/Commands/FuzzyMatcher.cs` | **new** — subsequence scorer |
| `app/Prima.App/Commands/CommandRegistry.cs` | **new** — register + ranked search |
| `tests/app/Prima.App.Tests/FuzzyMatcherTests.cs` | **new** — scoring/ordering tests |
| `tests/app/Prima.App.Tests/CommandRegistryTests.cs` | **new** — search/ranking tests |
| `ui/Prima.Desktop/Commands/CommandTarget.cs` | **new** — target + registry |
| `ui/Prima.Desktop/Commands/ControlHighlighter.cs` | **new** — flash animation |
| `ui/Prima.Desktop/Commands/CommandCatalog.cs` | **new** — register descriptors + targets |
| `ui/Prima.Desktop/Controls/CommandPalette.axaml` | **new** — overlay layout |
| `ui/Prima.Desktop/Controls/CommandPalette.axaml.cs` | **new** — filter, keyboard nav, events |
| `ui/Prima.Desktop/MainWindow.axaml` | wrap root in Panel; add `Help` menu + overlay; name menu items |
| `ui/Prima.Desktop/MainWindow.axaml.cs` | build registry/targets; `OpenMenuAsync`; show palette; reveal + flash |
| `ui/Prima.Desktop/Themes/Theme.axaml` | optional `PrimaFlashBrush` token |

## Task breakdown

| # | Task | Files | Notes |
|---|---|---|---|
| 1 | `CommandDescriptor` + `CommandMatch` records | `app/Prima.App/Commands/CommandDescriptor.cs` | Pure data; no logic |
| 2 | `FuzzyMatcher` static + tests | `app/Prima.App/Commands/FuzzyMatcher.cs`, `tests/app/Prima.App.Tests/FuzzyMatcherTests.cs` | Subsequence score, start-of-word/contiguous bonus, case-insensitive |
| 3 | `CommandRegistry` (Register/All/Search) + tests | `app/Prima.App/Commands/CommandRegistry.cs`, `tests/app/Prima.App.Tests/CommandRegistryTests.cs` | Ranks Title/Keywords/Category; empty → All; `max` cap |
| 4 | `CommandTarget` + `CommandTargetRegistry` | `ui/Prima.Desktop/Commands/CommandTarget.cs` | Locate + optional Reveal delegates |
| 5 | `ControlHighlighter.FlashAsync` | `ui/Prima.Desktop/Commands/ControlHighlighter.cs` | AdornerLayer highlight, accent pulse ~3x, auto-remove |
| 6 | `CommandPalette` overlay (axaml + cs) | `ui/Prima.Desktop/Controls/CommandPalette.axaml(.cs)` | Scrim + search box + results ListBox + `Up/Down/Enter/Esc`; `CommandChosen`/`Dismissed` events |
| 7 | Wrap root in Panel; add `Help → Find a Command…` (+opt `Ctrl+K`); name menu items; host overlay | `ui/Prima.Desktop/MainWindow.axaml` | Layout + menu markup |
| 8 | `CommandCatalog.Populate` + `MainWindow.OpenMenuAsync`/finders | `ui/Prima.Desktop/Commands/CommandCatalog.cs`, `MainWindow.axaml.cs` | Register descriptors + targets for all current controls |
| 9 | Wire palette in MainWindow: show/hide, `CommandChosen` → reveal → flash | `ui/Prima.Desktop/MainWindow.axaml.cs` | Glue |
| 10 | Optional theme token for flash highlight | `ui/Prima.Desktop/Themes/Theme.axaml` | Only if accent reuse reads poorly |

**Sequencing:** app layer + tests (tasks 1–3) can be built and verified
independently first. UI (4–10) builds on top; tasks 4, 5, and 6 are independent of
one another and can be done in any order before wiring in 7–9.

## Performance conformance

- **Off the hot path** — nothing here touches stroke input or canvas rendering.
- The `CommandPalette` overlay is built lazily on first open and reused
  thereafter; while hidden it costs nothing.
- Search is O(n) over a small in-memory list (tens of commands) with no
  allocation beyond the results list — trivially fast per keystroke.
- The flash is a single transient adorner with an opacity animation, created on
  reveal and removed when the pulse finishes; no lingering visual-tree cost.
