@echo off
set PUBLISH_DIR=.\publish
echo [CI] Starting .NET Build, Format and Single-File Publish...
dotnet build
if %errorlevel% neq 0 (pause & exit /b %errorlevel%)
dotnet format --verify-no-changes
if %errorlevel% neq 0 (pause & exit /b %errorlevel%)
dotnet test
if %errorlevel% neq 0 (pause & exit /b %errorlevel%)
dotnet publish examples\Toyopuc.DeviceMonitor\Toyopuc.DeviceMonitor.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true /p:PublishTrimmed=false -o "%PUBLISH_DIR%\DeviceMonitor"
if %errorlevel% neq 0 (pause & exit /b %errorlevel%)
echo [SUCCESS] Single-File published to .\publish
pause
