#!/usr/bin/env pwsh
# Build script for PhotoMover - Synology DS718+ (Intel x64)

param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "./publish/synology"
)

Write-Host "Building PhotoMover for Synology DS718+..." -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Cyan
Write-Host "Runtime: linux-x64" -ForegroundColor Cyan
Write-Host "Output Directory: $OutputDir" -ForegroundColor Cyan
Write-Host ""

# Clean output directory if it exists
if (Test-Path $OutputDir) {
    Write-Host "Cleaning output directory..." -ForegroundColor Yellow
    Remove-Item -Path $OutputDir -Recurse -Force
}

# Build self-contained single file executable with trimming
Write-Host "Running dotnet publish..." -ForegroundColor Green
dotnet publish PhotoMover/PhotoMover.csproj `
    --configuration $Configuration `
    --runtime linux-x64 `
    --self-contained true `
    --output $OutputDir `
    /p:PublishSingleFile=true `
    /p:PublishTrimmed=true

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "Build completed successfully!" -ForegroundColor Green
    Write-Host "Output location: $OutputDir" -ForegroundColor Cyan
    
    $executablePath = Join-Path $OutputDir "PhotoMover"
    if (Test-Path $executablePath) {
        Write-Host ""
        Write-Host "Executable: $executablePath" -ForegroundColor Cyan
        
        # Calculate file size
        $size = (Get-Item $executablePath).Length
        $sizeMB = [math]::Round($size / 1MB, 2)
        Write-Host "Size: $sizeMB MB" -ForegroundColor Cyan
        
        Write-Host ""
        Write-Host "To deploy to Synology DS718+:" -ForegroundColor Yellow
        Write-Host "1. Upload: scp $executablePath admin@synology-ip:/volume1/your-path/" -ForegroundColor White
        Write-Host "2. SSH in:  ssh admin@synology-ip" -ForegroundColor White
        Write-Host "3. Make executable: chmod +x /volume1/your-path/PhotoMover" -ForegroundColor White
        Write-Host "4. Run: ./PhotoMover --help" -ForegroundColor White
    }
} else {
    Write-Host ""
    Write-Host "Build failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}
