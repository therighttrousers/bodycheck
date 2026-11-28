<#
.SYNOPSIS
Writes a blind sheet of pairwise choices between two trials' versions of the same comment, and its key.

.DESCRIPTION
Comments are matched across versions by the line of code that follows them, since the code is the same in every
version. Each pair shows the two comments in a random order, with the code they describe. A reader chooses the clearer
one for each pair, which is easier than rating whole files.

Each comparison is "<left>|<right>|<count>", where <left> and <right> are directories that contain the commented files,
either a trial's workspace or a saved trial in a pilot's directory, and <count> is how many pairs to sample.

.EXAMPLE
.\experiments\comment-style\New-PairSheet.ps1 -TestSet test-set-2.txt -OutDir .\experiments\comment-style\pilot-2 `
    -Comparison 'pilot-2\iso-g2-opus-1|pilot-1\isolated-opus-1|10'
#>
param(
    [Parameter(Mandatory)] [string] $TestSet,
    [Parameter(Mandatory)] [string[]] $Comparison,
    [Parameter(Mandatory)] [string] $OutDir,
    # Lines of code to show after each comment.
    [int] $ContextLines = 4
)
$ErrorActionPreference = "Stop"
$files = Get-Content (Join-Path $PSScriptRoot $TestSet) | Where-Object { $_ }

# The commented file in a trial: either at its path in a workspace or saved flat as <name>.txt.
function Get-TrialFile([string] $dir, [string] $file) {
    $nested = Join-Path $dir $file
    if (Test-Path $nested) { return $nested }
    Join-Path $dir "$(Split-Path $file -Leaf).txt"
}

# Each comment block in a file, keyed by the file, the code line after it, and how many times that line came before.
function Get-Comments([string] $path, [string] $file) {
    $lines = @(Get-Content $path)
    $comments = @{}
    $seen = @{}
    $block = [Collections.Generic.List[string]]::new()
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line -match '^\s*//') { $block.Add($line); continue }
        if (-not $line.Trim()) { continue }
        $code = $line.Trim()
        $seen[$code] = 1 + ($seen[$code] ?? 0)
        if ($block.Count) {
            # The code that follows, without the next comment, which would belong to another pair.
            $context = (@($lines[$i..($lines.Count - 1)] | Where-Object { $_ -notmatch '^\s*//' }) |
                Select-Object -First $ContextLines) -join "`n"
            $comments["$file`n$code`n$($seen[$code])"] = [pscustomobject] @{
                File = $file; Line = $i + 1; Comment = ($block -join "`n"); Context = $context
            }
            $block.Clear()
        }
    }
    $comments
}

function Get-TrialComments([string] $dir) {
    $all = @{}
    foreach ($file in $files) {
        $comments = Get-Comments (Get-TrialFile $dir $file) $file
        foreach ($k in $comments.Keys) { $all[$k] = $comments[$k] }
    }
    $all
}

$root = $PSScriptRoot

# A trial's name in the key: the repository itself is the original.
function Get-Label([string] $dir) {
    if ((Resolve-Path (Join-Path $root $dir)).Path -eq (Resolve-Path "$root\..\..").Path) { 'original' } else { $dir }
}
$sheet = [Collections.Generic.List[string]]::new()
$key = [Collections.Generic.List[string]]::new()
$sheet.Add('# Pairwise choices')
$sheet.Add('')
$sheet.Add('Each pair shows two versions of the same comment, in a random order, above the code it describes. After "Choice:", write 1 or 2 for the clearer one, or = if neither is clearer. Add a note if you like. The key is in `pairs-key.md`.')
$key.Add('# Pairwise choices: key')
$key.Add('')
$rng = [Random]::new()
$pairs = foreach ($c in $Comparison) {
    $left, $right, $count = $c -split '\|'
    $a = Get-TrialComments (Join-Path $root $left)
    $b = Get-TrialComments (Join-Path $root $right)
    $shared = @($a.Keys | Where-Object { $b.ContainsKey($_) -and $a[$_].Comment.Trim() -ne $b[$_].Comment.Trim() })
    $shared | Sort-Object { $rng.Next() } | Select-Object -First ([int] $count) | ForEach-Object {
        [pscustomobject] @{ Left = $left; Right = $right; A = $a[$_]; B = $b[$_] }
    }
}
# Shuffled across comparisons, so that a pair's place doesn't tell which comparison it is from.
$n = 0
foreach ($pair in ($pairs | Sort-Object { $rng.Next() })) {
    $n++
    $swap = $rng.Next(2) -eq 1
    $first, $second = if ($swap) { $pair.B, $pair.A } else { $pair.A, $pair.B }
    $sheet.Add(''); $sheet.Add("## Pair $n"); $sheet.Add('')
    $sheet.Add("``$($first.File)``, before line $($first.Line) of version 1"); $sheet.Add('')
    $sheet.Add('Version 1:'); $sheet.Add(''); $sheet.Add('```csharp'); $sheet.Add($first.Comment); $sheet.Add('```')
    $sheet.Add('Version 2:'); $sheet.Add(''); $sheet.Add('```csharp'); $sheet.Add($second.Comment); $sheet.Add('```')
    $sheet.Add('Code:'); $sheet.Add(''); $sheet.Add('```csharp'); $sheet.Add($first.Context); $sheet.Add('```')
    $sheet.Add(''); $sheet.Add('Choice: ')
    $one, $two = if ($swap) { $pair.Right, $pair.Left } else { $pair.Left, $pair.Right }
    $key.Add("- Pair $n`: 1 is $(Get-Label $one), 2 is $(Get-Label $two)")
}
New-Item -ItemType Directory -Force $OutDir | Out-Null
[IO.File]::WriteAllText((Join-Path $OutDir 'pairs.md'), ($sheet -join "`n") + "`n")
[IO.File]::WriteAllText((Join-Path $OutDir 'pairs-key.md'), ($key -join "`n") + "`n")
"$n pairs"
