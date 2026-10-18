# 設計と確認の文書

KawaPlayer の設計・計画・確認の手順の文書です。1 つの文書に 1 つのフォルダーで、フォルダー名の頭に、もとになった issue の番号を付けています。

## 一覧

| フォルダー | issue | 題名 | 種類 | 状態 |
| --- | --- | --- | --- | --- |
| [url-pool/](url-pool/README.md) | なし（2026-03） | URL Pool 方式のプレイリストローダー（全体の設計、[Unity 側](url-pool/unity.md)、[サーバー側](url-pool/server.md)） | 設計 | 実装済み（PlaylistLoader モジュール） |
| [68-release-qa/](68-release-qa/README.md) | #68 | リリースの実機 QA の進め方（ランブック）と、[Playlist Editor の Editor 検証](68-release-qa/playlist-editor-verification.md) | 確認の手順 | v1.2.0 の QA（#68）で使った |
| [228-playback-sync/](228-playback-sync/README.md) | #228 | 再生の同期：VizVid（VVMW）との[比較](228-playback-sync/comparison.md)と、直す方針 | 方針 | 実装済み（2026-10-15）。方針は PR #229、実装は子の issue #232〜#237 と #248（PR #238〜#243・#249）。実装後の仕様は [docs/analysis/playback-sync.md](../analysis/playback-sync.md) |
| [262-intro-video/](262-intro-video/README.md) | #262 | KawaPlayer の紹介動画の絵コンテ、見た目の案の[比較](262-intro-video/look.md)、カットごとの[撮影台本](262-intro-video/script.md) | 台本 | 絵コンテは決まった（2026-10-17、PR #263）。v1 のレビューを入れ、見た目の案を比べた（2026-10-18、レビュー待ち）。撮影・編集・公開は #262 で続く |

## 置き方の決まり

- **フォルダー名:** `<issue 番号>-<題名>` にします。題名は英小文字とハイフンで書きます（例：`228-playback-sync`）。番号が先頭なので、一覧は番号順に並びます。issue を使う前の文書には番号を付けません（`url-pool/`）。
- **本文:** フォルダーの `README.md` に書きます。続きの文書は、同じフォルダーに置きます。図・グラフと、それを作るスクリプトは、同じフォルダーの `images/` に置きます。確認のログや、確認に使ったスクリプトを残すときは、Unity が取り込まないように、名前が `~` で終わるフォルダー（例：`evidence~/`）に置きます。
- **一覧:** 文書を足したときと、状態が変わったとき（方針が決まった、実装した、使わなくなった）に、上の表を直します。
- **ほかのフォルダー:** 特定の issue に結び付かない分析は `docs/analysis/`、使い方ガイドは `docs/guide/`、リリースノートは `docs/releases/` に置きます。
- **パッケージ:** `docs/` は配る zip にも `.unitypackage` にも入りません。`.meta` は `.gitignore` 済みなので作りません。

## 移した文書

2026-10-14 に、issue の番号で整理しました（#230）。閉じた issue などに古いパスが残っていたら、次の表でたどってください。

| 前のパス | 今のパス |
| --- | --- |
| `docs/design/url-pool-playlist-loader.md` | [`docs/design/url-pool/README.md`](url-pool/README.md) |
| `docs/design/url-pool-unity.md` | [`docs/design/url-pool/unity.md`](url-pool/unity.md) |
| `docs/design/url-pool-server.md` | [`docs/design/url-pool/server.md`](url-pool/server.md) |
| `docs/qa/release-qa-runbook.md` | [`docs/design/68-release-qa/README.md`](68-release-qa/README.md) |
| `docs/qa/playlist-editor-verification.md` | [`docs/design/68-release-qa/playlist-editor-verification.md`](68-release-qa/playlist-editor-verification.md) |
