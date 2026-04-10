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
    <td><img width="560"alt="macos" src="https://github.com/user-attachments/assets/bf4c5eb0-557f-4b63-bbb2-2b97d3a705de" /></td>
    <td><img width="560"alt="macos" src="https://github.com/user-attachments/assets/3c0d223c-20db-4f0f-bbf1-9bdc11479e4b" /></td>
  </tr>
</table>

## Features

Legend: `✅` fully supported, `🟡` partially supported, `❌` not yet supported

#### Game Management
| Capability | Status | Notes |
|---|---|---|
| Multi-instance Minecraft profiles | ✅ | Multiple game profiles are persisted and managed. |
| Custom profile folder names | ✅ | You can add more than one game into the same folder. |
| Version selection | ✅ | Supports all versions provided by Mojang |
| Global Minecraft settings | ✅ | File-backed global settings with autosave behavior. |
| Per-game settings overrides | ✅ | Profile-level override model is implemented. |
#### Mod Loaders  
| Capability | Status | Notes |
|---|---|---|
| Vanilla | ✅ | Supported in launcher flow. |
| Fabric | ✅ | Routed via installer pipeline. |
| Forge | ✅ | Routed via installer pipeline. |
| Quilt | ✅ | Routed via installer pipeline. |
| OptiFine | 🟡 | Routed via installer pipeline. WIP |
| LiteLoader | ✅ | Routed via installer pipeline. |
#### Runtime Info
| Capability | Status | Notes |
|---|---|---|
| Download Minecraft | ✅ | Downloads through offical Mojang servers, hash check available. |
| Launch Minecraft | ✅ | --- |
| Stop / force stop running game | ✅ |---|
| Runtime session tracking | ✅ | Tracks state, PID, timestamps, and run status. |
| Live game logs | ✅ | Captures runtime output with a rich UI |
#### Sign-in Methods
| Capability | Status | Notes |
|---|---|---|
| Microsoft account sign-in | ✅ | --- |
| Offline account support | ✅ | * |
>  We do not support piracy. You can't use Offline Accounts without logging in with at least one Microsoft Account.  
> If you wish to bypass it, it's your responsibility, and a toggle which you can change is hardcoded the project. But we will not ship any releases with it.
#### Store Support
| Capability | Status | Notes |
|---|---|---|
| Mods | ✅ |---|
| Shaders | ✅ |---|
| Data Packs | ✅ |---|
| Resource Packs | ✅ |---|
| Plugins | ✅ |---|
| Mods | 🟡 |WIP|
| Modrinth content browsing/details | 🟡 | Present, Does not exactly match/auto download versions |
| Modrinth install/remove tracking | 🟡 | Install/remove tracking exists; UX/workflow still evolving. |

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


## Contributing

- [Open an issue](https://github.com/RiversideValley/Emerald/issues/new/choose)
- [Submit a pull request](https://github.com/RiversideValley/Emerald/pulls)
- [Review recent commits](https://github.com/RiversideValley/Emerald/commits)

## License

Copyright (c) 2022-2026 Riverside Valley Corporation

Licensed under the Nightshade Vexillum license as stated in [LICENSE.md](LICENSE.md).
