@echo off
cd /d "%~dp0"
dotnet build MouseMapper/MouseMapper.csproj -c Debug --nologo -v q
if %ERRORLEVEL% EQU 0 (
    start "" "MouseMapper\bin\Debug\net8.0-windows\MouseMapper.exe"
)
