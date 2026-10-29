# 設計と確認の文書

KawaPlayer の設計・計画・確認の手順の文書です。1 つの文書に 1 つのフォルダーで、フォルダー名の頭に、もとになった issue の番号を付けています。

## 一覧

| フォルダー | issue | 題名 | 種類 | 状態 |
| --- | --- | --- | --- | --- |
| [url-pool/](url-pool/README.md) | なし（2026-03） | URL Pool 方式のプレイリストローダー（全体の設計、[Unity 側](url-pool/unity.md)、[サーバー側](url-pool/server.md)） | 設計 | 実装済み（PlaylistLoader モジュール） |
| [68-release-qa/](68-release-qa/README.md) | #68 | リリースの実機 QA の進め方（ランブック）と、[Playlist Editor の Editor 検証](68-release-qa/playlist-editor-verification.md) | 確認の手順 | v1.2.0 の QA（#68）で使った |
| [145-vhub-search/](145-vhub-search/README.md) | #145 | VHub のプレイリスト検索：VHub の公開 API の[今の記録](145-vhub-search/current-api.md)と、作る API と KawaPlayer の計画 | 計画 | 判断待ち（2026-10-21）。PR #292 |
| [228-playback-sync/](228-playback-sync/README.md) | #228 | 再生の同期：VizVid（VVMW）との[比較](228-playback-sync/comparison.md)と、直す方針 | 方針 | 実装済み（2026-10-15）。方針は PR #229、実装は子の issue #232〜#237 と #248（PR #238〜#243・#249）。実装後の仕様は [docs/analysis/playback-sync.md](../analysis/playback-sync.md) |
| [258-screen-ui/](258-screen-ui/README.md) | #258 | スクリーンの UI の作り直し：決めたデザイン（操作を下にまとめて重ねる）と、作り方の計画 | 計画 | デザインは決定（2026-10-26）。作り方の D1・D2（AudioLink は設定の「音声」）・D3・D4・D6（設定はタブレットと共通の部品）・D7（一覧は Playlist Panel と共通の部品）・D8（Playlist Panel にダイアログは置かない）は決定（2026-10-28）、D5 は判断待ち。実装は P1〜P6 の PR（P5 は P5a・P5b の 2 つ） |
| [262-intro-video/](262-intro-video/README.md) | #262 | KawaPlayer の紹介動画の絵コンテ、見た目の案の[比較](262-intro-video/look.md)、カットごとの[撮影台本](262-intro-video/script.md) | 台本 | 作った。v3.1（84.5 秒）を X と YouTube で公開した（2026-10-20）。絵コンテは PR #263、v1 のレビュー・見た目の比較・撮影台本と公開した版への書き直しは PR #264 |
| [300-vhub-library/](300-vhub-library/README.md) | #300 | VChat 認証を使う VHub の個人ライブラリ：利用の流れ・共有の境界・両リポジトリの分担と検証 | 実装計画 | 計画案・未実装。PoC は #301、VHub 側は kisaragi-official/vhub-playlist#317、KawaPlayer 側は #302。対象リリースは未定 |

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
