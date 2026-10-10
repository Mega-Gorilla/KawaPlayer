# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

KawaPlayer is a fork of the VRChat video player [YamaPlayer](https://github.com/koorimizuw/YamaPlayer), adapted to integrate with the **VHub Playlist** service (`playlist.vrc-hub.com`). The purpose of the fork is to let world creators manage videos/playlists from inside VRChat: loading VHub playlists at runtime (PlaylistLoader module) and setting the world's auto-play default URL in-world (DefaultUrl module). All other player functionality is inherited from upstream YamaPlayer. Package name: `com.vhub.kawaplayer`.

- **Unity version**: 2022.3
- **Language**: C# / UdonSharp (VRChat's Udon scripting layer)
- **Dependencies**: VRChat Worlds SDK (>=3.8.1), Newtonsoft.Json (3.2.1)

## Build & Release

There is no local build command — the project is opened and compiled in the Unity Editor with VRChat SDK installed.

**Release workflow** (`.github/workflows/release.yml`, manual trigger with a `publish` checkbox):
1. Reads version from `package.json`
2. Packages into ZIP and UnityPackage formats (the UnityPackage is built by `.github/scripts/create-unitypackage.sh`)
3. Uploads both plus `package.json` as a workflow artifact — **without `publish` the run stops here (dry run)**
4. With `publish`: creates a GitHub Release with both artifacts **and an empty body** (refused unless run from `develop` and the tag does not exist yet). The release notes are added afterwards — see the checklist
5. With `publish`: triggers `repository-dispatch` to `Mega-Gorilla/vpm-repos` to rebuild the VPM listing

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

**Two changelogs, two audiences**:
- `Assets/updatelog.txt` — **表示されるのはワールドの中**。`ScreenUI.prefab` が `UIController._updateLogTextAsset` として参照し、プレイヤーの Info パネルにそのまま出る UI テキスト。日本語のみ、1 バージョン数行、リンクや issue 番号は書かない
- `docs/releases/<version>.md` — **GitHub Release の本文**。Releases ページを読む利用者向けの詳細版で、`updatelog.txt` の転記ではなく独立した文章として書く。作法は `docs/releases/1.2.0.md` を前例とする: VRChat のリリースノート風に、目玉を冒頭で説明し、箇条書きは一項目一文で二人称、既知の問題の列挙はせず「困ったら Discord へ」（`https://discord.gg/tkrHek6PvN`）、入手方法は VCC のみ。`vcc://vpm/addRepo?url=...` はコピー用のコードブロックで書く — GitHub の Markdown は `vcc://` の href を落とすので、リンクにしても押せない
- `docs/**/*.meta` は `.gitignore` 済みなので `.meta` は作らない。`docs/` は zip には入るが `.unitypackage` には入らない

**Release checklist**:
1. `package.json` の `version` を更新
2. `Assets/updatelog.txt` の先頭に新バージョンのエントリを追加（主要な変更のみ簡潔に。軽微な修正は省略可）
3. `docs/releases/<version>.md` を書く（上記の作法。レビューは PR で受ける）
4. コミット・push
5. GitHub Actions の「Build Release」ワークフローを **`publish` にチェックを入れて**手動実行 (develop ブランチ)。チェック無しで実行すると成果物を Artifacts に上げるだけの dry run になる (ワークフロー変更の確認に使う)
6. リリースが作られたら本文を入れる: `gh release edit <version> --repo Mega-Gorilla/KawaPlayer --notes-file docs/releases/<version>.md`（ワークフローは本文を付けない。添付資産には触れない）
7. repository-dispatch → vpm-repos listing 再ビルド（`index.json` に新バージョンが載る）を確認

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

**Playback error details** — When a video fails, the screen's error message has a "Details" button that opens the last errors in the dialog, and asks for a screenshot on Discord. A world cannot open a link in VRChat, so the Discord address is in the dialog's copy field (`Modal.ShowCopyField`, an InputField the viewer can copy from). `Controller.ErrorReport.cs` keeps the last five, per viewer and unsynced: it records each failed try before the retry logic switches handlers, and numbers the matching log line; `UI/UIController.ErrorDetails.cs` builds the text. A handler can say more than its `VideoError` through `PlayerHandler.ErrorDetail` (the image viewer gives the downloader's error).

**Appearance** (`Runtime/Appearance/`, `Editor/Appearance/`) — KawaPlayer's colours are kept in one asset, `Assets/Appearance/KawaPlayerPalette.asset` (`ColorPalette`): the colour sets (KawaPlayer's first, then YamaPlayer's five) and the neutral colours. Each UI's `AppearanceSettings` points at it and picks a set by name (KawaPlayer's by default; a name no set has gets the first). Graphics carry a role (`ColorDefinition`) instead of a colour, and `AppearanceBuildProcess` colours them when the world is built; a button whose colour comes from its tint has the tint moved instead. To change a colour, edit the palette; "Apply Colors Now" on an `AppearanceSettings` also updates the colours saved in a prefab. Give new UI a role rather than a literal colour. The screen's idle image (待ち受け画像, `UIController._idleScreenSprite`, shown while stopped or loading) is `Assets/Images/default-idle-screen-image.png` by default, set on `ScreenUI.prefab`; a world creator can swap it in the KawaPlayer inspector. Its logo and name sit in the upper half, because the loading icon, the error message and the Details button draw over its middle.

**Playlist System** (`Playlist/`) — `PlaylistManager` coordinates `Playlist`, `QueueList`, and `HistoryList` components.

### Public API (`Runtime/Components/`)

Entry point components that world creators place in their scenes:
- `YamaPlayer` — main component, references PlaylistManager and ModuleManager
- `YamaPlayerScreen`, `YamaPlayerSpeaker`, `YamaPlayerSubController`

### Extension Pattern

`YamaPlayerListener` (base class) provides virtual callback methods (AfterVideoReady, AfterVideoStarted, AfterVolumeChanged, etc.) that modules and custom scripts override to react to player events.

### Modules (`Modules/`)

Each module is an independent assembly with its own `.asmdef`. Modules extend the player via the listener pattern and are optional dependencies.

A module's `YamaPlayerModuleDefinition` may sit deeper than directly under the player's `Modules` (DefaultUrl's is on `Modules/DefaultUrl/Controller`, beside its storage). Module Manager and the player inspector list every module under `Modules`; switching one off switches off its root, so its other parts go with it. The root (`YamaPlayerModuleBuildProcess.GetModuleRoot`) is the module's own object, or the one its definition names in `moduleRoot` when its parts sit beside it — DefaultUrl names `Modules/DefaultUrl`. It is never worked out from what else shares a folder. The screen's DefaultUrl settings card is not under that root, so `DefaultUrlSettingsUI` hides the whole card when its DefaultUrl is missing or switched off. The build leaves out a module whose own object or root is off (`IsModuleEnabled`), and the duplicate warning, the module translations and the autoplay check follow the same rule. DefaultUrl and AutoPlay both start a video on join with nothing deciding between them, so having both on one player is reported as an error in Module Manager, the player inspector and the build log (the build goes on, as for a duplicate). A module that comes with the prefab (KawaPlayer's own PlaylistLoader and DefaultUrl) cannot be deleted from the list, only switched off: there is no standalone prefab to add it back from.

KawaPlayer-specific modules (the reason this fork exists):
- **PlaylistLoader** — loads playlists from `playlist.vrc-hub.com` at runtime using the Pre-baked URL Pool pattern (see Key Constraints below). Design docs: `docs/design/url-pool-*.md`
- **DefaultUrl** — lets the Instance Owner set the world's auto-play video/playlist URL from inside VRChat, synced to all players and persisted across visits. Users never see "DefaultUrl" or "default URL" (issue #173): the in-world settings card is titled 自動再生 (Autoplay), without "(Global)", and Module Manager lists it as 自動再生(Owner), beside AutoPlay's 自動再生(World). Only these display names changed; the class, folder, object and translation-key names (`module.defaultUrl.*`, `module.autoPlay.*`) and the saved storage stay as they are, so worlds and saved URLs carry over

Inherited from upstream YamaPlayer:
- AudioLinkAdaptor, AutoPlay, LTCGIAdaptor, LightVolumeAdaptor
- PermissionManagement, Persistence, PitchShifter, SlideShower
- TimelineSync, VideoInfoDownloader

### Prefabs

`KawaPlayer.prefab` at the repository root is the main all-in-one prefab that users drop into scenes (also reachable via the **GameObject > KawaPlayer > Main** menu). Additional prefabs (ControlBar, PlaylistPanel, SubScreen, Tablet, UI parts) live in `Prefabs/`. The Tablet's scripts (`TabletPickup`, `TabletScreen`, `TabletReturnButton`, and one per app: `TabletImageApp`, `TabletSettingsApp`, `TabletVisitorsApp`) live in `Runtime/Internal/Tablet/`; its model and Blender build script in `Assets/Models/Tablet/`. The settings app's controls are wired to the tablet's own `UIController`, and modules add their rows to it through `uiSlots` (the AudioLink row, the DefaultUrl card), as they do for the player's settings panel. The visitors app reads a `TabletVisitorRecorder` that keeps one record for the whole world: every tablet carries one, and `TabletVisitorsBuildProcess` keeps the first, moves it to the scene root, switches the others off and points every visitors app at it. The home button below the screen carries a white rounded square (`square.png`), and the LED around it (`led-ring.png`, a ring with its glow) lights while an app is open; on the home screen the LED is off and the button takes no press (`TabletScreen` switches the LED's object and the button's `interactable`; issue #180). A soft disc behind the LED shows where the button is pointed at. The project renders in linear colour space, where a faint alpha shows far stronger than in an image editor, so the glow's alpha in `led-ring.png` is stored raised to the power 1.7. The model's own button stands 0.6 mm proud of the bezel, so the button's UI sits 1 mm in front of the canvas (z = -6), or the model hides what is drawn inside the LED.

The image app has two screens (issue #181): a list of the last five pictures shown, newest first, under the URL field, and the picture full size, with a back button that shows for a few seconds after the picture is touched and stays while a picture is loading or has failed. `TabletScreen` syncs the list (`VRCUrl[]`), the picture's URL and which screen is up, so late joiners see the same; the list lasts as long as the instance. A picture joins the list only when the download the entering player started succeeds, while that player still owns the screen and it is still the tablet's picture (synced URL), even if the list has been brought back up meanwhile: a late success, a retry or another player's download never writes over the shared state. Each player downloads only the picture that is up, never for the list, so a picture they have not loaded shows its site's name instead of a thumbnail. The download is copied into a `RenderTexture` no larger than 2048 (1024 on Quest) along its longest side and let go of at once; the last copy is freed before the next download starts, so only a picture being copied briefly shares memory with its copy. The RenderTextures have no depth buffer, which `VRCGraphics.Blit` needs on Quest. Thumbnails (at most 256 x 144, keeping the picture's shape) are made from that copy. A copy whose contents are lost is loaded again from its URL, and a lost thumbnail goes back to the site's name.

When the image app cannot load a picture, it tells that player why and what to do next (issue #178). It sorts the failure by `VRCImageDownloadError` and by what VRChat's message adds: the HTTP status of a download error (`HTTP/1.1 403 Forbidden`: many sites refuse anything that is not a browser), a redirect, or an untrusted domain. It asks the player to turn on "Allow Untrusted URLs" only when that is the cause, and shows a "Try again" button only when trying again can help; the downloads are each player's own, so neither reaches anyone else. Unity's `Text` wraps only at spaces and knows no line-breaking rules, so the Japanese and Chinese texts of these messages carry their own line breaks and use no-break spaces around Latin words, each line short enough for the tablet.

The screen turns with the tablet (issue #183). Five times a second `TabletScreen` works out which side is up, and once the tablet has turned 55° past its last quarter turn, it turns the canvas's `Display` by that quarter and swaps its size between 1600 x 900 and 900 x 1600; lying flat changes nothing. Held upright, the apps take their portrait layout: each part in `TabletScreen._layoutParts` takes the anchors and rect stored for that orientation, and the layout groups in `_landscapeLayouts` are off, since they would put the parts back. So the home tiles stand two to a row, the settings categories become tabs along the top, and the visitors app makes its rows from the portrait templates (`RowPortrait`), which take an extra line. To change a layout, select the tablet in the prefab and use **KawaPlayer > Tablet Layout**: Show Portrait, move the parts, Capture Portrait, then Show Landscape before saving; a part added to the list has to be captured in both. The KawaPlayer app is outside the canvas and stays landscape: held upright, it keeps its last landscape turn, and only upside down does it turn 180°. The scrolling views clip with `Mask`, not `RectMask2D`, which works out its rectangle from the root canvas and clips everything away once the display is turned; a mask's image must not be fully transparent (hide it with `showMaskGraphic` instead), or it clips everything too.

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
- **Never add an UdonBehaviour to, or remove one from, an object that already has one in a shipped prefab** (e.g. a second U# component beside the `Controller`). A world records the network components of each object when it is built; after the update the SDK reports "Network Components Changed" and refuses to build the world (`Failed to assign network IDs`) until its creator fixes it in the Network ID Utility. Put the new behaviour on a new child object, or into the existing one. ClientSim repairs this on Play, so only a world build shows it.
- The project is primarily documented in Japanese. README and UI localization files contain Japanese as the primary language.

## Coding Style & Commits

- C# style follows upstream: 2-space indentation, braces on their own lines, `PascalCase` types/methods/properties, `_camelCase` private fields, namespaces under `Yamadev.YamaStream`.
- UnityEditor APIs belong only in `Editor/` or module editor assemblies; runtime assemblies must stay UdonSharp-safe.
- Preserve Unity `.meta` files when moving or adding assets.
- Commits use conventional-commit prefixes with optional scope: `fix:`, `feat:`, `docs(readme):`, etc. Keep subjects imperative.

See also `AGENTS.md` (repository guidelines; overlaps with this file).
