@echo off
echo Building Gemini GUI...

REM Clean previous builds
if exist "bin" rmdir /s /q "bin"
if exist "obj" rmdir /s /q "obj"

REM Restore packages
echo Restoring packages...
dotnet restore

REM Build the application
echo Building application...
dotnet build -c Release

echo Build completed successfully!
echo Output: bin\Release\net8.0-windows\win-x64\GeminiGUI.exe
pause
