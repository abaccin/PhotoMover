#!/usr/bin/env pwsh
# Build script for PhotoMover - Self-contained Linux executable

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "linux-x64",
    [string]$OutputDir = "./publish/linux-x64",
    [switch]$SingleFile,
    [switch]$Trimmed
)

Write-Host "Building PhotoMover for Linux..." -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Cyan
Write-Host "Runtime: $Runtime" -ForegroundColor Cyan
Write-Host "Output Directory: $OutputDir" -ForegroundColor Cyan
Write-Host ""

# Base publish arguments
$publishArgs = @(
    "publish"
    "PhotoMover/PhotoMover.csproj"
    "--configuration", $Configuration
    "--runtime", $Runtime
    "--self-contained", "true"
    "--output", $OutputDir
)

# Add single file option if specified
if ($SingleFile) {
    Write-Host "Building as single file executable..." -ForegroundColor Yellow
    $publishArgs += "/p:PublishSingleFile=true"
}

# Add trimming option if specified
if ($Trimmed) {
    Write-Host "Enabling trimming to reduce size..." -ForegroundColor Yellow
    $publishArgs += "/p:PublishTrimmed=true"
}

# Clean output directory if it exists
if (Test-Path $OutputDir) {
    Write-Host "Cleaning output directory..." -ForegroundColor Yellow
    Remove-Item -Path $OutputDir -Recurse -Force
}

# Execute the publish command
Write-Host "Running dotnet publish..." -ForegroundColor Green
& dotnet @publishArgs

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "Build completed successfully!" -ForegroundColor Green
    Write-Host "Output location: $OutputDir" -ForegroundColor Cyan
    
    # Make the executable file executable (if on Linux/Mac)
    $executablePath = Join-Path $OutputDir "PhotoMover"
    if (Test-Path $executablePath) {
        Write-Host ""
        Write-Host "Executable: $executablePath" -ForegroundColor Cyan
        
        # Calculate file size
        $size = (Get-Item $executablePath).Length
        $sizeMB = [math]::Round($size / 1MB, 2)
        Write-Host "Size: $sizeMB MB" -ForegroundColor Cyan
        
        if ($IsLinux -or $IsMacOS) {
            chmod +x $executablePath
            Write-Host "Executable permissions set." -ForegroundColor Green
        } else {
            Write-Host ""
            Write-Host "To make the file executable on Linux, run:" -ForegroundColor Yellow
            Write-Host "chmod +x $executablePath" -ForegroundColor White
        }
    }
    
    Write-Host ""
    Write-Host "To run on Linux:" -ForegroundColor Yellow
    Write-Host "./PhotoMover --help" -ForegroundColor White
} else {
    Write-Host ""
    Write-Host "Build failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}
