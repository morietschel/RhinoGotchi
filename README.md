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

## Debug

`src/RhinoGotchi/Properties/launchSettings.json` has profiles that start Rhino 8
on Windows (.NET 7+ or .NET Framework) and on Mac with the build output on
`RHINO_PACKAGE_DIRS`. In Visual Studio / Rider pick a profile and press F5.

## Layout

```
RhinoGotchi.sln
src/RhinoGotchi/
  RhinoGotchi.csproj        multi-target net7.0;net48, outputs .rhp
  RhinoGotchiPlugin.cs      the PlugIn class (one per .rhp)
  RhinoGotchiCommand.cs     sample command: draw a line between two points
  Properties/AssemblyInfo.cs   plugin GUID + Plug-in Manager info
  Properties/launchSettings.json
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
