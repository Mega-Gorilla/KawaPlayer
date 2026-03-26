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
    [SerializeField] private PlaylistLoaderUI _ui;
    [SerializeField] private VRCUrl[] _redirectPool = new VRCUrl[0];
    [SerializeField] private string _poolId;

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
    [SerializeField] private Text _statusText;
    [SerializeField] private GameObject _loadingIndicator;
}
```

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

| 失敗ケース | UI メッセージ例 |
|-----------|----------------|
| resolve URL ダウンロード失敗 | `Playlist server is unavailable` |
| JSON パース失敗 | `Failed to parse playlist response` |
| `index` フィールド欠落 | `Invalid track data (missing index)` |
| index が pool 範囲外 | `Pool index out of range` |
| オーナーシップ不足 | (TakeOwnership で自動取得) |
| 0 件追加 | `No tracks found in playlist` |
| 一部失敗 | `Added 8/12 tracks (4 failed)` |

---

## 擬似コード

```csharp
public void LoadPlaylistFromUrl(VRCUrl resolveUrl)
{
    if (_isLoading) return;
    _isLoading = true;
    _pendingResolveUrl = resolveUrl;
    if (Utilities.IsValid(_ui)) _ui.ShowLoading("Loading playlist...");
    VRCStringDownloader.LoadUrl(resolveUrl, (IUdonEventReceiver)this);
}

public override void OnStringLoadSuccess(IVRCStringDownload result)
{
    if (result.Url.Get() != _pendingResolveUrl.Get()) return;
    _isLoading = false;

    // JSON パース
    if (!VRCJson.TryDeserializeFromJson(result.Result, out DataToken root)
        || root.TokenType != TokenType.DataDictionary)
    {
        if (Utilities.IsValid(_ui)) _ui.ShowError("Failed to parse playlist response.");
        return;
    }

    var rootDict = root.DataDictionary;

    // "ok" チェック
    if (rootDict.TryGetValue("ok", out DataToken okToken)
        && okToken.TokenType == TokenType.Boolean
        && !okToken.Boolean)
    {
        string error = "Playlist server returned an error.";
        if (rootDict.TryGetValue("error", out DataToken errToken)
            && errToken.TokenType == TokenType.String)
            error = errToken.String;
        if (Utilities.IsValid(_ui)) _ui.ShowError(error);
        return;
    }

    // "tracks" 配列を取得
    if (!rootDict.TryGetValue("tracks", out DataToken tracksToken)
        || tracksToken.TokenType != TokenType.DataList
        || tracksToken.DataList.Count == 0)
    {
        if (Utilities.IsValid(_ui)) _ui.ShowError("No tracks found in playlist.");
        return;
    }

    // index → VRCUrl → Track → Queue
    EnqueueFromIndexes(tracksToken.DataList);
}

public override void OnStringLoadError(IVRCStringDownload result)
{
    if (result.Url.Get() != _pendingResolveUrl.Get()) return;
    _isLoading = false;
    if (Utilities.IsValid(_ui)) _ui.ShowError("Playlist server is unavailable.");
}

private void EnqueueFromIndexes(DataList trackDicts)
{
    int totalCount = trackDicts.Count;

    // UdonSharp ではカスタム DTO 配列を使わず DataDictionary を直接処理
    object[][] tempTracks = new object[totalCount][];
    int addedCount = 0;
    int failedCount = 0;

    for (int i = 0; i < totalCount; i++)
    {
        if (trackDicts[i].TokenType != TokenType.DataDictionary)
        {
            failedCount++;
            continue;
        }
        var dict = trackDicts[i].DataDictionary;

        // index (必須)
        int index = TryGetInt(dict, "index", -1);
        if (index < 0 || index >= _redirectPool.Length)
        {
            failedCount++;
            continue;
        }

        int mode = TryGetInt(dict, "mode", 0);
        string title = "";
        if (dict.TryGetValue("title", out DataToken t)
            && t.TokenType == TokenType.String)
            title = t.String;

        VRCUrl redirectUrl = _redirectPool[index];
        tempTracks[addedCount] = TrackUtils.NewTrack(
            (VideoPlayerType)mode, title, redirectUrl);
        addedCount++;
    }

    if (addedCount == 0)
    {
        if (Utilities.IsValid(_ui)) _ui.ShowError("No valid tracks to add.");
        return;
    }

    // 実際のサイズに切り詰め
    object[][] finalTracks = new object[addedCount][];
    for (int i = 0; i < addedCount; i++) finalTracks[i] = tempTracks[i];

    _controller.TakeOwnership();
    _controller.Queue.AddTracks(finalTracks);

    var message = failedCount > 0
        ? $"Added {addedCount}/{totalCount} tracks ({failedCount} failed)"
        : $"Added {addedCount} tracks to queue";
    if (Utilities.IsValid(_ui)) _ui.ShowSuccess(message);
}

private int TryGetInt(DataDictionary dict, string key, int defaultValue)
{
    if (!dict.TryGetValue(key, out DataToken token)) return defaultValue;
    if (token.TokenType == TokenType.Double) return (int)token.Double;
    if (token.TokenType == TokenType.Float) return (int)token.Float;
    if (token.TokenType == TokenType.Int) return token.Int;
    if (token.TokenType == TokenType.Long) return (int)token.Long;
    return defaultValue;
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
