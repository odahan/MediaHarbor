[CmdletBinding()]
param([switch]$FromExplorer)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'app/MediaHarbor.csproj'
$publishDirectory = Join-Path $PSScriptRoot 'Publish'
$distribution = Join-Path $publishDirectory 'MediaHarbor.exe'
$noticeArchive = Join-Path $publishDirectory 'MediaHarbor-licenses.zip'
$stagingDirectory = Join-Path $publishDirectory ('.staging-' + [guid]::NewGuid().ToString('N'))

Push-Location -LiteralPath $PSScriptRoot
try {
    # WPF's mixed-mode DirectWriteForwarder assembly must be loaded from disk.
    dotnet publish $project -c Release -r win-x64 --self-contained true -m:1 `
        -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true `
        -p:RuntimeFrameworkVersion=11.0.0 `
        -p:EnableCompressionInSingleFile=true -p:DebugType=embedded -o $stagingDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }

    $output = @(Get-ChildItem -LiteralPath $stagingDirectory -Force)
    if ($output.Count -ne 1 -or $output[0].PSIsContainer -or $output[0].Name -ne 'MediaHarbor.exe') {
        throw 'The publication must contain exactly one file named MediaHarbor.exe.'
    }

    $noticeDirectory = Join-Path $stagingDirectory 'licenses'
    New-Item -ItemType Directory -Path $noticeDirectory | Out-Null
    Copy-Item -LiteralPath 'LICENSE','app/THIRD-PARTY-NOTICES.md' -Destination $noticeDirectory
    Copy-Item -Path 'app/licenses/*.txt' -Destination $noticeDirectory
    $stagedNoticeArchive = Join-Path $stagingDirectory 'MediaHarbor-licenses.zip'
    Compress-Archive -Path (Join-Path $noticeDirectory '*') -DestinationPath $stagedNoticeArchive
    Move-Item -LiteralPath $stagedNoticeArchive -Destination $noticeArchive -Force
    Move-Item -LiteralPath $output[0].FullName -Destination $distribution -Force
    Write-Output "Published $distribution"
    Write-Output "Published $noticeArchive"
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
