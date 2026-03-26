# KawaPlayer

[YamaPlayer](https://github.com/koorimizuw/YamaPlayer) のフォークリポジトリです。YamaPlayer の全機能に加え、外部プレイリストのランタイム読み込み機能 (PlaylistLoader) を搭載しています。

## PlaylistLoader

[playlist.vrc-hub.com](https://playlist.vrc-hub.com) で作成したプレイリストを、VRChat ワールド内の動画プレイヤーに読み込む機能です。

### 仕組み

1. ワールド制作者がビルド時に VRCUrl Pool を生成（Editor の Generate Pool ボタン）
2. VRChat 内でユーザーがプレイリスト URL を入力
3. サーバーからプレイリスト情報を取得し、Queue にトラックを一括追加
4. 停止中なら自動再生開始

VRChat/Udon の制約（ランタイムで `string → VRCUrl` 変換が不可能）を回避するため、**Pre-baked URL Pool + リダイレクトサーバー方式**を採用しています。

## YamaPlayer について

YamaPlayer は VRChat で使うことを想定して作られた動画プレイヤーです。以下の機能を備えています。

- YouTube、Twitch 等の動画・ストリーミング再生
- 動画タイトルの自動ロード
- 再生速度変更、区間リピート、最大解像度変更
- カラオケモード
- 再生キュー、再生履歴、プレイリスト
- ネットワーク同期（ユーザ間の遅延を内部で計算）
- AudioLink / LTCGI サポート
- 多言語対応

詳細は [本家 YamaPlayer](https://github.com/koorimizuw/YamaPlayer) を参照してください。

## 導入手順

このフォークは VPM 配布していません。Unity プロジェクトの `Packages/manifest.json` にローカルパッケージ参照を追加してください。

```json
{
  "dependencies": {
    "net.kwxxw.yama-stream": "file:<KawaPlayerリポジトリのパス>",
    ...
  }
}
```

VRChat Worlds SDK (>=3.8.1) が導入済みのプロジェクトが必要です。

## PlaylistLoader のセットアップ

1. シーンに YamaPlayer を配置
2. YamaPlayer の子に空の GameObject を作成し、`PlaylistLoader` と `PlaylistLoaderUI` コンポーネントを追加
3. PlaylistLoader の Inspector で Controller を割り当て
4. Pool ID を設定（デフォルト: `default`）し、Generate Pool を実行
5. YamaPlayer の UI 階層 (`LeftSide/Container`) に、UrlInput を複製した PlaylistLoaderInput を配置（EventTrigger / Animator を削除）
6. PlaylistLoaderUI の `Playlist Url Input` に PlaylistLoaderInput を割り当て

## 利用規約

本家 YamaPlayer の利用規約に準じます。

許可：
改変
ワールドの一部としてVRChatでの公開
有償・無償問わず販売ワールドアセットへの取り組み

クレジット記載は任意です、ただし販売ワールドアセットへ取り組む場合クレジット記載は必須です。

## クレジット

- [YamaPlayer](https://github.com/koorimizuw/YamaPlayer) by [kwxxw](https://yamadev.booth.pm)
- プレイリストサーバー: [playlist.vrc-hub.com](https://playlist.vrc-hub.com)
