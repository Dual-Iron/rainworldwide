## How to set up a server
0. Locate the `Rain World/` directory containing `RainWorld.exe`. To do this, right click on Rain World in the Steam library, click "Manage", and click "Browse local files"
1. Locate your repository clone, say `rainworldwide/`
2. Choose a location for your server, say `server/`
3. Copy the contents of `Rain World/` into `server/game/`
4. Build `rainworldwide/src/RainWorldwide.sln`

As a note, it's okay to have the `server/` directory inside the `Rain World/` directory. You can use the latest .NET version to build the project (using simply `dotnet build RainWorldwide.sln`), though you may need to install the .NET Framework 4.8 targeting pack too.

## TODO
- Direction from Dr. Card:
  - Design document
  - Decide when client gets authority over server, and why
  - Justify why player-player collision is disabled
  - How to handle grasps and paralysis: Should players stay stunned when grabbed while stunned?
- Trim PNGs, sound effects, and unneeded Unity assets from server

## Diff for SERVER-Assembly-CSharp.dll
- Modified `Options.OptionsFile_OnReadCompleted` to force-enable `rwremix` and `rain-worldwide-server`
- Modified `RainWorld.Awake()` to adjust save file path
- Added the `RainWorld.SavePath()` method
