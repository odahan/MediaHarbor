[CmdletBinding()]
param([switch]$FromExplorer)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'app/MediaHarbor.csproj'
$publishDirectory = Join-Path $PSScriptRoot 'Publish'
$distribution = Join-Path $publishDirectory 'MediaHarbor.exe'
$stagingDirectory = Join-Path $publishDirectory ('.staging-' + [guid]::NewGuid().ToString('N'))

Push-Location -LiteralPath $PSScriptRoot
try {
    $tools = Join-Path $PSScriptRoot 'app/tools'
    if (!(Test-Path -LiteralPath (Join-Path $tools 'ffmpeg.exe') -PathType Leaf) -or
        !(Test-Path -LiteralPath (Join-Path $tools 'yt-dlp.exe') -PathType Leaf)) {
        & (Join-Path $tools 'Restore-Tools.ps1')
    }

    # WPF's mixed-mode DirectWriteForwarder assembly must be loaded from disk.
    dotnet publish $project -c Release -r win-x64 --self-contained true -m:1 `
        -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true -p:DebugType=embedded -o $stagingDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }

    $output = @(Get-ChildItem -LiteralPath $stagingDirectory -Force)
    if ($output.Count -ne 1 -or $output[0].PSIsContainer -or $output[0].Name -ne 'MediaHarbor.exe') {
        throw 'The publication must contain exactly one file named MediaHarbor.exe.'
    }

    Move-Item -LiteralPath $output[0].FullName -Destination $distribution -Force
    Write-Output "Published $distribution"
    if ($FromExplorer) {
        Add-Type -AssemblyName PresentationFramework
        [void][System.Windows.MessageBox]::Show("Published $distribution", 'MediaHarbor Publish')
    }
}
catch {
    if ($FromExplorer) {
        Add-Type -AssemblyName PresentationFramework
        [void][System.Windows.MessageBox]::Show($_.Exception.Message, 'MediaHarbor Publish', 'OK', 'Error')
    }
    throw
}
finally {
    Pop-Location
    if (Test-Path -LiteralPath $stagingDirectory -PathType Container) {
        $resolvedStaging = (Resolve-Path -LiteralPath $stagingDirectory).ProviderPath
        if ($resolvedStaging -eq $stagingDirectory) {
            Remove-Item -LiteralPath $resolvedStaging -Recurse -Force
        }
    }
}
