[CmdletBinding()]
param(
    [string]$Version = "0.3.3.1",
    [string]$Runtime = "win-x64",
    [string]$PackageSource = "https://api.nuget.org/v3/index.json"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repositoryRoot "artifacts"
$releaseRoot = Join-Path $repositoryRoot "Release"
$workingRoot = Join-Path $artifactRoot (".publish-" + [guid]::NewGuid().ToString("N"))
$sourceRoot = Join-Path $workingRoot "source"
$project = Join-Path $sourceRoot "src\CodexMonitor.App\CodexMonitor.App.csproj"

function Assert-ChildPath([string]$Parent, [string]$Child) {
    $parentPath = [IO.Path]::GetFullPath($Parent).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    $childPath = [IO.Path]::GetFullPath($Child)
    if (-not $childPath.StartsWith($parentPath, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside the expected directory: $childPath"
    }
}

function Assert-SingleExecutable([string]$Directory, [string]$ExpectedVersion) {
    $files = @(Get-ChildItem -LiteralPath $Directory -Force)
    if ($files.Count -ne 1 -or $files[0].Name -ne "CodexMonitor.exe") {
        throw "Single-file publish produced extra files in $Directory."
    }

    $fileVersion = $files[0].VersionInfo.FileVersion
    if ($fileVersion -ne $ExpectedVersion) {
        throw "Unexpected executable version in $Directory`: $fileVersion"
    }
    return $files[0].FullName
}

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

$previousDotnetHome = $env:DOTNET_CLI_HOME
$previousNugetPackages = $env:NUGET_PACKAGES
$previousNugetHttpCache = $env:NUGET_HTTP_CACHE_PATH
New-Item -ItemType Directory -Path $workingRoot -Force | Out-Null
try {
    New-Item -ItemType Directory -Path $sourceRoot | Out-Null
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "Directory.Build.props") -Destination $sourceRoot
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "NuGet.Config") -Destination $sourceRoot
    & robocopy (Join-Path $repositoryRoot "src") (Join-Path $sourceRoot "src") /E /XJ /XD bin obj /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Could not stage the project source." }

    $env:DOTNET_CLI_HOME = Join-Path $workingRoot ".dotnet"
    $env:NUGET_PACKAGES = Join-Path $workingRoot ".nuget\packages"
    $env:NUGET_HTTP_CACHE_PATH = Join-Path $workingRoot ".nuget\http"
    $selfContainedDirectory = Join-Path $workingRoot "self-contained"
    $frameworkDirectory = Join-Path $workingRoot "framework-dependent"

    & $dotnet restore $project `
        -r $Runtime `
        --source $PackageSource `
        -p:SelfContained=true `
        -p:PublishSingleFile=true `
        -p:NuGetAudit=false `
        -p:PublishTrimmed=false
    if ($LASTEXITCODE -ne 0) { throw "Self-contained restore failed." }

    & $dotnet publish $project `
        -c Release `
        -r $Runtime `
        --self-contained true `
        --no-restore `
        -o $selfContainedDirectory `
        -p:Version=$Version `
        -p:PublishSingleFile=true `
        -p:IncludeAllContentForSelfExtract=true `
        -p:PublishTrimmed=false `
        -p:DebugType=None `
        -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw "Self-contained publish failed." }
    $selfContainedExe = Assert-SingleExecutable $selfContainedDirectory $Version

    & $dotnet restore $project `
        -r $Runtime `
        --source $PackageSource `
        -p:SelfContained=false `
        -p:PublishSingleFile=true `
        -p:NuGetAudit=false `
        -p:PublishTrimmed=false
    if ($LASTEXITCODE -ne 0) { throw "Framework-dependent restore failed." }

    & $dotnet publish $project `
        -c Release `
        -r $Runtime `
        --self-contained false `
        --no-restore `
        -o $frameworkDirectory `
        -p:Version=$Version `
        -p:PublishSingleFile=true `
        -p:IncludeAllContentForSelfExtract=true `
        -p:PublishTrimmed=false `
        -p:DebugType=None `
        -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw "Framework-dependent publish failed." }
    $frameworkExe = Assert-SingleExecutable $frameworkDirectory $Version

    if (Test-Path -LiteralPath $releaseRoot) {
        $releaseItem = Get-Item -LiteralPath $releaseRoot -Force
        if ($releaseItem.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw "Release is a link; refusing to clear it."
        }
        foreach ($item in Get-ChildItem -LiteralPath $releaseRoot -Force) {
            Assert-ChildPath $releaseRoot $item.FullName
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "Release contains a link; refusing to clear it: $($item.FullName)"
            }
        }
    } else {
        New-Item -ItemType Directory -Path $releaseRoot | Out-Null
    }

    $releasePrefix = [IO.Path]::GetFullPath($releaseRoot).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    $runningReleaseProcesses = @(Get-Process -Name "CodexMonitor" -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Path -and [IO.Path]::GetFullPath($_.Path).StartsWith(
                $releasePrefix, [StringComparison]::OrdinalIgnoreCase)
        })
    if ($runningReleaseProcesses.Count -gt 0) {
        throw "Close the running CodexMonitor in Release before publishing."
    }

    foreach ($item in Get-ChildItem -LiteralPath $releaseRoot -Force) {
        Assert-ChildPath $releaseRoot $item.FullName
        Remove-Item -LiteralPath $item.FullName -Recurse -Force
    }

    $selfContainedOutput = Join-Path $releaseRoot "CodexMonitor-$Version-$Runtime-self-contained.exe"
    $frameworkOutput = Join-Path $releaseRoot "CodexMonitor-$Version-$Runtime-framework-dependent.exe"
    Copy-Item -LiteralPath $selfContainedExe -Destination $selfContainedOutput
    Copy-Item -LiteralPath $frameworkExe -Destination $frameworkOutput

    $outputFiles = @(Get-ChildItem -LiteralPath $releaseRoot -Force)
    if ($outputFiles.Count -ne 2 -or
        -not (Test-Path -LiteralPath $selfContainedOutput) -or
        -not (Test-Path -LiteralPath $frameworkOutput)) {
        throw "Release does not contain exactly the two expected executables."
    }
    Get-Item -LiteralPath $selfContainedOutput, $frameworkOutput |
        Select-Object Name, Length, @{Name="FileVersion"; Expression={$_.VersionInfo.FileVersion}}
} finally {
    $env:DOTNET_CLI_HOME = $previousDotnetHome
    $env:NUGET_PACKAGES = $previousNugetPackages
    $env:NUGET_HTTP_CACHE_PATH = $previousNugetHttpCache
    if (Test-Path -LiteralPath $workingRoot) {
        Assert-ChildPath $artifactRoot $workingRoot
        Remove-Item -LiteralPath $workingRoot -Recurse -Force
    }
}
