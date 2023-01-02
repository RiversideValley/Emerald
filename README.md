# 💎 Emerald

#### A swift Minecraft launcher made using WinUI technologies in the fast C# language really pushes the boundary of the platform.

<p align="center">
  <a title="GitHub Releases" target="_blank" href="https://github.com/OpenAndrexial/Emerald/releases">
    <img align="left" src="https://img.shields.io/github/v/release/OpenAndrexial/Emerald?include_prereleases" alt="Release" />
  </a>
  <a title="GitHub Releases" target="_blank" href="https://github.com/OpenAndrexial/Emerald/releases">
    <img align="left" src="https://img.shields.io/github/downloads/SeaDevTeam/SDLauncher/total" alt="Release" />
  </a>
  <a title="GitHub Releases" target="_blank" href="https://github.com/OpenAndrexial/Emerald/releases">
    <img align="left" src="https://img.shields.io/github/repo-size/OpenAndrexial/Emerald?color=%23cc0000" alt="Release" />
  </a>
</p>

<br/>

---

## 🎁 Installation

### Via GitHub

See the [releases page](https://github.com/OpenAndrexial/Emerald/releases)

### Building from source
###### ⭐Recommended⭐

This is our preferred method.
See [this section](#-building-the-code)

### 📸 Screenshots

<a title="Emerald Screenshot" target="_blank" href="https://github.com/OpenAndrexial/Emerald">
  <img align="left" src="https://user-images.githubusercontent.com/82730163/210150183-fd324c12-5a90-4ffb-964d-c8ccae2c9cee.png" alt="Release" />
</a>

###### 📝 This screenshot is from [`redesign`](https://github.com/OpenAndrexial/Emerald/pull/19)

## 🦜 Contributing & Feedback

There are multiple ways to participate in the community:

- Upvote popular feature requests
- [Submit a new feature](https://github.com/DepthCDLS/Esmerelda/pulls)
- [File bugs and feature requests](https://github.com/OpenAndrexial/Emerald/issues/new/choose).
- Review source [code changes](https://github.com/OpenAndrexial/Emerald/commits)

### 🏗️ Codebase Structure

```
.
├──Emerald.App                       // Emerald app code and packager
|  ├──Emerald.App                    // Emerald app code (such as code related to UI but not Minecraft)
|  └──Emerald.App.Package            // Package code for generating an uploadable MSIX bundle.
└──Emerald.Core                      // Emerald core code (such as code related to launching and modifying Minecraft
```

### 🗃️ Contributors

<a href="https://github.com/OpenAndrexial/Emerald/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=OpenAndrexial/Emerald" />
</a>

## 🔨 Building the Code

##### 1. Prerequisites

Ensure you have following components:

- [Git](https://git-scm.com/)
- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) with following individual components:
  - Universal Windows Platform Software Development Kit
  - .NET 6+
  - Windows App Software Development Kit
  - Windows 11 SDK
- [Windows 11 or Windows 10](https://www.microsoft.com/en-us/windows) (version 1809+)
- [.NET Core Desktop Runtime 3.1](https://dotnet.microsoft.com/en-us/download/dotnet/3.1)
- At least 4gb of RAM
- [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)

### 2. Git

Clone the repository:

```git
git clone https://github.com/OpenAndrexial/Emerald
```

### 4. Build the project

- Open `Emerald.sln`.
- Set the Startup Project to `Emerald.Package`
- Build with `DEBUG|x64` (or `DEBUG|Any CPU`)

## ⚖️ License

Copyright (c) 2022 Depth

Licensed under the MIT license as stated in the [LICENSE](LICENSE.md).
