# MediaHarbor

MediaHarbor is a Windows desktop application for downloading YouTube media and converting local media files.

## Build from source

From the repository root on 64-bit Windows, use the .NET 11 SDK to build the solution and the .NET 8 Desktop Runtime to run the application and tests. Restore the pinned FFmpeg and yt-dlp binaries before building.

```powershell
.\app\tools\Restore-Tools.ps1
dotnet build app/MediaHarbor.slnx -m:1
dotnet test app/MediaHarbor.slnx -m:1
```

The script verifies SHA-256 checksums before placing the executables in `app/tools`. The application project copies the tools into its build output.

Keep executable files, compiled libraries, build outputs such as `bin` and `obj`, and distribution archives out of Git. Distribute binary packages separately through GitHub Releases. See [third-party notices](app/THIRD-PARTY-NOTICES.md) for the bundled tools and their licenses.
