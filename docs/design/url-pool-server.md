# URL Pool 方式プレイリストローダー — サーバー側設計

全体設計: [url-pool-playlist-loader.md](url-pool-playlist-loader.md)

---

## 責務

サーバーの責務は以下の3つに集約される。

1. **Resolver**: 外部プレイリスト JSON を取得し、各トラックに pool index を割り当てて返す
2. **Redirect**: `pool + index` に対して実際の動画 URL へ HTTP 302 リダイレクトする
3. **Pool 管理**: slot の割り当て、重複再利用、TTL 管理を行う

サーバーは Unity の状態管理や Queue 操作には関与しない。

---

## API 仕様

### 1. Playlist Resolver API

外部プレイリスト JSON を取得し、index 割り当て済みのレスポンスを返す。

```text
GET /playlist?src={encodedPlaylistJsonUrl}&pool={poolId}
```

**処理フロー**:
```text
1. src の URL からプレイリスト JSON を取得
2. JSON をパースし、tracks を抽出
3. 各 track.url に対して register(pool, url) で index を割り当て
4. url を除外した index 付き JSON をレスポンス
```

**レスポンス**:
```json
{
  "ok": true,
  "pool": "kawaplayer-main",
  "tracks": [
    { "index": 42, "title": "Song A", "mode": 0 },
    { "index": 43, "title": "Song B", "mode": 0 }
  ]
}
```

**エラーレスポンス**:
```json
{
  "ok": false,
  "error": "Failed to fetch playlist from src URL"
}
```

### 2. 登録済みプレイリスト API

サーバーに事前登録されたプレイリストを返す。内部処理は Resolver API と同じ。

```text
GET /playlists/{id}
```

**用途**: 実運用で推奨。URL が短く、VRChat 内での入力が容易。ワールド制作者がサーバーの管理画面からプレイリストを登録する運用を想定。

### 3. リダイレクト API

VRChat の動画プレイヤーがアクセスする。pool + index に紐付けられた動画 URL へリダイレクトする。

```text
GET /vrcurl/{pool}/{index}
```

**レスポンス**:
- 成功: `302 Location: https://www.youtube.com/watch?v=...`
- 未登録/失効: `404 Not Found`

---

## 入力プレイリスト JSON フォーマット

Resolver API が `src` から取得する外部 JSON のフォーマット。KawaPlayer のエディタ時フォーマット (`PlaylistExporter.cs`) と互換。

### 単一プレイリスト形式

```json
{
  "tracks": [
    { "mode": 0, "title": "Song A", "url": "https://www.youtube.com/watch?v=AAA" },
    { "mode": 1, "title": "Live Stream", "url": "https://www.youtube.com/watch?v=BBB" }
  ]
}
```

### 複数プレイリスト形式

```json
{
  "playlists": [
    {
      "name": "Playlist A",
      "tracks": [
        { "mode": 0, "title": "Song A", "url": "https://youtu.be/AAA" }
      ]
    }
  ]
}
```

| フィールド | 必須 | デフォルト | 説明 |
|-----------|------|-----------|------|
| `url` | **必須** | — | 動画 URL |
| `mode` | 任意 | `0` | VideoPlayerType (0=Unity, 1=AVPro, 2=ImageViewer) |
| `title` | 任意 | `""` | トラックタイトル |

サーバーは `mode` と `title` を解釈せず、そのままレスポンスに含める。`url` はサーバー内部で index に変換し、レスポンスには含めない。

---

## Pool 管理

### 内部モデル

```text
PoolState {
    poolId:    string
    size:      int
    nextIndex: int          // 次に割り当てる index (循環)
    slots: [
        {
            index:     int
            destUrl:   string
            expiresAt: timestamp
        }
    ]
    urlToIndex: map[string → int]   // URL → index の逆引き (重複再利用用)
}
```

### index 割り当てルール

```text
register(pool, url):
    // 1. 重複チェック: 同一 URL が登録済みかつ未失効なら再利用
    if urlToIndex[url] exists and not expired:
        touch(expiresAt)    // TTL 更新
        return existing index

    // 2. 空きスロット探索: 失効済み slot を優先
    index = findExpiredSlot()
    if not found:
        index = nextIndex   // 循環
        nextIndex = (nextIndex + 1) % size

    // 3. 登録
    slots[index] = { destUrl: url, expiresAt: now + TTL }
    urlToIndex[url] = index
    return index
```

### TTL (Time-To-Live)

| 設定 | 推奨値 | 説明 |
|------|--------|------|
| 初期 TTL | 30分 | slot 割り当て時に設定 |
| アクセス時更新 | あり | リダイレクト API アクセス時に TTL を延長 |
| 失効 slot | 再利用対象 | 新規割り当て時に優先使用 |

TTL により、pool サイズが固定でも長時間運用が可能。使われなくなった slot は自動的に解放される。

---

## セキュリティ

### src URL の検証

- `src` のスキームが `http` / `https` であること
- 内部 IP (127.0.0.1, 10.x.x.x, 192.168.x.x 等) へのリクエスト禁止 (SSRF 対策)
- レスポンスサイズ上限 (例: 1MB)

### track URL の検証

- 動画 URL のドメインが許可リスト内であること

| サービス | 許可ドメイン |
|---------|------------|
| YouTube | `*.youtube.com`, `youtu.be` |
| Twitch | `*.twitch.tv`, `*.ttvnw.net`, `*.twitchcdn.net` |
| NicoNico | `*.nicovideo.jp` |
| Vimeo | `*.vimeo.com` |
| Soundcloud | `soundcloud.com`, `*.sndcdn.com` |
| VRCDN | `*.vrcdn.live`, `*.vrcdn.video`, `*.vrcdn.cloud` |

許可リスト外の URL はスキップし、レスポンスに含めない。

### レート制限

- `/playlist`: 10 req/min per IP
- `/playlists/{id}`: 30 req/min per IP
- `/vrcurl/{pool}/{index}`: 制限なし (動画プレイヤーが直接アクセス)

### pool アクセス制御

- pool ごとにオプションで API キーを設定可能
- 管理画面でのプレイリスト登録にはサーバー認証が必要

---

## 擬似コード

### Resolver 処理

```text
handlePlaylistRequest(src, pool):
    // 1. 外部 JSON 取得
    response = httpGet(src)
    if response.error:
        return { ok: false, error: "Failed to fetch playlist" }

    // 2. JSON パース
    playlist = parseJson(response.body)
    rawTracks = extractTracks(playlist)  // "tracks" or "playlists"→"tracks"

    // 3. 各トラックに index 割り当て
    resolvedTracks = []
    for track in rawTracks:
        if not isAllowedDomain(track.url):
            continue
        index = register(pool, track.url)
        resolvedTracks.append({
            index: index,
            title: track.title or "",
            mode:  track.mode or 0
        })

    // 4. レスポンス (url はレスポンスに含めない)
    return { ok: true, pool: pool, tracks: resolvedTracks }
```

### リダイレクト処理

```text
handleRedirect(pool, index):
    slot = getSlot(pool, index)
    if slot is null or slot.expired:
        return 404

    slot.expiresAt = now + TTL   // アクセス時 TTL 更新
    return 302 Location: slot.destUrl
```

---

## 技術選定 (参考)

サーバーの実装技術は自由だが、参考として:

| 選択肢 | 利点 |
|--------|------|
| Node.js + Express | 軽量、VRChat コミュニティで実績あり (u2b.cx) |
| Python + FastAPI | 型安全、自動ドキュメント生成 |
| Go + net/http | 高パフォーマンス、デプロイが容易 |

ストレージ: pool 状態はインメモリで十分（再起動時に消えても問題ない。TTL が短いため）。永続化が必要な場合は Redis を追加。

---

## VRChat 側の注意事項

リダイレクトサーバーのドメインは VRChat の video player allowlist 外であるため:

- **VRCStringDownloader** (Resolver API へのアクセス): untrusted URL 扱い
- **動画プレイヤー** (リダイレクト API へのアクセス): untrusted URL 扱い

2024年12月以降、パブリックインスタンスでは untrusted URL がデフォルトでブロックされるため、ワールド制作者は VRChat ウェブサイトでドメインを allowlist に追加する必要がある。
