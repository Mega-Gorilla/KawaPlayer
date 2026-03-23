# YamaPlayer: YouTube URL入力からプレイヤー再生までのパイプライン解析

## 概要

YamaPlayer は VRChat SDK3 (UdonSharp) 上で動作する高機能動画プレイヤーである。partial class による分割設計、Handler パターンによるプレイヤー抽象化、モジュールシステムによる拡張性を特徴とする。

**コアファイル群** (Controller は partial class で分割):
| ファイル | 役割 |
|---------|------|
| `Runtime/Internal/Controller.cs` | メインコントローラ・再生制御 |
| `Runtime/Internal/Controller.Sync.cs` | ネットワーク同期 |
| `Runtime/Internal/Controller.Events.cs` | イベントハンドリング・エラーリトライ |
| `Runtime/Internal/Controller.Playlist.cs` | プレイリスト・キュー・シャッフル |
| `Runtime/Internal/Controller.Audio.cs` | 音声制御 |
| `Runtime/Internal/Controller.Screen.cs` | スクリーン描画制御 |
| `Runtime/Internal/Handlers/PlayerHandler.cs` | プレイヤー抽象基底クラス |
| `Runtime/Internal/Handlers/BaseVideoPlayerHandler.cs` | VRC動画プレイヤー実装 |
| `Runtime/Internal/Handlers/ImageViewerHandler.cs` | 画像ビューア実装 |
| `Runtime/Internal/UI/UIController.cs` | UIコントローラ (partial class) |

---

## アーキテクチャ概観

```
┌─────────────────────────────────────────────────────────────────────────┐
│                          YamaPlayer アーキテクチャ                      │
│                                                                         │
│  ┌─────────────┐   ┌──────────────────────────────────────────────────┐ │
│  │ UIController │──>│         Controller (partial class)              │ │
│  │ (partial)    │   │  ┌────────┬────────┬──────────┬───────┬──────┐ │ │
│  │  .cs         │   │  │ .cs    │ Sync   │ Events   │Playlist│Audio │ │ │
│  │  .Playlist   │   │  │ (core) │ .cs    │ .cs      │.cs    │.cs   │ │ │
│  │  .Lock       │   │  └────────┴────────┴──────────┴───────┴──────┘ │ │
│  │  .Localization│  └───────────────────────┬────────────────────────┘ │
│  └─────────────┘                            │                          │
│                                             ▼                          │
│                              ┌──────────────────────────┐              │
│                              │     PlayerHandler         │              │
│                              │     (abstract base)       │              │
│                              └──────┬───────────┬───────┘              │
│                                     │           │                      │
│                          ┌──────────┴──┐  ┌─────┴──────────┐          │
│                          │BaseVideoPlayer│  │ImageViewer     │          │
│                          │Handler       │  │Handler         │          │
│                          │(Unity/AVPro) │  │(VRCImageDL)    │          │
│                          └──────────────┘  └────────────────┘          │
│                                                                         │
│  ┌────────────┐  ┌──────────┐  ┌──────────────┐                       │
│  │ Playlist[] │  │ QueueList │  │ HistoryList  │                       │
│  │ (静的)     │  │ (動的同期)│  │ (動的同期)   │                       │
│  └────────────┘  └──────────┘  └──────────────┘                       │
│                                                                         │
│  ┌─────────────────────────────────────────┐                           │
│  │ Modules (拡張モジュール)                 │                           │
│  │ AutoPlay / AudioLink / Persistence /    │                           │
│  │ PitchShifter / SlideShower / etc.       │                           │
│  └─────────────────────────────────────────┘                           │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## データ構造

### Track (トラック)

YamaPlayer の基本データ単位。`object[]` 配列として表現される。

```csharp
// TrackUtils.cs
object[] track = new object[] {
    VideoPlayerType playerType,  // [0] プレイヤー種別
    string title,                // [1] タイトル
    VRCUrl url                   // [2] URL
};
```

**VideoPlayerType 列挙型**:
```
UnityVideoPlayer   - VRChat標準ビデオプレイヤー
AVProVideoPlayer   - AVPro ビデオプレイヤー
ImageViewer        - 画像ビューア (VRCImageDownloader)
```

### URL バリデーション

```csharp
// UrlUtils.cs
対応プロトコル: http, https, rtsp, rtmp, rtspt, rtspu, rtmps, rtsps
```

---

## パイプライン詳細

### Phase 1: URL入力 (UIController)

UIController には2つの URL入力欄がある:
- `_urlInputField` — メインコントロールバー内
- `_urlInputFieldTop` — トップバー内

```
ユーザー操作: VRCUrlInputField に URL を入力して Enter
    │
    ▼
PlayUrl() / PlayUrlTop()                     (UIController.cs:259-261)
    │
    ▼
PlayUrlField(urlInputField)                  (UIController.cs:263-290)
    │
    ├── URL バリデーション: UrlUtils.IsValidUrl()
    │   └── 無効 → 入力欄クリア、return
    │
    ├── 停止中 かつ ロード中でない場合:
    │   └── PlayUrlInternal() へ直接遷移 (モーダル不要)
    │
    └── 再生中の場合:
        └── モーダルダイアログ表示
            ├── [キャンセル]    → 何もしない
            ├── [キューに追加]  → AddUrlToQueueEvent()
            └── [再生]         → PlayUrlEvent()
```

**モーダルダイアログ**: 再生中に新しいURLを入力した場合、プレイヤータイプ選択 (Unity/AVPro/ImageViewer) と3つのアクション選択肢が表示される。

### Phase 2: トラック生成と再生開始

```
PlayUrlInternal(playerType, url)             (UIController.cs:368-378)
    │
    ├── InvokeBeforeEvent("BeforeUserPlayTrack")
    │   └── リスナーにキャンセル機会を提供 (PermissionManagement等)
    │       └── キャンセルされた場合 → UpdateUI(), return
    │
    ├── _controller.TakeOwnership()          オーナーシップ取得
    │
    └── _controller.PlayTrack(
    │       TrackUtils.NewTrack(playerType, "", url)
    │   )
    │
    ▼
Controller.PlayTrack(track)                  (Controller.cs:332-352)
    │
    ├── URL バリデーション: url.IsValidUrl()
    │   └── 無効 → PrintError(), return
    │
    ├── 再生中かつオーナーの場合:
    │   └── Stop() — 現在の再生を停止
    │
    ├── ClearPlaylistIndexes()
    │   └── _activePlaylistIndex = -1
    │       _playingTrackIndex = -1
    │
    ├── _syncedState = PlayerState.Playing
    │
    └── LoadTrack(track)
```

### Phase 3: トラックロード (LoadTrack)

```csharp
// Controller.cs:354-379
LoadTrack(track, isReload = false)
    │
    ├── _autoForward = false               自動次トラック無効化
    ├── Handler.Stop()                     現在のハンドラーを停止
    │
    ├── SetPlayerType(trackPlayerType)     ハンドラー切替
    │   │
    │   └── トラックの playerType に合致する
    │       PlayerHandler を _videoPlayerHandlers[] から検索
    │       └── Handler プロパティにセット
    │           └── AfterPlayerHandlerChanged イベント発火
    │
    ├── Track = track                      Track プロパティ更新
    │   └── AfterTrackUpdated イベント発火 (全リスナーに通知)
    │
    ├── Handler.LoadUrl(url)               ★ ハンドラーにURL渡し
    │
    ├── オーナーかつ非ローカルかつ非リロード:
    │   └── RequestSerialization()         ネットワーク同期開始
    │
    └── AfterTrackLoaded イベント発火
```

### Phase 4: ハンドラーレベルのURL読み込み

#### BaseVideoPlayerHandler (動画再生)

```csharp
// BaseVideoPlayerHandler.cs:252-266
LoadUrl(url)
    │
    ├── UseFallbackHandler の場合:
    │   └── _fallbackHandler.LoadUrl(url)   フォールバックに委譲
    │
    ├── _baseVideoPlayer.LoadURL(url)       VRChat SDK API呼び出し
    ├── _loadedUrl = url
    ├── _stopped = false
    ├── _loading = true
    └── _isError = false

    [VRChat SDK 内部処理]
    ├── URL解決 (YouTube → ストリームURL変換)
    ├── 動画データダウンロード
    │
    ├── 成功 → OnVideoReady() コールバック
    └── 失敗 → OnVideoError() コールバック
```

#### ImageViewerHandler (画像表示)

Controller は `LoadUrl()` 経路を使用する。`PlayUrl()` との違いに注意。

```
PlayUrl(url):                                (ImageViewerHandler.cs:81-88)
    ├── _playImmediately = true              ロード完了後に自動再生
    ├── ImageDownloader.DownloadImage(url)
    └── _loading = true

LoadUrl(url):                                (ImageViewerHandler.cs:90-97)
    ├── _playImmediately = false             ロードのみ、再生はControllerが制御
    ├── ImageDownloader.DownloadImage(url)
    └── _loading = true

    [ダウンロード完了]
    │
    ├── 成功 → OnImageLoadSuccess()          (ImageViewerHandler.cs:131-145)
    │         ├── _isReady = true, _loading = false
    │         ├── AfterVideoReady()          常に発火
    │         ├── _playImmediately の場合のみ:
    │         │   ├── Play()                 _isPlaying = true
    │         │   └── AfterVideoStarted()    ← LoadUrl経路では呼ばれない
    │         └── AfterTextureUpdated()
    │
    └── 失敗 → OnImageLoadError()            (ImageViewerHandler.cs:147-173)
              ├── エラー種別を VideoError に変換:
              │   AccessDenied → VideoError.AccessDenied
              │   InvalidURL   → VideoError.InvalidURL
              │   DownloadError → VideoError.PlayerError
              │   TooManyRequests → VideoError.RateLimited
              │   default → VideoError.Unknown
              └── AfterVideoErrorOccurred()
```

Controller が `LoadUrl()` を使う場合、`OnImageLoadSuccess` では `AfterVideoReady()` のみが発火する。その後 Controller の `AfterVideoReady()` 内で `SyncedState == Playing` なら `Handler.Play()` を呼び、そこで初めて再生が開始される。

### Phase 5: 動画準備完了 (OnVideoReady → AfterVideoReady)

```
BaseVideoPlayerHandler.OnVideoReady()        (BaseVideoPlayerHandler.cs:349-359)
    │
    ├── _loading = false
    ├── _stopped の場合 → _baseVideoPlayer.Stop(), return
    └── _listener.AfterVideoReady()
            │
            ▼
Controller.AfterVideoReady()                 (Controller.Events.cs:78-101)
    │
    ├── _errorRetryCount = 0                 エラーカウンタリセット
    ├── _retryTargetUrl = VRCUrl.Empty
    │
    ├── SyncedState に基づくアクション:
    │   ├── Playing → Handler.Play()         ★ 再生開始
    │   └── Idle    → Handler.Stop()         停止
    │
    ├── CheckRepeat()                        リピート区間チェック開始
    │
    ├── オーナーかつ非ローカルかつ非リロード:
    │   ├── UpdateSyncedVideoTime(0f)        時刻同期リセット
    │   └── RequestSerialization()           ネットワーク同期
    │
    ├── それ以外 (非オーナー / ローカルモード / リロード時):
    │   └── EnsureVideoTime()               時刻同期合わせ
    │
    └── 全リスナーに AfterVideoReady 通知
```

### Phase 6: 再生開始 (Handler.Play → AfterVideoStarted)

```
BaseVideoPlayerHandler.Play()                (BaseVideoPlayerHandler.cs:268-278)
    │
    ├── _stopped / IsPlaying の場合 → return (二重再生防止)
    ├── _baseVideoPlayer.Play()              VRChat SDK 再生開始
    └── _listener.AfterVideoPlayed()

BaseVideoPlayerHandler.OnVideoStart()        (BaseVideoPlayerHandler.cs:361-364)
    │
    └── _listener.AfterVideoStarted()
            │
            ▼
Controller.AfterVideoStarted()              (Controller.Events.cs:103-112)
    │
    └── 全リスナーに AfterVideoStarted 通知
```

### Phase 7: 再生中の Update ループ

**Controller.Update()** (Controller.cs:61-67):
```csharp
private void Update()
{
    // 再生中かつ同期間隔経過
    if (IsPlaying && Time.time - _lastSync > _syncFrequency)
    {
        EnsureVideoTime();  // 時刻同期チェック
    }
}
```

**UIController.Update()** (UIController.cs:138-146):
```csharp
private void Update()
{
    // ボリュームツールチップ更新
    // プログレスバー更新 (再生中かつロード中でない場合)
    if (!_controller.Stopped && !_controller.IsLoading) UpdateProgressView();
}
```

**BaseVideoPlayerHandler.Update()** (BaseVideoPlayerHandler.cs:35-60):
```
毎フレーム:
    ├── テクスチャ取得 (MaterialPropertyBlock or sharedMaterial)
    ├── AVPro (Windows) の場合:
    │   └── Blit処理で色空間変換 (LateUpdate)
    └── リスナーに AfterTextureUpdated 通知
```

---

## ネットワーク同期

### 同期変数 (Controller)

```csharp
// Controller.cs + Controller.Sync.cs
[UdonSynced] byte _syncedState;           // PlayerState (Idle/Playing/Paused)
[UdonSynced] VideoPlayerType _playerType; // プレイヤー種別
[UdonSynced] string _title;               // トラックタイトル
[UdonSynced] VRCUrl _url;                 // トラックURL
[UdonSynced] bool _loop;                  // ループフラグ
[UdonSynced] float _speed;                // 再生速度
[UdonSynced] ulong _repeat;              // リピート区間 (ビットパック)
[UdonSynced] float _syncedVideoTime;     // 同期再生時刻
[UdonSynced] long _networkDataTimeTicks;  // ネットワーク基準時刻
[UdonSynced] int _activePlaylistIndex;    // アクティブプレイリスト
[UdonSynced] int _playingTrackIndex;      // 再生中トラック番号
[UdonSynced] bool _shuffle;               // シャッフルフラグ
```

### 同期フロー

```
オーナー (操作者)                          他のクライアント
─────────────────                         ─────────────────
PlayTrack() / LoadTrack()
    │
    ├── OnPreSerialization()
    │   _playerType = track[0]
    │   _title = track[1]
    │   _url = track[2]
    │
    └── RequestSerialization()  ──sync──>  OnDeserialization()
                                               │
                                               ├── トラック復元:
                                               │   ActivePlaylist有効 ?
                                               │     YES → playlist.GetTrack(index)
                                               │     NO  → TrackUtils.NewTrack(...)
                                               │
                                               ├── AfterTrackSynced イベント
                                               │
                                               ├── URLが変更された場合:
                                               │   └── LoadTrack(track)
                                               │
                                               ├── ApplySyncedState()
                                               │   Playing → Handler.Play()
                                               │   Paused  → Handler.Pause()
                                               │   Idle    → Handler.Stop()
                                               │
                                               └── EnsureVideoTime()
                                                   時刻同期合わせ
```

### 時刻同期の仕組み

iwaSync3 がサーバー時刻を使うのに対し、YamaPlayer は **NetworkDateTime (Ticks)** を使用する。

```csharp
// Controller.Sync.cs
UpdateSyncedVideoTime(time):
    _syncedVideoTime = Clamp(time - _localDelay, 0, Duration)
    _networkDataTimeTicks = Networking.GetNetworkDateTime().Ticks

EnsureVideoTime():
    offset = (現在Ticks - _networkDataTimeTicks) / TicksPerSecond × Speed
    targetTime = Clamp(_syncedVideoTime + offset + _localDelay, 0, Duration)
    if |VideoTime - targetTime| >= _syncMargin (0.3秒):
        SetTime(targetTime)
```

**特徴**:
- 同期間隔: `_syncFrequency` = 5.0秒 (設定可能 1-10秒)
- 同期マージン: `_syncMargin` = 0.3秒 (設定可能 0-1秒)
- 速度倍率 (`_speed`) を考慮した時刻計算
- ローカルディレイ (`_localDelay`) によるリップシンク補正
- 一時停止中は offset = 0 (時刻ドリフトなし)

---

## エラーハンドリングとリトライ

```
OnVideoError(videoError)
    │
    ▼
Controller.AfterVideoErrorOccurred()         (Controller.Events.cs:157-168)
    │
    └── HandleErrorRetry(videoError)         (Controller.Events.cs:170-216)
        │
        ├── AccessDenied → リトライなし、即終了 (return)
        ├── InvalidURL   → リトライなし、即終了 (return)
        │
        ├── PlayerError:
        │   └── フォールバック判定:
        │       _useFallbackAfterErrors > 0 かつ
        │       _errorRetryCount == _useFallbackAfterErrors - 1 の場合:
        │         → Handler.UseFallbackHandler = true
        │           (例: AVPro → Unity に自動切替)
        │       それ以外:
        │         → Handler.UseFallbackHandler = false
        │   └── (break → 下のリトライ処理へ進む)
        │
        ├── RateLimited / Unknown / その他:
        │   └── フォールバック判定なし、直接リトライ処理へ
        │       (switch文の default: break で通過)
        │
        └── [共通リトライ処理] (AccessDenied/InvalidURL以外が到達)
            │
            ├── _errorRetryCount < _maxErrorRetry (デフォルト5):
            │   ├── カウンタ増加
            │   ├── 安全間隔: SAFETY_RETRY_INTERVAL = 5.1秒
            │   └── ErrorRetry() をスケジュール
            │
            └── 最大リトライ超過:
                └── リトライ停止、エラーログ出力
```

**注意**: フォールバックハンドラーへの切替は `PlayerError` のときのみ発生する。`RateLimited` や `Unknown` ではフォールバック無しでリトライのみが行われる。

**ErrorRetry()** (Controller.Events.cs:218-245):
```
ErrorRetry()
    ├── トラックが変更されていたら → キャンセル
    ├── 既に再生中 or URL無効 → キャンセル
    └── Handler.LoadUrl(currentUrl) で再ロード
        └── AfterVideoRetry イベント発火
```

---

## 動画終了とAutoForward

```
OnVideoEnd()
    │
    ▼
Controller.AfterVideoEnded()                 (Controller.Events.cs:131-155)
    │
    ├── オーナー or ローカル:
    │   ├── _forwardInterval >= 0:
    │   │   ├── _autoForward = true
    │   │   └── AutoForward() を _forwardInterval秒後にスケジュール
    │   │       │
    │   │       └── Forward() を呼び出し
    │   │           ├── キューにトラックあり → PlayTrackFromQueue()
    │   │           ├── プレイリスト内 + シャッフル → ランダム選択
    │   │           └── プレイリスト内 → 次のトラック (循環)
    │   │
    │   └── _forwardInterval < 0:
    │       └── ClearPlaylistIndexes() (自動進行なし)
    │
    ├── _syncedState = Idle
    ├── Handler.Stop()
    └── 全リスナーに AfterVideoEnded 通知
```

---

## 完全なパイプライン図

```
┌────────────────────────────────────────────────────────────────────────────┐
│                  YouTube URL → 再生 パイプライン (YamaPlayer)              │
└────────────────────────────────────────────────────────────────────────────┘

[Phase 1] URL入力
    ユーザー → VRCUrlInputField に URL 入力 → Enter
                    │
                    ▼
            PlayUrl() / PlayUrlTop()
            PlayUrlField(inputField)
                    │
            ┌───────┴───────────────────────┐
            │ UrlUtils.IsValidUrl() 検証     │
            │ プロトコル: http(s)/rtsp/rtmp  │
            └───────┬───────────────────────┘
                    │
            ┌───────┴───────┐
            │               │
        (停止中)       (再生中)
            │          モーダルダイアログ
            │            ┌─────┬──────┐
            │          [Cancel][Queue][Play]
            │               │         │
            │     AddUrlToQueue  PlayUrlInternal
            │                        │
            ▼────────────────────────▼
[Phase 2] トラック生成
        PlayUrlInternal(playerType, url)
            │
            ├── InvokeBeforeEvent("BeforeUserPlayTrack")
            │   └── キャンセル可能 (権限チェック等)
            │
            ├── TakeOwnership()
            └── PlayTrack(NewTrack(type, "", url))
                    │
[Phase 3] ロード開始
            LoadTrack(track)
                    │
            ┌───────┴───────────────────────┐
            │ Handler.Stop()                │
            │ SetPlayerType(trackType)      │
            │ Track = track                 │
            │ Handler.LoadUrl(url)    ★     │
            │ RequestSerialization()        │
            │ AfterTrackLoaded イベント     │
            └───────┬───────────────────────┘
                    │
[Phase 4] SDK処理
        ┌───────────┴───────────────┐
        │                           │
   BaseVideoPlayer            ImageViewer
   Handler                    Handler
        │                           │
   _baseVideoPlayer           VRCImageDownloader
   .LoadURL(url)              .DownloadImage(url)
        │                           │
   [VRChat SDK内部]           [VRChat SDK内部]
   URL解決・DL               画像DL
        │                           │
   ┌────┴────┐               ┌─────┴─────┐
   │         │               │           │
 Ready    Error           Success     Error
   │         │               │           │
   ▼         ▼               ▼           ▼
[Phase 5] コールバック
   OnVideoReady()          OnImageLoadSuccess()
        │                        │
        └────────┬───────────────┘
                 ▼
    Controller.AfterVideoReady()
                 │
         ┌───────┴───────────────────────┐
         │ _errorRetryCount = 0          │
         │ SyncedState == Playing?       │
         │   → Handler.Play() ★再生開始  │
         │ UpdateSyncedVideoTime(0)      │
         │ RequestSerialization()        │
         │ CheckRepeat() 開始           │
         └───────┬───────────────────────┘
                 │
[Phase 6] 再生開始
    Handler.Play() → _baseVideoPlayer.Play()
                 │
    OnVideoStart() → AfterVideoStarted()
                 │
[Phase 7] 再生中
    Controller.Update() 毎フレーム
         ├── EnsureVideoTime() 5秒毎に時刻同期
         └── UIController.Update()
              └── UpdateProgressView() プログレスバー更新

    BaseVideoPlayerHandler.Update() 毎フレーム
         └── テクスチャ取得 → AfterTextureUpdated()
              └── スクリーンに描画

[動画終了時]
    OnVideoEnd() → AfterVideoEnded()
         ├── AutoForward → Forward()
         │   ├── Queue → PlayTrackFromQueue()
         │   ├── Shuffle → ランダム選択
         │   └── Sequential → 次トラック
         └── Stop()
```

---

## Handler パターンの設計

### クラス階層

```
PlayerHandler (abstract)                     PlayerHandler.cs
├── PlayUrl(url)  / LoadUrl(url)             Play即時 / Load後Play
├── Play() / Pause() / Stop()
├── Time, Duration, IsPlaying, IsLive...
├── FallbackHandler                          フォールバック先
└── UseFallbackHandler                       フォールバック有効フラグ
    │
    ├── BaseVideoPlayerHandler (sealed)      BaseVideoPlayerHandler.cs
    │   ├── _baseVideoPlayer (BaseVRCVideoPlayer)
    │   ├── _animator (速度・解像度制御)
    │   ├── Blit処理 (AVPro色空間変換)
    │   └── VRCビデオイベント受信
    │
    └── ImageViewerHandler                   ImageViewerHandler.cs
        ├── VRCImageDownloader
        └── 静的画像表示 (Duration/Time なし)
```

### PlayUrl vs LoadUrl の違い

```
PlayUrl(url):  ロード完了後に自動再生 (_playImmediately = true)
LoadUrl(url):  ロードのみ、再生は Controller が制御
               → AfterVideoReady で SyncedState に応じて Play/Stop
```

YamaPlayer は `LoadUrl` を使い、再生制御を Controller に集約している。

### フォールバックハンドラー

```
例: AVPro → Unity へのフォールバック

AVProHandler (primary)
    │
    ├── _fallbackHandler = UnityHandler
    │
    ├── PlayerError発生 (1回目, _useFallbackAfterErrors=1):
    │   └── UseFallbackHandler = true
    │       └── 以後全操作が _fallbackHandler に委譲
    │
    └── リトライ → UnityHandler.LoadUrl(url)
```

---

## 状態管理

### PlayerState

```csharp
public enum PlayerState
{
    Idle,      // 停止中 (何も再生していない)
    Playing,   // 再生中
    Paused,    // 一時停止中
}
```

iwaSync3 のビットフラグ方式と異なり、YamaPlayer は enum + Handler の実状態の2層管理:

```
SyncedState: ネットワーク同期される意図された状態 (byte)
State:       Handler から導出される実際の状態
             Handler.IsStopped → Idle
             Handler.IsPaused  → Paused
             Handler.IsPlaying → Playing
```

### 状態遷移図

```
        PlayTrack()          AfterVideoReady()
Idle ──────────────→ [Loading] ──────────────→ Playing
  ▲                                              │ │
  │                                              │ │
  │     Stop()              Pause()              │ │
  ├──────────────────────────────────── Playing ◄─┘ │
  │                                     │           │
  │                                     ▼           │
  │                                   Paused        │
  │                                     │           │
  │                         Play()      │           │
  │                    ◄────────────────┘           │
  │                                                  │
  │                    OnVideoEnd()                  │
  ├──────────────────────────────────────────────────┘
  │
  │  Forward() / AutoForward
  └────→ 次のトラック → PlayTrack() → [Loading] → ...
```

---

## イベントシステム

YamaPlayer には **2つの独立したリスナー配列** が存在する。これらは別々のクラスに属し、別々の用途を持つ。

### 1. Controller._listeners[] — After系イベント (Controller.cs:35)

`YamaPlayerListener[]` 型。Controller が管理し、再生状態変化を通知する。

```
Controller._listeners[] に登録されるもの:
    ├── UIController        (UI更新)
    ├── QueueList          (キュー同期)
    ├── HistoryList        (履歴同期)
    ├── YamaPlayerModule[] (各種モジュール)
    └── 外部カスタムリスナー

登録: Controller.AddListener(listener)
通知: Controller.SendCustomVideoEvent(eventName)
      → 全リスナーに SendCustomEvent() でブロードキャスト
```

**主要イベント一覧**:

| イベント | 発火タイミング |
|---------|--------------|
| `AfterVideoReady` | 動画ロード完了 |
| `AfterVideoStarted` | 再生開始 |
| `AfterVideoPlayed` | 一時停止からの復帰 |
| `AfterVideoPaused` | 一時停止 |
| `AfterVideoStopped` | 停止 (履歴追加) |
| `AfterVideoEnded` | 動画終了 (AutoForward) |
| `AfterVideoErrorOccurred` | エラー発生 |
| `AfterVideoLooped` | ループ再生 |
| `AfterVideoRetry` | エラーリトライ |
| `AfterTrackUpdated` | トラック変更 |
| `AfterTrackLoaded` | トラックロード |
| `AfterTrackSynced` | ネットワーク同期受信 |
| `AfterQueueUpdated` | キュー変更 |
| `AfterHistoryUpdated` | 履歴変更 |
| `AfterPlayerHandlerChanged` | ハンドラー切替 |
| `AfterTextureUpdated` | テクスチャ更新 (毎フレーム) |
| `AfterTimeChanged` | シーク |
| `AfterSpeedChanged` | 速度変更 |
| `AfterLoopChanged` | ループ切替 |
| `AfterShufflePlayChanged` | シャッフル切替 |
| `AfterVolumeChanged` | 音量変更 |
| `AfterMuteChanged` | ミュート切替 |

### 2. UIController._listeners[] — Before系イベント (UIController.cs:109)

`UdonSharpBehaviour[]` 型。UIController が独自に管理する **別の配列**。ユーザー操作をキャンセルする権限チェック等に使われる。

```
UIController._listeners[] に登録されるもの:
    └── PermissionManagement 等の UI拡張モジュール

登録: UIController.AddListener(listener)
通知: UIController.InvokeBeforeEvent(eventName)
      → 各リスナーに SendCustomEvent()
      → いずれかが CancelCurrentAction() を呼ぶと操作中止
```

```csharp
// UIController.cs:154-165
public bool InvokeBeforeEvent(string eventName)
{
    _actionCancelled = false;
    for (int i = 0; i < _listeners.Length; i++)
    {
        _listeners[i].SendCustomEvent(eventName);
        if (_actionCancelled) break;
    }
    return !_actionCancelled;  // false = 操作キャンセル
}

// 使用例: 権限管理モジュールがキャンセル可能
if (!InvokeBeforeEvent("BeforeUserPlayTrack"))
{
    UpdateUI();
    return;  // 操作キャンセル
}
```

**重要**: この2つのリスナー配列は完全に独立している。Controller の After系イベントと UIController の Before系イベントは異なるイベント基盤で動作する。

---

## 音声制御

```csharp
// Controller.Audio.cs
Volume: 0.0 ~ 1.0 (全AudioSourceに適用)
Mute: true/false
Pitch:
    - ライブ配信 → 常に 1.0
    - UnityVideoPlayer → 常に 1.0
    - AVProVideoPlayer → _speed に追従
```

---

## iwaSync3 との比較

| 項目 | iwaSync3 | YamaPlayer |
|------|----------|------------|
| ファイル数 | 2 (579行 + 179行) | 130+ ファイル |
| 設計パターン | 単一クラス | partial class + Handler + Observer |
| プレイヤー切替 | 手動 (Video/Live ボタン) | トラック単位で自動 |
| プレイリスト | なし (計画中) | 静的Playlist + 動的Queue + History |
| エラー処理 | フラグ表示のみ | 自動リトライ + フォールバック |
| 画像表示 | なし | ImageViewerHandler |
| 時刻同期方式 | ServerTimeInSeconds | NetworkDateTime.Ticks |
| 時刻同期間隔 | 毎フレーム (ドリフトチェック) | 5秒間隔 |
| モジュール拡張 | なし | 10種類のモジュール |
| 多言語対応 | なし | 10言語 |
| 速度制御 | なし | 可変速度 (Animator経由) |
| リピート機能 | なし | 区間リピート (ビットパック) |
| シャッフル | なし | あり |
| 権限管理 | masterOnly フラグ | PermissionManagement モジュール |
