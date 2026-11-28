<#
.SYNOPSIS
Prepares an isolated rewrite of some files' comments, or copies a finished one back into the repository.

.DESCRIPTION
The workspace contains the C# code of Core and the game, with every whole-line comment removed, so that a session
can read the code that the files use without reading the prose of the repository, which it would imitate. Its
AGENTS.md contains only a guideline. The prompt for the session is written beside the workspace.

The process, which decision record 067 describes:
1. A session rewrites the files' comments in the workspace, given prompt.txt.
2. -Apply copies them into the repository, where a reviewer compares them with the code and the old comments, and
   writes review.md beside the workspace: notes on what is wrong, not new prose.
3. A session in the workspace fixes what review.md lists, and -Apply copies the files back again.

.EXAMPLE
.\experiments\comment-style\New-Rewrite.ps1 -Path src\BodyCheck.Core\Scripts -OutDir $env:TEMP\rewrite-scripts
.\experiments\comment-style\New-Rewrite.ps1 -OutDir $env:TEMP\rewrite-scripts -Apply
#>
param(
    # The files to rewrite, or directories whose .cs files to rewrite, relative to the repository.
    [string[]] $Path,
    [Parameter(Mandatory)] [string] $OutDir,
    [string] $Guideline = 'guideline-2.md',
    [switch] $Apply
)
$ErrorActionPreference = "Stop"
$repo = (Resolve-Path "${PSScriptRoot}\..\..").Path
$workspace = Join-Path $OutDir 'workspace'
$listPath = Join-Path $OutDir 'files.txt'

if ($Apply) {
    foreach ($file in Get-Content $listPath) {
        $text = [IO.File]::ReadAllText((Join-Path $workspace $file)) -replace "`r`n", "`n"
        [IO.File]::WriteAllText((Join-Path $repo $file), $text)
    }
    return
}

if (Test-Path $OutDir) { throw "$OutDir already exists." }
$files = @(git -C $repo ls-files -- ($Path | ForEach-Object { if ($_ -like '*.cs') { $_ } else { "$_/*.cs" } }))
if (-not $files) { throw "No C# files in $Path." }
New-Item -ItemType Directory -Force $workspace | Out-Null
[IO.File]::WriteAllText($listPath, ($files -join "`n") + "`n")

foreach ($file in git -C $repo ls-files -- 'src/BodyCheck.Core/*.cs' 'game/bodycheck/*.cs') {
    $target = Join-Path $workspace $file
    New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
    $lines = Get-Content (Join-Path $repo $file) | Where-Object { $_ -notmatch '^\s*//' }
    [IO.File]::WriteAllText($target, ($lines -join "`n") + "`n")
}
$body = (Get-Content (Join-Path $PSScriptRoot $Guideline) | Select-Object -Skip 1) -join "`n"
[IO.File]::WriteAllText((Join-Path $workspace 'AGENTS.md'), "# Writing comments and docs`n$body`n")

$fileList = ($files | ForEach-Object { "- $_" }) -join "`n"
$prompt = @"
The comments have been removed from every C# file in this workspace. Write the doc comments and inline comments that
these files should have:

$fileList

Read any other file you need, to understand how the code is used, but change only these files, and only their
comments. Keep the code exactly as it is. Work as AGENTS.md says.
"@
[IO.File]::WriteAllText((Join-Path $OutDir 'prompt.txt'), $prompt)
"$($files.Count) files to rewrite in $workspace"
