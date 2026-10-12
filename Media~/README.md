# KawaPlayer の素材

KawaPlayer のロゴと、クイックスタートガイドのポスターです。KawaPlayer を置いたワールドへの掲示や、KawaPlayer の紹介に使えます。

## 利用条件

**著作権フリーではありません。** KawaPlayer を紹介する目的であれば使えます。たとえば、KawaPlayer を置いたワールドへの掲示や、紹介の記事・動画・配信・SNS です。

正式な利用条件は検討中です（[#196](https://github.com/Mega-Gorilla/KawaPlayer/issues/196)）。この素材には、README の「利用規約」（コードとパッケージの条件）とは別の条件を付けます。

## 中身

| ファイル | 中身 |
| --- | --- |
| `logo/kawaplayer-logo-white.png` | ロゴ（白、1024 × 1024） |
| `logo/kawaplayer-logo-black.png` | ロゴ（黒、1024 × 1024） |
| `logo/kawaplayer-logo-gradient.png` | ロゴ（緑から青へのグラデーション、1024 × 1024） |
| `poster/kawaplayer-quickstart-poster.png` | クイックスタートガイドのポスター（1448 × 2048）。VHub PlayList のプレイリストを KawaPlayer で再生する方法 |
| `poster/source/` | ポスターの元（`poster.html`、手順 3・4 の画面、描き出すスクリプト） |

## ロゴ

- **元の画像:** 提供を受けたロゴで、もとは 8192 × 8192 です。ここでは 1024 × 1024 に縮めています。
- **直したところ:** ロゴから離れた場所に、編集のときの薄い消し残しがありました（不透明度 1〜12%）。それを透明にしています。ロゴの形は変えていません。
- **パッケージが使うもの:** KawaPlayer のパッケージが使うのは、白と同じ画像の `Assets/Images/kawaplayer-logo.png` だけです（issue #189）。色は使う側で付けます（待ち受け画像では白、タブレットの裏では銀）。

## ポスター

- **大きさ:** 1448 × 2048 で、縦横の比は A 判と同じです。
- **ワールドに置くとき:** 画像を自分のプロジェクトの `Assets` にコピーして使ってください。このフォルダーは Unity に読み込まれません。
- **例の名前:** 手順 2〜4 のプレイリスト名（「作業用BGM」）と曲名（「サンプル曲 01」など）は、説明のための架空の名前です。
- **作り直すとき:** `poster/source/poster.html` を直して、Windows の Git Bash で `sh "Media~/poster/source/render.sh"` を実行します。
  - Microsoft Edge で `poster/kawaplayer-quickstart-poster.png` に描き出します。
  - 見出しのフォント（Zen Maru Gothic・Noto Sans JP、SIL Open Font License）は Google Fonts から読み込むので、ネットにつながっている必要があります。
  - 本文の Zen Maru Gothic は、パッケージの `Assets/Fonts/ZenMaruGothic-Regular.ttf` を使います。
- **手順 1・2 の画面:** VHub PlayList のサイトを、HTML で描き直したものです。
- **手順 3・4 の画面（`step3.png`・`step4.png`）:**
  - KawaPlayer を置いたワールドを Unity で Play し、VHub のプレイリストを読み込んで撮りました。
  - ワールドの映像の代わりに、無地の背景で撮っています。
  - プレイリスト名と曲名は、撮る前に架空の名前に差し替えています。

## Unity との関係

フォルダー名が `~` で終わるので、Unity はこのフォルダーを読み込みません。

- **ワールド:** 入りません。
- **VCC で入れたとき:** パッケージのフォルダー（`Packages/com.vhub.kawaplayer/Media~`）にあります。
- **`.unitypackage`:** 入りません。
