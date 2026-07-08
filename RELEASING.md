# Releasing to the Steam Workshop

Updates ship via `release.ps1` — it builds the assembly, stages a clean copy of the
mod (no `.git`, no `Source/`, no dev files), and pushes it to Workshop item
[3679943662](https://steamcommunity.com/sharedfiles/filedetails/?id=3679943662)
with a changelog, using SteamCMD.

## One-time setup

1. **Install SteamCMD** (a standalone command-line Steam client from Valve) and note
   its path.

2. **Log in once, interactively** — in PowerShell:

   ```powershell
   & "path\to\steamcmd.exe" +login YOUR_STEAM_USERNAME
   ```

   Enter your password and Steam Guard code when prompted, then type `quit`.
   SteamCMD caches the session locally; future runs are non-interactive.
   (If uploads ever fail with a login error, just repeat this step.)

3. **Create `release.local.json`** — copy `release.local.example.json` and fill in
   your Steam username and SteamCMD path. This file is gitignored: your username
   never enters the repository.

## Releasing

```powershell
.\release.ps1 -ChangeNote "v1.0.13: fixed the thing"
# or, for a longer changelog written in a file:
.\release.ps1 -ChangeNoteFile .\changenote.txt
```

The changenote lands on the Workshop item's Change Notes tab. Multi-line notes and
Steam BBCode (`[h1]`, `[b]`, `[list]`...) both work.

Flags: `-DryRun` does everything except the upload (inspect `.release-staging/` and
the generated `workshop_item.vdf`), `-SkipBuild` reuses the existing DLL.

The script only pushes content + changenote — the Workshop page's title,
description, preview and visibility are left untouched.

## Checklist

1. Test the change in-game
2. Update `PatchNotes.md` (the changenote usually summarizes its newest section)
3. Commit and push to GitHub
4. `.\release.ps1 -ChangeNoteFile ...`
5. Verify the [Change Notes page](https://steamcommunity.com/sharedfiles/filedetails/changelog/3679943662)
