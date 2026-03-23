# KawaPlayer

[YamaPlayer](https://github.com/koorimizuw/YamaPlayer) のフォークリポジトリです。

## 目的

YamaPlayer のプレイリスト機能を拡張し、**外部プレイリストをランタイムで読み込んで Queue に自動追加する機能**を実装することを目指しています。

VRChat/Udon の制約（ランタイムで `string → VRCUrl` 変換が不可能）を回避するため、**Pre-baked URL Pool + リダイレクトサーバー方式**を採用する設計です。

## 現在の状態

設計段階です。実装はまだ開始していません。

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

## ドキュメント

### 調査・分析

- [プレイリストパイプライン解析](docs/analysis/playlist-pipeline.md)
- [URL→再生パイプライン解析](docs/analysis/url-to-playback-pipeline.md)
- [ランタイムJSONプレイリストの制約](docs/analysis/why-no-runtime-json-playlist.md)

### 設計

- [URL Pool 方式プレイリストローダー — 全体設計](docs/design/url-pool-playlist-loader.md)
- [URL Pool 方式 — Unity 側設計](docs/design/url-pool-unity.md)
- [URL Pool 方式 — サーバー側設計](docs/design/url-pool-server.md)

## 利用規約

本家 YamaPlayer の利用規約に準じます。

許可：
改変
ワールドの一部としてVRChatでの公開
有償・無償問わず販売ワールドアセットへの取り組み

クレジット記載は任意です、ただし販売ワールドアセットへ取り組む場合クレジット記載は必須です。

## クレジット

- [YamaPlayer](https://github.com/koorimizuw/YamaPlayer) by [kwxxw](https://yamadev.booth.pm)
