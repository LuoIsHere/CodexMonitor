[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = "0.3.3",
    [string]$Runtime = "win-x64",
    [string]$PackageSource = "https://api.nuget.org/v3/index.json"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repositoryRoot "artifacts"
$workingRoot = Join-Path $artifactRoot (".publish-" + [guid]::NewGuid().ToString("N"))
$sourceRoot = Join-Path $workingRoot "source"
$project = Join-Path $sourceRoot "src\CodexMonitor.App\CodexMonitor.App.csproj"
$fileVersion = ([version]"$Version.0").ToString(4)
$versionProperties = @(
    "-p:Version=$Version",
    "-p:AssemblyVersion=$fileVersion",
    "-p:FileVersion=$fileVersion",
    "-p:InformationalVersion=$Version",
    "-p:IncludeSourceRevisionInInformationalVersion=false"
)

function Assert-ChildPath([string]$Parent, [string]$Child) {
    $parentPath = [IO.Path]::GetFullPath($Parent).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    $childPath = [IO.Path]::GetFullPath($Child)
    if (-not $childPath.StartsWith($parentPath, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside the expected directory: $childPath"
    }
}

function Assert-PublishedExecutable([string]$Directory, [string]$ExpectedVersion, [switch]$SingleFile) {
    $files = @(Get-ChildItem -LiteralPath $Directory -Force)
    if ($SingleFile -and ($files.Count -ne 1 -or $files[0].Name -ne "CodexMonitor.exe")) {
        throw "Single-file publish produced extra files in $Directory."
    }

    $executable = Get-Item -LiteralPath (Join-Path $Directory "CodexMonitor.exe")
    $expectedFileVersion = ([version]"$ExpectedVersion.0").ToString(4)
    if ($executable.VersionInfo.FileVersion -ne $expectedFileVersion -or
        $executable.VersionInfo.ProductVersion -ne $ExpectedVersion) {
        throw "Unexpected executable version in $Directory`: $($executable.VersionInfo.FileVersion) / $($executable.VersionInfo.ProductVersion)"
    }
    return $executable.FullName
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
    $frameworkSingleFileDirectory = Join-Path $workingRoot "framework-dependent-single-file"

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
        @versionProperties `
        -p:PublishSingleFile=true `
        -p:IncludeAllContentForSelfExtract=true `
        -p:PublishTrimmed=false `
        -p:DebugType=None `
        -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw "Self-contained publish failed." }
    $selfContainedExe = Assert-PublishedExecutable $selfContainedDirectory $Version -SingleFile

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
        @versionProperties `
        -p:PublishSingleFile=false `
        -p:PublishTrimmed=false `
        -p:DebugType=None `
        -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw "Framework-dependent publish failed." }
    $null = Assert-PublishedExecutable $frameworkDirectory $Version

    & $dotnet publish $project `
        -c Release `
        -r $Runtime `
        --self-contained false `
        --no-restore `
        -o $frameworkSingleFileDirectory `
        @versionProperties `
        -p:PublishSingleFile=true `
        -p:IncludeAllContentForSelfExtract=true `
        -p:PublishTrimmed=false `
        -p:DebugType=None `
        -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw "Framework-dependent single-file publish failed." }
    $frameworkExe = Assert-PublishedExecutable $frameworkSingleFileDirectory $Version -SingleFile

    $publicFiles = @("README.md", "README_cn.md", "README_hk.md", "CHANGELOG.md", "LICENSE") |
        ForEach-Object { Join-Path $repositoryRoot $_ }
    foreach ($directory in @($selfContainedDirectory, $frameworkDirectory)) {
        Copy-Item -LiteralPath $publicFiles -Destination $directory
        $packageAssets = Join-Path $directory "assets"
        New-Item -ItemType Directory -Path $packageAssets | Out-Null
        Copy-Item -LiteralPath (Join-Path $repositoryRoot "assets\screenshots") -Destination $packageAssets -Recurse
        $packageSource = Join-Path $directory "src"
        New-Item -ItemType Directory -Path $packageSource | Out-Null
        Copy-Item -LiteralPath (Join-Path $repositoryRoot "src\CHANGELOG.md") -Destination $packageSource
    }

    $selfContainedOutput = Join-Path $artifactRoot "CodexMonitor-$Version-$Runtime-self-contained.exe"
    $frameworkOutput = Join-Path $artifactRoot "CodexMonitor-$Version-$Runtime-framework-dependent.exe"
    Copy-Item -LiteralPath $selfContainedExe -Destination $selfContainedOutput -Force
    Copy-Item -LiteralPath $frameworkExe -Destination $frameworkOutput -Force
    $selfContainedZip = Join-Path $artifactRoot "CodexMonitor-$Version-$Runtime-self-contained.zip"
    $frameworkZip = Join-Path $artifactRoot "CodexMonitor-$Version-$Runtime-framework-dependent.zip"
    Compress-Archive -Path (Join-Path $selfContainedDirectory "*") -DestinationPath $selfContainedZip -Force
    Compress-Archive -Path (Join-Path $frameworkDirectory "*") -DestinationPath $frameworkZip -Force

    $outputs = @($selfContainedOutput, $frameworkOutput, $selfContainedZip, $frameworkZip)
    foreach ($output in $outputs) {
        if (-not (Test-Path -LiteralPath $output -PathType Leaf)) {
            throw "Missing release package: $output"
        }
    }
    Get-Item -LiteralPath $outputs | Select-Object Name, Length, LastWriteTime
} finally {
    $env:DOTNET_CLI_HOME = $previousDotnetHome
    $env:NUGET_PACKAGES = $previousNugetPackages
    $env:NUGET_HTTP_CACHE_PATH = $previousNugetHttpCache
    if (Test-Path -LiteralPath $workingRoot) {
        Assert-ChildPath $artifactRoot $workingRoot
        Remove-Item -LiteralPath $workingRoot -Recurse -Force
    }
}
