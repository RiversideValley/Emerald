![Emerald Banner](https://github.com/user-attachments/assets/dbe4839c-eddf-49fa-97cc-edbd70b3d81f)

#### A swift Minecraft launcher made using WinUI technologies in the fast C# language that really pushes the boundary of the platform.

<p align="center">
  <a title="GitHub Releases" target="_blank" href="https://github.com/RiversideValley/Emerald/releases">
    <img align="left" src="https://img.shields.io/github/v/release/RiversideValley/Emerald?include_prereleases" alt="Release" />
  </a>
  <a title="Repository Size" target="_blank" href="https://github.com/RiversideValley/Emerald/activity">
    <img align="left" src="https://img.shields.io/github/repo-size/RiversideValley/Emerald?color=%23cc0000" alt="Release" />
  </a>
  <a title="Platform" target="_blank" href="https://github.com/topics/windows">
    <img align="left" src="https://img.shields.io/badge/platform-windows-purple" alt="Platform" />
  </a>
  <a title="Language" target="_blank" href="https://github.com/RiversideValley/Emerald/search?l=c%23">
    <img align="left" src="https://img.shields.io/badge/language-c_sharp-green" alt="Platform" />
  </a>
</p>

<br/>

---

## 🎁 Installation

<!--### 🪟 Microsoft Store

<a title="Microsoft Store" href="https://apps.microsoft.com/store/detail/9PPC02GP33FT">
  <img src="https://user-images.githubusercontent.com/76810494/189479518-fc0f18a9-b0a4-4a63-8e7b-27a4284d93af.png" alt="Release" />
</a>-->

### 😺 GitHub

<a title="GitHub" href='https://github.com/RiversideValley/Emerald/releases/latest'>
  <img src='https://user-images.githubusercontent.com/74561130/160255105-5e32f911-574f-4cc4-b90b-8769099086e4.png'alt='Get it from GitHub' />
</a>

### 🔨 Building from source
###### ⭐Recommended⭐

This is our preferred method.
See [this section](#-building-the-code)

### 📸 Screenshots

[![emerald-screenshot](https://github.com/user-attachments/assets/eb65ec6e-3dce-46a9-8f0a-1ffaf9dc43c3)](https://github.com/RiversideValley/Emerald)

## 🦜 Contributing & Feedback

There are multiple ways to participate in the community:

- Upvote popular feature requests
- [Submit a new feature](https://github.com/RiversideValley/Emerald/pulls)
- [File bugs and feature requests](https://github.com/RiversideValley/Emerald/issues/new/choose).
- Review source [code changes](https://github.com/RiversideValley/Emerald/commits)

### 🏗️ Codebase Structure

```
.
├──Emerald.App                       // Emerald app code and packager
|  ├──Emerald.App                    // Emerald app code (such as code related to UI but not Minecraft)
|  └──Emerald.App.Package            // Package code for generating an uploadable MSIX bundle.
└──Emerald.Core                      // Emerald core code (such as code related to launching and modifying Minecraft
```

## 🔨 Building the Code

### 1️⃣ Prerequisites

Ensure you have following components:

- [Git](https://git-scm.com/)
- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) with following individual components:
  - Universal Windows Platform Software Development Kit
  - .NET 7
  - Windows App Software Development Kit
  - Windows 11 SDK
- [Windows 11 or Windows 10](https://www.microsoft.com/en-us/windows) (version 1809+)
- At least 4gb of RAM
- [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)

### 2️⃣ Git

Clone the repository:

```git
git clone https://github.com/RiversideValley/Emerald
```
(`main` is the latest branch)

### 3️⃣ Build the project

- Open `Emerald.sln`.
- Set the Startup Project to `Emerald.Package`
- Build with `DEBUG|x64` (or `DEBUG|Any CPU`)

## ⚖️ License

Copyright (c) 2022-2024 Riverside Valley Corporation

Licensed under the Nightshade Vexillum license as stated in the [LICENSE](LICENSE.md).
