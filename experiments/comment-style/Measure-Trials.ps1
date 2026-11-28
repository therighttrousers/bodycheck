<#
.SYNOPSIS
Scores the trials that New-Trial.ps1 prepared, one row per trial, after checking that each changed only comments.

.EXAMPLE
.\experiments\comment-style\Measure-Trials.ps1 (Get-ChildItem $env:TEMP\trials -Directory)
#>
param(
    [Parameter(Mandatory, ValueFromRemainingArguments)]
    [string[]] $Trial
)
$ErrorActionPreference = "Stop"
$repo = (Resolve-Path "${PSScriptRoot}\..\..").Path

# The code without its comments, blank lines or trailing spaces, so that only a change to the code differs.
function Get-Code([string] $path) {
    (Get-Content $path | Where-Object { $_ -notmatch '^\s*//' } | ForEach-Object { $_.TrimEnd() } |
        Where-Object { $_ }) -join "`n"
}

foreach ($dir in $Trial) {
    $testSet = Get-Content (Join-Path $dir 'test-set.txt') | Where-Object { $_ }
    $files = $testSet | ForEach-Object { Join-Path $dir "workspace\$_" }
    $changed = @($testSet | Where-Object { (Get-Code (Join-Path $dir "workspace\$_")) -ne (Get-Code (Join-Path $repo $_)) })
    $row = & "${PSScriptRoot}\Measure-CommentStyle.ps1" $files
    $row.Name = Split-Path $dir -Leaf
    $row | Add-Member FilesWithCodeChanged ($changed -join ' ')
    $row
}
