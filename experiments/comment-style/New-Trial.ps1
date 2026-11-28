<#
.SYNOPSIS
Prepares one trial: a workspace with a test set's files stripped of comments, and the prompt for a session that
writes them again.

.DESCRIPTION
The workspace is outside the repository, so a session started in it has no memory, settings or instructions but those
the condition gives it:

- Isolated: only the test set's files.
- Repo: the repository at HEAD, as a session working in it would see it.

With -Guideline, the guideline is added to the workspace's AGENTS.md, which an Isolated workspace otherwise lacks, and
the prompt asks the session to follow AGENTS.md.

The workspace leaves out this directory and decision record 067, which describe the experiment. The prompt and the
test set are written to <OutDir>\<Name>, beside the workspace, for Measure-Trials.ps1.

With -Run, a fresh Claude Code session runs the trial, which needs the claude CLI to be logged in. Otherwise, start
the session yourself in the workspace, or give a subagent the prompt and the workspace's path.

.EXAMPLE
.\experiments\comment-style\New-Trial.ps1 -Condition Repo -TestSet test-set-1.txt -Guideline guideline-1.md `
    -Name repo-1 -OutDir $env:TEMP\trials -Run -Model opus
#>
param(
    [Parameter(Mandatory)] [ValidateSet('Isolated', 'Repo')] [string] $Condition,
    # A file in this directory that lists the files to strip, one per line.
    [Parameter(Mandatory)] [string] $TestSet,
    # A Markdown file in this directory whose body, after its title, is added to AGENTS.md.
    [string] $Guideline,
    [Parameter(Mandatory)] [string] $Name,
    [Parameter(Mandatory)] [string] $OutDir,
    [switch] $Run,
    [string] $Model = 'opus'
)
$ErrorActionPreference = "Stop"
$repo = (Resolve-Path "${PSScriptRoot}\..\..").Path
$files = Get-Content (Join-Path $PSScriptRoot $TestSet) | Where-Object { $_ }

$trialDir = Join-Path $OutDir $Name
$workspace = Join-Path $trialDir 'workspace'
if (Test-Path $trialDir) { throw "$trialDir already exists." }
New-Item -ItemType Directory -Force $workspace | Out-Null
Set-Content (Join-Path $trialDir 'test-set.txt') $files

if ($Condition -eq 'Isolated') {
    foreach ($file in $files) {
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
}

if ($Guideline) {
    $body = (Get-Content (Join-Path $PSScriptRoot $Guideline) | Select-Object -Skip 1) -join "`n"
    Add-Content (Join-Path $workspace 'AGENTS.md') "`n## Writing comments and docs`n$body"
}

# Only whole-line comments: the test sets' files have no trailing or block comments.
foreach ($file in $files) {
    $path = Join-Path $workspace $file
    $lines = Get-Content $path | Where-Object { $_ -notmatch '^\s*//' }
    # LF, as .gitattributes asks, which Set-Content wouldn't write on Windows.
    [IO.File]::WriteAllText($path, ($lines -join "`n") + "`n")
}

$fileList = ($files | ForEach-Object { "- $_" }) -join "`n"
$prompt = @"
The comments have been removed from these C# files:

$fileList

Add the doc comments and inline comments that this code should have, as if you had written the code yourself. Edit the
files in place, and change only comments.
"@
if ($Condition -eq 'Repo') {
    $prompt += "`n`nThis is the project's repository. Work as its AGENTS.md says."
} elseif ($Guideline) {
    $prompt += "`n`nWork as AGENTS.md says."
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
