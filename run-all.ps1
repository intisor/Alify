# Run all Alify projects in parallel with coordinated loopback port binding (ALI-026)
Write-Host "Starting all Alify projects..." -ForegroundColor Green
Write-Host ""

# Define project paths and explicit loopback ports
$projects = @(
    @{ Name = "Alify.Lyrics"; Path = ".\Alify.Lyrics"; Port = 5000 },
    @{ Name = "Alify.Spotify"; Path = ".\Alify.Spotify"; Port = 5001 }
)

# Pre-flight check: ensure ports are not already in use
foreach ($project in $projects) {
    try {
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $project.Port)
        $listener.Start()
        $listener.Stop()
    }
    catch {
        Write-Host "ERROR: Port $($project.Port) is already in use by another process. Please free this port before starting $($project.Name)." -ForegroundColor Red
        exit 1
    }
}

# Start each project in a new process with explicit loopback binding
$processes = @()

foreach ($project in $projects) {
    Write-Host "Starting $($project.Name) on http://127.0.0.1:$($project.Port)..." -ForegroundColor Cyan
    
    $args = "run --project $($project.Path) --no-launch-profile --urls http://127.0.0.1:$($project.Port)"
    $process = Start-Process -FilePath "dotnet" -ArgumentList $args -NoNewWindow -PassThru
    $processes += @{ Name = $project.Name; Process = $process }
    
    Start-Sleep -Seconds 1
}

Write-Host ""
Write-Host "All projects started successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Services running at:" -ForegroundColor Yellow
Write-Host "  - Alify.Lyrics:  http://127.0.0.1:5000" -ForegroundColor Cyan
Write-Host "  - Alify.Spotify: http://127.0.0.1:5001" -ForegroundColor Cyan
Write-Host ""
Write-Host "Press Ctrl+C to stop all services..." -ForegroundColor Yellow

# Keep script running and monitor processes
try {
    while ($true) {
        foreach ($proc in $processes) {
            if ($proc.Process.HasExited) {
                Write-Host "$($proc.Name) has exited (ExitCode: $($proc.Process.ExitCode))" -ForegroundColor Red
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
