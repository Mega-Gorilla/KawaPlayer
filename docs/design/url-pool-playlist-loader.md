# URL Pool 方式プレイリストローダー — 全体設計

## 目的

サーバー上で管理されたプレイリストから KawaPlayer の Queue をランタイムで自動構築する。

VRChat/Udon はランタイムで `string → VRCUrl` 変換を行えないため（[調査結果](../analysis/why-no-runtime-json-playlist.md)）、**Pre-baked URL Pool + リダイレクトサーバー方式**を採用する。

---

## 背景と制約

### VRChat 側の制約

| 制約 | 影響 |
|------|------|
| `new VRCUrl(string)` はエディタ時のみ使用可能 | ランタイムで動画URLからVRCUrlを生成できない |
| `VRCStringDownloader.LoadUrl()` は `VRCUrl` を要求 | 動的に構築した URL 文字列では HTTP リクエストを送れない |
| `TryGenerateVRCUrl` は SDK 3.10.2 で未公開 | 将来の公開予定も未定 (2026-03-22 検証) |

### 採用する回避策

VRChat のカラオケワールド等で実績のある **Pre-baked URL Pool** パターンを応用する。

- ビルド時に大量の `VRCUrl` を「固定スロット」として焼き込む
- 各スロットはリダイレクトサーバーの URL を指す
- ランタイムではサーバー側が「スロット → 実際の動画URL」のマッピングを管理
- VRChat の動画プレイヤーがスロット URL にアクセスすると、サーバーが HTTP 302 で実 URL にリダイレクト

---

## アーキテクチャ

```text
┌─────────────────────────────────────────────────────────────────┐
│                        ビルド時 (Unity Editor)                    │
│                                                                   │
│  PlaylistLoaderPoolGenerator が VRCUrl[0..N-1] を生成             │
│    VRCUrl[0] = https://api.example.com/vrcurl/{pool}/0            │
│    VRCUrl[1] = https://api.example.com/vrcurl/{pool}/1            │
│    ...                                                            │
│    VRCUrl[N] = https://api.example.com/vrcurl/{pool}/N            │
│                                                                   │
│  → PlaylistLoader の _redirectPool に SerializedField で焼き込み  │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                     ランタイム                                     │
│                                                                   │
│  [ユーザー]                                                       │
│    │  resolve URL を VRCUrlInputField に入力                      │
│    │  例: https://api.example.com/r/kawaplayer-main/V1StGXR8      │
│    ▼                                                              │
│  [PlaylistLoader (Unity)]                                         │
│    │  VRCStringDownloader.LoadUrl(resolveUrl)                     │
│    ▼                                                              │
│  [Resolve API (サーバー)]                                         │
│    │  1. DB からプレイリストのトラック一覧を取得                      │
│    │  2. 各トラック URL に pool index を割り当て                    │
│    │  3. index 付き JSON をレスポンス                               │
│    ▼                                                              │
│  [PlaylistLoader (Unity)]                                         │
│    │  レスポンスをパース                                           │
│    │  _redirectPool[index] で VRCUrl を取得                        │
│    │  TrackUtils.NewTrack(mode, title, redirectUrl)                │
│    │  QueueList.AddTracks() で一括追加 (※新規追加メソッド)           │
│    ▼                                                              │
│  [VRChat 動画プレイヤー]                                          │
│    │  VRCUrl[index] にアクセス                                     │
│    ▼                                                              │
│  [リダイレクト API (サーバー)]                                     │
│    │  /vrcurl/{pool}/{index} → HTTP 302 → 実際の動画 URL          │
│    ▼                                                              │
│  [動画再生]                                                       │
└─────────────────────────────────────────────────────────────────┘
```

---

## 責務の分離

| 担当 | 責務 | やらないこと |
|------|------|------------|
| **Unity Editor** | VRCUrl Pool のビルド時生成 | サーバーとの通信 |
| **Unity Runtime** | resolve URL の受け取り、index 付き JSON のパース、Queue 追加 | 実 URL の解決、VRCUrl の動的生成 |
| **サーバー (Hasura)** | 動画カタログ CRUD、プレイリスト CRUD、データアクセス制御 | Unity の状態管理 |
| **サーバー (API Routes)** | MiAuth 認証、Resolve API (index 割り当て)、Redirect API (HTTP 302)、Pool 管理 | Queue 操作 |

**設計の核心**: Unity 側は実際の動画 URL を一切知らなくても動作する。動的な要素はすべてサーバー側に集約される。

---

## Unity とサーバーの接続契約

### index 範囲

サーバーは常に `0 <= index < poolSize` を返す。Unity 側は範囲外 index を拒否する。

### pool 一意性

`poolId` はワールド単位で一意にする。異なるワールドで同じ pool を共有すると slot 衝突が発生する。

### レスポンス JSON 仕様

Resolve API (`/r/{poolId}/{playlistId}`) が返す JSON は index 化済み。Unity 側は生の動画 URL を扱わない。1回のリクエストで1プレイリストを解決する。

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

| フィールド | 説明 |
|-----------|------|
| `ok` | 成功なら `true`、エラーなら `false` |
| `pool` | pool ID (確認用) |
| `name` | プレイリスト名 |
| `tracks[].index` | サーバーが割り当てた pool スロット番号 |
| `tracks[].title` | トラックタイトル。空の場合 Unity 側の VideoInfoDownloader が補完可能 |
| `tracks[].mode` | VideoPlayerType (0=Unity, 1=AVPro, 2=ImageViewer)。サーバーは解釈せずパススルー |

---

## 実装フェーズ

| Phase | 内容 | 詳細 |
|-------|------|------|
| **B1: サーバー** | MiAuth 認証, Resolve API, Redirect API, Pool 管理, 動画カタログ, プレイリスト管理 | [サーバー側設計](url-pool-server.md) |
| **B2: Unity Editor** | Pool 生成 Inspector, バリデーション | [Unity 側設計](url-pool-unity.md) |
| **B3: Unity Runtime** | PlaylistLoader モジュール, UI, Queue 追加 | [Unity 側設計](url-pool-unity.md) |
| **B4: 品質** | テスト (pool 上限, サーバーダウン, untrusted URL 等) | 両方 |

### 既存コードへの変更 (実装順の最初に行う)

本設計の実装にあたり、既存コードへの変更が1件必要。**Phase B2・B3 より先に実装すること**（PlaylistLoader モジュールがこのメソッドに依存するため）:

- `QueueList.AddTracks(object[][] tracks)` メソッドの新規追加 (`Runtime/Internal/Playlist/QueueList.cs`)。既存の `AddTrack()` は1件ずつ同期・イベント発火するため、一括追加時の効率が悪い。詳細は [Unity 側設計](url-pool-unity.md) を参照。

---

## 制約と注意事項

### Untrusted URL 制約

リダイレクトサーバーのドメインは VRChat の allowlist 外であるため:

- **プライベートインスタンス**: ユーザーが「Allow Untrusted URLs」を有効にする必要あり
- **パブリックインスタンス** (2024年12月〜): ワールド制作者が VRChat ウェブサイトでドメインを allowlist に追加する必要あり

### Pool サイズの上限

Pool サイズは 100,000 件に固定（ファイルサイズ実測 5.33MB）。サーバー側と一致させる必要があるため、Inspector で編集不可。同時に参照可能なトラック数は Pool サイズが上限だが、TTL による slot の循環再利用で実用上は緩和される。

### サーバー依存

サーバーがダウンすると、プレイリスト読み込みと動画再生の両方が不可能になる。

---

## 将来の移行パス

`VRCUrl.TryGenerateVRCUrl` が VRChat SDK で公開された場合、サーバーと Pool が不要な完全自動方式 (案A) に移行可能。ただし、URL Pool 方式のレスポンスは index ベース、完全自動方式では生 URL ベースとなるため、流用できるのは VRCStringDownloader によるダウンロードとパースの骨格部分に限られる。Queue 追加・TrackUtils.NewTrack の呼び出しパターンは方式ごとに異なる。

---

## 参考

- [Pre-baked URL Pool 実例: vrchat-youtube-search-api](https://gitea.moe/lamp/vrchat-youtube-search-api)
- [Pre-baked URL Pool 実例: Building a Dynamic VRChat World](https://blog.natalie.ee/posts/building-dynamic-vrchat-world/)
- [VRChat Feedback: Allow Dynamic URLs at Runtime](https://feedback.vrchat.com/udon/p/allow-vrcurl-url-dynamic-urls-at-runtime-its-time-for-a-change)
- [Phase 0 検証: Mega-Gorilla/KawaPlayer#1](https://github.com/Mega-Gorilla/KawaPlayer/issues/1)
