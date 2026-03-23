# YamaPlayer: なぜランタイムで JSON プレイリストを直接取り込めないのか

## 結論

**JSON の取得や解析は可能だが、JSON から得た URL 文字列をランタイムで `VRCUrl` に変換して KawaPlayer の再生パイプラインへ自動投入することができない。**

2026-03-22 時点で確認できた事実は次の通り:

1. `VRCJson` によりランタイムで JSON をパースできる
2. `VRCStringDownloader` により外部サーバーから JSON を取得できる
3. `TMP_InputField` により複数行の任意文字列を入力できる
4. しかし公開仕様上、ランタイムで `string -> VRCUrl` を行う手段は確認できない

したがって、**`TryGenerateVRCUrl` に依存する完全自動方式は成立しない**。一方で、**ビルド時に VRCUrl Pool を事前生成し、リダイレクトサーバー経由で動的URLを再生する方式 (Pre-baked URL Pool パターン)** は VRChat のカラオケワールド等で実用されており、現時点で実装可能な代替手段として成立する。

---

## KawaPlayer 側の前提

KawaPlayer の再生トラックは最終的に `VRCUrl` を持つ。

```csharp
// Runtime/Internal/Utils/TrackUtils.cs
public static object[] NewTrack(VideoPlayerType player, string title, VRCUrl url)
{
    return new object[] { player, title, url };
}
```

```csharp
// Runtime/Internal/Playlist/QueueList.cs
[UdonSynced] private VRCUrl[] _urls = new VRCUrl[0];
```

つまり、JSON から URL 文字列を取り出せても、`VRCUrl` にできなければ Queue や Playlist に追加できない。

---

## できること

### 1. JSON を外部から取得する

`VRCStringDownloader.LoadUrl()` により、外部サーバー上の `.json` や `.txt` を取得できる。

### 2. JSON をランタイムで解析する

`VRCJson.TryDeserializeFromJson()` により、`DataDictionary` / `DataList` としてパースできる。

### 3. 補助的に可能なこと

- `TMP_InputField` で任意文字列を受け取ることは可能（改行区切りの URL リストや JSON 文字列の入力）
- 取得・解析したトラック情報を UI 上に一覧表示することも可能

ただし、これらは本設計の主要なアプローチ (URL Pool + リダイレクト方式) では直接使用しない。

---

## できないこと

### ランタイムで `string -> VRCUrl` を自動変換すること

公開仕様上、2026-03-22 時点で確認できた状況は以下の通り。

| 方法 | 状態 |
|------|------|
| `new VRCUrl(string)` | editor time only |
| `VRCUrl.TryGenerateVRCUrl` | SDK 3.10.2 でコンパイルエラー |
| `TryCreateAllowlistedVRCUrl` | 内部 API の言及のみ。公開確認できず |
| `VRCUrlInputField` | ユーザー入力でのみ `VRCUrl` を得る |

2024-03-14 の Developer Update では `TryGenerateVRCUrl` が予告されたが、2026-03-22 時点で公開 SDK 上の利用は確認できていない。

---

## つまり何がボトルネックか

問題は「VRChat が JSON を扱えないこと」ではない。

- JSON の取得: 可能
- JSON の解析: 可能
- URL 文字列の一覧表示: 可能
- URL 文字列から `VRCUrl` を自動生成: **不可**

ボトルネックは、**ランタイムで新しい `VRCUrl` をコードから作れないこと**にある。

---

## 成立する代替案

## 案A: 事前定義済み `VRCUrl` を選ぶ

エディタ時に候補動画をすべて `VRCUrl` として埋め込み、外部 JSON では URL そのものではなく `id` や `playlist + index` を返す方式。

```json
{
  "items": [
    { "playlist": "Default", "index": 0 },
    { "playlist": "Default", "index": 3 }
  ]
}
```

この方式ならランタイムで新しい `VRCUrl` を生成しないため成立する。ただし、**再生候補は事前登録済みのものに限られる**。

## 案B: Pre-baked URL Pool + リダイレクトサーバー方式

VRChat のカラオケワールドや検索ワールドで実用されているパターン。`TryGenerateVRCUrl` に依存しない。

```
[ビルド時]
  カスタムエディタで大量の VRCUrl を事前生成:
    VRCUrl[0] = "https://api.example.com/vrcurl/pool/0"
    VRCUrl[1] = "https://api.example.com/vrcurl/pool/1"
    ...
    VRCUrl[N] = "https://api.example.com/vrcurl/pool/N"

[ランタイム]
  1. resolver サーバーにプレイリスト URL を渡す
  2. サーバーがプレイリストを取得し、各トラックに index を割り当てる
  3. index 付きレスポンス JSON をワールドが取得する
  4. 対応する VRCUrl[index] で QueueList に追加
  5. 再生時にサーバーが実際の動画URLにリダイレクト (HTTP 302)
```

この方式なら、

- ランタイムで `VRCUrl` を新規生成しない (事前生成済みの Pool を使う)
- 外部 JSON の取得・解析は既存 API で可能
- 完全自動でキュー追加が可能

が成立する。制約として、**外部リダイレクトサーバーの運用が必須**であり、サーバードメインが VRChat の allowlist 外の場合は untrusted URL 扱いとなる。

### URL Pool 方式の本質

この方式は、**動画URLそのものを事前登録するのではなく、動画を受け取るための固定スロットを大量に事前登録しておく**設計である。

ビルド時に生成するのは次のような固定 URL 群:

```text
VRCUrl[0] = https://api.example.com/vrcurl/pool1/0
VRCUrl[1] = https://api.example.com/vrcurl/pool1/1
VRCUrl[2] = https://api.example.com/vrcurl/pool1/2
...
```

これらは YouTube 等の動画URLではなく、**リダイレクトサーバー上の固定スロット URL** である。ランタイムではサーバー側が `pool + index` に対して実際の動画URLを紐付ける。

```text
slot 0 -> https://www.youtube.com/watch?v=AAA
slot 1 -> https://www.youtube.com/watch?v=BBB
slot 2 -> https://www.youtube.com/watch?v=CCC
```

ワールド側は `VRCUrl[slot]` を再生するだけでよい。プレイリスト解決と index 割り当てはサーバー側で完結し、再生時にサーバーが HTTP 302 で実URLへリダイレクトする。

### KawaPlayer へ適用する場合の意味

KawaPlayer にこの方式を持ち込む場合、ワールド側で必要なのは次の3点になる。

1. ビルド時に生成した `VRCUrl[] redirectPool`
2. サーバーが返した `index` から `redirectPool[index]` を引く処理
3. その `VRCUrl` を `TrackUtils.NewTrack(...)` と `QueueList.AddTrack()` / `AddTracks()` に渡す処理

つまり、KawaPlayer 側は「実URLを知らなくても再生キューを組める」ようになる。動的要素はサーバー側の `index -> 実URL` 管理に集約される。

実例:
- [vrchat-youtube-search-api](https://gitea.moe/lamp/vrchat-youtube-search-api) — 1万件の VRCUrl Pool による YouTube 検索・再生
- [Building a Dynamic VRChat World](https://blog.natalie.ee/posts/building-dynamic-vrchat-world/) — 数千件の VRCUrl + NGINX リダイレクト

---

## KawaPlayer に対する判断

現時点で着手可能なのは **URL Pool + リダイレクト方式 (案B)** である。

- JSON 取得・解析部分は既存 API (`VRCStringDownloader`, `VRCJson`) で実装可能
- ビルド時に VRCUrl Pool を事前生成するカスタムエディタが必要
- 外部リダイレクトサーバーの設計・運用が必要
- 将来 `TryGenerateVRCUrl` が公開された場合、サーバー不要の完全自動方式 (案A) へ移行可能

設計詳細:
- [全体設計](../design/url-pool-playlist-loader.md)
- [Unity 側設計](../design/url-pool-unity.md)
- [サーバー側設計](../design/url-pool-server.md)

---

## 参考

- VRChat External URLs: https://creators.vrchat.com/worlds/udon/external-urls/
- VRChat String Loading: https://creators.vrchat.com/worlds/udon/string-loading/
- VRChat VRCJson: https://creators.vrchat.com/worlds/udon/data-containers/vrcjson/
- VRChat TMP_InputField: https://creators.vrchat.com/worlds/components/textmeshpro/tmp_inputfield
- Developer Update - 14 March 2024: https://ask.vrchat.com/t/developer-update-14-march-2024/23401
- Developer Update - 31 July 2025 / community discussion: https://ask.vrchat.com/t/developer-update-31-july-2025/45861/31
- Feature Request: runtime VRCUrl generation: https://feedback.vrchat.com/feature-requests/p/please-provide-a-method-to-generate-vrcurl-at-runtime-for-trusted-domain-urls
- Pre-baked URL Pool 実例 (vrchat-youtube-search-api): https://gitea.moe/lamp/vrchat-youtube-search-api
- Pre-baked URL Pool 実例 (Dynamic VRChat World): https://blog.natalie.ee/posts/building-dynamic-vrchat-world/
- 検証 Issue: https://github.com/Mega-Gorilla/KawaPlayer/issues/1
