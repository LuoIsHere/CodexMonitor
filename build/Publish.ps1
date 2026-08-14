[CmdletBinding()]
param(
    [string]$Version = "0.3.0",
    [string]$Runtime = "win-x64",
    [string]$PackageSource = "https://api.nuget.org/v3/index.json"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot "src\CodexMonitor.App\CodexMonitor.App.csproj"
$artifactRoot = Join-Path $repositoryRoot "artifacts"
$workingRoot = Join-Path $artifactRoot ".working"

$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) {
    $standardDotnet = "C:\Program Files\dotnet\dotnet.exe"
    if (Test-Path -LiteralPath $standardDotnet) {
        $dotnet = $standardDotnet
    }
}
if (-not $dotnet) {
    throw ".NET SDK was not found."
}

if (Test-Path -LiteralPath $workingRoot) {
    Remove-Item -LiteralPath $workingRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $workingRoot -Force | Out-Null

$selfContainedDirectory = Join-Path $workingRoot "self-contained"
$frameworkDirectory = Join-Path $workingRoot "framework-dependent"
$frameworkSingleFileDirectory = Join-Path $workingRoot "framework-dependent-single-file"

& $dotnet restore $project `
    -r $Runtime `
    --source $PackageSource `
    -p:PublishSingleFile=true `
    -p:PublishTrimmed=false
if ($LASTEXITCODE -ne 0) { throw "Runtime package restore failed." }

& $dotnet publish $project `
    -c Release `
    -r $Runtime `
    --self-contained true `
    --no-restore `
    -o $selfContainedDirectory `
    -p:Version=$Version `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -p:DebugType=None `
    -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw "Self-contained publish failed." }

& $dotnet publish $project `
    -c Release `
    -r $Runtime `
    --self-contained false `
    --no-restore `
    -o $frameworkDirectory `
    -p:Version=$Version `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=false `
    -p:DebugType=None `
    -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw "Framework-dependent publish failed." }

& $dotnet publish $project `
    -c Release `
    -r $Runtime `
    --self-contained false `
    --no-restore `
    -o $frameworkSingleFileDirectory `
    -p:Version=$Version `
    -p:PublishSingleFile=true `
    -p:PublishTrimmed=false `
    -p:DebugType=None `
    -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw "Framework-dependent single-file publish failed." }

$publicFiles = @(
    (Join-Path $repositoryRoot "README.md"),
    (Join-Path $repositoryRoot "CHANGELOG.md"),
    (Join-Path $repositoryRoot "LICENSE")
)
Copy-Item -LiteralPath $publicFiles -Destination $selfContainedDirectory -Force
Copy-Item -LiteralPath $publicFiles -Destination $frameworkDirectory -Force

$selfContainedExe = Join-Path $selfContainedDirectory "CodexMonitor.exe"
$selfContainedStandaloneExe = Join-Path $artifactRoot "CodexMonitor-$Version-$Runtime-self-contained.exe"
Copy-Item -LiteralPath $selfContainedExe -Destination $selfContainedStandaloneExe -Force

$frameworkSingleFileExe = Join-Path $frameworkSingleFileDirectory "CodexMonitor.exe"
$frameworkStandaloneExe = Join-Path $artifactRoot "CodexMonitor-$Version-$Runtime-framework-dependent.exe"
Copy-Item -LiteralPath $frameworkSingleFileExe -Destination $frameworkStandaloneExe -Force

$selfContainedZip = Join-Path $artifactRoot "CodexMonitor-$Version-$Runtime-self-contained.zip"
$frameworkZip = Join-Path $artifactRoot "CodexMonitor-$Version-$Runtime-framework-dependent.zip"
if (Test-Path -LiteralPath $selfContainedZip) { Remove-Item -LiteralPath $selfContainedZip -Force }
if (Test-Path -LiteralPath $frameworkZip) { Remove-Item -LiteralPath $frameworkZip -Force }
Compress-Archive -Path (Join-Path $selfContainedDirectory "*") -DestinationPath $selfContainedZip
Compress-Archive -Path (Join-Path $frameworkDirectory "*") -DestinationPath $frameworkZip

Get-Item -LiteralPath $selfContainedStandaloneExe, $frameworkStandaloneExe, $selfContainedZip, $frameworkZip |
    Select-Object Name, Length, LastWriteTime
