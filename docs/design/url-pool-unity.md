# URL Pool 方式プレイリストローダー — Unity 側設計

全体設計: [url-pool-playlist-loader.md](url-pool-playlist-loader.md)

---

## 責務

Unity 側の責務は以下の4つに限定する。

1. **ビルド時**: Pool 用 `VRCUrl[]` の生成
2. **ランタイム**: resolve URL の受け取り
3. **ランタイム**: index 付きレスポンス JSON のパース
4. **ランタイム**: index を使った Queue 追加

Unity 側は **実際の動画 URL を一切知らなくても動作する**。

---

## ファイル構成

```text
Modules/
└── PlaylistLoader/
    ├── PlaylistLoader.cs              ← メインロジック (UdonSharpBehaviour)
    ├── PlaylistLoaderUI.cs            ← 専用 UI
    ├── PlaylistLoader.prefab          ← プレハブ
    ├── Localization.Editor.json
    ├── Localization.Runtime.json
    ├── Yamadev.YamaStream.Modules.PlaylistLoader.asmdef
    └── Editor/
        ├── PlaylistLoaderEditor.cs    ← Inspector 拡張
        ├── PlaylistLoaderPoolGenerator.cs  ← Pool 生成ツール
        └── Yamadev.YamaStream.Modules.PlaylistLoader.Editor.asmdef
```

---

## データモデル

### PlaylistLoader

```csharp
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PlaylistLoader : YamaPlayerModule
{
    [SerializeField] private VRCUrl[] _redirectPool = new VRCUrl[0];
    [SerializeField] private string _poolId = "default";
    [SerializeField] private string _poolBaseUrl = "https://playlist.vrc-hub.com";
    [SerializeField] private int _poolSize = 100000;

    private bool _isLoading;
    private VRCUrl _pendingResolveUrl;
}
```

### PlaylistLoaderUI

```csharp
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PlaylistLoaderUI : YamaPlayerListener
{
    [SerializeField] private PlaylistLoader _loader;
    [SerializeField] private VRCUrlInputField _playlistUrlInput;
}
```

PlaylistLoaderUI は URL 入力の受け渡しのみに特化。成功/エラー通知はログ出力 (`PrintLog` / `PrintError`) のみ。

### Editor 用設定

| 項目 | 値 | 編集 |
|------|-----|------|
| Pool Base URL | `https://playlist.vrc-hub.com` | 固定（読み取り専用） |
| Pool ID | `default` | 編集可能 |
| Pool Size | `100000` | 固定（読み取り専用） |

Pool Base URL と Pool Size はサーバー側で固定されているため、Inspector で編集不可。Pool ID はワールド固有の識別子として編集可能。

---

## Phase B2: ビルド時 Pool 生成

Editor スクリプトが `poolBaseUrl`, `poolId`, `poolSize` から `VRCUrl[]` を生成する。

```csharp
for (int i = 0; i < poolSize; i++)
{
    pool[i] = new VRCUrl($"{poolBaseUrl}/vrcurl/{poolId}/{i}");
}
```

生成結果は `PlaylistLoader._redirectPool` に焼き込む。`new VRCUrl(string)` はエディタ時に使用可能。

### Inspector UI

| 項目 | 説明 |
|------|------|
| Pool Base URL | `https://playlist.vrc-hub.com` (読み取り専用) |
| Pool ID | サーバーの Pool ID (デフォルト: `default`) |
| Pool Size | `100,000` (読み取り専用。実測 5.33MB) |
| **Generate Pool** ボタン | Pool ID をサーバーで検証後、`VRCUrl[]` を生成して `_redirectPool` に保存 |

### Generate Pool 時のサーバー検証

Generate Pool 実行前にサーバーに `/r/{poolId}/_validate` をリクエストし、Pool ID の有効性を確認する。

| サーバー応答 | 動作 |
|------------|------|
| Pool ID 有効 | Pool 生成を続行 |
| `Unknown pool` | エラーダイアログを表示し、生成を中止 |
| 接続失敗 | 警告ダイアログ（生成続行 or キャンセルを選択可能） |

---

## Phase B3: ランタイム処理フロー

```text
ユーザー入力                VRCStringDownloader         VRCJson              Queue
    │                            │                       │                   │
    │  resolve URL 入力          │                       │                   │
    │  (VRCUrlInputField)        │                       │                   │
    ├──────────────────────────>│                       │                   │
    │                            │  LoadUrl(resolveUrl)   │                   │
    │                            ├─── HTTP GET ─────────>│ (サーバー)         │
    │                            │                       │                   │
    │                            │<── index 付き JSON ───│                   │
    │                            │                       │                   │
    │                       OnStringLoadSuccess           │                   │
    │                            │                       │                   │
    │                            │  JSON パース ─────────>│                   │
    │                            │                       │  tracks 抽出       │
    │                            │                       │                   │
    │                            │  _redirectPool[index] で Track 構築        │
    │                            │                       │                   │
    │                            │  AddTracks() ────────────────────────────>│
    │                            │                       │                   │
    │                       UI: "Added N tracks"         │                   │
```

### 入力 URL の2つのパターン

| パターン | URL 例 | 用途 |
|---------|--------|------|
| resolve URL | `https://api.example.com/r/{poolId}/{playlistId}` | VRChat から入力 |

プレイリストはサーバーの Web UI で作成・管理し、resolve URL を VRChat に入力する。Web ページ (`/playlists/{id}`) にコピー用の VRChat URL が表示される。

### JSON パース

Resolve API が返す index 付きレスポンスをパースする。1回のリクエストで1プレイリストが返される。`url` フィールドは含まれない（Unity 側は実 URL を知る必要がない）。

```json
{
  "ok": true,
  "pool": "kawaplayer-main",
  "name": "お気に入りカラオケ",
  "tracks": [
    { "index": 42, "title": "Song A", "mode": 0 },
    { "index": 43, "title": "Song B", "mode": 0 }
  ]
}
```

```text
パース手順:
  1. "ok" が true であることを確認
  2. "tracks" 配列を取得
  3. 各要素から index, title, mode を取得
  4. _redirectPool[index] で VRCUrl に変換
```

### Queue 追加

```csharp
// index から pre-baked VRCUrl を取得し Track を構築
var redirectUrl = _redirectPool[index];
var track = TrackUtils.NewTrack((VideoPlayerType)mode, title, redirectUrl);
```

複数件を一括追加するため、`QueueList` に `AddTracks(object[][] tracks)` メソッドを追加する（既存 `AddTrack` の拡張。同期 1 回・イベント 1 回）。

---

## エラー処理

エラーはログ出力 (`PrintError` / `PrintWarning`) のみ。UI ダイアログは表示しない。

| 失敗ケース | ログ出力 | ユーザーの見え方 |
|-----------|---------|----------------|
| resolve URL ダウンロード失敗 | `PrintError("Failed to download playlist: ...")` | 何も起きない |
| JSON パース失敗 | `PrintError("Failed to parse playlist response.")` | 何も起きない |
| サーバーエラー (ok: false) | `PrintError(error)` | 何も起きない |
| トラック 0 件 | `PrintWarning("No tracks found in playlist.")` | 何も起きない |
| 一部失敗 | `PrintLog("Added 3/5 tracks (2 failed)")` | Queue に部分追加 |
| 成功 | `PrintLog("Added 5 tracks to queue")` | Queue に追加 + 自動再生 |

---

## 擬似コード

```csharp
// --- PlaylistLoader.cs ---

public void LoadPlaylistFromUrl(VRCUrl resolveUrl)
{
    if (_isLoading) return;
    _isLoading = true;
    _pendingResolveUrl = resolveUrl;
    VRCStringDownloader.LoadUrl(resolveUrl, (IUdonEventReceiver)this);
    PrintLog($"Downloading playlist from {resolveUrl.Get()}...");
}

public override void OnStringLoadSuccess(IVRCStringDownload result)
{
    if (result.Url.Get() != _pendingResolveUrl.Get()) return;
    _isLoading = false;

    if (!TryParseResponse(result.Result, out DataList tracks)) return;
    var builtTracks = BuildTracks(tracks, out int failedCount);
    if (builtTracks == null) return;
    EnqueueAndPlay(builtTracks, tracks.Count, failedCount);
}

public override void OnStringLoadError(IVRCStringDownload result)
{
    if (result.Url.Get() != _pendingResolveUrl.Get()) return;
    _isLoading = false;
    PrintError($"Failed to download playlist: {result.Error}");
}

// TryParseResponse: JSON パース + ok チェック + tracks 抽出
// BuildTracks: DataList → object[][] 変換 (pool 範囲外はスキップ)
// EnqueueAndPlay: Queue 追加 + 停止中なら自動再生

private void EnqueueAndPlay(object[][] tracks, int totalCount, int failedCount)
{
    _controller.TakeOwnership();
    _controller.Queue.AddTracks(tracks);

    // 自動再生: 停止中のみ。再生中・一時停止中はキュー追加のみ
    if (_controller.Stopped)
    {
        _controller.Forward();
    }

    PrintLog(failedCount > 0
        ? $"Added {tracks.Length}/{totalCount} tracks ({failedCount} failed)"
        : $"Added {tracks.Length} tracks to queue");
}

// --- PlaylistLoaderUI.cs ---

public void OnPlaylistUrlSubmit()
{
    if (!Utilities.IsValid(_playlistUrlInput) || !Utilities.IsValid(_loader)) return;
    if (_loader.IsLoading) return;
    var url = _playlistUrlInput.GetUrl();
    if (!Utilities.IsValid(url) || string.IsNullOrEmpty(url.Get())) return;
    _playlistUrlInput.SetUrl(VRCUrl.Empty);
    _loader.LoadPlaylistFromUrl(url);
}
```

> **注**: `TrackDraft` のようなカスタム構造体は UdonSharp では使えないため、`DataDictionary` から直接値を取得するパターンを使用している。

---

## QueueList.AddTracks (新規追加メソッド)

`Runtime/Internal/Playlist/QueueList.cs` に追加する。既存の `AddTrack()` は変更しない。

```csharp
public void AddTracks(object[][] tracks)
{
    if (!Utilities.IsValid(tracks) || tracks.Length == 0) return;

    int currentLength = _tracks.Length;
    int addLength = tracks.Length;
    object[][] newTracks = new object[currentLength + addLength][];

    for (int i = 0; i < currentLength; i++)
        newTracks[i] = _tracks[i];
    for (int i = 0; i < addLength; i++)
        newTracks[currentLength + i] = tracks[i];
    _tracks = newTracks;

    if (Networking.IsOwner(_controller.gameObject) && !_controller.IsLocal)
        RequestSerialization();
    _controller.SendCustomVideoEvent(nameof(AfterQueueUpdated));
}
```
