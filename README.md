## Design document
[This document](https://docs.google.com/document/d/e/2PACX-1vRcZ7R7M11ipMXaGYxjGvobF7zxPsWS1V6VeMHSnj0GeD_4NE6SoPITkrAWxF_1SsgdaSchAxIhWsTb/pub) includes many design considerations. It also contains current TODOs.

## How to set up a server on Windows
First, set up the server files.

1. Locate the `Rain World/` directory containing `RainWorld.exe`
    1. Right click on Rain World in the Steam library
    2. Click "Manage"
    3. Click "Browse local files"
2. Copy the contents of `Rain World/` into `Rain World/server/game/`

Then, build this project.

1. Set the `RainWorldDir` env var to point to the `Rain World/` directory
2. Clone this repository into some directory, say `rainworldwide/`
3. Build `rainworldwide/src/RainWorldwide.sln`
    1. If not already installed, install [the .NET Framework 4.8 targeting pack](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48)
    2. If not already installed, install [the latest version of .NET](https://dotnet.microsoft.com/en-us/download)
    3. Run `dotnet build RainWorldwide.sln` in the command line

After that, you can move or rename the `Rain World/server/` directory as you wish. Whenever you're ready, run the server using `server/startserver.bat`. All done!

## Edits to SERVER-Assembly-CSharp.dll
- Modified `Options.OptionsFile_OnReadCompleted` to force-enable `rwremix` and `rain-worldwide-server`
- Modified `RainWorld.Awake()` to adjust save file path
- Added the `RainWorld.SavePath()` method
