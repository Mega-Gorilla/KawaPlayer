# URL Pool 方式プレイリストローダー — サーバー側設計

全体設計: [url-pool-playlist-loader.md](url-pool-playlist-loader.md)

---

## 概要

サーバーは以下の機能を提供する。

1. **ユーザー管理**: ユーザー登録・認証
2. **動画カタログ**: 動画 URL の登録・メタデータ管理
3. **プレイリスト管理**: ユーザーがカタログから自由にプレイリストを作成・編集
4. **VRChat Resolve API**: プレイリストのトラックに pool index を割り当て、index 付き JSON を返す
5. **VRChat Redirect API**: `pool + index` から実際の動画 URL へ HTTP 302 リダイレクト
6. **Pool 管理**: slot の割り当て、重複再利用、TTL 管理（PostgreSQL に永続化）

サーバーは Unity の状態管理や Queue 操作には関与しない。

---

## 技術スタック

| レイヤー | 技術 | 備考 |
|---------|------|------|
| フロントエンド | Next.js | プレイリスト管理 UI、ユーザー登録画面 |
| GraphQL API | Hasura | PostgreSQL 上の CRUD を自動公開 |
| カスタム API | Next.js API Routes | VRChat 向け resolve / redirect エンドポイント |
| DB | PostgreSQL | ユーザー、動画カタログ、プレイリスト、pool state |

参考構成: [vhub-world-search](https://github.com/kisaragi-official/vhub-world-search)

### Hasura で扱うもの

- ユーザー登録・認証
- 動画カタログの CRUD
- プレイリストの作成・編集・削除
- プレイリスト一覧の取得 (GraphQL)

### Next.js API Routes で扱うもの

- `GET /r/{poolId}/{playlistId}` → VRChat 向け resolve（低レイテンシが必要）
- `GET /vrcurl/{poolId}/{index}` → HTTP 302 リダイレクト（動画プレイヤーが毎回アクセス）
- Pool state の管理

---

## データモデル

### users

| カラム | 型 | 説明 |
|--------|-----|------|
| id | uuid | PK |
| name | text | 表示名 |
| created_at | timestamptz | |

### videos (動画カタログ)

| カラム | 型 | 説明 |
|--------|-----|------|
| id | uuid | PK |
| url | text | 動画 URL (unique) |
| title | text | タイトル |
| mode | int | VideoPlayerType (0=Unity, 1=AVPro, 2=ImageViewer) |
| thumbnail_url | text | サムネイル URL (nullable) |
| registered_by | uuid | FK → users |
| created_at | timestamptz | |

### playlists

| カラム | 型 | 説明 |
|--------|-----|------|
| id | text | PK。nanoid (21文字, URL-safe) |
| name | text | プレイリスト名 |
| owner_id | uuid | FK → users |
| is_public | boolean | 公開フラグ |
| created_at | timestamptz | |
| updated_at | timestamptz | |

### playlist_tracks

| カラム | 型 | 説明 |
|--------|-----|------|
| id | uuid | PK |
| playlist_id | text | FK → playlists |
| video_id | uuid | FK → videos |
| position | int | プレイリスト内の順番 |

### pool_slots (Pool 状態の永続化)

| カラム | 型 | 説明 |
|--------|-----|------|
| pool_id | text | Pool 識別子 |
| index | int | スロット番号 (0 〜 poolSize-1) |
| dest_url | text | リダイレクト先 URL |
| expires_at | timestamptz | TTL 期限 |
| (PK) | | (pool_id, index) |

### pool_url_index (逆引き)

| カラム | 型 | 説明 |
|--------|-----|------|
| pool_id | text | Pool 識別子 |
| url | text | 動画 URL |
| index | int | 割り当て済みスロット番号 |
| (PK) | | (pool_id, url) |

---

## API 仕様

### 1. Web ページ — プレイリスト閲覧・編集

```text
GET /playlists/{id}
```

Next.js のページとして提供。ブラウザ向け HTML を返す。

**機能**:
- プレイリスト名、トラック一覧（タイトル、URL、サムネイル）を表示
- オーナーは編集・削除が可能（認証必要）
- **VRChat URL の表示**: ユーザーがコピーして VRChat に入力するための resolve URL を表示

```text
┌─────────────────────────────────────────────┐
│  My Playlist (12 tracks)                     │
│  ──────────────────────────────              │
│  1. Song A - youtube.com/...                 │
│  2. Song B - youtube.com/...                 │
│  ...                                         │
│                                              │
│  VRChat URL:                                 │
│  ┌─────────────────────────────────────────┐ │
│  │ https://api.example.com/r/kawa/V1StGXR8 │ │
│  └─────────────────────────────────────────┘ │
│  [Copy]                                      │
└─────────────────────────────────────────────┘
```

### 2. VRChat Resolve API

プレイリストのトラックに pool index を割り当て、index 付き JSON を返す。VRChat の `VRCStringDownloader` がアクセスする。

```text
GET /r/{poolId}/{playlistId}
```

**処理フロー**:
```text
1. playlistId で DB からプレイリストを取得
2. playlist_tracks → videos を JOIN してトラック一覧を取得
3. 各 video.url に対して register(poolId, url) で index を割り当て
4. url を除外した index 付き JSON をレスポンス
```

**レスポンス**:
```json
{
  "ok": true,
  "pool": "kawaplayer-main",
  "name": "My Playlist",
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
  "error": "Playlist not found"
}
```

### 3. VRChat Redirect API

VRChat の動画プレイヤーがアクセスする。pool + index に紐付けられた動画 URL へリダイレクトする。

```text
GET /vrcurl/{poolId}/{index}
```

**レスポンス**:
- 成功: `302 Location: https://www.youtube.com/watch?v=...`
- 未登録/失効: `404 Not Found`

**パフォーマンス要件**: 動画プレイヤーが再生のたびにアクセスするため、低レイテンシが必要。DB クエリは `pool_slots` テーブルへの PK 検索のみ。必要に応じてインメモリキャッシュを追加。

---

## Pool 管理

### index 割り当てルール

```text
register(poolId, url):
    // 1. 重複チェック: pool_url_index から検索
    existing = SELECT index FROM pool_url_index
               WHERE pool_id = poolId AND url = url
    if existing and not expired:
        UPDATE pool_slots SET expires_at = now + TTL
            WHERE pool_id = poolId AND index = existing.index
        return existing.index

    // 2. 空きスロット探索: 失効済み slot を優先
    expired_slot = SELECT index FROM pool_slots
                   WHERE pool_id = poolId AND expires_at < now
                   ORDER BY index LIMIT 1
    if expired_slot:
        index = expired_slot.index
    else:
        index = next_index(poolId)  // 循環カウンタ

    // 3. 登録 (UPSERT)
    UPSERT pool_slots (pool_id, index, dest_url, expires_at)
        VALUES (poolId, index, url, now + TTL)
    UPSERT pool_url_index (pool_id, url, index)
        VALUES (poolId, url, index)
    return index
```

### TTL (Time-To-Live)

| 設定 | 推奨値 | 説明 |
|------|--------|------|
| 初期 TTL | 30分 | slot 割り当て時に設定 |
| アクセス時更新 | あり | Redirect API アクセス時に TTL を延長 |
| 失効 slot | 再利用対象 | 新規割り当て時に優先使用 |

TTL により、pool サイズが固定でも長時間運用が可能。使われなくなった slot は自動的に解放される。

Pool state は PostgreSQL に永続化するため、サーバー再起動時も slot mapping が保持される。

---

## セキュリティ

### 動画 URL の検証

動画カタログに登録可能な URL のドメインを制限する。

| サービス | 許可ドメイン |
|---------|------------|
| YouTube | `*.youtube.com`, `youtu.be` |
| Twitch | `*.twitch.tv`, `*.ttvnw.net`, `*.twitchcdn.net` |
| NicoNico | `*.nicovideo.jp` |
| Vimeo | `*.vimeo.com` |
| Soundcloud | `soundcloud.com`, `*.sndcdn.com` |
| VRCDN | `*.vrcdn.live`, `*.vrcdn.video`, `*.vrcdn.cloud` |

許可リスト外の URL はカタログ登録時に拒否する。

### レート制限

| エンドポイント | 制限 | 理由 |
|--------------|------|------|
| `/r/{poolId}/{playlistId}` | 30 req/min per IP | VRChat からのアクセス |
| `/vrcurl/{poolId}/{index}` | 制限緩め or なし | 動画プレイヤーが直接アクセス |
| Hasura GraphQL | Hasura の設定に準じる | Web UI からのアクセス |

### 認証

| 操作 | 認証 |
|------|------|
| 動画カタログ登録・編集 | 要認証 (登録ユーザー) |
| プレイリスト作成・編集・削除 | 要認証 (オーナー) |
| プレイリスト閲覧 (Web) | 公開プレイリストは不要、非公開は要認証 |
| Resolve API (`/r/...`) | 不要 (共有 URL で公開アクセス) |
| Redirect API (`/vrcurl/...`) | 不要 |

---

## 擬似コード

### Resolve 処理 (Next.js API Route)

```text
handleResolve(poolId, playlistId):
    // 1. DB からプレイリスト取得
    playlist = SELECT * FROM playlists WHERE id = playlistId
    if not playlist or (not playlist.is_public):
        return { ok: false, error: "Playlist not found" }

    // 2. トラック一覧を取得
    tracks = SELECT v.url, v.title, v.mode, pt.position
             FROM playlist_tracks pt
             JOIN videos v ON pt.video_id = v.id
             WHERE pt.playlist_id = playlistId
             ORDER BY pt.position

    // 3. 各トラックに index 割り当て
    resolvedTracks = []
    for track in tracks:
        index = register(poolId, track.url)
        resolvedTracks.append({
            index: index,
            title: track.title,
            mode:  track.mode
        })

    // 4. レスポンス (url は含めない)
    return {
        ok: true,
        pool: poolId,
        name: playlist.name,
        tracks: resolvedTracks
    }
```

### Redirect 処理 (Next.js API Route)

```text
handleRedirect(poolId, index):
    slot = SELECT dest_url, expires_at FROM pool_slots
           WHERE pool_id = poolId AND index = index

    if slot is null or slot.expires_at < now:
        return 404

    // アクセス時 TTL 更新
    UPDATE pool_slots SET expires_at = now + TTL
        WHERE pool_id = poolId AND index = index

    return 302 Location: slot.dest_url
```

---

## VRChat 側の注意事項

サーバーのドメインは VRChat の video player allowlist 外であるため:

- **VRCStringDownloader** (Resolve API へのアクセス): untrusted URL 扱い
- **動画プレイヤー** (Redirect API へのアクセス): untrusted URL 扱い

2024年12月以降、パブリックインスタンスでは untrusted URL がデフォルトでブロックされるため、ワールド制作者は VRChat ウェブサイトでドメインを allowlist に追加する必要がある。
