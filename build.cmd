@echo off
rem Same as "npm run build" for people without Node: release build into releases\<version>\ and dist\DirtSweeper.exe.
rem Building needs the .NET SDK (https://dot.net). The exe itself runs on any Windows 10/11 PC without installing
rem anything (it uses the built-in .NET Framework 4.8).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\build.ps1" %*
