# VHub の公開 API の今：検索を計画するための記録

| 項目 | 内容 |
| --- | --- |
| 文書の種類 | 仕様と経緯の記録（調べた時点のもの） |
| 関連する issue | #145 |
| 調べたもの | VHub PlayList の公開 API の仕様（VHub のリポジトリ `kisaragi-official/vhub-playlist` の `docs/api/public-api.md`。`main` の `5cd25f7`、2026-10-15。文書の最終確認は 2026-10-08）、VHub 側の issue（#88・#103・#269・#287）、KawaPlayer `ff532c4`（develop） |
| 日付 | 2026-10-21 |
| 方針 | [README.md](README.md) |

## 要旨

KawaPlayer の中で VHub のプレイリストを検索できるようにするため、今の VHub の公開 API と、VRChat の制約と、検索をめぐる経緯をまとめた。VHub の公開 API で今使えるのは、プレイリストを取り出す resolve（`/r/default/{id}`）と、曲の中継（`/vrcurl/default/{i}`）と、Editor 用の確認（`/r/default/_validate`）の 3 つだけである。人気・新着の一覧と、一覧からプレイリストを読み込むための `playlist` プールは、VhubPlaylistViewer のサポート終了（2026-10-08）で廃止され、検索の経路は 2026-05-02 に廃止されている。したがって、#145 の最初の計画（人気・新着の一覧）は、サーバーの側で成り立たなくなった。検索を作るには、VHub に新しい経路が要る。この文書は事実の記録で、何を作るかは [README.md](README.md) に書く。

**キーワード：** VHub、公開 API、URL Pool、VRCUrl、検索、記録

## 目次

1. 今使える経路
2. プール
3. VRChat の制約
4. 検索と一覧の経緯
5. KawaPlayer の今
6. ほかの検索の部品の作り
7. 参考文献

---

## 1. 今使える経路

VHub の公開 API のベース URL は `https://playlist.vrc-hub.com` で、認証はない。メソッドは GET だけである。

**表 1** VHub の公開 API の経路（2026-10-08 の仕様）

| 経路 | 返すもの | KawaPlayer | 状態 |
| --- | --- | --- | --- |
| `GET /r/default/{playlistId}`（resolve） | プレイリストの名前と曲。曲ごとに `/vrcurl/default/{i}` の番号（JSON） | 使う（ワールドの中の読み込み、Playlist Editor の取り込み） | 使える |
| `GET /vrcurl/default/{i}` | 登録された動画の URL へ 302 | 使う（曲を再生するとき） | 使える |
| `GET /r/default/_validate` | プールがあるかの確認（`{ ok, pool, poolSize }`） | 使う（Editor で URL Pool を作るとき） | 使える |
| `GET /api/vrc/playlists/popular`・`recent` | サポート終了の案内（一覧の形、200） | 使わない | 廃止（2026-10-08） |
| `GET /vrcurl/playlist/{i}` | `{ ok: false }` とサポート終了の案内（200） | 使わない | 廃止（2026-10-08） |
| `GET /api/vrc/yt-thumb-direct-baking` | 410 | 使わない | 廃止（2026-10-08） |
| `GET /api/vrc/news` | サポート終了の案内（200） | 使わない | 廃止 |
| `GET /api/vrc/playlists/search` | 404（HTML） | 使わない | 廃止（2026-05-02） |

resolve の応答の形を次に示す。曲の元の URL は返さず、番号だけを返す。

```jsonc
{
  "ok": true,
  "id": "1PFtjGIDlFrjdMMkJlT9F",   // 照合用
  "pool": "default",
  "name": "DISCO",
  "tracks": [
    { "index": 7, "title": "…", "mode": 0, "provider": "youtube" },   // index は /vrcurl/default/{i} の i
    { "index": 8, "title": "…", "mode": 1 }                           // provider は YouTube のときだけ
  ]
}
```

**表 2** 共通の決まり

| 項目 | 内容 |
| --- | --- |
| 回数の制限 | resolve だけ、1 つの IP につき 1 分あたり 60 回。超えると 429 `{ "ok": false, "error": "Rate limit exceeded" }`（`Retry-After` なし） |
| キャッシュ | resolve はサーバーの中で 60 秒 |
| 非公開のプレイリスト | resolve は公開かどうかを見ない。URL（21 文字の ID）を知っていれば読める |
| 末尾のスラッシュ | API の経路は、あってもなくても同じ応答を直接返す（308 にしない） |
| エラーの文 | 英語の固定文。KawaPlayer は `"Unknown pool"` の文で分岐している |
| 文字コード | UTF-8 の JSON。日本語はそのまま |

## 2. プール

`/vrcurl/{pool}/{i}` は、サーバーが持つ「番号 → URL」の表を引いて応答する。ワールドは、この経路の URL を `VRCUrl[]` に焼き込んでおく。

**表 3** プール（2026-10-10 から）

| プール | 中身 | 容量 | 上書き | 応答 |
| --- | --- | --- | --- | --- |
| `default` | 動画の URL | 100,000 | する（いちばん長く使われていない番号から） | 302 |
| `playlist` | （廃止。以前は resolve の URL） | — | — | サポート終了の案内 |
| `yt-thumb-direct`・`default-thumb` | （廃止。以前はサムネイルの URL） | — | — | 404 |

- `default` は、同じ URL には同じ番号を使う。上書きされることがあるので、利用側は resolve の応答の番号を、そのつど使う。
- 廃止した 3 つのプールの記録は、本番から消されている（2026-10-10）。
- KawaPlayer は、`/vrcurl/default/0` から `99999` までの 100,000 個を `PlaylistLoader._redirectPool` に焼き込んでいる。

## 3. VRChat の制約

**表 4** 検索の作りに効く VRChat の制約

| 制約 | 内容 | 出どころ |
| --- | --- | --- |
| `VRCUrl` は実行中に作れない | `new VRCUrl(string)` は Editor だけ。実行中に手に入る `VRCUrl` は、焼き込んだものと、`VRCUrlInputField.GetUrl()` だけ | [なぜ実行時に JSON のプレイリストを作れないか](../../analysis/why-no-runtime-json-playlist.md) |
| 入力欄の文字を Udon から書けない | `VRCUrlInputField.text` は Udon に出ていない。`SetUrl(VRCUrl)` は使える（KawaPlayer の URL 欄と自動再生(Global) の欄で使っている） | KawaPlayer `UIController.cs`・`DefaultUrlSettingsUI.cs` |
| 入力欄の placeholder は出ない | `VRCUrlInputField` の placeholder は働かない。案内を出すなら、入力欄の文字そのものに入れるしかない | KawaPlayer での確認 |
| 文字列と画像は redirect を追わない | `VRCStringDownloader`・`VRCImageDownloader` は 302 を追わない。動画プレイヤーは追う | VRChat の String Loading・Image Loading の文書、VHub #91 |
| 読み込みは 5 秒に 1 回 | 文字列と画像の読み込みは、それぞれ 5 秒に 1 回まで。超えた分は順番待ち | 同上 |
| 画像の大きさ | 画像の読み込みは 2048 × 2048 まで | Image Loading の文書 |
| 信頼されていないドメイン | `playlist.vrc-hub.com` は VRChat の信頼済みの一覧にない。文字列と画像の読み込みには、見る人それぞれの「Allow Untrusted URLs」が要る | 同上 |
| ボット対策 | `playlist.vrc-hub.com` では、ブラウザー以外からの要求に確認画面（403、`cf-mitigated: challenge`）が返る。`/r/` と `/vrcurl/` の GET・HEAD だけは通る（2026-10-08 から） | VHub の運用の記録 |

ボット対策の例外が `/r/` と `/vrcurl/` だけなので、ワールドから呼ぶ新しい経路は、このどちらかの下に置くか、例外を広げる必要がある。

## 4. 検索と一覧の経緯

**表 5** VHub の検索と一覧の経緯

| 日付 | できごと |
| --- | --- |
| 2026-04-30 | 人気・新着・検索の一覧（`/api/vrc/playlists/{popular,recent,search}`）と、一覧からプレイリストを読み込む `playlist` プールを作った（VHub #88） |
| 2026-05-01 | `/vrcurl/playlist/{i}` を 302 から 200 の JSON に変えた（文字列の読み込みは redirect を追わない、VHub #91） |
| 2026-05-02 | 検索の経路を廃止した（VHub #103）。理由は下に書く |
| 2026-10-05 | KawaPlayer #145 を立てた。段階 1 を人気・新着の閲覧、段階 2 を文字での検索とした。VHub #269 で、一覧のサムネイルの画像シートと、検索の作り直しを提案した |
| 2026-10-08 | VhubPlaylistViewer のサポートを終え、人気・新着の一覧・`/vrcurl/playlist/{i}`・サムネイルの経路を廃止した（VHub #287）。KawaPlayer が使う経路は変わらない |
| 2026-10-10 | 廃止したプールの記録を、本番から消した |

検索を廃止した理由（VHub #103）は、入力のしかたである。

- 実行中に手に入る `VRCUrl` は `VRCUrlInputField.GetUrl()` だけで、入力欄の文字全体が URL になる。
- 入力欄の文字を Udon から書けないので、検索欄にフルの URL（`https://playlist.vrc-hub.com/api/vrc/playlists/search?q=test` のような 60 文字ほど）を見せるしかない、と判断した。
- VR のキーボードで自由に文字を打つことは、そもそも向いていない、とも判断した。

一方、VHub #269（2026-10-05、開いたまま）では、この判断への答えを次のように書いた。

- `VRCUrlInputField.SetUrl()` は Udon から使えるので、送るたびに入力欄を初期値に戻せる。
- 入力欄の初期値を「URL の前半 ＋ 空白で挟んだ案内文 ＋ 区切り文字」にして、見る人に末尾へ検索語を打ってもらう方式が、ほかのワールドの検索の部品で実際に使われている（6 章）。
- 着手の前に、VRChat の中で次の 3 つを確かめる：キーボードを開いたときにカーソルが末尾にあるか、日本語を打てるか、日本語の検索語がサーバーに正しく届くか。

VHub のデータには、プレイリストの名前を検索用に正規化した列（`normalized_name`）が残っている。保存のときに書き込まれるが、今は読む経路がない。

## 5. KawaPlayer の今

**表 6** KawaPlayer（`ff532c4`）の VHub まわり

| 部品 | 今の動き |
| --- | --- |
| `PlaylistLoader` | URL 欄の `VRCUrl` で resolve を読み、曲の番号を `_redirectPool[index]` で `VRCUrl` に変えて、曲を足す |
| URL の見分け | `PlaylistUrlUtils` が `/r/{pool}/{id}` の形かを見る。`/playlists/{id}`（Web のページ）には「VRChat用URL」を使うよう案内する |
| 同じプレイリストの見分け | 取得に使った URL から重複のキーを作る（`PlaylistUrlUtils.GetSourceKey`）。`/r/{pool}/{id}` は `{pool}/{id}` に、それ以外の URL はパスのままにする |
| プレイリストの枠 | `DynamicPlaylist` が同期するのは、取得に使った URL（`SourceUrl`）と名前と曲。どのプレイリストかの識別は、別に持たない。満杯のときの確認と削除の確認は、`SourceUrl` の文字列で「確認したときと同じ中身か」を比べる |
| 更新 | 更新ボタンは、枠の `SourceUrl` を読み直す。`PlaylistLoaderUI` は、`/r/default/{id}` の形でない URL を更新に受け付けない |
| 権限 | 読み込む前に `BeforeUserLoadPlaylist` を送る（権限管理が止められる） |
| 焼き込み | `_redirectPool` に `/vrcurl/default/{i}` を 100,000 個。Editor の **Generate Pool** で、`_validate` で確かめてから作る |

#145 の段階 1 は、廃止された人気・新着の一覧と `/vrcurl/playlist/{i}` を前提にしていた。そのため、そのままでは作れない。

## 6. ほかの検索の部品の作り

VRChat のワールドで YouTube を検索する部品には、オープンソースの検索 API（`vrchat-youtube-search-api`、[URL Pool の設計](../url-pool/README.md)でも参考にしたもの）を使うものがある。公開されている説明から読み取れる作りは次のとおりである。応答の JSON の形は、確かめられていない。

- **要求の形:** `GET /search?pool=<プール名><大きさ>&thumbnails=1&input=<検索語>`。
- **検索語は最後:** `input` より後ろは、`&` なども含めて文字どおりに読む。ワールドは入力欄に `…&input=` までを入れておき、見る人が後ろに検索語を打つ。これで、打った URL 全体が `VRCUrl` になる。
- **結果の読み込み:** 結果の動画は、ワールドごとのプール（使い回し型）に割り当てる。ワールドは、そのプールの番号の URL を焼き込んでおく。
- **サムネイル:** 結果のサムネイルを 1 枚の画像にまとめて返し、クライアントで切り分けるもの（画像シート）が使われている。5 秒に 1 枚の制限の中で、1 回で全件を出すためである。
- **プールの大きさの目安:** 小さな非公開のワールドで数百、人気の公開ワールドで数万。同じプール名をほかのワールドと使い回すと、結果が混ざる。

## 7. 参考文献

1. VHub PlayList 公開 API リファレンス（VHub のリポジトリ `docs/api/public-api.md`、`5cd25f7`、最終確認 2026-10-08）
2. VHub #88（一覧と検索の新設）、#91（`/vrcurl/playlist` を 200 に）、#103（検索の廃止）、#269（画像シートと検索の作り直しの提案）、#287（VhubPlaylistViewer 用の経路の廃止）
3. VRChat Creators：[String Loading](https://creators.vrchat.com/worlds/udon/string-loading/)、[Image Loading](https://creators.vrchat.com/worlds/udon/image-loading/)、[External URLs](https://creators.vrchat.com/worlds/udon/external-urls)
4. [URL Pool 方式のプレイリストローダー](../url-pool/README.md)、[なぜ実行時に JSON のプレイリストを作れないか](../../analysis/why-no-runtime-json-playlist.md)
5. [vrchat-youtube-search-api](https://gitea.moe/lamp/vrchat-youtube-search-api)（オープンソースの YouTube 検索 API）
