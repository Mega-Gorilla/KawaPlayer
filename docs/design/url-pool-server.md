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

- 動画カタログの CRUD
- プレイリストの作成・編集・削除
- プレイリスト一覧の取得 (GraphQL)
- ユーザーごとのデータアクセス制御 (Hasura permissions)

### Next.js API Routes で扱うもの

- `GET /api/auth/login` → MiAuth (Misskey 認証) リダイレクト
- `GET /api/auth/callback` → MiAuth コールバック → ユーザー upsert → セッション設定
- `GET /api/auth/logout` → セッション破棄
- `GET /api/auth/me` → 現在のセッション情報
- `GET /r/{poolId}/{playlistId}` → VRChat 向け resolve（低レイテンシが必要）
- `GET /vrcurl/{poolId}/{index}` → HTTP 302 リダイレクト（動画プレイヤーが毎回アクセス）
- Pool state の管理

### 認証方式

[vhub-world-search](https://github.com/kisaragi-official/vhub-world-search) と同じ **MiAuth** (Misskey 認証) を採用する。

```text
[ログインフロー]
1. ユーザーが /api/auth/login にアクセス
2. サーバーが MiAuth session ID を生成し Cookie に保存
3. Misskey の MiAuth 画面にリダイレクト
   → https://sns.vrc-hub.com/miauth/{session}?name=KawaPlayer&callback=...
4. ユーザーが Misskey 上で認証を許可
5. Misskey が /api/auth/callback にリダイレクト
6. サーバーが MiAuth check API で認証を検証
7. users テーブルに upsert (misskey_id をキーにユーザー作成 or 更新)
8. iron-session でセッション Cookie を設定
```

セッション管理: [iron-session](https://github.com/vvo/iron-session)（暗号化 Cookie ベース）

---

## データモデル

### リレーション図

```text
users
  │
  ├── (owner_id) ──────→ playlists
  │                          │
  │                          ├── (playlist_id) ──→ playlist_tracks ←── (video_id) ──→ videos
  │                                                                                     │
  └── (registered_by) ──────────────────────────────────────────────────────────────────┘

pool_slots        ← pool_id + index で動画 URL へのリダイレクトを管理
pool_url_index    ← pool_id + url で重複チェック (pool_slots の逆引き)
```

- `users` → `playlists`: 1対多。ユーザーは複数のプレイリストを作成できる
- `playlists` → `playlist_tracks` → `videos`: 多対多。`playlist_tracks` が中間テーブル。1つのプレイリストに複数の動画、1つの動画が複数のプレイリストに属せる
- `users` → `videos`: 1対多。動画カタログへの登録者を記録
- `pool_slots` / `pool_url_index`: プレイリストや動画テーブルとは FK で結ばない。Resolve API 実行時に動的に割り当てられる一時的なマッピング

### Resolve API でのクエリ

VRChat から `/r/{poolId}/{playlistId}` にアクセスされると、`playlist_tracks` と `videos` を JOIN してトラック一覧を取得する:

```sql
SELECT v.url, v.title, v.mode
FROM playlist_tracks pt
JOIN videos v ON pt.video_id = v.id
WHERE pt.playlist_id = '{playlistId}'
ORDER BY pt.position
```

各トラックの `v.url` に pool index が割り当てられ、`url` を除外した index 付き JSON がレスポンスとなる。

---

### users

| カラム | 型 | 説明 |
|--------|-----|------|
| id | uuid | PK (自動生成) |
| misskey_id | text | Misskey ユーザー ID (unique) |
| misskey_username | text | Misskey ユーザー名 |
| display_name | text | 表示名 (nullable, Misskey の name) |
| avatar_url | text | アバター画像 URL (nullable) |
| created_at | timestamptz | |
| updated_at | timestamptz | |

例:

| id | misskey_id | misskey_username | display_name | avatar_url |
|----|-----------|-----------------|-------------|-----------|
| `a1b2c3d4-...` | `9x8y7z6w` | `mega_gorilla` | `Mega Gorilla` | `https://sns.vrc-hub.com/files/...` |

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

例:

| id | url | title | mode | thumbnail_url | registered_by |
|----|-----|-------|------|--------------|--------------|
| `f1e2d3c4-...` | `https://www.youtube.com/watch?v=dQw4w9WgXcQ` | `Rick Astley - Never Gonna Give You Up` | 0 | `https://i.ytimg.com/vi/dQw4w9WgXcQ/default.jpg` | `a1b2c3d4-...` |
| `b5a6c7d8-...` | `https://www.twitch.tv/example` | `Example Live` | 1 | `null` | `a1b2c3d4-...` |

### playlists

| カラム | 型 | 説明 |
|--------|-----|------|
| id | text | PK。nanoid (21文字, URL-safe) |
| name | text | プレイリスト名 |
| owner_id | uuid | FK → users |
| is_public | boolean | 公開フラグ |
| created_at | timestamptz | |
| updated_at | timestamptz | |

例:

| id | name | owner_id | is_public |
|----|------|---------|----------|
| `V1StGXR8_Z5jdHi6B-myT` | `お気に入りカラオケ` | `a1b2c3d4-...` | `true` |
| `xYz9Abc3Def7Ghi1Jkl5m` | `作業用BGM` | `a1b2c3d4-...` | `false` |

### playlist_tracks

playlists と videos の多対多リレーションを解決する中間テーブル。`position` でプレイリスト内の並び順を管理する。

| カラム | 型 | 説明 |
|--------|-----|------|
| id | uuid | PK |
| playlist_id | text | FK → playlists |
| video_id | uuid | FK → videos |
| position | int | プレイリスト内の順番 (0始まり) |

例:

| id | playlist_id | video_id | position |
|----|------------|---------|---------|
| `11111111-...` | `V1StGXR8_Z5jdHi6B-myT` | `f1e2d3c4-...` | 0 |
| `22222222-...` | `V1StGXR8_Z5jdHi6B-myT` | `b5a6c7d8-...` | 1 |

### pool_slots (Pool 状態の永続化)

| カラム | 型 | 説明 |
|--------|-----|------|
| pool_id | text | Pool 識別子 |
| index | int | スロット番号 (0 〜 poolSize-1) |
| dest_url | text | リダイレクト先 URL |
| expires_at | timestamptz | TTL 期限 |
| (PK) | | (pool_id, index) |

例:

| pool_id | index | dest_url | expires_at |
|---------|-------|---------|-----------|
| `kawaplayer-main` | 42 | `https://www.youtube.com/watch?v=dQw4w9WgXcQ` | `2026-03-24T15:30:00Z` |
| `kawaplayer-main` | 43 | `https://www.twitch.tv/example` | `2026-03-24T15:30:00Z` |

### pool_url_index (逆引き)

| カラム | 型 | 説明 |
|--------|-----|------|
| pool_id | text | Pool 識別子 |
| url | text | 動画 URL |
| index | int | 割り当て済みスロット番号 |
| (PK) | | (pool_id, url) |

例:

| pool_id | url | index |
|---------|-----|-------|
| `kawaplayer-main` | `https://www.youtube.com/watch?v=dQw4w9WgXcQ` | 42 |
| `kawaplayer-main` | `https://www.twitch.tv/example` | 43 |

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

**TTL 切れ時の UX**: slot が失効した場合、Redirect API は `404` を返し、VRChat 側では動画の読み込みエラーとして表示される。自動再 resolve は VRCUrl 制約により困難（新しい resolve URL を動的に構築できない）。ユーザーが手動でプレイリストを再読み込みする運用とする。Redirect API でのアクセス時 TTL 延長により、再生中のトラックが突然切れることは通常発生しない。長時間放置されたキュー内トラックが失効するケースが主な該当シナリオ。

**監視**: Redirect API の `404` 発生率は運用監視の対象とする。`404` が頻発する場合、pool サイズ不足または slot TTL が短すぎるシグナルである。メトリクスとして `redirect_404_count` (pool 別) を記録し、アラート閾値を設定することを推奨する。

---

## Pool 管理

### index 割り当てルール

```text
register(poolId, url):
    // ★ 全操作を単一トランザクション + pool 単位の排他ロックで実行
    BEGIN TRANSACTION
    SELECT pg_advisory_xact_lock(hashtext(poolId))

    // 1. 重複チェック: pool_url_index から検索
    existing = SELECT index FROM pool_url_index
               WHERE pool_id = poolId AND url = url
    if existing:
        slot = SELECT expires_at FROM pool_slots
               WHERE pool_id = poolId AND index = existing.index
        if slot and slot.expires_at >= now:
            // 未失効 → TTL 延長して再利用
            UPDATE pool_slots SET expires_at = now + TTL
                WHERE pool_id = poolId AND index = existing.index
            COMMIT
            return existing.index
        // 失効済み → 逆引きが古い状態で残っている
        // ★ 先に古い逆引きを削除してからスロット探索に進む
        DELETE FROM pool_url_index
            WHERE pool_id = poolId AND url = url

    // 2. 空きスロット探索: 失効済み slot を優先
    expired_slot = SELECT index, dest_url FROM pool_slots
                   WHERE pool_id = poolId AND expires_at < now
                   ORDER BY index LIMIT 1
                   FOR UPDATE  // 行ロック (他トランザクションとの競合防止)
    if expired_slot:
        index = expired_slot.index
        // ★ 旧 URL の逆引きエントリを削除 (不整合防止)
        DELETE FROM pool_url_index
            WHERE pool_id = poolId AND url = expired_slot.dest_url
    else:
        index = next_index(poolId)  // 循環カウンタ
        // 循環で未失効 slot を上書きする場合も旧逆引きを削除
        old_slot = SELECT dest_url FROM pool_slots
                   WHERE pool_id = poolId AND index = index
        if old_slot:
            DELETE FROM pool_url_index
                WHERE pool_id = poolId AND url = old_slot.dest_url

    // 3. 登録 (UPSERT)
    UPSERT pool_slots (pool_id, index, dest_url, expires_at)
        VALUES (poolId, index, url, now + TTL)
    UPSERT pool_url_index (pool_id, url, index)
        VALUES (poolId, url, index)

    COMMIT
    return index
```

### 排他制御

`register()` は以下の2層で並行アクセスを制御する:

1. **`pg_advisory_xact_lock(hashtext(poolId))`**: pool 単位のアドバイザリロック。同一 pool への同時 register を直列化する。トランザクション終了時に自動解放
2. **`SELECT ... FOR UPDATE`**: 失効 slot の行ロック。他トランザクションが同じ slot を取得するのを防止

これにより、同時に複数の Resolve API が走っても、同じ slot の二重割当てや同じ URL への重複 index 割り当てを防止する。

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

### Resolve キャッシュ

同じプレイリストの Resolve 結果を短期キャッシュする。同一 NAT 配下の複数ユーザーが同じ resolve URL を入力しても、DB 負荷を抑えつつ高速にレスポンスできる。

| 設定 | 推奨値 | 説明 |
|------|--------|------|
| キャッシュキー | `poolId + playlistId` | |
| TTL | 60秒 | プレイリスト編集後の反映遅延は最大60秒 |
| 保存先 | インメモリ (Next.js プロセス内) | Redis は不要。プロセス再起動でクリアされても問題ない |

キャッシュヒット時は register() を実行せず、キャッシュ済みの index 付き JSON をそのまま返す。pool_slots の TTL はキャッシュ元の resolve 時に設定済みなので、キャッシュヒット時の TTL 延長は不要。

**前提条件**: Resolve キャッシュ TTL は slot TTL より十分短いこと。キャッシュ TTL が slot TTL に近いと、キャッシュヒットで返した index の slot が既に失効している可能性がある。現在の設定（キャッシュ 60秒、slot 30分）では問題にならない。

### レート制限

| エンドポイント | 制限 | 理由 |
|--------------|------|------|
| `/r/{poolId}/{playlistId}` | 60 req/min per IP | VRChat からのアクセス。Resolve キャッシュにより実際の DB 負荷は低い |
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
