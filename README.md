# RhinoGotchi

A RhinoCommon plugin for **Rhino 8 on Windows and Mac**, based on McNeel's
[Your First Plugin](https://developer.rhino3d.com/guides/rhinocommon/your-first-plugin-windows/) guide.

One project builds two targets:

| Target | Runs in |
| --- | --- |
| `net7.0` | Rhino 8 Mac, Rhino 8 Windows (default .NET Core runtime) |
| `net48` | Rhino 8 Windows started with `SetDotNetRuntime` = .NET Framework |

## Requirements

- .NET SDK 7 or newer (`dotnet --list-sdks`)
- Rhino 8

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

## Debug in VS Code (Mac and Windows)

1. Install the recommended extensions (VS Code prompts you): **C#** and **C# Dev Kit**.
2. Open the `RhinoGotchi` folder, go to Run and Debug, pick a configuration, press F5:
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

## RhinoOnTheStreet tutorial

Type `RhinoOnTheStreet` in Rhino. A floating Perspective view pops out over the
viewports, the camera flies down onto a ghosted street that ends at an archway,
and two lessons follow, played with Rhino's real commands:

1. **Curve**: type `Curve`, click the five coloured rings, press Enter.
2. **Arch**: `Line` for the left pillar, Enter to repeat Line for the right one,
   then `Arc`, `S` (Start option), and click start, end and crown.

Targets are colour-coded red → green → blue (the order of Rhino's X, Y, Z axes).
The rhino dances when a lesson is complete, dozes after 5 s without a click,
falls over when a lesson is incomplete (a missed ring, Enter too early, Esc),
and does its final dance if the whole run had zero misses. A finale walks it
through the arch into a blue horizon.

While it runs, Osnap is on with Point snap, and locked target points sit on a
`RhinoOnTheStreet` layer. Run `RhinoOnTheStreet` again for SkipIntro,
RestartLesson, ReplayFinale, RestartTutorial or Quit. Quit restores your Osnap
settings, removes the targets and closes the view; the street and arch stay.

A browser preview of the same tutorial is in `tutorial/RhinoOnTheStreet.html`.

## Layout

```
RhinoGotchi.sln
src/RhinoGotchi/
  RhinoGotchi.csproj        multi-target net7.0;net48, outputs .rhp
  RhinoGotchiPlugin.cs      the PlugIn class (one per .rhp)
  RhinoGotchiCommand.cs     sample command: draw a line between two points
  RhinoOnTheStreetCommand.cs   starts / controls the RhinoOnTheStreet tutorial
  Tutorials/RhinoOnTheStreet/  scene geometry, lessons, tutorial flow, display conduit
  Assets/RhinoOnTheStreet/     rhino sprites, copied next to the .rhp
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
