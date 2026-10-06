$url = "https://downloads.godotengine.org/?version=4.7.2&flavor=stable&slug=mono_win64.zip&platform=windows.64"
$zipPath = "Godot_v4.7.2-stable_mono_win64.zip"

if (!(Test-Path -Path $zipPath)) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Write-Host "Downloading Godot..."
    Invoke-WebRequest -Uri $url -OutFile $zipPath
}

Write-Host "Extracting files..."
Expand-Archive -Path $zipPath -DestinationPath "." -Force
Remove-Item -Path "Godot" -Recurse -Force -ErrorAction SilentlyContinue
Rename-Item -Path "Godot_v4.7.2-stable_mono_win64" -NewName "Godot"
Rename-Item -Path "Godot\Godot_v4.7.2-stable_mono_win64.exe" -NewName "Godot.exe"
Rename-Item -Path "Godot\Godot_v4.7.2-stable_mono_win64_console.exe" -NewName "Godot_console.exe"

Write-Host "Done!"
