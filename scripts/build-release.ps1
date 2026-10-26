$ErrorActionPreference = "Stop"
Set-Location "${PSScriptRoot}\.."

$outputPath = "build\windows_release_x86_64"
$logPath = "build\export.log"

if (Test-Path -Path $outputPath) {
    Remove-Item -Path $outputPath -Recurse -Force
}
# Godot's --export-release needs the output directory to exist first.
New-Item -Path ".\${outputPath}" -ItemType Directory -Force | Out-Null

Write-Host "Exporting the game into ${outputPath}..."
# The console executable, because PowerShell doesn't wait for Godot.exe, a GUI program, or get its exit code.
# The output path is relative to the game's directory. Godot's output goes to the log, which is shown only if the
# export fails. Errors don't stop the script here, since Windows PowerShell makes each line of a native command's
# redirected stderr an error.
& {
    $ErrorActionPreference = "Continue"
    .\Godot\Godot_console.exe --path game --headless --export-release "Windows Desktop" "..\${outputPath}\BodyCheck.exe" *> $logPath
}
if ($LASTEXITCODE -ne 0) {
    Get-Content -Path $logPath
    throw "Godot's export failed with exit code ${LASTEXITCODE}. Its output is above, and in ${logPath}."
}
Write-Host "Exported. Godot's output is in ${logPath}."
