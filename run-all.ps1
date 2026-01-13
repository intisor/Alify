# Run all Alify projects in parallel
Write-Host "Starting all Alify projects..." -ForegroundColor Green
Write-Host ""

# Define project paths
$projects = @(
    @{ Name = "Alify.Lyrics"; Path = ".\Alify.Lyrics"; Port = 5000 },
    @{ Name = "Alify.Spotify"; Path = ".\Alify.Spotify"; Port = 5001 }
)

# Start each project in a new process
$processes = @()

foreach ($project in $projects) {
    Write-Host "Starting $($project.Name) on port $($project.Port)..." -ForegroundColor Cyan
    
    $process = Start-Process -FilePath "dotnet" -ArgumentList "run --project $($project.Path)" -NoNewWindow -PassThru
    $processes += @{ Name = $project.Name; Process = $process }
    
    Start-Sleep -Seconds 1
}

Write-Host ""
Write-Host "All projects started successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Services running at:" -ForegroundColor Yellow
Write-Host "  - Alify.Lyrics:  http://localhost:5000" -ForegroundColor Cyan
Write-Host "  - Alify.Spotify: http://localhost:5001" -ForegroundColor Cyan
Write-Host ""
Write-Host "Press Ctrl+C to stop all services..." -ForegroundColor Yellow

# Keep script running and monitor processes
try {
    while ($true) {
        foreach ($proc in $processes) {
            if ($proc.Process.HasExited) {
                Write-Host "$($proc.Name) has exited unexpectedly!" -ForegroundColor Red
            }
        }
        Start-Sleep -Seconds 5
    }
}
finally {
    Write-Host ""
    Write-Host "Stopping all projects..." -ForegroundColor Yellow
    foreach ($proc in $processes) {
        Stop-Process -Id $proc.Process.Id -Force -ErrorAction SilentlyContinue
        Write-Host "$($proc.Name) stopped." -ForegroundColor Cyan
    }
}
