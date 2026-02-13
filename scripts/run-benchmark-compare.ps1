param(
    [string]$Baseline = "benchmark-results/benchmark-20260212-080146.json",
    [string]$FaceModel = "opencv-dnn",
    [string]$TestDataPath = "tests/Data",
    [string]$OutputPath = "benchmark-results"
)

$ErrorActionPreference = "Stop"

Write-Host "Running benchmark (face model: $FaceModel)..." -ForegroundColor Green
dotnet run --project src/PhotoMapperAI/PhotoMapperAI.csproj -- benchmark `
    --faceModels $FaceModel `
    --testDataPath $TestDataPath `
    --outputPath $OutputPath

$LatestFile = Get-ChildItem -Path $OutputPath -Filter "benchmark-*.json" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $LatestFile) {
    throw "No benchmark result files found in: $OutputPath"
}

Write-Host ""
Write-Host "Comparing latest run with baseline..." -ForegroundColor Green
Write-Host "Baseline : $Baseline"
Write-Host "Candidate: $($LatestFile.FullName)"

dotnet run --project src/PhotoMapperAI/PhotoMapperAI.csproj -- benchmark-compare `
    --baseline $Baseline `
    --candidate $LatestFile.FullName `
    --faceModel $FaceModel
