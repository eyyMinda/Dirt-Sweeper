@echo off
rem Builds dist\DirtSweeper.exe.
rem Building needs the .NET SDK (https://dot.net). The exe itself runs on any
rem Windows 10/11 PC without installing anything (it uses the built-in .NET Framework 4.8).
setlocal
set ROOT=%~dp0
dotnet build "%ROOT%src\DirtSweeper\DirtSweeper.csproj" -c Release -nologo -v q -o "%ROOT%src\DirtSweeper\bin\out" || exit /b 1
if not exist "%ROOT%dist" mkdir "%ROOT%dist"
copy /y "%ROOT%src\DirtSweeper\bin\out\DirtSweeper.exe" "%ROOT%dist\DirtSweeper.exe" >nul || exit /b 1
echo Built %ROOT%dist\DirtSweeper.exe
