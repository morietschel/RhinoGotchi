# RhinoGotchi

A RhinoCommon plugin for **Rhino 8 (Windows and Mac) and Rhino 9 (Mac)**, based on McNeel's
[Your First Plugin](https://developer.rhino3d.com/guides/rhinocommon/your-first-plugin-windows/) guide.

One project builds two targets:

| Target | Runs in |
| --- | --- |
| `net7.0` | Rhino 9 Mac (.NET 10), Rhino 8 Mac, Rhino 8 Windows (default .NET Core runtime) |
| `net48` | Rhino 8 Windows started with `SetDotNetRuntime` = .NET Framework |

## Requirements

- .NET SDK 7 or newer (`dotnet --list-sdks`)
- Rhino 8 or Rhino 9

## Build

```bash
dotnet build RhinoGotchi.sln -c Release
```

Output:

- `src/RhinoGotchi/bin/Release/net7.0/RhinoGotchi.rhp`
- `src/RhinoGotchi/bin/Release/net48/RhinoGotchi.rhp`

## Load and run

- **Mac:** drag `bin/<Config>/net7.0/RhinoGotchi.rhp` onto the Rhino 8 window,
  or run `PluginManager` → Install.
- **Windows:** drag the `.rhp` that matches your runtime onto Rhino 8
  (`net7.0` by default, `net48` if you switched with `SetDotNetRuntime`).

Then type `RhinoGotchi`, pick two points, and a line is added.

## The lesson panel

Type `Gotchi` to open the RhinoGotchi panel.

- **First run:** pick *Start from scratch* (Level 1) or *Find my level*, a
  placement test with 2 questions per level (up to 20), easiest first. It stops at the
  first miss, and that question's level is where you start. Every level below it
  is marked done.
- **Home (level map):** shows *Your level*, total XP, and a path of levels:
  done (gold), current (green) and locked (gray). Done and current levels can be
  played; you can retake the placement test any time.
- **Progress** (finished levels, XP, placement taken) is saved in the plugin's
  settings, per user on this machine, so it survives a Rhino restart.

Levels live in `src/RhinoGotchi/Lessons/LessonCatalog.cs` (10 levels): Curves,
Solids (includes the Box build level), Surfaces, Sweeps & Pipes, Editing Solids,
Curve Tools, Transforms, Surface Tools, Advanced Solids, Mastery.

Three question types, from easy to hard:

| Type | Card | Used from |
| --- | --- | --- |
| Pick the icon | "Which one of these is Sweep2?" + 4 icons | Level 1 |
| Name it | One big icon + 4 look-alike names | Level 3 |
| Solve the task | A modeling task in words + 4 icons, no names | Level 4 |

The placement test asks each level's first two questions, so it ramps from
pick-the-icon to scenarios and shows an EASY / MEDIUM / HARD marker.

## Debug in VS Code (Mac and Windows)

1. Install the recommended extensions (VS Code prompts you): **C#** and **C# Dev Kit**.
2. Open the `RhinoGotchi` folder, go to Run and Debug, pick a configuration, press F5:
   - **Rhino 9 - Mac**: builds `net7.0` and starts `/Applications/Rhino 9.app`.
     Rhino 9 runs on .NET 10 and loads the `net7.0` build as is. Quit any
     running Rhino 9 first, or the new one won't pick up `RHINO_PACKAGE_DIRS`.
   - **Rhino 8 (.NET 7+) - Mac / Windows**: builds `net7.0` and starts Rhino 8
     (`/Applications/Rhino 8.app` on Mac, `C:\Program Files\Rhino 8` on Windows).
   - **Rhino 8 (.NET Framework) - Windows**: builds `net48` and starts Rhino 8 with `/netfx`.
3. Type `RhinoGotchi` in Rhino. Breakpoints in the command hit.

Rhino finds the plugin through `RHINO_PACKAGE_DIRS`, which points at the Debug
build folder, so you don't install anything. Stop debugging (or quit Rhino)
before rebuilding, because Rhino locks the loaded `.rhp`.

If Rhino 8 is installed somewhere else, edit `program` in `.vscode/launch.json`.

## Debug in Visual Studio / Rider

`src/RhinoGotchi/Properties/launchSettings.json` has the same three profiles.
Pick one and press F5.

## Layout

```
RhinoGotchi.sln
src/RhinoGotchi/
  RhinoGotchi.csproj        multi-target net7.0;net48, outputs .rhp
  RhinoGotchiPlugin.cs      the PlugIn class (one per .rhp)
  RhinoGotchiCommand.cs     sample command: draw a line between two points
  Properties/AssemblyInfo.cs   plugin GUID + Plug-in Manager info
  Properties/launchSettings.json
.vscode/                    launch.json (F5 into Rhino), tasks.json (builds)
```

## Cross-platform notes

- Keep code to RhinoCommon and Eto.Forms for UI. Avoid WinForms/WPF and
  `System.Drawing` APIs that only exist on Windows.
- Use `#if NETFRAMEWORK` for the rare spot where net48 and net7.0 differ.
- Never change the assembly `Guid` in `AssemblyInfo.cs` or the command `Guid`
  after shipping.
- RhinoCommon is referenced at `8.0.23304.9001` (the first Rhino 8 SDK) with
  `ExcludeAssets="runtime"` so the plugin loads in every Rhino 8 release and
  never ships its own copy of RhinoCommon.
