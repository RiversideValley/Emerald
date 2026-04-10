![Emerald Banner](https://github.com/user-attachments/assets/dbe4839c-eddf-49fa-97cc-edbd70b3d81f)

# Emerald

Emerald is a cross-platform Minecraft launcher built with Uno Platform and C#.

[![CI Build & Artifacts](https://github.com/RiversideValley/Emerald/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/RiversideValley/Emerald/actions/workflows/ci.yml)
[![GitHub Releases](https://img.shields.io/github/v/release/RiversideValley/Emerald?include_prereleases)](https://github.com/RiversideValley/Emerald/releases)
[![Platform](https://img.shields.io/badge/platform-windows%20%7C%20macOS%20%7C%20linux-2ea44f)](https://github.com/RiversideValley/Emerald)
[![Language](https://img.shields.io/badge/language-c%23-239120)](https://github.com/RiversideValley/Emerald/search?l=c%23)

## Platform Support

- Windows 10/11: packaged unsigned `.msixbundle` (x64 + arm64)
- macOS: unsigned arm64 `.app` archive
- Linux: x64 package archive

## Features

- Multi-instance game management with custom profile folders
- Version selection flow with release-type filtering
- Mod loader routing and installation support for:
  - Vanilla
  - Fabric
  - Forge
  - Quilt
  - OptiFine
  - LiteLoader
- Runtime session controls:
  - Launch
  - Gentle stop / force stop
  - Session tracking with status and PID visibility
- Built-in log viewer:
  - Live capture
  - Search + level filters
  - Pagination
  - Session-based history
- Account management:
  - Microsoft account sign-in
  - Offline account support
  - Select active account for launch
- Global and per-game Minecraft settings with override support
- Modrinth-backed store workflows (browse, install, and manage tracked content)

## Releases

Emerald is currently in its cross-platform Uno transition phase.

### Nightly Builds (GitHub Actions)

Nightly preview builds are distributed through GitHub Actions CI artifacts.

1. Open [CI Build & Artifacts](https://github.com/RiversideValley/Emerald/actions/workflows/ci.yml).
2. Select a recent successful run on `main`.
3. Download the artifact for your platform:
   - `Emerald-Windows-Unsigned-x64-arm64`
   - `Emerald-macOS-arm64-app`
   - `Emerald-linux-x64`

### Stable Releases

Stable releases for the new cross-platform version will be available soon.

In the meantime, you can use nightly CI artifacts for the latest changes, or older tagged releases from [GitHub Releases](https://github.com/RiversideValley/Emerald/releases).

## Building From Source

### Prerequisites

- [Git](https://git-scm.com/)
- [.NET 10 SDK](https://dotnet.microsoft.com/) (preview channel supported)

Optional for local Windows packaging workflows:

- Visual Studio 2022 / Build Tools with Windows SDK components

### Clone

```bash
git clone https://github.com/RiversideValley/Emerald
cd Emerald
```

### Build

```bash
dotnet restore Emerald.slnx
dotnet build Emerald.slnx
```

### Tests

```bash
dotnet test Emerald.CoreX.Tests/Emerald.CoreX.Tests.csproj
```

## Codebase Structure

Maintained solution (`Emerald.slnx`):

- `Emerald/` - Uno app shell, UI, pages, controls, and composition root
- `Emerald.CoreX/` - launcher domain/runtime/services (accounts, installers, logging, settings)
- `Emerald.CoreX.Tests/` - active CoreX regression tests

Legacy projects (`Emerald.App/`, `Emerald.Core/`) are retained for history and are not the primary active architecture.

## Screenshots

[![emerald-screenshot](https://github.com/user-attachments/assets/eb65ec6e-3dce-46a9-8f0a-1ffaf9dc43c3)](https://github.com/RiversideValley/Emerald)

## Contributing

- [Open an issue](https://github.com/RiversideValley/Emerald/issues/new/choose)
- [Submit a pull request](https://github.com/RiversideValley/Emerald/pulls)
- [Review recent commits](https://github.com/RiversideValley/Emerald/commits)

## License

Copyright (c) 2022-2026 Riverside Valley Corporation

Licensed under the Nightshade Vexillum license as stated in [LICENSE.md](LICENSE.md).
