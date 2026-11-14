# GotchiHouse

A Rhino 8 plugin (Windows and Mac) that adds a **GotchiHouse** panel, an
egg-shaped virtual-pet device in green, white and black. It walks you through
building a simple pitched-roof house from two shapes:

1. **Walls**: a 10 × 10 × 8 box (`Box`)
2. **Roof**: a four-sided pyramid that overhangs by 1 and rises 8 (`Pyramid`)

## How it plays

- **START** turns the Perspective viewport into an **Axon** view (parallel
  projection from the south-west) in a minimal green/white/black display mode,
  and shows the finished house there as a green **ghost**.
- Each step shows exactly what to type. **GO** starts the command for you.
  Green dots in the viewport show where each typed value lands.
- The key axon on the device's screen mirrors the model: the parts you've built
  turn solid and the rest stay ghosted. The two squares in the corner count the
  parts you've built.
- When both parts are built, you get a congratulations screen.
  (Joining them with `BooleanUnion` still counts as finished.)
- **BACK** and **NEXT** let you review earlier steps.

## Use it

1. Install the plugin (see below).
2. In Rhino, type `GotchiHouse`. The panel opens and docks like any Rhino panel.
3. Press **START**.

## Font

The panel uses **Korolev** when it's installed (for example through Adobe Fonts),
and the system font otherwise. Korolev is a commercial font, so it isn't
included in this repository.

## Requirements

- Rhino 8
- .NET SDK 7 or newer to build (`dotnet --list-sdks`)

## Build

```bash
dotnet build GotchiHouse.sln -c Release
```

| File | Runs in |
| --- | --- |
| `src/GotchiHouse/bin/Release/net7.0/GotchiHouse.rhp` | Rhino 8 Mac, Rhino 8 Windows (default) |
| `src/GotchiHouse/bin/Release/net48/GotchiHouse.rhp` | Rhino 8 Windows started on .NET Framework |

## Install

Drag the `.rhp` onto the Rhino 8 window, or run `PluginManager` → Install.
On Windows use `net7.0` unless you switched Rhino to .NET Framework with `SetDotNetRuntime`.

## Develop in VS Code

1. Install the recommended extensions: **C#** and **C# Dev Kit**.
2. Run and Debug → **Rhino 8 (.NET 7+) - Mac / Windows** → F5.
   Rhino starts with the plugin loaded from `bin/Debug`, so there's nothing to install.
3. Close Rhino before rebuilding, because Rhino locks the loaded `.rhp`.

## Layout

```
GotchiHouse.sln
src/GotchiHouse/
  GotchiHousePlugin.cs   plugin class: registers the panel, starts tracking
  GotchiHouseCommand.cs  GotchiHouse command: opens the panel
  GotchiHousePanel.cs    the dockable panel (hosts GotchiDevice)
  GotchiDevice.cs        draws the device: shell, screen, key axon, buttons
  TutorialPage.cs        the pages: text, values to type, command to run
  HouseModel.cs          the goal geometry and how a built part is recognised
  HouseSession.cs        progress tracking, Axon view and display mode setup
  HouseGhostConduit.cs   the ghost house and value dots in the viewports
  Palette.cs             colours and font
  Resources/GotchiHouse.ico  panel tab icon
```
