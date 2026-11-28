<#
.SYNOPSIS
Counts markers of the comment style in issue #215 in C# files' comments.

.DESCRIPTION
A rough screen, not a judge: each marker is a regular expression, so it misses some instances and catches some fine
English. Use it to compare conditions over the same code, never to grade one comment. Rates are per 100 comment words.

.EXAMPLE
.\experiments\comment-style\Measure-CommentStyle.ps1 (git ls-files '*.cs')
#>
param(
    [Parameter(Mandatory, ValueFromRemainingArguments)]
    [string[]] $Path,
    # Report each file instead of only the total.
    [switch] $PerFile
)
$ErrorActionPreference = "Stop"

# General-purpose verbs, as in Ogden's Basic English, standing in for more precise ones; and "way" for path or method.
$operatorVerbs = '\b(hold|holds|holding|held|keep|keeps|keeping|kept|go|goes|going|gone|went|ask|asks|asking|asked|' +
    'say|says|saying|said|live|lives|living|lived|way|ways)\b'
$markers = [ordered]@{
    # A sentence that starts with a free relative used as a noun phrase: "What the dialogue presented sees".
    FreeRelative = '(^|[.:;]\s+)What\s+(?!is\b|are\b|does\b|do\b|if\b)'
    OperatorVerb = $operatorVerbs
    # An auxiliary left without its verb at the end of a clause: "while none is", "as the player does".
    StrandedAux = '\b(is|are|was|were|does|do|did|has|have|had)\s*([.,;:)]|$)'
    IfAny = '\bif any\b'
}

function Get-CommentText([string] $file) {
    if ($file -like '*.xml') {
        # A compiled doc file, such as .NET's or Godot's, for a baseline of docs that people wrote.
        $text = ([xml] (Get-Content -LiteralPath $file -Raw)).SelectNodes('//member/summary') |
            ForEach-Object { $_.InnerXml } | Join-String -Separator "`n"
    } else {
        $text = (Get-Content -LiteralPath $file | Where-Object { $_ -match '^\s*//' } |
            ForEach-Object { $_ -replace '^\s*///?\s?', '' }) -join "`n"
    }
    # Keep the name a cross-reference shows, and drop the other XML doc tags.
    $text = $text -replace '<(see|seealso|paramref|typeparamref)\s+(cref|name|langword)="([^"]*)"\s*/>', '$3'
    $text -replace '<[^>]+>', ' '
}

function Measure-Text([string] $name, [string] $text) {
    $words = ([regex]::Matches($text, "[A-Za-z][A-Za-z'-]*")).Count
    $sentences = [math]::Max(1, ([regex]::Matches($text, '[.!?](\s|$)')).Count)
    $row = [ordered]@{ Name = $name; Words = $words; WordsPerSentence = [math]::Round($words / $sentences, 1) }
    foreach ($marker in $markers.GetEnumerator()) {
        $count = ([regex]::Matches($text, $marker.Value, 'Multiline')).Count
        $row[$marker.Key] = if ($words) { [math]::Round(100 * $count / $words, 2) } else { 0 }
    }
    [pscustomobject] $row
}

$texts = foreach ($file in $Path) { [pscustomobject] @{ File = $file; Text = Get-CommentText $file } }
if ($PerFile) {
    $texts | ForEach-Object { Measure-Text $_.File $_.Text }
}
Measure-Text 'Total' (($texts | ForEach-Object Text) -join "`n")
