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

| Area | Capability | Status | Notes |
|---|---|---|---|
| Platform | Cross-platform Uno desktop shell | ✅ Supported | Maintained in `Emerald/` with Uno + WinUI-style UI. |
| Platform | Windows artifact output | ✅ Supported | CI publishes unsigned packaged `.msixbundle` (x64 + arm64). |
| Platform | macOS artifact output | ✅ Supported | CI publishes arm64 `.app` zip (unsigned). |
| Platform | Linux artifact output | ✅ Supported | CI publishes x64 package archive. |
| Game Management | Multi-instance profiles | ✅ Supported | Multiple game profiles are persisted and managed. |
| Game Management | Custom profile folder names | ✅ Supported | Add-game flow supports custom folder names. |
| Game Management | Add game wizard flow | ✅ Supported | Version + loader + profile configuration steps. |
| Game Management | Vanilla version refresh | ✅ Supported | Pulls available vanilla versions from launcher backend. |
| Game Management | Install selected profile version | ✅ Supported | Per-profile install workflow is available. |
| Game Management | Remove profile | ✅ Supported | Profiles can be removed from managed list. |
| Runtime | Launch game | ✅ Supported | Runtime launch goes through `IGameRuntimeService`. |
| Runtime | Gentle stop | ✅ Supported | Graceful stop path is implemented. |
| Runtime | Force stop | ✅ Supported | Force termination path is implemented. |
| Runtime | Active session tracking | ✅ Supported | Tracks state, PID, timestamps, and run status. |
| Logs | Live log capture | ✅ Supported | Captures runtime output when stream access is available. |
| Logs | Lifecycle fallback mode | ✅ Supported | Falls back to lifecycle-only logs when needed. |
| Logs | Search logs | ✅ Supported | Search query filtering on log projection. |
| Logs | Level filter | ✅ Supported | Filter by Trace/Debug/Info/Warn/Error/Fatal/Unknown. |
| Logs | Pagination | ✅ Supported | Page-size options with previous/next navigation. |
| Logs | Session-based history | ✅ Supported | Logs are grouped by runtime sessions. |
| Accounts | Microsoft account sign-in | ✅ Supported | Microsoft authentication flow is available. |
| Accounts | Offline accounts | ✅ Supported | Offline account creation and launch are supported. |
| Accounts | Selected account for launch | ✅ Supported | Selected account is used for launch flow. |
| Accounts | Selected account persistence | ✅ Supported | Selected account state is persisted. |
| Settings | Global Minecraft settings | ✅ Supported | Global settings are file-backed and autosaved. |
| Settings | Per-game settings overrides | ✅ Supported | Profile-level override model is implemented. |
| Settings | JVM argument settings | ✅ Supported | JVM args are part of managed game settings. |
| Settings | Theme/appearance customization | ✅ Supported | Theme + tint customization is available. |
| Loader Support | Vanilla loader | ✅ Supported | Supported in launcher flow. |
| Loader Support | Fabric loader | ✅ Supported | Routed via installer pipeline. |
| Loader Support | Forge loader | ✅ Supported | Routed via installer pipeline. |
| Loader Support | Quilt loader | ✅ Supported | Routed via installer pipeline. |
| Loader Support | OptiFine loader | ✅ Supported | Routed via installer pipeline. |
| Loader Support | LiteLoader | ✅ Supported | Routed via installer pipeline. |
| Store | Modrinth discovery and detail flow | 🟡 Partial | Present, but store surface is still under active refinement. |
| Store | Modrinth install/remove tracking | 🟡 Partial | Install/remove tracking exists; UX/workflow still evolving. |
| Distribution | Nightly GitHub Actions builds | ✅ Supported | Artifacts are published by CI workflow runs. |
| Distribution | Stable cross-platform release channel | ❌ Not Yet | Stable releases for the new cross-platform era are coming soon. |
| Distribution | Signed/notarized desktop binaries | ❌ Not Yet | Current CI outputs are unsigned artifacts. |

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
