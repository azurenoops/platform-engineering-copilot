# Platform Engineering Copilot - Quick Local Setup
# Run this script to get up and running quickly
# Created: March 11, 2026

Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "Platform Engineering Copilot - Quick Setup" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan
Write-Host ""

# Verify branch
$currentBranch = git branch --show-current
if ($currentBranch -ne "BT_deploy") {
    Write-Host "WARNING: Not on BT_deploy branch (currently on: $currentBranch)" -ForegroundColor Yellow
    $switch = Read-Host "Switch to BT_deploy? (Y/n)"
    if ($switch -eq "" -or $switch -eq "Y" -or $switch -eq "y") {
        git checkout BT_deploy
    }
}

Write-Host "✓ On branch: BT_deploy" -ForegroundColor Green
Write-Host ""

# Check .env file
$envPath = Join-Path $PSScriptRoot "..\.env"
if (-not (Test-Path $envPath)) {
    Write-Host "ERROR: .env file not found!" -ForegroundColor Red
    Write-Host "Please copy .env.example to .env and fill in your credentials" -ForegroundColor Yellow
    exit 1
}

# Check for required fields in .env
$envContent = Get-Content $envPath -Raw
$missingFields = @()

if ($envContent -match 'AZURE_OPENAI_API_KEY=<.*?>') { $missingFields += "AZURE_OPENAI_API_KEY" }
if ($envContent -match 'AZURE_OPENAI_ENDPOINT=https://<.*?>/')  { $missingFields += "AZURE_OPENAI_ENDPOINT" }

if ($missingFields.Count -gt 0) {
    Write-Host "⚠️  Missing required fields in .env:" -ForegroundColor Yellow
    $missingFields | ForEach-Object { Write-Host "   - $_" -ForegroundColor Red }
    Write-Host ""
    Write-Host "Please create an Azure OpenAI resource and update .env file:" -ForegroundColor Yellow
    Write-Host "  1. Go to https://portal.azure.us" -ForegroundColor Cyan
    Write-Host "  2. Create Azure OpenAI resource in USGov Virginia" -ForegroundColor Cyan
    Write-Host "  3. Deploy gpt-4 model" -ForegroundColor Cyan
    Write-Host "  4. Copy Endpoint and API Key to .env file" -ForegroundColor Cyan
    Write-Host ""
    exit 1
}

Write-Host "✓ .env file configured" -ForegroundColor Green
Write-Host ""

# Build solution
Write-Host "Building .NET solution..." -ForegroundColor Yellow
dotnet build

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Build failed" -ForegroundColor Red
    exit 1
}

Write-Host "✓ Build successful" -ForegroundColor Green
Write-Host ""

# Offer to start Docker  
Write-Host "Start services with Docker Compose? (Y/n)" -ForegroundColor Yellow
$startDocker = Read-Host
if ($startDocker -eq "" -or $startDocker -eq "Y" -or $startDocker -eq "y") {
    Write-Host "Starting services..." -ForegroundColor Yellow
    docker-compose -f docker-compose.mcp.yml up -d
    
    Write-Host ""
    Write-Host "✓ Services started!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Access points:" -ForegroundColor Cyan
    Write-Host "  MCP Server: http://localhost:5100" -ForegroundColor White
    Write-Host "  Health: http://localhost:5100/health" -ForegroundColor White
    Write-Host ""
    Write-Host "To view logs:" -ForegroundColor Yellow
    Write-Host "  docker-compose -f docker-compose.mcp.yml logs -f" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "=================================================" -ForegroundColor Green
Write-Host "Setup Complete!" -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Green
