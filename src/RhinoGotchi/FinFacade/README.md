# FinGotchi

A RhinoGotchi game for Rhino 8 (Windows and Mac): **Fin Facade**. Six levels, each
teaching one Rhino command, build a box wrapped in rippling fins, light it with the
sun and give the fins a finish. A pixel rhino pet cheers you on, and dances when
you're done.

In this repository the plugin lives in `FinGotchi/` (the project and plugin are named
`GotchiFins`). Open `FinGotchi/GotchiFins.sln`, or the `FinGotchi` folder in VS Code.

| Level | You build | Command you learn |
| --- | --- | --- |
| 1 · Mass | a 24 × 10 × 8 box | `Box` |
| 2 · Profile | the fin's zig-zag outline, seen side-on | `Polyline` (closed) |
| 3 · Fin | the profile extruded 0.3 thick | `ExtrudeCrv` (Solid) |
| 4 · Array | 68 fins around the mass | `ArrayCrv` (Roadlike, Top) |
| 5 · Sun | shadows on the fins, seen from around the building | `Sun`, then orbit |
| 6 · Finish | birch wood, matte aluminium or painted blue, texture box 2 × 2 × 2 | `ApplyBoxMapping` |

## How it plays

- **Three ways to run each command:** type it, click its icon in the **RhinoGotchi
  toolbar**, or press **Run** (or the icon) in the panel. Run and the toolbar start the
  level properly: in level 3 they pick your profile and set the extrude direction, in
  level 4 they pick your fin.
- **Sidebar panel** (`GotchiFins`): title, six level tiles, a looping demo of the
  current step, a lesson card with the command, a yellow Run button, the steps (lit
  up in time with the demo), "ways to do it", and your six command icons.
- **Viewport** (the Axon view, in the *GotchiFins Rendered* display mode: white
  background, black edges, a white shadow-only ground):
  - top left: the lesson card · top right: the level check (pass/fail per check)
  - top centre: a yellow callout saying what to type
  - yellow numbered click points with arrows and labels, plus per-level marks
  - bottom left: the rhino pet with a speech bubble · bottom centre: recorded steps
- **The pet** reacts to what you do:
  - **winning** rhino when a level is solved
  - **standby** rhino (Z's) after 5 seconds without a click or key
  - **losing** rhino (ghost) after 3 misses on a level; a miss is a command that
    changed the model but didn't solve the level
  - **final** rhino when all six levels are solved without a loss (a perfect run)
  - then the **winning dance**: hops, a turn-around and a spinning jump under falling
    confetti, looping on the end page and in the viewport
- Level 5 switches the Axon view to perspective; turn the Sun on and right-drag to
  orbit at least 40°.
- Level 6: pick a finish in the panel (or select the fins, Properties → Material; the
  game adds the three "GotchiFins …" materials), then `ApplyBoxMapping` on all the fins
  with a 2 × 2 × 2 box, which sets how big the texture is. Run selects the fins for you.

## Use it

1. Build (below), or drag a released `.rhp` onto Rhino 8. Keep `GotchiFins.rui` next
   to the `.rhp`; Rhino loads the RhinoGotchi toolbar from it.
2. Type `GotchiFins` to open the panel, then press **Start**
   (or type `GotchiFinsStart`).

Commands: `GotchiFins` (panel), `GotchiFinsStart`, `GotchiFinsRun <command>` (what the
toolbar buttons call).

## Fonts

Zilla Slab, Libre Franklin and JetBrains Mono when installed (all free from Google
Fonts), otherwise Georgia / Segoe UI / Consolas or the system fonts.

## Build

```bash
dotnet build GotchiFins.sln -c Release
```

Close Rhino first: a running Rhino locks the `.rhp`. Debugging from VS Code works the
same way as GotchiHouse: pick a configuration in Run and Debug and press F5.

## Files

| File | What it is |
| --- | --- |
| `FinSession.cs` | game state: progress, checks, misses, the pet's mood, display mode, ground |
| `FinGhostConduit.cs` | everything drawn in the viewport |
| `PuzzleBoard.cs` | the sidebar panel and its demo animations |
| `Cards.cs`, `Hud.cs` | the cards (shared by panel and viewport) and their viewport bitmaps |
| `Pets.cs`, `Resources/Pets/` | the rhino pet: picture layers and their animations |
| `Icons.cs`, `GotchiFins.rui` | command icons and the RhinoGotchi toolbar built from them |
| `FinModel.cs`, `GamePage.cs` | the facade's geometry and the level texts |
