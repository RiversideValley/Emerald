![Emerald Banner](https://github.com/user-attachments/assets/dbe4839c-eddf-49fa-97cc-edbd70b3d81f)

# Emerald

Emerald is a cross-platform Minecraft launcher built with Uno Platform and C#.

[![CI Build & Artifacts](https://github.com/RiversideValley/Emerald/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/RiversideValley/Emerald/actions/workflows/ci.yml)
[![GitHub Releases](https://img.shields.io/github/v/release/RiversideValley/Emerald?include_prereleases)](https://github.com/RiversideValley/Emerald/releases)
[![Platform](https://img.shields.io/badge/platform-windows%20%7C%20macOS%20%7C%20linux-2ea44f)](https://github.com/RiversideValley/Emerald)
[![Language](https://img.shields.io/badge/language-c%23-239120)](https://github.com/RiversideValley/Emerald/search?l=c%23)

## Screenshots
<table>
  <tr>
    <td><img width="560" alt="windows" src="https://github.com/user-attachments/assets/e5eacfdf-17f0-409f-845a-390e3adad31e" /></td>
    <td><img width="560"alt="windows"  src="https://github.com/user-attachments/assets/af406e42-5330-4f03-8ddb-139c0115222f" /></td>
  </tr>
  <tr>
    <td><img width="560"alt="macos" src="https://github.com/user-attachments/assets/f11f80ba-69e2-4c0a-90c8-4f57973a9fd3" /></td>
    <td><img width="560"alt="macos" src="https://github.com/user-attachments/assets/a4422d48-c95e-4923-996c-ba0839b764f2" /></td>
  </tr>
</table>

## Features

Legend: `✅` fully supported, `🟡` partially supported, `❌` not yet supported

#### Game Management
| Capability | Status | Notes |
|---|---|---|
| Multi-instance Minecraft profiles | ✅ | Multiple game profiles are persisted and managed. |
| Custom profile folder names | ✅ | You can add more than one game into the same folder. |
| Version selection | ✅ | Supports all versions provided by Mojang. |
| Global Minecraft settings | ✅ | File-backed global settings with autosave behavior. |
| Per-game settings overrides | ✅ | Profile-level override model is implemented. |

#### Mod Loaders  
| Capability | Status | Notes |
|---|---|---|
| Vanilla | ✅ | Supported in launcher flow. |
| Fabric | ✅ | Routed via installer pipeline. |
| Forge | ✅ | Routed via installer pipeline. |
| NeoForge | ✅ | Routed via installer pipeline. |
| Quilt | ✅ | Routed via installer pipeline. |
| OptiFine | 🟡 | Routed via installer pipeline. WIP. |
| LiteLoader | ✅ | Routed via installer pipeline. |

#### Sign-in Methods
| Capability | Status | Notes |
|---|---|---|
| Microsoft account sign-in | ✅ | --- |
| Offline account support | ✅ | *** |
| [Ely.by](https://ely.by) account support | ✅ | *** |

> *** We do not encourage you to use other login methods without having a legal copy of Minecraft.

#### Store Support
| Capability | Status | Notes |
|---|---|---|
| Mods | ✅ |---|
| Shaders | ✅ |---|
| Data Packs | ✅ |---|
| Resource Packs | ✅ |---|
| Plugins | ✅ |---|
| Modpacks | ✅ | Full support for Modrinth .mrpack files. |
| Modrinth content browsing/details | 🟡 | Present, Does not exactly match/auto download versions. |
| Modrinth install/remove tracking | 🟡 | Install/remove tracking exists; UX/workflow still evolving. |

## Releases

Emerald is available for Windows, macOS, and Linux. 

### Stable Releases
You can download the latest stable release from [GitHub Releases](https://github.com/RiversideValley/Emerald/releases). See the Installation instructions below for platform-specific setup.

### Nightly Builds (GitHub Actions)
Nightly preview builds are distributed through GitHub Actions CI artifacts.
1. Open [CI Build & Artifacts](https://github.com/RiversideValley/Emerald/actions/workflows/ci.yml).
2. Select a recent successful run on `main`.
3. Download the artifact for your platform.

## Installation

### Windows
Emerald is distributed as a signed MSIX package. If this is your **first time** installing Emerald, you **must** trust the signing certificate:
1. Download the `.cer` certificate file from the release assets.
2. Right-click the `.cer` file and select **Install Certificate**.
3. Choose **Local Machine**, place it in **Trusted People** (If installation didnt work then try **Trusted Root Certification Authorities** ), and confirm.
4. Once trusted, you can download and run the `.appinstaller` file for automatic updates, or download the offline `.msix`/`.appxbundle` for your specific architecture.

### macOS
The macOS build is currently **not notarized**. Gatekeeper will block it by default. To run Emerald, you must remove the quarantine attribute for every new binary you download. Open Terminal and run:
```bash
xattr -cr /path/to/Emerald.app
```
### Linux
Emerald is distributed as a Snap package (x86_64 and ~~arm64~~). Currently, the .snap file needs to be installed in classic dangerous mode. Run the following command in your terminal:
Bash
```
sudo snap install ./emerald_*.snap --dangerous --classic
```

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

### Windows Signed Packaging

Windows packaged builds now use signed MSIX output.

- Setup and local publish instructions: [docs/windows-signing.md](docs/windows-signing.md)

## Codebase Structure

Maintained solution (`Emerald.slnx`):

- `Emerald/` - Uno app shell, UI, pages, controls, and composition root
- `Emerald.CoreX/` - launcher domain/runtime/services (accounts, installers, logging, settings)
- `Emerald.CoreX.Tests/` - active CoreX regression tests

Legacy projects (`Emerald.App/`, `Emerald.Core/`) are retained for history and are not the primary active architecture.


## Contributing

- [Open an issue](https://github.com/RiversideValley/Emerald/issues/new/choose)
- [Submit a pull request](https://github.com/RiversideValley/Emerald/pulls)
- [Review recent commits](https://github.com/RiversideValley/Emerald/commits)

## License

Copyright (c) 2022-2026 Riverside Valley Corporation

Licensed under the Nightshade Vexillum license as stated in [LICENSE.md](LICENSE.md).
