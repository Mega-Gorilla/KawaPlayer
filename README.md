# KawaPlayer

[YamaPlayer](https://github.com/koorimizuw/YamaPlayer) のフォークリポジトリです。YamaPlayer の全機能に加え、外部プレイリストのランタイム読み込み機能 (PlaylistLoader) を搭載しています（基本機能実装済み・VRChat 実機検証継続中）。

> **KawaPlayer は YamaPlayer を置き換えて使用するパッケージです。** YamaPlayer と同時にインストールすることはできません。YamaPlayer を導入済みの場合は、下記の「YamaPlayer からの移行」手順に従ってください。

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

### 新規導入（YamaPlayer 未導入の場合）

1. VRChat Worlds SDK (>=3.8.1) が導入済みの Unity プロジェクトを用意
2. [Releases ページ](https://github.com/Mega-Gorilla/KawaPlayer/releases) から `com.vhub.kawaplayer-x.x.x.unitypackage` をダウンロード
3. Unity メニュー: **Assets > Import Package > Custom Package** で `.unitypackage` をインポート
4. **GameObject > KawaPlayer > Main** メニュー、または `KawaPlayer.prefab` をシーンにドラッグして配置

### YamaPlayer からの移行

KawaPlayer は YamaPlayer と同じアセンブリ定義を使用しているため、**YamaPlayer を先に削除してから** KawaPlayer を導入してください。両方が同時に存在するとコンパイルエラーが発生します。

1. **Unity を閉じる**
2. **YamaPlayer を削除**
   - **VCC で導入した場合:** VCC でプロジェクトを開き、`YamaPlayer` パッケージを削除
   - **.unitypackage で導入した場合:** `Packages/net.kwxxw.yama-stream/` フォルダを手動で削除
3. `Packages/net.kwxxw.yama-stream/` フォルダが残っていないことを確認
4. **Unity を開き**、コンパイルエラーがないことを確認
5. [Releases ページ](https://github.com/Mega-Gorilla/KawaPlayer/releases) から `.unitypackage` をダウンロードしインポート
6. シーン内の YamaPlayer プレハブを `KawaPlayer.prefab` に置き換え

### PlaylistLoader の設定

PlaylistLoader は KawaPlayer.prefab に組み込み済みです。デフォルトの Pool ID (`default`) がそのまま利用可能です。Pool ID を変更する場合のみ、PlaylistLoader の Inspector で Pool ID を設定し Generate Pool を実行してください。

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
