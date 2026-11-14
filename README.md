# MediaHarbor

MediaHarbor is a Windows desktop application for downloading YouTube media and converting local media files.

## Build prerequisites

- Windows x64 and PowerShell.
- The .NET 11 SDK. [`global.json`](global.json) selects `11.0.100` or a later stable patch in the `11.0.1xx` SDK feature band. Run `dotnet --version` from the repository root to check the selected SDK.
- An internet connection for the initial NuGet restore and to download FFmpeg and yt-dlp.

## Build and test from source

Run these commands in PowerShell from the repository root.

```powershell
.\app\tools\Restore-Tools.ps1
dotnet build .\app\MediaHarbor.slnx -c Release -m:1
dotnet test .\app\MediaHarbor.slnx -c Release -m:1 --no-build
```

`Restore-Tools.ps1` verifies SHA-256 checksums before placing the pinned executables in `app/tools`. The build restores NuGet packages and copies the tools into the application output. The test command uses the Release binaries from that build. Keep `-m:1` on build and test commands to limit MSBuild to one worker.

To run the local build with the .NET 11 Desktop Runtime, use:

```powershell
.\app\bin\Release\net11.0-windows\MediaHarbor.exe
```

## Publish the standalone executable

Run the following command or double-click `Publish.BAT` to create the self-contained Windows x64 distribution.

```powershell
.\Publish.ps1
```

The script downloads the pinned tools if either is missing and writes `Publish/MediaHarbor.exe` at the repository root. This single executable includes the .NET 11 Desktop Runtime. Run it from a writable folder. The app places the HTML guide and third-party notices beside the executable and extracts missing copies of FFmpeg, yt-dlp and their licenses into `tools`. It automatically replaces a local guide that differs from the bundled version. **About → Help** opens the local guide in English by default.

Keep executable files, compiled libraries, build outputs such as `bin` and `obj`, and distribution archives out of Git. Distribute binary packages separately through GitHub Releases. See [third-party notices](app/THIRD-PARTY-NOTICES.md) for the bundled tools and their licenses.
