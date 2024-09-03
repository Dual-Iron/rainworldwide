## Diff for SERVER-Assembly-CSharp.dll
- Modified `Options.OptionsFile_OnReadCompleted` to force-enable `rwremix` and `rain-worldwide-server`
- Modified `RainWorld.Awake()` to adjust save file path
- Added the `RainWorld.SavePath()` method

## How to set up a server
0. Locate the `Rain World/` directory containing `RainWorld.exe`. To do this, right click on Rain World in the Steam library, click "Manage", and click "Browse local files"
1. Locate your repository clone, say `rainworldwide/`
2. Choose a location for your server, say `server/`
3. Copy the contents of `Rain World/` into `server/game/`
4. Copy the contents of `rainworldwide/assets/game/` into `server/game/`
5. Copy `rainworldwide/assets/startserver.bat` into `server/`

As a note, it's okay to have the `server/` directory inside the `Rain World/` directory.
