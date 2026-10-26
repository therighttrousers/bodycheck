$ErrorActionPreference = "Stop"
Set-Location "${PSScriptRoot}\.."

Add-Type -AssemblyName System.IO.Compression.FileSystem

$godotVersion = "4.7.2"
$godotRelease = "stable"
$godotVersionSlug = "${godotVersion}-${godotRelease}"
$godotBaseName = "Godot_v${godotVersionSlug}_mono_win64"

$godotZipName = "${godotBaseName}.zip"
$godotUrl = "https://github.com/godotengine/godot/releases/download/${godotVersionSlug}/${godotZipName}"
$godotZipPath = ".\tmp\${godotZipName}"
$godotInstallPath = ".\Godot"

$templatesZipName = "Godot_v${godotVersionSlug}_mono_export_templates.tpz"
$templatesUrl = "https://github.com/godotengine/godot/releases/download/${godotVersionSlug}/${templatesZipName}"
$templatesZipPath = ".\tmp\${templatesZipName}"
$templatesZipGlob = 'windows_*_x86_64*.exe'
$templatesInstallPath = "${env:APPDATA}\Godot\export_templates\${godotVersion}.${godotRelease}.mono"

New-Item -Path ".\tmp" -ItemType Directory -Force | Out-Null

# Downloads to a temporary name first, so that an interrupted download isn't taken for a finished one next time.
function Get-Download($description, $url, $path) {
    if (!(Test-Path -Path $path)) {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Write-Host "Downloading ${description}..."
        Invoke-WebRequest -Uri $url -OutFile "${path}.part"
        Move-Item -Path "${path}.part" -Destination $path -Force
    }
}

# PowerShell doesn't stop on a native command's failure, so check its exit code.
function Invoke-Native([scriptblock] $command) {
    & $command
    if ($LASTEXITCODE -ne 0) {
        throw "Failed with exit code ${LASTEXITCODE}: $command"
    }
}

Get-Download "Godot" $godotUrl $godotZipPath
Get-Download "Godot export templates" $templatesUrl $templatesZipPath

Write-Host "Extracting Godot runtime+editor binaries into ${godotInstallPath}..."
Expand-Archive -Path $godotZipPath -DestinationPath "." -Force
Remove-Item -Path $godotInstallPath -Recurse -Force -ErrorAction SilentlyContinue
Rename-Item -Path $godotBaseName -NewName $godotInstallPath
Rename-Item -Path "${godotInstallPath}\${godotBaseName}.exe" -NewName "Godot.exe"
Rename-Item -Path "${godotInstallPath}\${godotBaseName}_console.exe" -NewName "Godot_console.exe"
Write-Host "- Successfully extracted Godot" -ForegroundColor Green

Write-Host "Extracting export templates into ${templatesInstallPath}..."
if (-not (Test-Path -Path $templatesInstallPath)) {
    New-Item -Path $templatesInstallPath -ItemType Directory -Force | Out-Null
}
$zip = [System.IO.Compression.ZipFile]::OpenRead($templatesZipPath)
try {
    $zip.Entries | Where-Object { $_.Name -like $templatesZipGlob } | ForEach-Object {
        $destPath = Join-Path -Path $templatesInstallPath -ChildPath $_.Name
        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($_, $destPath, $true)
        Write-Host "- Successfully extracted $($_.Name)" -ForegroundColor Green
    }
}
finally {
    $zip.Dispose()
}

Invoke-Native { dotnet restore }
Invoke-Native { dotnet tool restore }
Invoke-Native { dotnet husky install }
