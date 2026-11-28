<#
.SYNOPSIS
Prepares an isolated rewrite of some files' comments, or of a doc, or copies a finished rewrite back into the
repository.

.DESCRIPTION
The workspace contains the C# code of Core and the game, with every whole-line comment removed, so that a session can
read the code without reading the repository's old prose, which it would imitate. Its AGENTS.md contains only the
guideline. The prompt for the session is written beside the workspace, in prompt.txt. SKILL.md gives the whole
process.

With -Path, the session writes the comments of those C# files.

With -Doc, the session writes that doc from facts.md, which a reviewer extracts from the old doc into the workspace.
The workspace keeps the comments of the C# files under the doc's directory, and copies any docs in its
subdirectories, since those are rewritten first. It also contains format.md, the rules for the doc's format.

-Apply copies the rewritten files back into the repository.

.EXAMPLE
.\.agents\skills\rewrite-prose\New-Rewrite.ps1 -Path src\BodyCheck.Core\Scripts -OutDir $env:TEMP\rewrite-scripts
.\.agents\skills\rewrite-prose\New-Rewrite.ps1 -Doc src\BodyCheck.Core\Scripts\architecture.md -OutDir $env:TEMP\doc
.\.agents\skills\rewrite-prose\New-Rewrite.ps1 -OutDir $env:TEMP\rewrite-scripts -Apply
#>
param(
    # The C# files to rewrite the comments of, or directories whose .cs files to rewrite, relative to the repository.
    [string[]] $Path,
    # The doc to rewrite, relative to the repository.
    [string] $Doc,
    [Parameter(Mandatory)] [string] $OutDir,
    [switch] $Apply
)
$ErrorActionPreference = "Stop"
$repo = (Resolve-Path "${PSScriptRoot}\..\..\..").Path
$workspace = Join-Path $OutDir 'workspace'
$listPath = Join-Path $OutDir 'files.txt'

if ($Apply) {
    # The code without its comments, blank lines or trailing spaces, so that only a change to the code differs.
    function Get-Code([string[]] $lines) {
        ($lines | Where-Object { $_ -notmatch '^\s*//' } | ForEach-Object { $_.TrimEnd() } | Where-Object { $_ }) -join "`n"
    }
    foreach ($file in Get-Content $listPath) {
        $text = [IO.File]::ReadAllText((Join-Path $workspace $file)) -replace "`r`n", "`n"
        $target = Join-Path $repo $file
        if ($file -like '*.cs' -and (Get-Code ($text -split "`n")) -ne (Get-Code (Get-Content $target))) {
            Write-Warning "The rewrite changed code in $file, not only comments."
        }
        [IO.File]::WriteAllText($target, $text)
    }
    return
}

if ([bool] $Path -eq [bool] $Doc) { throw 'Give either -Path or -Doc.' }
if (Test-Path $OutDir) { throw "$OutDir already exists." }
New-Item -ItemType Directory -Force $workspace | Out-Null

# Repository paths use forward slashes, as git prints them.
function ConvertTo-RepoPath([string] $path) { $path -replace '\\', '/' -replace '^\./', '' }

if ($Path) {
    $files = @(git -C $repo ls-files -- ($Path | ForEach-Object { if ($_ -like '*.cs') { $_ } else { "$_/*.cs" } }))
    if (-not $files) { throw "No C# files in $Path." }
    $keepComments = @()
} else {
    $files = @(ConvertTo-RepoPath $Doc)
    $docDir = Split-Path $files[0]
    $keepComments = @(git -C $repo ls-files -- "$docDir/*.cs")
    foreach ($child in git -C $repo ls-files -- "$docDir/*/architecture.md") {
        $target = Join-Path $workspace $child
        New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
        Copy-Item (Join-Path $repo $child) $target
    }
}
[IO.File]::WriteAllText($listPath, ($files -join "`n") + "`n")

foreach ($file in git -C $repo ls-files -- 'src/BodyCheck.Core/*.cs' 'game/bodycheck/*.cs') {
    $target = Join-Path $workspace $file
    New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
    $lines = Get-Content (Join-Path $repo $file)
    if ($file -notin $keepComments) { $lines = $lines | Where-Object { $_ -notmatch '^\s*//' } }
    [IO.File]::WriteAllText($target, ($lines -join "`n") + "`n")
}
$guideline = Get-Content (Join-Path $PSScriptRoot 'guideline.md') -Raw
[IO.File]::WriteAllText((Join-Path $workspace 'AGENTS.md'), $guideline)

if ($Path) {
    $fileList = ($files | ForEach-Object { "- $_" }) -join "`n"
    $prompt = @"
The comments have been removed from every C# file in this workspace. Write the doc comments and inline comments that
these files should have:

$fileList

Read any other file you need, to understand how the code is used, but change only these files, and only their
comments. Keep the code exactly as it is. Work as AGENTS.md says.
"@
} else {
    # The rules for the doc's format: the architecture docs' sections and the design doc format they extend.
    $rootRules = (Get-Content (Join-Path $repo 'AGENTS.md') -Raw) -replace '(?s)^.*?(## Architecture docs.*?)(\n## .*)?$', '$1'
    $designRules = (Get-Content (Join-Path $repo 'docs\designs\AGENTS.md') -Raw) -replace '(?s)^.*?(## Design doc format.*)$', '$1'
    [IO.File]::WriteAllText((Join-Path $workspace 'format.md'), "# The doc's format`n`n$rootRules`n`n$designRules")
    $prompt = @"
Write $($files[0]), a doc for the code in its directory. facts.md lists everything it must say, with the citations
and links to keep beside each fact. format.md gives the doc's sections and format. Its links are relative to the
doc's own directory, as facts.md gives them. Read the code in the directory, and any other code you need, to
understand the facts, but don't add facts that facts.md doesn't give, except what the code plainly shows. Work as
AGENTS.md says.

facts.md is your source, not a draft: don't copy its bullets, one to a line. Write the doc to explain the design to
someone about to work on this code. Group related facts, and connect them, in short paragraphs, using a list only
where the items are parallel, such as one entry per class. Each fact should appear once, where a reader would look
for it.

If the directory has no subdirectories, the doc has no "Its parts and how they relate" section. After "What it is"
and "What it is not for", organize the rest under headings by topic, such as one for each group of classes that work
together, and end with "Open questions" if facts.md has any.
"@
}
[IO.File]::WriteAllText((Join-Path $OutDir 'prompt.txt'), $prompt)
"$($files.Count) file(s) to rewrite in $workspace"
