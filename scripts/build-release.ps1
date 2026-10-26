$ErrorActionPreference = "Stop"
Set-Location "${PSScriptRoot}\.."

$outputPath = "build\windows_release_x86_64"

if (Test-Path -Path $outputPath) {
    Remove-Item -Path $outputPath -Recurse -Force
}
# Godot's --export-release needs the output directory to exist first.
New-Item -Path ".\${outputPath}" -ItemType Directory -Force | Out-Null
# The console executable, because PowerShell doesn't wait for Godot.exe, a GUI program, or get its exit code.
# The output path is relative to the game's directory.
.\Godot\Godot_console.exe --path game --headless --export-release "Windows Desktop" "..\${outputPath}\BodyCheck.exe"
if ($LASTEXITCODE -ne 0) {
    throw "Godot's export failed with exit code ${LASTEXITCODE}."
}
