#!/usr/bin/env pwsh
# Starts Alify.Spotify and Alify.Lyrics side-by-side without Aspire.
# Use this when you want to run/debug the projects individually.
#
# To use Aspire instead, set Alify.AppHost as the startup project in VS and press F5.

$root = $PSScriptRoot

Write-Host ""
Write-Host "Starting Alify without Aspire..." -ForegroundColor Cyan
Write-Host "  Spotify -> http://127.0.0.1:7236" -ForegroundColor Green
Write-Host "  Lyrics  -> http://localhost:5143"  -ForegroundColor Green
Write-Host ""
Write-Host "Press Ctrl+C to stop both." -ForegroundColor Yellow
Write-Host ""

$spotify = Start-Process "dotnet" `
    -ArgumentList "run --project `"$root\Alify.Spotify\Alify.Spotify.csproj`" --launch-profile http" `
    -PassThru -NoNewWindow

$lyrics = Start-Process "dotnet" `
    -ArgumentList "run --project `"$root\Alify.Lyrics\Alify.Lyrics.csproj`" --launch-profile http" `
    -PassThru -NoNewWindow

try {
    Wait-Process -Id $spotify.Id, $lyrics.Id
}
finally {
    # Ensure both processes are stopped if one exits or Ctrl+C is pressed.
    if (-not $spotify.HasExited) { Stop-Process -Id $spotify.Id -Force }
    if (-not $lyrics.HasExited)  { Stop-Process -Id $lyrics.Id  -Force }
}
