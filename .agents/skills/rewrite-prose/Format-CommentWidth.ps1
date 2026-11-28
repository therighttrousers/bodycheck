<#
.SYNOPSIS
Rewraps C# comments that are longer than 120 characters, or that split an inline doc tag across lines.

.DESCRIPTION
Each run of comment lines with the same prefix, such as "    /// ", is a paragraph, except that a line with only a
doc tag, such as "<summary>", stands alone, and a line that starts with a block tag, such as "<param", starts a new
paragraph. A paragraph that fits and splits no tag is left as it is. Inline tags, such as <see cref="X"/>, are never
split.

.EXAMPLE
.\.agents\skills\rewrite-prose\Format-CommentWidth.ps1 (git ls-files 'src/BodyCheck.Core/Scripts/*.cs')
#>
param(
    [Parameter(Mandatory, ValueFromRemainingArguments)]
    [string[]] $Path
)
$ErrorActionPreference = "Stop"
$width = 120
$prefixPattern = '^(\s*///? )(.*)$'
$tagOnly = '^</?\w+[^>]*>$'
$startsParagraph = '^<(param|typeparam|returns|exception|remarks|summary|para|value)\b'
$enclosed = '^(<(\w+)[^>]*>)(.*)(</\2>)$'

function Format-Wrapped([string] $prefix, [string] $text) {
    # Words, with each inline tag kept whole.
    $words = [regex]::Matches($text, '(<[^>]+>|[^\s<]+)+') | ForEach-Object Value
    $line = ''
    foreach ($word in $words) {
        if ($line -and ($prefix.Length + $line.Length + 1 + $word.Length) -gt $width) {
            $prefix + $line
            $line = $word
        } else {
            $line = if ($line) { "$line $word" } else { $word }
        }
    }
    if ($line) { $prefix + $line }
}

function Format-Paragraph([string] $prefix, [string[]] $run) {
    if (-not $run) { return }
    $splitsTag = $run | Where-Object { $_ -match '<\w+$' }
    $tooLong = $run | Where-Object { ($prefix + $_).Length -gt $width }
    if (-not $splitsTag -and -not $tooLong) { return $run | ForEach-Object { $prefix + $_ } }
    $text = $run -join ' '
    if ($run.Count -eq 1 -and $text -match $enclosed) {
        $prefix + $Matches[1]
        Format-Wrapped $prefix $Matches[3]
        $prefix + $Matches[4]
    } else {
        Format-Wrapped $prefix $text
    }
}

foreach ($file in $Path) {
    $lines = [IO.File]::ReadAllText($file) -split "`n"
    $out = [Collections.Generic.List[string]]::new()
    $run = [Collections.Generic.List[string]]::new()
    $prefix = $null
    foreach ($line in $lines) {
        $isComment = $line -match $prefixPattern
        $linePrefix, $content = if ($isComment) { $Matches[1], $Matches[2] } else { $null, $null }
        if ($isComment -and $linePrefix -eq $prefix -and $content -notmatch $tagOnly -and
            $content -notmatch $startsParagraph) {
            $run.Add($content)
            continue
        }
        Format-Paragraph $prefix $run | ForEach-Object { $out.Add($_) }
        $run.Clear()
        $prefix = $null
        if ($isComment -and $content -notmatch $tagOnly) {
            $prefix = $linePrefix
            $run.Add($content)
        } else {
            $out.Add($line)
        }
    }
    Format-Paragraph $prefix $run | ForEach-Object { $out.Add($_) }
    [IO.File]::WriteAllText($file, $out -join "`n")
}
