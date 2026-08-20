# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

KawaPlayer is a fork of the VRChat video player [YamaPlayer](https://github.com/koorimizuw/YamaPlayer), adapted to integrate with the **VHub Playlist** service (`playlist.vrc-hub.com`). The purpose of the fork is to let world creators manage videos/playlists from inside VRChat: loading VHub playlists at runtime (PlaylistLoader module) and setting the world's auto-play default URL in-world (DefaultUrl module). All other player functionality is inherited from upstream YamaPlayer. Package name: `com.vhub.kawaplayer`.

- **Unity version**: 2022.3
- **Language**: C# / UdonSharp (VRChat's Udon scripting layer)
- **Dependencies**: VRChat Worlds SDK (>=3.8.1), Newtonsoft.Json (3.2.1)

## Build & Release

There is no local build command — the project is opened and compiled in the Unity Editor with VRChat SDK installed.

**Release workflow** (`.github/workflows/release.yml`, manual trigger):
1. Reads version from `package.json`
2. Packages into ZIP and UnityPackage formats
3. Creates a GitHub Release with both artifacts
4. Triggers `repository-dispatch` to `Mega-Gorilla/vpm-repos` to rebuild the VPM listing

**VPM distribution** (`Mega-Gorilla/vpm-repos`):
- Built from VRChat's `template-package-listing` template
- `source.json` references `Mega-Gorilla/KawaPlayer` in `githubRepos`
- GitHub Actions auto-generates `index.json` from GitHub Releases
- Published at `https://mega-gorilla.github.io/vpm-repos/index.json`
- Users add this URL in VCC (Settings > Packages > Add Repository) to install/update KawaPlayer

**Required secrets**: `PAT` — Fine-grained Personal Access Token with `contents: read and write` permission on `vpm-repos`. `read-only` では `repository-dispatch` が `Resource not accessible` エラーで失敗する。

**Version format**: 独立した SemVer (`Major.Minor.Patch`)。`1.0.0` から開始。
- KawaPlayer は独立パッケージのため、upstream YamaPlayer のバージョンには従わない
- upstream のベースバージョンは `Assets/updatelog.txt` とコミット履歴で追跡する
- SemVer の pre-release 識別子 (`-beta`, `-kawa` 等) を含むと VCC で「Show Pre-Release Packages」を ON にしないと表示されないため、正式リリースでは使用しない

**Release checklist**:
1. `package.json` の `version` を更新
2. `Assets/updatelog.txt` の先頭に新バージョンのエントリを追加（主要な変更のみ簡潔に。軽微な修正は省略可）
3. コミット・push
4. GitHub Actions の「Build Release」ワークフローを手動実行 (develop ブランチ)
5. GitHub Release 作成 → repository-dispatch → vpm-repos listing 再ビルドを確認

Published VPM versions must not be deleted (breaks projects using source control).

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

Each module is an independent assembly with its own `.asmdef`. Modules extend the player via the listener pattern and are optional dependencies.

KawaPlayer-specific modules (the reason this fork exists):
- **PlaylistLoader** — loads playlists from `playlist.vrc-hub.com` at runtime using the Pre-baked URL Pool pattern (see Key Constraints below). Design docs: `docs/design/url-pool-*.md`
- **DefaultUrl** — lets the Instance Owner set the world's auto-play video/playlist URL from inside VRChat, synced to all players and persisted across visits

Inherited from upstream YamaPlayer:
- AudioLinkAdaptor, AutoPlay, LTCGIAdaptor, LightVolumeAdaptor
- PermissionManagement, Persistence, PitchShifter, SlideShower
- TimelineSync, VideoInfoDownloader

### Prefabs

`KawaPlayer.prefab` at the repository root is the main all-in-one prefab that users drop into scenes (also reachable via the **GameObject > KawaPlayer > Main** menu). Additional prefabs (ControlBar, PlaylistPanel, SubScreen, UI parts) live in `Prefabs/`.

### Editor Tools (`Editor/`)

Custom inspectors, build processors, menu items, and the module/localization editors. Separate assembly (`Yamadev.YamaStream.Editor.asmdef`) referencing the runtime assembly.

### Assembly Definitions

- `Yamadev.YamaStream.Runtime` — core runtime
- `Yamadev.YamaStream.Editor` — editor tooling
- Each module has its own runtime and editor asmdef

## GitHub Operations

This repository is a fork of `koorimizuw/YamaPlayer` (upstream). When using `gh` CLI commands (issue, PR, etc.), **always** specify `--repo Mega-Gorilla/KawaPlayer` explicitly. Never create issues, PRs, or comments on the upstream repository (`koorimizuw/YamaPlayer`).

### Upstream Sync Tracking

- `.github/workflows/upstream-check.yml` runs monthly (and on manual dispatch), comparing upstream `develop` against the last reviewed SHA and commenting the unreviewed commit list on tracking issue #66.
- The last reviewed SHA lives in `.github/UPSTREAM_BASE` (first line). **Update this file in every upstream sync PR** — after adopting or deliberately skipping upstream commits, set it to the upstream SHA reviewed up to. `HEAD..upstream` counting is not used because cherry-picked/skipped commits would be misreported as unmerged.

## Testing Project

The testing project `kawa-player-playlist-testing-chamber` (`D:\vrchat\kawa-player-playlist-testing-chamber`) references KawaPlayer via `file:` path in `Packages/manifest.json`. This means Unity opens the package source directly — changes in this repository are reflected in the testing project on the next Unity refresh, with no copy step. It also references the sibling package `KawaPlayer_PlaylistViewer` (separate repository at `D:\Nextcloud\Vhub\VRChat_Player\KawaPlayer_PlaylistViewer`) the same way.

**Prefab Override vs Prefab Edit**: Changes made to KawaPlayer objects in the testing project's scene Hierarchy are stored as **Prefab Instance Overrides** in the scene file (`.unity`) only — they are NOT included in the KawaPlayer package. To include changes in the package, edit `KawaPlayer.prefab` directly (via Prefab Mode in Unity or text edit in this repository). Never rely on scene-level overrides for changes intended to ship with the package.

**`.meta` file regeneration**: Opening the testing project may cause Unity to regenerate `.meta` files in the KawaPlayer source directory with new GUIDs. This breaks asmdef cross-references and causes CS0246 compilation errors. If this happens, discard the changes with `git checkout -- .` in the KawaPlayer repository.

### Design & Analysis Docs (`docs/`)

Japanese-language docs explaining the playlist/URL-loading architecture. Read these before touching PlaylistLoader, DefaultUrl, or the playlist pipeline:
- `docs/analysis/` — how the playlist system works (Playlist/QueueList/HistoryList), URL-to-playback pipeline, and why runtime JSON playlists are impossible
- `docs/design/` — the URL Pool design (Unity side, loader side, server side)

## Key Constraints

- All runtime scripts must be valid UdonSharp (subset of C#). Many standard C# features are unavailable (no generics on UdonSharpBehaviour, limited reflection, no async/await, etc.).
- **`string → VRCUrl` conversion is impossible at runtime** (`new VRCUrl(string)` is editor-only). Any feature needing dynamic URLs must use the Pre-baked URL Pool pattern: a large `VRCUrl[]` of redirect-server slot URLs is baked into serialized fields at build time, and the server maps slots to real URLs via HTTP 302. See `docs/design/url-pool-playlist-loader.md`.
- Network sync uses `[UdonSynced]` fields with manual sync (`UdonBehaviourSyncMode.Manual`). Only the owner can modify synced variables.
- The project is primarily documented in Japanese. README and UI localization files contain Japanese as the primary language.

## Coding Style & Commits

- C# style follows upstream: 2-space indentation, braces on their own lines, `PascalCase` types/methods/properties, `_camelCase` private fields, namespaces under `Yamadev.YamaStream`.
- UnityEditor APIs belong only in `Editor/` or module editor assemblies; runtime assemblies must stay UdonSharp-safe.
- Preserve Unity `.meta` files when moving or adding assets.
- Commits use conventional-commit prefixes with optional scope: `fix:`, `feat:`, `docs(readme):`, etc. Keep subjects imperative.

See also `AGENTS.md` (repository guidelines; overlaps with this file).
