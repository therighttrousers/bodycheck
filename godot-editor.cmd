@echo off
rem Builds the game and opens it in the Godot editor. Press the editor's Run button to play it, with the toolbar
rem that changes the game's speed. Double-click it.
cd /d "%~dp0"
dotnet build game\BodyCheck.csproj || (pause & exit /b 1)
start "" Godot\Godot.exe --path game --editor
