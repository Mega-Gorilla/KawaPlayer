# URL Pool 方式プレイリストローダー — Unity 側設計

全体設計: [url-pool-playlist-loader.md](url-pool-playlist-loader.md)

---

## 責務

Unity 側の責務は以下の4つに限定する。

1. **ビルド時**: Pool 用 `VRCUrl[]` の生成
2. **ランタイム**: resolver URL の受け取り
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
    private VRCUrl _pendingResolverUrl;
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

```csharp
[Serializable]
public class PlaylistLoaderPoolSettings
{
    public string poolBaseUrl;   // 例: https://api.example.com
    public string poolId;        // 例: kawaplayer-main
    public int poolSize;         // 例: 10000 / 100000 (デフォルト: 100000)
}
```

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
| Pool Base URL | リダイレクトサーバーの URL (例: `https://api.example.com`) |
| Pool ID | このワールド固有の識別子 (例: `kawaplayer-main`) |
| Pool Size | スロット数 (デフォルト: 100,000。推定サイズ ~11-16MB) |
| **Generate Pool** ボタン | 上記設定から `VRCUrl[]` を生成し `_redirectPool` に保存 |
| **Validate Pool** ボタン | 生成済み Pool の整合性を検証 |

### Validate で確認する項目

- `poolBaseUrl` が `http://` or `https://` で始まる
- `poolId` が空でない
- `_redirectPool.Length == poolSize`
- 各 `VRCUrl.Get()` が `/vrcurl/{poolId}/{index}` のパターンに一致

---

## Phase B3: ランタイム処理フロー

```text
ユーザー入力                VRCStringDownloader         VRCJson              Queue
    │                            │                       │                   │
    │  resolver URL 入力         │                       │                   │
    │  (VRCUrlInputField)        │                       │                   │
    ├──────────────────────────>│                       │                   │
    │                            │  LoadUrl(resolverUrl)  │                   │
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
| resolver 直指定 | `https://api.example.com/playlist?src=https://example.com/list.json` | 開発・検証 |
| 事前登録プレイリスト | `https://api.example.com/playlists/abc123` | **実運用 (推奨)** |

実運用では事前登録方式を推奨。URL が短く、VRChat 内でのテキスト入力が容易。

### JSON パース

サーバーが返す index 付きレスポンスをパースする。`url` フィールドは含まれない（Unity 側は実 URL を知る必要がない）。

```text
レスポンス JSON
    │
    ├── "tracks" キーあり → 単一プレイリスト
    │     └── 各要素から index, title, mode を取得
    │
    └── "playlists" キーあり → 複数プレイリスト
          └── 各プレイリストの "tracks" を走査
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
| resolver URL ダウンロード失敗 | `Playlist server is unavailable` |
| JSON パース失敗 | `Failed to parse playlist response` |
| `index` フィールド欠落 | `Invalid track data (missing index)` |
| index が pool 範囲外 | `Pool index out of range` |
| オーナーシップ不足 | (TakeOwnership で自動取得) |
| 0 件追加 | `No tracks found in playlist` |
| 一部失敗 | `Added 8/12 tracks (4 failed)` |

---

## 擬似コード

```csharp
public void LoadPlaylistFromUrl(VRCUrl resolverUrl)
{
    if (_isLoading) return;
    _isLoading = true;
    _pendingResolverUrl = resolverUrl;
    if (Utilities.IsValid(_ui)) _ui.ShowLoading("Loading playlist...");
    VRCStringDownloader.LoadUrl(resolverUrl, (IUdonEventReceiver)this);
}

public override void OnStringLoadSuccess(IVRCStringDownload result)
{
    if (result.Url.Get() != _pendingResolverUrl.Get()) return;
    _isLoading = false;

    // JSON パース
    if (!VRCJson.TryDeserializeFromJson(result.Result, out DataToken root)
        || root.TokenType != TokenType.DataDictionary)
    {
        if (Utilities.IsValid(_ui)) _ui.ShowError("Failed to parse playlist response.");
        return;
    }

    // tracks 抽出 (単一/複数プレイリスト対応)
    DataList allTracks = ExtractAllTracks(root.DataDictionary);
    if (allTracks.Count == 0)
    {
        if (Utilities.IsValid(_ui)) _ui.ShowError("No tracks found.");
        return;
    }

    // index → VRCUrl → Track → Queue
    EnqueueFromIndexes(allTracks);
}

private void EnqueueFromIndexes(DataList trackDicts)
{
    int totalCount = trackDicts.Count;

    // UdonSharp では並列配列で管理
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
