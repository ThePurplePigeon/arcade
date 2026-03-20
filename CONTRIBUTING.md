# Contributing to Arcade

Thanks for contributing to Arcade.

## Branching Model

- `master` is the stable branch and should stay releasable.
- Create short-lived branches for work:
  - `feat/<short-name>`
  - `fix/<short-name>`
  - `chore/<short-name>`
  - `docs/<short-name>`
- Open a pull request to merge into `master`.

## Local Validation

Run these commands before opening a PR:

```powershell
dotnet restore .\Arcade.sln
dotnet build .\Arcade.sln -v minimal
dotnet test .\Arcade.sln -v minimal
```

## Coding Expectations

- Keep gameplay UI mouse-first and avoid adding keyboard-dependent interactions.
- Preserve existing style and keep changes focused.
- Add or update tests when behavior changes.

## Custom Repo Release Flow

To prepare a custom-repo release package and metadata:

```powershell
.\scripts\Publish-CustomRepo.ps1
```

This script restores, builds Release, runs tests, rebuilds `dist/Arcade.zip`, and syncs `repo.json` metadata.
