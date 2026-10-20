# KawaPlayer

VRChat のワールドに置く、無料の動画プレイヤーです。[VHub Playlist](https://playlist.vrc-hub.com) のプレイリストを URL ひとつで読み込め、手に持つタブレットからも操作できます。[YamaPlayer](https://github.com/koorimizuw/YamaPlayer) のフォークで、YamaPlayer の機能もそのまま使えます。

[![KawaPlayer の紹介動画（1 分 24 秒）を YouTube で観る](https://raw.githubusercontent.com/Mega-Gorilla/KawaPlayer/develop/docs/images/readme/video-card.webp)](https://youtu.be/6Lwdvkbyp14)

紹介動画：[YouTube](https://youtu.be/6Lwdvkbyp14)・[X](https://x.com/Mega_Gorilla_/status/2112364799049044427)

## できること

- **VHub のプレイリスト：** VHub で作ったプレイリストを、ワールドの中で URL を貼るだけで読み込めます。
- **タブレット：** 手元で動画を操作し、写真を見せたり、スクリーンに送ったりできます。誰が来たかも分かります。
- **自動再生(Owner)：** インスタンスを作った人が、入ったときに流す動画を VRChat の中で決めておけます（自分で作った Invite・Friends などのインスタンスで使えます）。
- **全員リロードボタン：** ライブ配信のイベントで、全員の動画を一度に読み込み直せます。
- **YamaPlayer の機能：** YouTube・Twitch・ライブ配信の再生、キューと履歴、カラオケ、AudioLink・LTCGI・Light Volume、9 言語など。

## 入れ方

VRChat Worlds SDK 3.10.2 以上のワールドのプロジェクトに、VCC で入れます。

1. [KawaPlayer の VCC 用のページ](https://mega-gorilla.github.io/vpm-repos/) を開き、**Add to VCC** を押します。
2. VCC の **Manage Project** で、KawaPlayer の **Add** を押します。
3. Unity で **GameObject > KawaPlayer > Main** を選ぶと、シーンに KawaPlayer が置かれます。

画像付きの手順は [クイックスタート](https://github.com/Mega-Gorilla/KawaPlayer/blob/develop/docs/guide/quickstart.md) にあります。VHub のプレイリストを Public・Group Public のインスタンスで流すには、VRChat.com でワールドの Video Player Allowed Domains に `playlist.vrc-hub.com` を足します。更新も VCC から行えます。VCC を使わない入れ方は [インストールと更新](https://github.com/Mega-Gorilla/KawaPlayer/blob/develop/docs/guide/install.md) にあります。

## 使い方

[使い方ガイド](https://github.com/Mega-Gorilla/KawaPlayer/blob/develop/docs/guide/README.md) に、設定・プレイリスト・モジュール・タブレット・全員リロードボタン・ワールドの中での使い方を、画像付きでまとめています。困ったときは [Discord](https://discord.gg/tkrHek6PvN) で気軽に聞いてください。

## YamaPlayer からの移行

YamaPlayer と KawaPlayer は、同じプロジェクトに一緒には入れられません（コンパイルエラーになります）。

1. Unity を閉じ、YamaPlayer を外します（VCC で入れたなら **Manage Project** の **Remove Package**、.unitypackage なら Package Manager の **Remove**）。`Packages/net.kwxxw.yama-stream/` が残っていないことを確かめます。
2. Unity を開いてエラーがないことを確かめてから、上の手順で KawaPlayer を入れます。
3. シーンの YamaPlayer を `KawaPlayer.prefab` に置き換えます。

## 利用規約

本家 YamaPlayer の利用規約に準じます。

許可:
- 改変
- ワールドの一部として VRChat での公開
- 有償・無償問わず販売ワールドアセットへの取り組み

クレジット記載は任意ですが、**販売ワールドアセットに取り組む場合はクレジット記載が必須**です。

ロゴとクイックスタートのポスター（[`Media~/`](Media~/)）は、この利用規約とは別の [KawaPlayer 素材ライセンス](Media~/LICENSE.md) に従います。KawaPlayer を置いたワールドへの掲示（販売するワールドへの同梱を含む）や紹介に使えますが、著作権フリーではありません。パッケージの中のロゴ（`Assets/Images/kawaplayer-logo.png`）も同じ条件です。

## クレジット

- [YamaPlayer](https://github.com/koorimizuw/YamaPlayer) by [kwxxw](https://yamadev.booth.pm)
- プレイリストサーバー: [playlist.vrc-hub.com](https://playlist.vrc-hub.com)
