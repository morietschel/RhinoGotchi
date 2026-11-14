# HouseTutor

A Rhino 8 plugin (Windows and Mac) that adds a **House Tutor** panel. It walks
you, step by step, through building a simple house:

1. **Walls** – a box (`Box`)
2. **Roof** – a four-sided pyramid (`Pyramid`)
3. **Join** – optionally merge them into one solid (`BooleanUnion`)

Each step shows exactly what to type in the command line and has a button that
starts the right command for you.

## Use it

1. Install the plugin (see below).
2. In Rhino, type `HouseTutor`. The panel opens and can be docked like any Rhino panel.
3. Follow the pages with **Next** and **Back**.

## Requirements

- Rhino 8
- .NET SDK 7 or newer to build (`dotnet --list-sdks`)

## Build

```bash
dotnet build HouseTutor.sln -c Release
```

Output:

| File | Runs in |
| --- | --- |
| `src/HouseTutor/bin/Release/net7.0/HouseTutor.rhp` | Rhino 8 Mac, Rhino 8 Windows (default) |
| `src/HouseTutor/bin/Release/net48/HouseTutor.rhp` | Rhino 8 Windows started on .NET Framework |

## Install

Drag the `.rhp` onto the Rhino 8 window, or run `PluginManager` → Install.
On Windows use `net7.0` unless you switched Rhino to .NET Framework with `SetDotNetRuntime`.

## Develop in VS Code

1. Install the recommended extensions: **C#** and **C# Dev Kit**.
2. Run and Debug → **Rhino 8 (.NET 7+) - Mac / Windows** → F5.
   Rhino starts with the plugin loaded from `bin/Debug`; nothing to install.
3. Close Rhino before rebuilding, because Rhino locks the loaded `.rhp`.

## Layout

```
HouseTutor.sln
src/HouseTutor/
  HouseTutorPlugin.cs    plugin class, registers the panel
  HouseTutorCommand.cs   HouseTutor command, opens the panel
  HouseTutorPanel.cs     the Eto panel (pages, Back/Next, action button)
  TutorialStep.cs        the tutorial text and the command each step runs
  Resources/HouseTutor.ico  panel tab icon
```

To change the instructions, edit `TutorialStep.House` in `TutorialStep.cs`.
