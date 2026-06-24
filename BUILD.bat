@echo off
REM ════════════════════════════════════════════════════════════════════
REM  RimClaim build script
REM  Double-click this file, or run it from a terminal in the mod folder.
REM ════════════════════════════════════════════════════════════════════

echo.
echo Building RimClaim.dll...
echo.

cd /d "%~dp0Source\RimClaim"

dotnet build -c Release

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ========================================
    echo  BUILD FAILED. See errors above.
    echo ========================================
    echo.
    echo Common fixes:
    echo  - Install .NET SDK: https://dotnet.microsoft.com/download
    echo  - Subscribe to Zetrith's Multiplayer mod in Steam
    echo  - If RimWorld isn't at F:\SteamLibrary\steamapps\common\RimWorld,
    echo    run: dotnet build -c Release -p:RimWorldPath="YOUR\PATH\HERE"
    echo.
    pause
    exit /b 1
)

echo.
echo ========================================
echo  BUILD SUCCEEDED
echo ========================================
echo.
echo RimClaim.dll is now in the Assemblies folder.
echo.
echo Next: copy the entire RimClaim folder into
echo   F:\SteamLibrary\steamapps\common\RimWorld\Mods\
echo (if it isn't already there), enable it in the mod list
echo below Harmony and Multiplayer, and restart RimWorld.
echo.
pause
