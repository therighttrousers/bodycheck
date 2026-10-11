$godotUrl = "https://downloads.godotengine.org/?version=4.7.2&flavor=stable&slug=mono_win64.zip&platform=windows.64"
$godotZipPath = "Godot_v4.7.2-stable_mono_win64.zip"

if (!(Test-Path -Path $godotZipPath)) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Write-Host "Downloading Godot..."
    Invoke-WebRequest -Uri $godotUrl -OutFile $godotZipPath
}

Write-Host "Extracting Godot files..."
Expand-Archive -Path $godotZipPath -DestinationPath "." -Force
Remove-Item -Path "Godot" -Recurse -Force -ErrorAction SilentlyContinue
Rename-Item -Path "Godot_v4.7.2-stable_mono_win64" -NewName "Godot"
Rename-Item -Path "Godot\Godot_v4.7.2-stable_mono_win64.exe" -NewName "Godot.exe"
Rename-Item -Path "Godot\Godot_v4.7.2-stable_mono_win64_console.exe" -NewName "Godot_console.exe"

dotnet restore
dotnet husky install
