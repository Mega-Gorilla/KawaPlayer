# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

KawaPlayer is a fork of the VRChat video player [YamaPlayer](https://github.com/koorimizuw/YamaPlayer), adapted to integrate with the **VHub Playlist** service (`playlist.vrc-hub.com`). The purpose of the fork is to let world creators manage videos/playlists from inside VRChat: loading VHub playlists at runtime (PlaylistLoader module) and setting the world's auto-play default URL in-world (DefaultUrl module). All other player functionality is inherited from upstream YamaPlayer. Package name: `com.vhub.kawaplayer`.

- **Unity version**: 2022.3
- **Language**: C# / UdonSharp (VRChat's Udon scripting layer)
- **Dependencies**: VRChat Worlds SDK (>=3.10.2), Newtonsoft.Json (3.2.1)

## Build & Release

There is no local build command — the project is opened and compiled in the Unity Editor with VRChat SDK installed.

**Release workflow** (`.github/workflows/release.yml`, manual trigger with a `publish` checkbox):
1. Reads version from `package.json`
2. Packages into ZIP and UnityPackage formats (the UnityPackage is built by `.github/scripts/create-unitypackage.sh`), after leaving out what a world does not use: `docs/`, `CLAUDE.md`, `AGENTS.md`, `Assets/Models/Tablet/Source~/`, `Media~/poster/source/` (what the poster is drawn from; the poster itself ships) and `Tests/` (the list is in the workflow's "Leave Out What Is Not Part Of The Package" step; a new folder that is not for worlds goes there too)
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
- `docs/**/*.meta` は `.gitignore` 済みなので `.meta` は作らない。`docs/` はリリースの zip にも `.unitypackage` にも入らない（`release.yml` で除く）。使い方ガイドなどは GitHub で読む

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

Every client places the video by the same clock: the owner shares a video time and the server time it was taken at (`_syncedServerTimeMs`, from `Networking.GetServerTimeInMilliseconds`, 0 while there is none), and the time since is worked out with `Networking.CalculateServerDeltaTime`, which takes seconds and copes with the value wrapping. `GetNetworkDateTime`, used before, stays on the local clock on a client whose date sync fails (VRChat logs `Failed to sync network DateTime`), and that client played off by its clock's error for good (issue #237). A synced field whose type changes keeps instances of older builds apart (VRChat's network compatibility check), but PC and Android are separate builds that meet in one instance: when the synced fields change, both have to be rebuilt.

**Handler System** (`Handlers/`) — Abstraction over video backends:
- `PlayerHandler` — abstract base
- `BaseVideoPlayerHandler` — wraps VRC's built-in video players
- `ImageViewerHandler` — static image display

**UI System** (`UI/UIController.cs`) — Manages all interactive controls (play, pause, seek, volume, speed, playlists, modals). Heavy use of serialized fields for Unity inspector binding.

**Playback error details** — When a video fails, the screen's error message has a "Details" button that opens the last errors in the dialog, and asks for a screenshot on Discord. A world cannot open a link in VRChat, so the Discord address is in the dialog's copy field (`Modal.ShowCopyField`, an InputField the viewer can copy from). `Controller.ErrorReport.cs` keeps the last five, per viewer and unsynced: it records each failed try before the retry logic switches handlers, and numbers the matching log line; `UI/UIController.ErrorDetails.cs` builds the text. A handler can say more than its `VideoError` through `PlayerHandler.ErrorDetail` (the image viewer gives the downloader's error). For Access Denied, the screen asks the viewer to turn on Allow Untrusted URLs and, if the video still will not play, to ask the world's creator: in Public and Group Public instances an untrusted site plays only when the world lists it in its Video Player Allowed Domains, which only the creator can change (issue #225). The details then add, under the latest error, a note for the creator naming the domain to add. A picture needs only Allow Untrusted URLs, so the image viewer's Access Denied gets no note.

**Which player a URL plays on** — A URL entered in the world plays on AVPro unless someone chose the image viewer in the settings (issues #139, #182). `Controller` records whether the player in use was switched to by a track rather than chosen (`HandlerFromTrack`, synced): a picture from the tablet, a URL played on the player picked for it in the URL dialog, or a playlist's or the queue's track on another player, leaves it switched, and then the URL field, the URL dialog's selector and the settings all offer AVPro (`UIController.OfferedPlayerType`), so the next URL does not load as a picture. A player picked in the URL dialog is for that URL only; choosing one in the settings (`Controller.SetPlayerType`) keeps it for the URLs entered next, even the image viewer a picture had switched to already, which needs no question since nothing stops. A player starts on AVPro, the first of its video players. A track whose own player is missing, or refuses its URL, plays on the first in the order AVPro, Unity, image viewer that takes it; an error during playback switches through each player's own fallback (`_fallbackHandler`, AVPro and Unity to each other), whatever the order. The inspector offers no choice of player, and `VideoPlayerOrderBuildProcess` sorts every `Controller`'s players into that order at build and on Play, whatever order a scene saved (issue #166). A VHub track that names Unity (mode 0, or no mode) loads as AVPro, both at run time (`PlaylistLoader`) and when imported in the Playlist Editor. A playlist imported from VHub (`PlaylistItem.vhubPlaylistUrl`) has its Unity tracks baked as AVPro (`PlaylistBuildProcess.BakedPlayerType`), so one imported before #166 plays on AVPro once the world is built again, without its saved tracks changing; each track's origin is not kept, so a track meant for Unity goes in a playlist not imported from VHub, as the Playlist Editor notes. Any other playlist's Unity track still plays on Unity.

**Appearance** (`Runtime/Appearance/`, `Editor/Appearance/`) — KawaPlayer's colours are kept in one asset, `Assets/Appearance/KawaPlayerPalette.asset` (`ColorPalette`): the colour sets (KawaPlayer's first, then YamaPlayer's five) and the neutral colours. Each UI's `AppearanceSettings` points at it and picks a set by name (KawaPlayer's by default; a name no set has gets the first). Graphics carry a role (`ColorDefinition`) instead of a colour, and `AppearanceBuildProcess` colours them when the world is built; a button whose colour comes from its tint has the tint moved instead. To change a colour, edit the palette; "Apply Colors Now" on an `AppearanceSettings` also updates the colours saved in a prefab. Give new UI a role rather than a literal colour. The screen's idle image (待ち受け画像, `UIController._idleScreenSprite`, shown while stopped or loading) is `Assets/Images/default-idle-screen-image.png` by default, set on `ScreenUI.prefab`; a world creator can swap it in the KawaPlayer inspector. That image is only the background, 128 x 72 and stretched (uncompressed and without mipmaps, as block compression would show); the logo and the name are drawn over it by `IdleImage/IdleLogo`, which `UIController` shows only while the sprite is the default one (`_defaultIdleScreenSprite`), so an image the creator chose is shown alone (issue #189). They sit in the upper half, because the loading icon, the error message and the Details button draw over the middle.

**Logo and images** — KawaPlayer's logo is one white image, `Assets/Images/kawaplayer-logo.png` (1024 x 1024, crunched), coloured where it is used: white on the idle screen, silver on the back of the tablet. The logo's original images (white, black and gradient, reduced from 8192 x 8192, with faint leftovers of its editing cleared) are kept in `Media~/logo/`, with the quick start poster and its source in `Media~/poster/`: material creators may use to show KawaPlayer, under terms of its own rather than the package's (`Media~/LICENSE.md`, issue #196). Unity does not import `Media~`; the zip carries it, the `.unitypackage` does not. Keep what images cost in a world small: the icons are crunched and at most 512, and an image with mipmaps whose sides are not powers of two cannot be block-compressed, so Unity keeps it uncompressed (the old 1920 x 1080 idle image took 8 MB). An image nothing references is not built into a world, but is still in the package, so delete it.

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
- **PlaylistLoader** — loads playlists from `playlist.vrc-hub.com` at runtime using the Pre-baked URL Pool pattern (see Key Constraints below). Design docs: `docs/design/url-pool/`. VRChat plays VHub's tracks in Public and Group Public instances only if `playlist.vrc-hub.com` is in the world's Video Player Allowed Domains, which the creator sets on VRChat.com (Edit World Info), not in Unity, and the listener has Allow Untrusted URLs on: the domain alone is not enough; other instances need only each listener's Allow Untrusted URLs. Loading a playlist in the world is a string download, which always needs each player's Allow Untrusted URLs.
- **DefaultUrl** — lets the Instance Owner set the world's auto-play video/playlist URL from inside VRChat, synced to all players and persisted across visits. Users never see "DefaultUrl" or "default URL" (issue #173): the in-world settings card is titled 自動再生(Global) (Autoplay (Global)), with the "(Global)" every setting that reaches the whole instance carries, drawn at the panel's own size (`_globalSuffixSize`, 44 on the screen, 28 on the tablet; #173 had dropped it and #216 put it back), and Module Manager lists it as 自動再生(Owner), beside AutoPlay's 自動再生(World). Only these display names changed; the class, folder, object and translation-key names (`module.defaultUrl.*`, `module.autoPlay.*`) and the saved storage stay as they are, so worlds and saved URLs carry over

Inherited from upstream YamaPlayer:
- AudioLinkAdaptor, AutoPlay, LTCGIAdaptor, LightVolumeAdaptor
- PermissionManagement, Persistence, PitchShifter, SlideShower
- TimelineSync, VideoInfoDownloader

### Prefabs

`KawaPlayer.prefab` at the repository root is the main all-in-one prefab that users drop into scenes (also reachable via the **GameObject > KawaPlayer > Main** menu). Additional prefabs (ControlBar, PlaylistPanel, SubScreen, Tablet, UI parts) live in `Prefabs/`. The Tablet's scripts (`TabletPickup`, `TabletScreen`, `TabletReturnButton`, and one per app: `TabletImageApp`, `TabletSettingsApp`, `TabletVisitorsApp`) live in `Runtime/Internal/Tablet/`; its model and Blender build script in `Assets/Models/Tablet/`. The settings app's controls are wired to the tablet's own `UIController`, and modules add their rows to it through `uiSlots` (the AudioLink row, the DefaultUrl card), as they do for the player's settings panel. PermissionManagement puts its listener under the tablet's `Canvas/Display` the same way, so its permissions apply to what the tablet plays; that slot reuses the listener template of its slot for the screen (`Canvas/User`), and the build copies it into each UI that has the slot's path. The visitors app reads a `TabletVisitorRecorder` that keeps one record for the whole world: every tablet carries one, and `TabletVisitorsBuildProcess` keeps the first, moves it to the scene root, switches the others off and points every visitors app at it. The home button below the screen carries a white rounded square (`square.png`), and the LED around it (`led-ring.png`, a ring with its glow) lights while an app is open; on the home screen the LED is off and the button takes no press (`TabletScreen` switches the LED's object and the button's `interactable`; issue #180). A soft disc behind the LED shows where the button is pointed at. The project renders in linear colour space, where a faint alpha shows far stronger than in an image editor, so the glow's alpha in `led-ring.png` is stored raised to the power 1.7. The model's own button stands 0.6 mm proud of the bezel, so the button's UI sits 1 mm in front of the canvas (z = -6), or the model hides what is drawn inside the LED. The back carries the logo as a silver metal badge, 5 cm wide like a maker's mark, on a quad 0.3 mm off the body (`Body/Model/BackLogo`, `KawaTablet_Logo.mat`); the body's mobile shader has no transparency, so the logo uses the Standard shader in Fade mode. Unlike the matte body it is metallic and glossy, and a normal map (`KawaTablet_LogoNormal.png`, 256 x 256, crunched) gives it a chamfered edge, so it looks raised without more triangles and its edges catch the light. `Source~/logo_normal.py` makes the map from the logo; run it again when the logo changes.

The image app has two screens (issue #181): a list of the last five pictures shown, newest first, under the URL field, and the picture full size, with a back button that shows for a few seconds after the picture is touched and stays while a picture is loading or has failed. `TabletScreen` syncs the list (`VRCUrl[]`), the picture's URL and which screen is up, so late joiners see the same; the list lasts as long as the instance. A picture joins the list only when the download the entering player started succeeds, while that player still owns the screen and it is still the tablet's picture (synced URL), even if the list has been brought back up meanwhile: a late success, a retry or another player's download never writes over the shared state. Each player downloads only the picture that is up, never for the list, so a picture they have not loaded shows its site's name instead of a thumbnail. The download is copied into a `RenderTexture` no larger than 2048 (1024 on Quest) along its longest side and let go of at once; the last copy is freed before the next download starts, so only a picture being copied briefly shares memory with its copy. The RenderTextures have no depth buffer, which `VRCGraphics.Blit` needs on Quest. Thumbnails (at most 256 x 144, keeping the picture's shape) are made from that copy. A copy whose contents are lost is loaded again from its URL, and a lost thumbnail goes back to the site's name. A picture that has loaded can be shown on the KawaPlayer screen (issue #182): the button beside the back button sends it to the tablet's own `UIController` (`PlayImageUrl`), which plays it at once on the image viewer, without asking even over something playing, and through `BeforeUserPlayTrack`, so the permissions apply. The button shows only for a picture that has loaded, on a tablet pointed at a player.

When the image app cannot load a picture, it tells that player why and what to do next (issue #178). It sorts the failure by `VRCImageDownloadError` and by what VRChat's message adds: the HTTP status of a download error (`HTTP/1.1 403 Forbidden`: many sites refuse anything that is not a browser), a redirect, or an untrusted domain. It asks the player to turn on "Allow Untrusted URLs" only when that is the cause, and shows a "Try again" button only when trying again can help; the downloads are each player's own, so neither reaches anyone else. Unity's `Text` wraps only at spaces and knows no line-breaking rules, so the Japanese and Chinese texts of these messages carry their own line breaks and use no-break spaces around Latin words, each line short enough for the tablet.

The screen turns with the tablet (issue #183). Five times a second `TabletScreen` works out which side is up, and once the tablet has turned 55° past its last quarter turn, it turns the canvas's `Display` by that quarter and swaps its size between 1600 x 900 and 900 x 1600; lying flat changes nothing. Held upright, the apps take their portrait layout: each part in `TabletScreen._layoutParts` takes the anchors and rect stored for that orientation, and the layout groups in `_landscapeLayouts` are off, since they would put the parts back. So the home tiles stand two to a row, the settings categories become tabs along the top, and the visitors app makes its rows from the portrait templates (`RowPortrait`), which take an extra line. To change a layout, select the tablet in the prefab and use **KawaPlayer > Tablet Layout**: Show Portrait, move the parts, Capture Portrait, then Show Landscape before saving; a part added to the list has to be captured in both. The KawaPlayer app is outside the canvas and stays landscape: held upright, it keeps its last landscape turn, and only upside down does it turn 180°. The scrolling views clip with `Mask`, not `RectMask2D`, which works out its rectangle from the root canvas and clips everything away once the display is turned; a mask's image must not be fully transparent (hide it with `showMaskGraphic` instead), or it clips everything too.

### Editor Tools (`Editor/`)

Custom inspectors, build processors, menu items, and the module/localization editors. Separate assembly (`Yamadev.YamaStream.Editor.asmdef`) referencing the runtime assembly. The header every KawaPlayer inspector draws (`EditorBase`: logo, version, language) has a button that opens the user guide on GitHub (`docs/guide` on develop), since the guide is not in the package; module inspectors, which hide the header, do not show it.

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

Japanese-language docs. Read the analyses and the URL Pool design before touching PlaylistLoader, DefaultUrl, or the playlist pipeline:
- `docs/analysis/` — how the playlist system works (Playlist/QueueList/HistoryList), URL-to-playback pipeline, and why runtime JSON playlists are impossible
- `docs/design/` — one folder per design, plan or test procedure, named after the issue it came from, `<issue>-<topic>/` (for instance `68-release-qa/`, the runbook for a release's QA), with the document as its `README.md` and its pictures, and the scripts that draw them, beside it in `images/`. `url-pool/` (the URL Pool design: loader, Unity side, server side) predates the issue numbers and carries none. `docs/design/README.md` lists them with their state and where each used to live: add a row with a new one, and change its state when it is decided, built or dropped (issue #230). `228-playback-sync/` (issue #228) keeps a record apart from a plan: `comparison.md` records how KawaPlayer and VizVid (VVMW) synced playback, feature by feature, as of the commits it names, with what was tried in Unity and VRChat, and `README.md` is the decided plan, with the problems, how each is fixed and why, and how to check it. The record ages with the code and the plan does not; once the fix lands, KawaPlayer's sync as built is written up in `docs/analysis/`. Its charts are SVGs drawn by `images/figures.py`, the other figures are mermaid, drawn by GitHub, and the logs and scripts behind the checks are in `evidence~/`, which Unity does not import

`docs/guide/` is the user guide, with pictures: `quickstart.md` takes a creator from VCC to a video playing in the world, and a page per category gives the details (`install.md`, `settings.md` for the inspector, `playlists.md`, `modules.md`, `tablet.md`, `playing.md` for players, `vhub.md` for finding, copying and making playlists on VHub); `README.md` lists them. The guide calls the URL KawaPlayer reads a playlist from VHub's **VRChat用URL** (`/r/default/…`), as VHub's own buttons do, never its page address (`/playlists/…`). When a change alters what a guide page says or a picture shows (the inspector, Module Manager, the Playlist Editor, the screen UI, the tablet apps), change that page or take that picture again. The pictures are WebP, a few tens of KB each (`docs/` is left out of the package by `release.yml`, so they never reach a creator's project; the package's own `README.md` links to the guide by its GitHub address for the same reason), in `docs/guide/images/` by what they show: `vcc/`, `unity/`, `vrchat/` (VRChat's own menus, cut so that no name, status, balance or other avatar shows), `screen/`, `tablet/` and `vhub/` (VHub's site, taken with a sample playlist made for the picture and deleted after, and cut so that the signed-in account, its library and other people's playlists do not show). Each is drawn by `annotate.py` from the unmarked picture of the same name in `original/`, with outlines, numbered badges and captions on what to press or fill in: a picture taken again replaces its original, and a mark is changed in `annotate.py`, never on the picture (`docs/guide/images/README.md` has the steps and the sizes). The guide's ① and ② name the badges. Steps are numbered 1, 2, 3; ways to do the same thing are labelled A, B, C (bold labels, as Markdown has no lettered list), so they are not read as steps. A page that explains a window or the inspector goes through it in the order it is laid out (top to bottom, then left to right), as `settings.md` and the Playlist Editor in `playlists.md` do, not by task; a task that spans parts is told where its first part is. GitHub drops a `vcc://` href, so the guide links to the VPM listing page (`https://mega-gorilla.github.io/vpm-repos/`) instead, whose own Add to VCC button opens VCC, and shows that page with its button outlined. Show sample names, never a real playlist or song (the testing project's are real); the one exception is the track list picture (`unity/playlist-tracks`), which the owner allowed.

## Key Constraints

- All runtime scripts must be valid UdonSharp (subset of C#). Many standard C# features are unavailable (no generics on UdonSharpBehaviour, limited reflection, no async/await, etc.).
- **`string → VRCUrl` conversion is impossible at runtime** (`new VRCUrl(string)` is editor-only). Any feature needing dynamic URLs must use the Pre-baked URL Pool pattern: a large `VRCUrl[]` of redirect-server slot URLs is baked into serialized fields at build time, and the server maps slots to real URLs via HTTP 302. See `docs/design/url-pool/README.md`.
- Network sync uses `[UdonSynced]` fields with manual sync (`UdonBehaviourSyncMode.Manual`). Only the owner can modify synced variables.
- **Never add an UdonBehaviour to, or remove one from, an object that already has one in a shipped prefab** (e.g. a second U# component beside the `Controller`). A world records the network components of each object when it is built; after the update the SDK reports "Network Components Changed" and refuses to build the world (`Failed to assign network IDs`) until its creator fixes it in the Network ID Utility. Put the new behaviour on a new child object, or into the existing one. ClientSim repairs this on Play, so only a world build shows it.
- **The U# code must compile on the oldest SDK the package accepts** (`vpmDependencies`: `com.vrchat.worlds` 3.10.2 and later, issue #193). A creator's project may stay on that SDK, since it satisfies the range, and Udon there can expose less than the SDK this project is developed on. The minimum was 3.8.1, inherited from upstream and never tried, until the tablet failed to compile there: Udon gained `Behaviour.enabled` only in 3.10.2 (issue #191). Call a Unity member through the concrete type where you can, and before relying on something newer, compile in a project with only the oldest SDK and KawaPlayer; to need a newer SDK, raise the minimum in `package.json`, this file and the README together.
- The project is primarily documented in Japanese. README and UI localization files contain Japanese as the primary language.

## Coding Style & Commits

- C# style follows upstream: 2-space indentation, braces on their own lines, `PascalCase` types/methods/properties, `_camelCase` private fields, namespaces under `Yamadev.YamaStream`.
- UnityEditor APIs belong only in `Editor/` or module editor assemblies; runtime assemblies must stay UdonSharp-safe.
- Preserve Unity `.meta` files when moving or adding assets.
- Commits use conventional-commit prefixes with optional scope: `fix:`, `feat:`, `docs(readme):`, etc. Keep subjects imperative.

See also `AGENTS.md` (repository guidelines; overlaps with this file).
