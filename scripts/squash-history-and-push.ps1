# Squash entire repo history into a single commit and force-push to origin main.
# Use this when you want GitHub to show only one commit (no change history).
#
# How it works:
#   Creates a temporary orphan branch (zero history), stages all current files,
#   commits everything as one snapshot, replaces main with that branch, then
#   force-pushes then updates the release tag so GitHub reflects a single commit.
#
# Usage:
#   .\scripts\squash-history-and-push.ps1                    # Default message "Initial release"
#   .\scripts\squash-history-and-push.ps1 -Message "v1.0.0"  # Custom commit message
#   .\scripts\squash-history-and-push.ps1 -Force             # Skip confirmation prompt
#
# WARNING: This rewrites remote history. Anyone who cloned must re-clone or reset.
# Releases/tags that pointed at old commits will no longer point at the new single commit.
# NOTE: Git only pushes files inside the repository worktree. Copilot repo memory
# (for example /memories/repo/rda-knowledge.md) lives in local editor storage and
# is NOT part of the git repo, so it will not be preserved by this script or by a
# re-clone. Keep durable research in committed files such as docs/rda-research/.

param(
    # The commit message for the single squashed commit.
    [string]$Message = "Initial release",

    # Skip the interactive confirmation prompt (useful for automation).
    [switch]$Force
)

# Any failed command throws immediately rather than silently continuing.
$ErrorActionPreference = "Stop"

# Source shared helpers
. (Join-Path $PSScriptRoot "build-common.ps1")

# Resolve repo root: when run via .\scripts\..., PSScriptRoot is the scripts/ folder,
# so go one level up. Fall back to the current directory if PSScriptRoot is unavailable.
$repoRoot = if ($PSScriptRoot) { (Resolve-Path "$PSScriptRoot/..").Path } else { (Get-Location).Path }

# Quick check that a .git folder exists before calling any git commands.
if (-not (Test-Path (Join-Path $repoRoot ".git"))) {
    Write-Host "[ERROR] Not a git repository: $repoRoot" -ForegroundColor Red
    exit 1
}

# Ensure .git is a real repo, not a stub (e.g. Cursor creates an incomplete .git
# with only its workspace file, which passes Test-Path but has no HEAD or config).
$gitCheck = $null
$revParseExit = -1
& { $ErrorActionPreference = 'SilentlyContinue'; $script:gitCheck = & git -C $repoRoot rev-parse --is-inside-work-tree 2>$null; $script:revParseExit = $LASTEXITCODE }
if ($revParseExit -ne 0 -or $gitCheck -ne "true") {
    Write-Host "[ERROR] Not a valid git repository: $repoRoot" -ForegroundColor Red
    Write-Host "  The .git folder exists but is incomplete (e.g. missing HEAD or config)." -ForegroundColor Gray
    Write-Host "  Run 'git init' here, or re-clone if this was a clone." -ForegroundColor Gray
    exit 1
}

# cd into the repo root for all subsequent git commands, then restore the original
# directory on exit (even if the script throws).
Push-Location $repoRoot
try {
    # Verify a remote named 'origin' exists; without it the final push has nowhere to go.
    $remote = git remote get-url origin 2>$null
    if (-not $remote) {
        Write-Host "[ERROR] No remote 'origin' configured." -ForegroundColor Red
        exit 1
    }

    # Show a summary of what is about to happen and ask the user to confirm,
    # unless -Force was passed to skip the prompt.
    if (-not $Force) {
        Write-Host "This will:" -ForegroundColor Yellow
        Write-Host "  1. Create a new branch with no history"
        Write-Host "  2. Add all current files and commit as: `"$Message`""
        Write-Host "  3. Replace 'main' with that single commit"
        $researchPath = Join-Path $repoRoot "docs\rda-research"
        if (Test-Path $researchPath) {
            Write-Host "  Durable RDA notes should live under docs/rda-research/ if you want them on GitHub." -ForegroundColor Gray
        }
        Write-Host "  4. Force-push to origin (all previous commits will disappear on GitHub)"
        Write-Host "  5. Push only repository files; Copilot /memories/repo notes are local-only" -ForegroundColor DarkYellow
        Write-Host ""
        $readmePath = Join-Path $repoRoot "README.md"
        if (Test-Path $readmePath) {
            $lineCount = (Get-Content $readmePath -Raw | Measure-Object -Line).Lines
            Write-Host "  README.md will be included ($lineCount lines). Save any open files first." -ForegroundColor Gray
        }
        Write-Host ""
        Write-Host "Remote: $remote" -ForegroundColor Cyan
        $confirm = Read-Host "Type 'yes' to continue"
        if ($confirm -ne "yes") {
            Write-Host "Aborted." -ForegroundColor Gray
            exit 0
        }
    }

    # STEP 1 -- Create an orphan branch.
    Write-Host "[1/6] Creating orphan branch (no history)..." -ForegroundColor Cyan
    Write-Status "GIT" "Creating orphan branch squash-temp..."
    git checkout --orphan squash-temp
    if ($LASTEXITCODE -ne 0) { throw "git checkout --orphan failed" }
    Write-Status "OK" "Switched to orphan branch squash-temp" "Green"

    # STEP 2 -- Stage everything.
    Write-Host "[2/6] Staging all files (excluding release zip)..." -ForegroundColor Cyan
    Write-Status "GIT" "Running git add -A..."
    git add -A
    git reset -- "Asset-Splitter-UI-v*.zip" 2>$null
    # Count staged files
    $stagedCount = (git diff --cached --name-only 2>$null | Measure-Object).Count
    Write-Status "OK" "Staged $stagedCount files" "Green"

    # Re-add README.md explicitly
    $readmePath = Join-Path $repoRoot "README.md"
    if (Test-Path $readmePath) {
        git add -f $readmePath
        $lines = (Get-Content $readmePath -Raw | Measure-Object -Line).Lines
        if ($lines -lt 20) {
            Write-Status "ERROR" "README.md has only $lines lines. Save the full README first." "Red"
            git checkout -- .
            git clean -fd
            git checkout main
            git branch -D squash-temp
            exit 1
        }
        Write-Status "INFO" "README.md included ($lines lines)" "DarkGreen"
    }
    git status --short | Out-Null

    # STEP 3 -- Create the single squashed commit.
    $prevCommitCount = & { $ErrorActionPreference = 'SilentlyContinue'; git rev-list --count HEAD 2>$null; if ($LASTEXITCODE -ne 0) { "0" } }
    Write-Host "[3/6] Committing as: $Message" -ForegroundColor Cyan
    Write-Status "GIT" "Creating single squashed commit (replacing $prevCommitCount previous commits)..."
    git commit -m "$Message"
    if ($LASTEXITCODE -ne 0) { throw "git commit failed" }
    $newSha = (git rev-parse --short HEAD)
    Write-Status "OK" "Committed as $newSha" "Green"

    # STEP 4 -- Replace main with the new single-commit branch.
    Write-Host "[4/6] Replacing main with new history..." -ForegroundColor Cyan
    Write-Status "GIT" "Deleting old main, renaming squash-temp -> main..."
    & { $ErrorActionPreference = 'SilentlyContinue'; git branch -D main 2>$null }
    git branch -m main
    Write-Status "OK" "main now points to $newSha" "Green"

    # STEP 5 -- Force-push to overwrite remote main.
    Write-Host "[5/6] Force-pushing to origin main..." -ForegroundColor Cyan
    Write-Status "GIT" "Pushing to origin main (force)..."
    $oldEA = $ErrorActionPreference; $ErrorActionPreference = "Continue"
    git push --force origin main 2>&1 | Out-Null
    $ErrorActionPreference = $oldEA
    Write-Status "OK" "Push command completed" "Green"

    # STEP 6 -- Update the release tag.
    $csprojPath = Join-Path $repoRoot "src\AssetSplitterUI\AssetSplitterUI.csproj"
    $tagName = "1.0.0"
    if (Test-Path $csprojPath) {
        $csprojContent = Get-Content $csprojPath -Raw
        if ($csprojContent -match '<Version>([^<]+)</Version>') {
            $tagName = $Matches[1].Trim()
        }
    }
    Write-Host "[6/6] Updating release tag..." -ForegroundColor Cyan
    Write-Status "TAG" "Deleting old remote tag $tagName..."
    & { $ErrorActionPreference = 'SilentlyContinue'; git push origin :refs/tags/$tagName 2>$null }
    Write-Status "TAG" "Creating tag $tagName -> $newSha..."
    git tag -f $tagName HEAD
    $oldEA = $ErrorActionPreference; $ErrorActionPreference = "Continue"
    $tagOutput = & git push origin $tagName 2>&1
    $ErrorActionPreference = $oldEA
    $tagExit = $LASTEXITCODE
    if ($tagOutput -match "\[new tag\]") { Write-Status "OK" "Tag $tagName -> $newSha" "Green" }
    if ($tagExit -ne 0) { throw "git push tag failed" }

    Write-Host ""
    Write-Status "DONE" "Single commit $newSha + tag $tagName pushed to GitHub" "Green"
}
finally {
    # Always restore the original working directory, even if the script failed mid-way.
    Pop-Location
}
