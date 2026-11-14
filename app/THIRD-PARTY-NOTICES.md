# Components

Media Harbor is independently written from scratch using WPF.

- CommunityToolkit.Mvvm 8.4.0, Microsoft / .NET Foundation contributors, MIT: https://github.com/CommunityToolkit/dotnet/blob/main/License.md
- yt-dlp 2026.10.26 executable, yt-dlp contributors. The development script downloads the pinned [release](https://github.com/yt-dlp/yt-dlp/releases/tag/2026.10.26). Standalone binary distributions include third-party code with their own licenses. See the [license](https://github.com/yt-dlp/yt-dlp/blob/2026.10.26/LICENSE) and [third-party license details](https://github.com/yt-dlp/yt-dlp/tree/2026.10.26#license).
- FFmpeg 8.1.2 essentials build by gyan.dev, FFmpeg developers and contributors. This binary reports --enable-gpl --enable-version3. Source, applicable GPLv3 terms and build information: https://www.gyan.dev/ffmpeg/builds/ and https://ffmpeg.org/legal.html. FFmpeg is invoked as a separate process. See tools/LICENSE-FFmpeg.txt for GPLv3.

Before public binary redistribution, provide the corresponding source for the exact FFmpeg build and satisfy all bundled component notices.
