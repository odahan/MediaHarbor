# MediaHarbor

MediaHarbor is a Windows desktop application for downloading YouTube media and converting local media files.

## Download for Windows x64

Download `MediaHarbor.exe` from the [latest release](https://github.com/odahan/MediaHarbor/releases/latest). Run it from a folder where you can save files. On first launch, allow MediaHarbor to download FFmpeg and yt-dlp directly from their publishers. An internet connection is required for this setup.

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

The script writes `Publish/MediaHarbor.exe` at the repository root. The script also writes Publish/MediaHarbor-licenses.zip for the release page. The executable includes the .NET Desktop Runtime and extracts the guide and licensing documents beside the executable. **About → Help** opens the local guide.

Keep executable files, compiled libraries, build outputs such as `bin` and `obj`, and distribution archives out of Git. Distribute binary packages separately through GitHub Releases. See [third-party notices](app/THIRD-PARTY-NOTICES.md) for the bundled tools and their licenses.


## License

Copyright (c) 2026 Olivier Dahan. MediaHarbor is licensed under
GPL-3.0-or-later. See [LICENSE](LICENSE) and the [license notice](app/licenses/MediaHarbor-NOTICE.txt).
The source and build scripts for the Windows executable are available at its
matching GitHub release tag.
