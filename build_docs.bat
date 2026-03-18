@echo off
echo [DOCS] Building Toyopuc .NET Docs with DocFX...
docfx build docfx.json
if %errorlevel% neq 0 (pause & exit /b %errorlevel%)
pause
