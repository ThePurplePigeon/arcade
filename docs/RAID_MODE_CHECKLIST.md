# Arcade Dev Checklist (No In-Game Testing)

## Fast Local Gate
1. `dotnet restore .\Arcade.sln`
2. `dotnet build .\Arcade.sln -v minimal`
3. `dotnet test .\Arcade.sln -v minimal`

## Pre-Commit Janitorial Checks
1. Confirm no placeholder/template text remains in plugin UI/metadata.
2. Confirm command names and help text are accurate (`/arcade`, `/pmycommand`).
3. Confirm no unused assets are copied by the project file.
4. Confirm CI workflow still restores, builds, tests, and uploads artifacts.

## Data Health Checks
1. Run tests and verify `DataFileValidationTests` pass.
2. If adding Hangman words, avoid normalized duplicates.
3. If adding Sudoku puzzles, keep ids/givens unique and difficulty-tagged.

## When You Get Back In-Game (5-minute Smoke)
1. Load plugin, open with `/arcade`, verify no startup errors.
2. Open each module tab once: Minesweeper, Hangman, Sudoku.
3. Run one basic action per module:
   - Minesweeper: reveal tile, place/remove flag.
   - Hangman: make one guess and start next round.
   - Sudoku: select cell, toggle note mode, place value.
4. Open `Settings` and `Account Stats` windows.
5. Reload plugin once and confirm no exceptions in logs.
