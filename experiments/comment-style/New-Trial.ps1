<#
.SYNOPSIS
Prepares one trial: a workspace with the test set's files stripped of comments, and the prompt for a session that
writes them again.

.DESCRIPTION
The workspace is outside the repository, so a session started in it has no memory, settings or instructions but those
the condition gives it:

- Isolated: only the test set's files.
- Repo: the repository at HEAD, as a session working in it would see it.
- RepoGuideline: as Repo, with guideline.md added to the workspace's AGENTS.md.

The workspace leaves out this directory and decision record 067, which describe the experiment. The prompt is written
to <OutDir>\<Name>\prompt.txt and the workspace to <OutDir>\<Name>\workspace.

With -Run, a fresh Claude Code session runs the trial, which needs the claude CLI to be logged in. Otherwise, start
the session yourself in the workspace, or give a subagent the prompt and the workspace's path.

.EXAMPLE
.\experiments\comment-style\New-Trial.ps1 -Condition Repo -Name repo-1 -OutDir $env:TEMP\trials -Run -Model opus
#>
param(
    [Parameter(Mandatory)] [ValidateSet('Isolated', 'Repo', 'RepoGuideline')] [string] $Condition,
    [Parameter(Mandatory)] [string] $Name,
    [Parameter(Mandatory)] [string] $OutDir,
    [switch] $Run,
    [string] $Model = 'opus'
)
$ErrorActionPreference = "Stop"
$repo = (Resolve-Path "${PSScriptRoot}\..\..").Path
$testSet = Get-Content "${PSScriptRoot}\test-set.txt" | Where-Object { $_ }

$trialDir = Join-Path $OutDir $Name
$workspace = Join-Path $trialDir 'workspace'
if (Test-Path $trialDir) { throw "$trialDir already exists." }
New-Item -ItemType Directory -Force $workspace | Out-Null

if ($Condition -eq 'Isolated') {
    foreach ($file in $testSet) {
        $target = Join-Path $workspace $file
        New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
        Copy-Item (Join-Path $repo $file) $target
    }
} else {
    $archive = Join-Path $trialDir 'repo.tar'
    git -C $repo archive --output $archive HEAD
    tar -xf $archive -C $workspace
    Remove-Item $archive
    Remove-Item -Recurse -Force (Join-Path $workspace 'experiments') -ErrorAction SilentlyContinue
    Get-ChildItem (Join-Path $workspace 'docs\decision-records') -Filter '067-*' | Remove-Item
    if ($Condition -eq 'RepoGuideline') {
        $guideline = (Get-Content "${PSScriptRoot}\guideline.md" | Select-Object -Skip 1) -join "`n"
        Add-Content (Join-Path $workspace 'AGENTS.md') "`n## Writing comments and docs`n$guideline"
    }
}

# Only whole-line comments: the test set's files have no trailing or block comments.
foreach ($file in $testSet) {
    $path = Join-Path $workspace $file
    $lines = Get-Content $path | Where-Object { $_ -notmatch '^\s*//' }
    # LF, as .gitattributes asks, which Set-Content wouldn't write on Windows.
    [IO.File]::WriteAllText($path, ($lines -join "`n") + "`n")
}

$fileList = ($testSet | ForEach-Object { "- $_" }) -join "`n"
$prompt = @"
The comments have been removed from these C# files:

$fileList

Add the doc comments and inline comments that this code should have, as if you had written the code yourself. Edit the
files in place, and change only comments.
"@
if ($Condition -ne 'Isolated') {
    $prompt += "`n`nThis is the project's repository. Work as its AGENTS.md says."
}
Set-Content (Join-Path $trialDir 'prompt.txt') $prompt

if ($Run) {
    Push-Location $workspace
    try {
        claude -p $prompt --model $Model --permission-mode acceptEdits --allowedTools 'Read,Glob,Grep,Edit,Write' `
            --output-format json > (Join-Path $trialDir 'session.json')
    } finally {
        Pop-Location
    }
}
