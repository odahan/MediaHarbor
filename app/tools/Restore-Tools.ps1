# Downloads the pinned Windows tools used for local development.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

function Receive-VerifiedFile {
    param([string]$Uri, [string]$Path, [string]$Sha256)

    Invoke-WebRequest -UseBasicParsing -Uri $Uri -OutFile $Path
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Sha256) {
        throw "SHA-256 verification failed for $Uri."
    }
}

$temporaryPrefix = Join-Path $PSScriptRoot ('.restore-' + [guid]::NewGuid().ToString('N'))
$archivePath = "$temporaryPrefix.zip"
$ffmpegPath = "$temporaryPrefix.ffmpeg.exe"
$ytdlpPath = "$temporaryPrefix.yt-dlp.exe"

try {
    Receive-VerifiedFile -Uri 'https://github.com/GyanD/codexffmpeg/releases/download/8.1.2/ffmpeg-8.1.2-essentials_build.zip' -Path $archivePath -Sha256 'db580001caa24ac104c8cb856cd113a87b0a443f7bdf47d8c12b1d740584a2ec'
    Receive-VerifiedFile -Uri 'https://github.com/yt-dlp/yt-dlp/releases/download/2026.10.26/yt-dlp.exe' -Path $ytdlpPath -Sha256 'f5389d3698ecde4ef06f4e6b1ce0294fe9ac1396228c8990b8d3416d6d177a7d'

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        $entry = $archive.GetEntry('ffmpeg-8.1.2-essentials_build/bin/ffmpeg.exe')
        if ($null -eq $entry) { throw 'ffmpeg.exe was not found in the archive.' }
        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $ffmpegPath)
    }
    finally {
        $archive.Dispose()
    }

    Move-Item -LiteralPath $ffmpegPath -Destination (Join-Path $PSScriptRoot 'ffmpeg.exe') -Force
    Move-Item -LiteralPath $ytdlpPath -Destination (Join-Path $PSScriptRoot 'yt-dlp.exe') -Force
}
finally {
    foreach ($path in @($archivePath, $ffmpegPath, $ytdlpPath)) {
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
    }
}
