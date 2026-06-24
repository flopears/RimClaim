# RimClaim build script (PowerShell)
# Right-click > Run with PowerShell, or: ./BUILD.ps1

Write-Host "`nBuilding RimClaim.dll...`n" -ForegroundColor Cyan

Push-Location "$PSScriptRoot\Source\RimClaim"
dotnet build -c Release
$code = $LASTEXITCODE
Pop-Location

if ($code -ne 0) {
    Write-Host "`nBUILD FAILED. See errors above.`n" -ForegroundColor Red
    Write-Host "Common fixes:"
    Write-Host " - Install .NET SDK: https://dotnet.microsoft.com/download"
    Write-Host " - Subscribe to Zetrith's Multiplayer mod in Steam"
    Write-Host " - Custom RimWorld path: dotnet build -c Release -p:RimWorldPath='YOUR\PATH'"
    Read-Host "`nPress Enter to close"
    exit 1
}

Write-Host "`nBUILD SUCCEEDED`n" -ForegroundColor Green
Write-Host "RimClaim.dll is in the Assemblies folder."
Write-Host "Copy the RimClaim folder into your RimWorld\Mods\ folder,"
Write-Host "enable it below Harmony and Multiplayer, restart RimWorld."
Read-Host "`nPress Enter to close"
