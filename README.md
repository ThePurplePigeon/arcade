# Arcade

Arcade is a Dalamud plugin that bundles several mini-games into one window:

- Minesweeper
- Hangman
- Sudoku

The plugin command is `/arcade`.

## Requirements

- XIVLauncher, FINAL FANTASY XIV, and Dalamud installed and working
- Game launched at least once with Dalamud enabled
- .NET SDK 10.0 or newer
- Optional: `DALAMUD_HOME` set if your Dalamud dev path is custom

## Build

From the repository root:

```powershell
dotnet restore .\Arcade.sln
dotnet build .\Arcade.sln -v minimal
dotnet test .\Arcade.sln -v minimal
```

The plugin output is at:

- `Arcade\bin\x64\Debug\Arcade.dll` for Debug builds
- `Arcade\bin\x64\Release\Arcade.dll` for Release builds

## Enable In Game

1. Open Dalamud settings with `/xlsettings`.
2. Go to `Experimental` and add the full path to `Arcade.dll` under Dev Plugin Locations.
3. Open the plugin installer with `/xlplugins`.
4. In `Dev Tools > Installed Dev Plugins`, enable `Arcade`.
5. Use `/arcade` to open the plugin window.

## Repo Layout

- `Arcade/`: plugin runtime code (windows, modules, game logic)
- `Arcade.Tests/`: automated tests
- `Data/`: data files used by Hangman and Sudoku providers

## Notes For Contributors

- Keep data files clean and conflict free. `DataFileValidationTests` enforces this.
- If you add words or puzzles, run `dotnet test .\Arcade.sln -v minimal` before committing.
- CI runs restore, build, and test on pull requests via `.github/workflows/pr-build.yml`.

## References

- Dalamud docs: https://dalamud.dev
- Dalamud plugin submission docs: https://dalamud.dev/plugin-publishing/submission
