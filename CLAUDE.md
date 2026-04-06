# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

KawaPlayer is a VRChat video player (YamaPlayer fork) with PlaylistLoader for loading playlists from playlist.vrc-hub.com. Package name: `com.vhub.kawaplayer`.

- **Unity version**: 2022.3
- **Language**: C# / UdonSharp (VRChat's Udon scripting layer)
- **Dependencies**: VRChat Worlds SDK (>=3.8.1), Newtonsoft.Json (3.2.1)

## Build & Release

There is no local build command — the project is opened and compiled in the Unity Editor with VRChat SDK installed. Releases are created via a manual GitHub Actions workflow (`.github/workflows/release.yml`) that packages into ZIP and UnityPackage formats and publishes to the VPM repository at `https://vpm.kwxxw.net/`.

## Architecture

### Core Runtime (`Runtime/Internal/`)

**Controller** (`Controller.cs` + partial files) — Central UdonSharpBehaviour managing:
- Playback state machine (Idle, Playing, Paused) with network sync via `[UdonSynced]` fields
- Multiple video player handlers with automatic fallback on error
- Owner-based authority model (VRChat networking)

**Handler System** (`Handlers/`) — Abstraction over video backends:
- `PlayerHandler` — abstract base
- `BaseVideoPlayerHandler` — wraps VRC's built-in video players
- `ImageViewerHandler` — static image display

**UI System** (`UI/UIController.cs`) — Manages all interactive controls (play, pause, seek, volume, speed, playlists, modals). Heavy use of serialized fields for Unity inspector binding.

**Playlist System** (`Playlist/`) — `PlaylistManager` coordinates `Playlist`, `QueueList`, and `HistoryList` components.

### Public API (`Runtime/Components/`)

Entry point components that world creators place in their scenes:
- `YamaPlayer` — main component, references PlaylistManager and ModuleManager
- `YamaPlayerScreen`, `YamaPlayerSpeaker`, `YamaPlayerSubController`

### Extension Pattern

`YamaPlayerListener` (base class) provides virtual callback methods (AfterVideoReady, AfterVideoStarted, AfterVolumeChanged, etc.) that modules and custom scripts override to react to player events.

### Modules (`Modules/`)

Each module is an independent assembly with its own `.asmdef`:
- AudioLinkAdaptor, AutoPlay, LTCGIAdaptor, LightVolumeAdaptor
- PermissionManagement, Persistence, PitchShifter, SlideShower
- TimelineSync, VideoInfoDownloader

Modules extend the player via the listener pattern and are optional dependencies.

### Editor Tools (`Editor/`)

Custom inspectors, build processors, menu items, and the module/localization editors. Separate assembly (`Yamadev.YamaStream.Editor.asmdef`) referencing the runtime assembly.

### Assembly Definitions

- `Yamadev.YamaStream.Runtime` — core runtime
- `Yamadev.YamaStream.Editor` — editor tooling
- Each module has its own runtime and editor asmdef

## GitHub Operations

This repository is a fork of `koorimizuw/YamaPlayer` (upstream). When using `gh` CLI commands (issue, PR, etc.), **always** specify `--repo Mega-Gorilla/KawaPlayer` explicitly. Never create issues, PRs, or comments on the upstream repository (`koorimizuw/YamaPlayer`).

## Key Constraints

- All runtime scripts must be valid UdonSharp (subset of C#). Many standard C# features are unavailable (no generics on UdonSharpBehaviour, limited reflection, no async/await, etc.).
- Network sync uses `[UdonSynced]` fields with manual sync (`UdonBehaviourSyncMode.Manual`). Only the owner can modify synced variables.
- The project is primarily documented in Japanese. README and UI localization files contain Japanese as the primary language.
