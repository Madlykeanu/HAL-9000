# HAL-9000 for Kerbal Space Program

HAL-9000 is a v0.1 in-flight AI terminal for KSP. It provides a text chat window, sends messages to OpenRouter, and exposes one read-only tool, `get_ship_info`, so the model can answer using live vessel data.

The project uses an SDK-style `.csproj` with `KSPBuildTools`, similar to newer KSP mod repos. `dotnet build` resolves the installed KSP assemblies and stages the plugin into the repo's `GameData` folder.

## Build

From this folder:

```powershell
dotnet build -c Release
```

The build outputs:

```text
GameData\HAL-9000\Plugins\HAL9000.dll
```

`build.ps1` is just a thin wrapper around the same `dotnet build` command.

## Configure

Edit:

```text
GameData\HAL-9000\PluginData\settings.cfg
```

Set `OPENROUTER_API_KEY` before launching KSP. `OPENROUTER_MODEL` can be changed without rebuilding.

## Test Install

Copy the `GameData\HAL-9000` folder into KSP's `GameData` folder. In flight, press `F8` or use the visible HAL button to open the terminal.
