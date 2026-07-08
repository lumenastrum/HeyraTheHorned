# release.ps1 — build, stage, and push Heyra the Horned to the Steam Workshop.
#
#   .\release.ps1 -ChangeNote "v1.0.12: fixed claws on non-English clients"
#   .\release.ps1 -ChangeNoteFile .\changenote.txt
#   .\release.ps1 -ChangeNote "..." -DryRun     # everything except the upload
#
# One-time setup: see RELEASING.md (steamcmd login + release.local.json).
# Uploads use steamcmd's cached credentials — no password ever stored here.

param(
    [string]$ChangeNote,
    [string]$ChangeNoteFile,
    [switch]$DryRun,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repo    = $PSScriptRoot
$staging = Join-Path $repo '.release-staging'
$vdfPath = Join-Path $repo 'workshop_item.vdf'

# ── Config ──────────────────────────────────────────────────────────
$configPath = Join-Path $repo 'release.local.json'
if (-not (Test-Path $configPath)) {
    Write-Error "release.local.json not found. Copy release.local.example.json to release.local.json and fill in your Steam username. See RELEASING.md."
}
$config = Get-Content $configPath -Raw | ConvertFrom-Json
if (-not (Test-Path $config.steamcmd)) {
    Write-Error "steamcmd not found at '$($config.steamcmd)' — fix the path in release.local.json."
}

# ── Changenote ──────────────────────────────────────────────────────
if ($ChangeNoteFile) {
    if (-not (Test-Path $ChangeNoteFile)) { Write-Error "Changenote file not found: $ChangeNoteFile" }
    $ChangeNote = Get-Content $ChangeNoteFile -Raw
}
if (-not $ChangeNote) {
    Write-Error "Provide -ChangeNote or -ChangeNoteFile. The Workshop deserves a changelog."
}

# ── Build ───────────────────────────────────────────────────────────
$dll = Join-Path $repo '1.6\Assemblies\Heyra.dll'
if (-not $SkipBuild) {
    $before = (Get-Item $dll).LastWriteTime
    dotnet build (Join-Path $repo 'Source\Heyra.csproj') --configuration Release
    if ($LASTEXITCODE -ne 0) { Write-Error "Build failed — aborting release." }
    $after = (Get-Item $dll).LastWriteTime
    if ($after -le $before) { Write-Error "Build succeeded but $dll was not refreshed — check the csproj CopyToMod target." }
    Write-Host "Build OK — Heyra.dll refreshed ($after)" -ForegroundColor Green
}

# ── Stage a clean copy (mod content only, no dev files) ─────────────
robocopy $repo $staging /MIR /NFL /NDL /NJH /NJS `
    /XD .git .vs Source .release-staging `
    /XF README.md .gitignore release.ps1 release.local.json release.local.example.json RELEASING.md workshop_item.vdf changenote.txt | Out-Null
if ($LASTEXITCODE -ge 8) { Write-Error "robocopy staging failed with exit code $LASTEXITCODE" }

$fileCount = (Get-ChildItem $staging -Recurse -File).Count
$sizeMB    = [math]::Round(((Get-ChildItem $staging -Recurse -File | Measure-Object Length -Sum).Sum / 1MB), 1)
Write-Host "Staged $fileCount files ($sizeMB MB) → $staging" -ForegroundColor Green

# ── Generate VDF ────────────────────────────────────────────────────
# Only content + changenote are set; title/description/visibility on the
# Workshop page stay untouched.
# Regex replacement strings don't process backslash escapes: '\\' here is the
# two-character output for each single input backslash, as VDF expects.
$esc = { param($s) $s -replace '\\', '\\' -replace '"', '\"' }
$vdf = @"
"workshopitem"
{
    "appid"           "294100"
    "publishedfileid" "3679943662"
    "contentfolder"   "$(& $esc $staging)"
    "previewfile"     "$(& $esc (Join-Path $staging 'About\Preview.png'))"
    "changenote"      "$(& $esc $ChangeNote)"
}
"@
Set-Content -Path $vdfPath -Value $vdf -Encoding UTF8
Write-Host "VDF written → $vdfPath"

if ($DryRun) {
    Write-Host "`n-- DryRun: skipping upload. VDF contents --" -ForegroundColor Yellow
    Write-Host $vdf
    exit 0
}

# ── Upload ──────────────────────────────────────────────────────────
if (-not $config.steamUser -or $config.steamUser -eq 'YOUR_STEAM_USERNAME') {
    Write-Error "Set steamUser in release.local.json (see RELEASING.md) before a real release."
}
Write-Host "`nPushing to Steam Workshop as $($config.steamUser)..." -ForegroundColor Cyan
$output = & $config.steamcmd +login $config.steamUser +workshop_build_item $vdfPath +quit 2>&1
$output | ForEach-Object { Write-Host $_ }

if ($output -match 'Success') {
    Write-Host "`n✔ Workshop item updated!" -ForegroundColor Green
    Write-Host "  Page:        https://steamcommunity.com/sharedfiles/filedetails/?id=3679943662"
    Write-Host "  Change notes: https://steamcommunity.com/sharedfiles/filedetails/changelog/3679943662"
} elseif ($output -match 'Cached credentials not found|password') {
    Write-Error "steamcmd needs a fresh login. Run the one-time login from RELEASING.md, then re-run this script."
} else {
    Write-Error "Upload did not report success — read the steamcmd output above."
}
