# YamaPlayer: プレイリスト機能パイプライン解析

## 概要

YamaPlayer のプレイリストシステムは3つのリスト構造から成る:

| コンポーネント | クラス | 同期モード | 役割 |
|--------------|--------|-----------|------|
| **Playlist** | `Playlist` | None (同期なし) | エディタ時に定義される静的プレイリスト |
| **QueueList** | `QueueList` | Manual (手動同期) | ランタイムに動的追加されるキュー |
| **HistoryList** | `HistoryList` | Manual (手動同期) | 再生済みトラックの履歴 |

**関連ファイル**:
```
Runtime/Internal/Playlist/
    ├── Playlist.cs          静的プレイリスト (UdonSharpBehaviour)
    ├── PlaylistItem.cs      プレイリストデータ (MonoBehaviour, エディタ用)
    ├── PlaylistManager.cs   プレイリスト管理 (MonoBehaviour, エディタ用)
    ├── QueueList.cs         動的キュー (UdonSharpBehaviour, ネットワーク同期)
    └── HistoryList.cs       再生履歴 (UdonSharpBehaviour, ネットワーク同期)

Runtime/Internal/
    ├── Controller.Playlist.cs   コントローラのプレイリスト制御ロジック
    └── Utils/TrackUtils.cs      トラックデータ操作ユーティリティ

Runtime/Internal/UI/
    └── UIController.Playlist.cs プレイリストUI

Editor/Playlist/
    ├── PlaylistData.cs          プレイリストエディタ用データ
    ├── PlaylistBuildProcess.cs   ビルド時変換処理
    ├── PlaylistExporter.cs      JSON エクスポート/インポート
    ├── PlaylistImporter.cs      他プレイヤーからの移行
    ├── PlaylistEditorWindow.cs  エディタウィンドウ
    ├── PlaylistItemEditor.cs    インスペクタ拡張
    ├── PlaylistManagerEditor.cs マネージャーインスペクタ
    └── YtdlpResolver.cs        yt-dlp によるメタデータ取得
```

---

## プレイリストデータの流れ (ビルドパイプライン)

YamaPlayer のプレイリストは **2段階のデータ変換** を経る:

```
[エディタ時]                        [ビルド時]                    [ランタイム]
PlaylistItem                       PlaylistBuildProcess           Playlist
(MonoBehaviour)          ──────>   (IYamaPlayerBuildProcess)  ──> (UdonSharpBehaviour)

┌─────────────────┐               ┌──────────────────┐         ┌──────────────────┐
│ playlistName    │               │ AddUdonSharp     │         │ _playlistName    │
│ tracks[]        │     ビルド    │ Component<>()    │  実行   │ _videoPlayerTypes│
│   .playerType   │  ──────────>  │ SetProgram      │ ──────> │ _titles[]        │
│   .title        │               │ Variable()      │         │ _urls[]          │
│   .url          │               └──────────────────┘         │ _tracks[][] (lazy)│
│ youtubePlaylistId│                                           └──────────────────┘
└─────────────────┘
```

### PlaylistItem (エディタ時データ)

```csharp
// PlaylistItem.cs - MonoBehaviour (エディタ専用)
public class PlaylistItem : MonoBehaviour
{
    public string playlistName;
    public PlaylistTrack[] tracks;
    public string youtubePlaylistId;
}

[Serializable]
public class PlaylistTrack
{
    public VideoPlayerType playerType;  // Unity / AVPro / ImageViewer
    public string title;
    public string url;
}
```

### PlaylistBuildProcess (ビルド時変換)

```csharp
// PlaylistBuildProcess.cs
// VRChat ワールドビルド時に自動実行される
public class PlaylistBuildProcess : IYamaPlayerBuildProcess
{
    public void Process()
    {
        // PlaylistManager から PlaylistItem[] を取得
        // 各 PlaylistItem に対して:
        //   1. UdonSharpComponent<Playlist> を追加
        //   2. SetProgramVariable で配列データを注入
        //     _playlistName, _videoPlayerTypes[], _titles[], _urls[]
    }
}
```

### Playlist (ランタイムデータ)

```csharp
// Playlist.cs - UdonSharpBehaviour (ランタイム)
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]  // 同期なし (静的)
public class Playlist : YamaPlayerListener
{
    [SerializeField] string _playlistName;
    [SerializeField] VideoPlayerType[] _videoPlayerTypes;
    [SerializeField] string[] _titles;
    [SerializeField] VRCUrl[] _urls;
    private object[][] _tracks;  // 遅延初期化

    // アクセス時に初めて object[][] に変換 (_initialized ガード付き)
    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;
        GenerateTracks();
    }

    public object[] GetTrack(int index)
    {
        EnsureInitialized();
        return _tracks[index];  // [playerType, title, url]
    }
}
```

---

## JSON インポート/エクスポート

### JSON フォーマット

```json
{
  "playlists": [
    {
      "active": true,
      "name": "プレイリスト名",
      "youtubeListId": "PLxxxxxxxxxxxxxxxx",
      "tracks": [
        {
          "mode": 0,
          "title": "動画タイトル",
          "url": "https://www.youtube.com/watch?v=XXXXX"
        },
        {
          "mode": 1,
          "title": "ライブ配信",
          "url": "https://www.youtube.com/watch?v=YYYYY"
        }
      ]
    }
  ]
}
```

**mode (playerType) の値**:
| 値 | VideoPlayerType | 用途 |
|---|----------------|------|
| 0 | UnityVideoPlayer | 標準動画 |
| 1 | AVProVideoPlayer | AVPro動画/ライブ |
| 2 | ImageViewer | 静止画 |

### エクスポート処理

```csharp
// PlaylistExporter.cs
PlaylistExporter.Export(playlists)
    │
    ├── PlaylistItem[] → PlaylistData[] に変換
    ├── JsonConvert.SerializeObject (camelCase)
    └── EditorUtility.SaveFilePanel → File.WriteAllText
```

### インポート処理

```csharp
// PlaylistExporter.cs (Import メソッド)
PlaylistExporter.Import()
    │
    ├── EditorUtility.OpenFilePanel → JSON ファイル選択
    ├── JObject.Parse(jsonStr)
    ├── "playlists" 配列を取得
    └── 各要素を ParsePlaylistData():
        ├── active, name, youtubeListId を抽出
        │   (camelCase / PascalCase 両対応)
        └── tracks[] を ParsePlaylistTrack():
            ├── mode → VideoPlayerType
            ├── title → string
            └── url → string
```

### 他プレイヤーからの移行 (PlaylistImporter)

```
PlaylistImporter は以下のプレイヤーからの移行をサポート:
    ├── iwaSync3
    ├── Kinel VideoPlayer
    ├── VizVid (VVMW)
    ├── USharp Video
    └── ProTV
```

---

## ランタイム プレイリスト パイプライン

### Controller.Playlist.cs の構造

```csharp
public partial class Controller
{
    // 静的プレイリスト
    [SerializeField] Playlist[] _playlists;           // ビルド時注入
    [UdonSynced] int _activePlaylistIndex = -1;       // 再生中のプレイリスト
    [UdonSynced] int _playingTrackIndex = -1;         // 再生中のトラック番号

    // 動的リスト
    [SerializeField] QueueList _queue;
    [SerializeField] HistoryList _history;

    // シャッフル
    [UdonSynced] bool _shuffle;

    // 自動進行
    [SerializeField] float _forwardInterval = 0;      // 0秒 = 即時進行
    bool _autoForward;
}
```

### プレイリスト初期化

```
Controller.Start()
    │
    └── ReadPlaylists()
        └── _playlists = GetComponentsInChildren<Playlist>()
            子オブジェクトから全 Playlist コンポーネントを収集
```

---

## プレイリストからの再生パイプライン

### 1. プレイリストからの直接再生

```
ユーザー操作: プレイリスト内のトラックをクリック
    │
    ▼
UIController.PlayPlaylistTrack()
    │
    ▼
Controller.PlayTrack(Playlist playlist, int index)
                                            (Controller.Playlist.cs:96-120)
    │
    ├── バリデーション:
    │   ├── playlist が有効か
    │   ├── index が範囲内か
    │   └── URL が有効か
    │
    ├── 再生中かつオーナー → Stop()
    │
    ├── _activePlaylistIndex = Array.IndexOf(_playlists, playlist)
    ├── _playingTrackIndex = index
    │   └── この2つが UdonSynced → 全クライアントに同期
    │
    ├── _syncedState = Playing
    │
    └── LoadTrack(track)
        └── (url-to-playback-pipeline.md Phase 3 参照)
```

### 2. キューからの再生

```
Controller.PlayTrackFromQueue()              (Controller.Playlist.cs:78-86)
    │
    ├── _queue.TrackCount == 0 → return
    │
    ├── track = _queue.GetTrack(0)          先頭トラック取得
    ├── PlayTrack(track)                    再生開始
    │   └── ClearPlaylistIndexes()          プレイリストインデックスクリア
    │
    └── _queue.RemoveTrack(0)               キューから削除
        └── RequestSerialization()          他クライアントに同期
```

### 3. 履歴からの再生

```
Controller.PlayTrackFromHistory(int index)   (Controller.Playlist.cs:88-94)
    │
    ├── バリデーション
    ├── track = _history.GetTrack(index)
    └── PlayTrack(track)
```

---

## キュー管理パイプライン

### キューへの追加

```
ユーザー操作: URL入力中にモーダルで「キューに追加」選択
           or プレイリスト内トラックの「キューに追加」ボタン
    │
    ▼
UIController.AddUrlToQueueEventInternal(playerType, url)
                                            (UIController.cs:342-352)
    │
    ├── InvokeBeforeEvent("BeforeUserAddTrackToQueue")
    │   └── 権限チェック等
    │
    ├── _controller.TakeOwnership()
    │
    └── _controller.Queue.AddTrack(
            TrackUtils.NewTrack(playerType, "", url)
        )
            │
            ▼
QueueList.AddTrack(track)                    (QueueList.cs:46-62)
    │
    ├── _tracks 配列の末尾に track を追加
    │   (新しい配列を作成してコピー + 追加)
    │
    ├── オーナーかつ非ローカル:
    │   └── RequestSerialization()
    │       │
    │       └── OnPreSerialization()          (QueueList.cs:116-130)
    │           ├── _tracks[][] を分解:
    │           │   _videoPlayerTypes[] ← track[0]
    │           │   _titles[]           ← track[1]
    │           │   _urls[]             ← track[2]
    │           └── [UdonSynced] で同期
    │                   │
    │                   ▼ (他クライアント)
    │               OnDeserialization()       (QueueList.cs:132-136)
    │               ├── GenerateTracks()
    │               │   3つの配列 → _tracks[][] に再構築
    │               └── AfterQueueUpdated イベント
    │
    └── AfterQueueUpdated イベント発火
        └── UIController がプレイリストUI更新
```

### キューの並べ替え

```
QueueList.MoveUp(index)                      (QueueList.cs:86-99)
    ├── _tracks[index] と _tracks[index-1] をスワップ
    ├── RequestSerialization()
    └── AfterQueueUpdated

QueueList.MoveDown(index)                    (QueueList.cs:101-114)
    ├── _tracks[index] と _tracks[index+1] をスワップ
    ├── RequestSerialization()
    └── AfterQueueUpdated
```

### キューからの削除

```
QueueList.RemoveTrack(index)                 (QueueList.cs:64-84)
    ├── 新配列作成 (index を除外してコピー)
    ├── RequestSerialization()
    └── AfterQueueUpdated
```

---

## 履歴管理パイプライン

```
動画停止時 (AfterVideoStopped)              (Controller.Events.cs:43-76)
    │
    ├── オーナーかつ非リロード:
    │   ├── Track の URL が空でない場合:
    │   │   └── _history.AddTrack(Track)     履歴に追加
    │   │       │
    │   │       └── HistoryList.AddTrack()   (HistoryList.cs:47-63)
    │   │           ├── _tracks 末尾に追加
    │   │           ├── RequestSerialization()
    │   │           └── AfterHistoryUpdated イベント
    │   │
    │   ├── Track = CreateEmptyTrack()       トラッククリア
    │   ├── ResetSyncedVideoTime()
    │   └── RequestSerialization()
    │
    └── 全リスナーに AfterVideoStopped 通知
```

---

## 自動進行 (Forward / AutoForward)

### 動画終了時の自動進行

```
OnVideoEnd() → AfterVideoEnded()
    │
    ├── _forwardInterval >= 0:
    │   ├── _autoForward = true
    │   └── SendCustomEventDelayedSeconds("AutoForward", _forwardInterval)
    │
    └── _forwardInterval < 0:
        └── ClearPlaylistIndexes() (自動進行無効)

AutoForward()                                (Controller.Playlist.cs:151-155)
    │
    ├── _autoForward == false → return (キャンセル済み)
    └── Forward()
```

### Forward() のトラック選択ロジック

```
Controller.Forward()                         (Controller.Playlist.cs:157-177)
    │
    ├── 1. キューにトラックがある場合:
    │   └── PlayTrackFromQueue()             キュー優先
    │       (キュー先頭を再生、キューから削除)
    │
    ├── 2. プレイリスト内で再生中の場合:
    │   │
    │   ├── シャッフル ON:
    │   │   └── next = GetRandomIndex(trackCount, exclude=current)
    │   │       現在のトラックを除外してランダム選択
    │   │
    │   └── シャッフル OFF:
    │       └── next = (_playingTrackIndex + 1) % trackCount
    │           順番に次へ (末尾→先頭に循環)
    │   │
    │   └── PlayTrack(ActivePlaylist, next)
    │
    └── 3. プレイリスト外: 何もしない
```

### Backward() のトラック選択ロジック

```
Controller.Backward()                        (Controller.Playlist.cs:122-127)
    │
    ├── ActivePlaylist 無効 or index < 0 → return
    │
    └── next = (_playingTrackIndex - 1 < 0)
              ? ActivePlaylist.TrackCount - 1   先頭→末尾
              : _playingTrackIndex - 1          1つ前
        └── PlayTrack(ActivePlaylist, next)
```

---

## シャッフル

```csharp
// Controller.Playlist.cs
[UdonSynced, FieldChangeCallback(nameof(ShufflePlay))]
private bool _shuffle = false;

public bool ShufflePlay
{
    get => _shuffle;
    set
    {
        _shuffle = value;
        RequestSerialization();  // 全クライアントに同期
        // AfterShufflePlayChanged イベント発火
    }
}

// ランダムインデックス生成 (現在のトラックを除外)
public int GetRandomIndex(int trackCount, int exclude = -1)
{
    var r = new System.Random();
    int next = r.Next(0, hasExclude ? trackCount - 1 : trackCount);
    return (hasExclude && next >= exclude) ? next + 1 : next;
}
```

---

## ネットワーク同期のまとめ

### 同期されるプレイリスト関連変数

```
Controller (BehaviourSyncMode.Manual):
    [UdonSynced] int _activePlaylistIndex    // どのプレイリストか
    [UdonSynced] int _playingTrackIndex      // 何番目のトラックか
    [UdonSynced] bool _shuffle               // シャッフルモード
    [UdonSynced] VRCUrl _url                 // 再生中URL
    [UdonSynced] string _title               // トラックタイトル
    [UdonSynced] VideoPlayerType _playerType  // プレイヤー種別

QueueList (BehaviourSyncMode.Manual):
    [UdonSynced] VideoPlayerType[] _videoPlayerTypes
    [UdonSynced] string[] _titles
    [UdonSynced] VRCUrl[] _urls

HistoryList (BehaviourSyncMode.Manual):
    [UdonSynced] VideoPlayerType[] _videoPlayerTypes
    [UdonSynced] string[] _titles
    [UdonSynced] VRCUrl[] _urls

Playlist (BehaviourSyncMode.None):
    同期なし (全クライアントに同一データがビルド時に埋め込み済み)
```

### QueueList/HistoryList のシリアライゼーション

```
object[][] _tracks  ←─(ランタイム表現)─→  3つの[UdonSynced]配列

OnPreSerialization():
    _tracks[][] → _videoPlayerTypes[] + _titles[] + _urls[]

OnDeserialization():
    _videoPlayerTypes[] + _titles[] + _urls[] → _tracks[][]
```

UdonSharp の制約により `object[][]` は直接同期できないため、プリミティブ配列に分解して同期する。

### オーナーシップ連動

```csharp
// QueueList.cs:138-150 / HistoryList.cs:87-99
public override void AfterOwnerChanged()
{
    // Controller のオーナーが変わったら追随して取得を試みる
    if (Networking.IsOwner(_controller.gameObject))
        TakeOwnership();
}
```

Controller のオーナーシップが移転すると、`OnOwnershipTransferred` → `AfterOwnerChanged` イベントを通じて QueueList / HistoryList も追随してオーナーシップを取得する。設計意図として Controller, QueueList, HistoryList は同一オーナーになるよう同期されるが、これは不変条件ではなくイベント駆動の追随取得である (`Controller.Events.cs:247-255`)。

---

## 完全なプレイリストパイプライン図

```
┌────────────────────────────────────────────────────────────────────────┐
│              YamaPlayer プレイリストパイプライン                        │
└────────────────────────────────────────────────────────────────────────┘

═══ ビルド時 (エディタ) ═══

[JSON Import]              [YouTube Playlist]        [手動入力]
playlists.json             yt-dlp メタデータ取得     Inspectorで編集
     │                          │                        │
     ▼                          ▼                        ▼
PlaylistExporter.Import()  YtdlpResolver         PlaylistEditorWindow
     │                          │                        │
     └──────────────┬───────────┘────────────────────────┘
                    ▼
            PlaylistItem[] (MonoBehaviour)
            ┌─────────────────────┐
            │ playlistName        │
            │ tracks[]:           │
            │   playerType        │
            │   title             │
            │   url               │
            │ youtubePlaylistId   │
            └─────────┬───────────┘
                      │
              VRChat Build 時
                      │
                      ▼
            PlaylistBuildProcess
            ┌─────────────────────┐
            │ AddUdonSharpComponent│
            │ <Playlist>()        │
            │ SetProgramVariable  │
            └─────────┬───────────┘
                      │
═══ ランタイム ═══     │
                      ▼
            Playlist (UdonSharpBehaviour)
            ┌─────────────────────┐
            │ _videoPlayerTypes[] │
            │ _titles[]           │    ← 同期不要 (全員同一データ)
            │ _urls[]             │
            │ _tracks[][] (lazy)  │
            └─────────┬───────────┘
                      │
        Controller.Start()
            │
            └── ReadPlaylists()
                _playlists = GetComponentsInChildren<Playlist>()

═══ 再生フロー ═══

    ┌──────────────┐  ┌──────────┐  ┌──────────────┐
    │  Playlist[]  │  │ QueueList │  │ HistoryList  │
    │  (静的)      │  │ (動的)    │  │ (自動追加)   │
    └──────┬───────┘  └────┬─────┘  └──────┬───────┘
           │               │               │
    ┌──────┴───────────────┴───────────────┴───────┐
    │              再生トリガー                      │
    │                                               │
    │  PlayTrack(playlist, index)  ← プレイリスト   │
    │  PlayTrackFromQueue()       ← キュー先頭     │
    │  PlayTrackFromHistory(idx)  ← 履歴           │
    │  PlayTrack(track)           ← 直接URL入力    │
    │                                               │
    │  Forward()                  ← 次トラック     │
    │    ├── Queue優先                               │
    │    ├── Shuffle → ランダム                     │
    │    └── Sequential → 次番号                    │
    │                                               │
    │  Backward()                 ← 前トラック     │
    └──────────────────┬────────────────────────────┘
                       │
                       ▼
                LoadTrack(track)
                       │
              Handler.LoadUrl(url)
                       │
                  [VRChat SDK]
                       │
                AfterVideoReady()
                       │
                Handler.Play()
                       │
                  ★ 再生開始 ★
                       │
                       │ (再生終了時)
                       │
                AfterVideoEnded()
                       │
               ┌───────┴───────┐
               │               │
          AutoForward()     Stop()
               │
          Forward()
               │
          ┌────┴────┐
          │         │
     Queue有り  Playlist内
          │         │
     Queue再生  次トラック
               (or ランダム)
```

---

## AutoPlay モジュール

ワールドロード時に自動再生を開始するオプションモジュール。

```
Modules/AutoPlay/AutoPlay.cs

AutoPlayMode:
    Off         - 無効
    FromTrack   - 指定トラックを再生
    FromPlaylist - プレイリストから再生 (ランダム or 指定インデックス)

トリガー条件: Master or ローカルプレイヤー
遅延: 設定可能な delay 後に PlayDefaultTrack() 実行
```
