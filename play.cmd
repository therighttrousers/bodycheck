@echo off
rem Builds the game and plays it. Double-click it, or run it with a world's name and other arguments.
cd /d "%~dp0"
dotnet build game\BodyCheck.csproj || (pause & exit /b 1)
start "" Godot\Godot.exe --path game -- %*
