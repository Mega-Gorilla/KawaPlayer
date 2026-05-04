# KawaPlayer

[YamaPlayer](https://github.com/koorimizuw/YamaPlayer) のフォークリポジトリです。YamaPlayer の全機能に加え、外部プレイリストのランタイム読み込み機能 (PlaylistLoader) と Owner-tied default URL 機能 (DefaultUrl) を搭載しています（基本機能実装済み・VRChat 実機検証継続中）。

> **KawaPlayer は YamaPlayer を置き換えて使用するパッケージです。** YamaPlayer と同時にインストールすることはできません。YamaPlayer を導入済みの場合は、下記の「YamaPlayer からの移行」手順に従ってください。

## PlaylistLoader

[playlist.vrc-hub.com](https://playlist.vrc-hub.com) で作成したプレイリストを、VRChat ワールド内の動画プレイヤーに読み込む機能です。

### 仕組み

1. ワールド制作者がビルド時に VRCUrl Pool を生成（Editor の Generate Pool ボタン）
2. VRChat 内でユーザーがプレイリスト URL を入力
3. サーバーからプレイリスト情報を取得し、Queue にトラックを一括追加
4. 停止中なら自動再生開始

VRChat/Udon の制約（ランタイムで `string → VRCUrl` 変換が不可能）を回避するため、**Pre-baked URL Pool + リダイレクトサーバー方式**を採用しています。

## DefaultUrl

ワールドの **「デフォルト動画 / プレイリスト URL」** を VRChat 内で Instance Owner が設定し、自動再生 + セッション間永続化する機能です。

### 使い方 (Owner として)

1. ワールドに入る (Instance Owner として)
2. KawaPlayer UI を開く → **Settings** → **Playback** タブ
3. 末尾の **Default URL** セクションで URL を入力 → **保存** をクリック
4. プレイヤーが停止中であれば即時に再生が開始されます (再生中・一時停止中の場合は中断せず、URL は次回以降の autoplay 対象として保存されます)
5. 保存した URL は **次回以降の入室時にも自動的に復元** されます (VRChat Persistence による永続化)

### 仕組み

- **Layer 1 (instance sync)**: 設定された URL は `[UdonSynced]` で同 instance の全 player に同期、Master が autoplay を起動
- **Layer 2 (Owner persistence)**: Owner の `VRCPlayerObject + VRCEnablePersistence` で URL を永続化、再 join 時に自動復元
- **URL 種別自動判定**: `playlist.vrc-hub.com` を含めば PlaylistLoader 経路、それ以外は直接動画再生
- **Owner 限定 UI**: Save / Clear ボタンは Instance Owner にのみ表示 (他 player は現在の URL 表示のみ)

### KawaPlayer.prefab に内蔵 (zero-setup)

`v1.1.0` 以降、DefaultUrl は **KawaPlayer.prefab に内蔵されています**。`KawaPlayer.prefab` を scene に配置するだけで利用可能で、別途 prefab を追加する必要はありません。`v1.1.1` 以降は直接 embed され、別 `DefaultUrl.prefab` ファイルは存在しません。`v1.1.2` 以降は UI セクションが他 settings (再生速度・区間リピート 等) と同じ `ScreenUI/.../PlaybackView/Content` 配下に直接配置され、Hierarchy 上で WYSIWYG に編集可能です。

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

### VCC（VRChat Creator Companion）で導入（推奨）

1. VCC で以下の URL をリポジトリとして追加:
   ```
   https://mega-gorilla.github.io/vpm-repos/index.json
   ```
   VCC メニュー: **Settings > Packages > Add Repository** に URL を入力
2. VCC の **Manage Project** でプロジェクトを開く
3. **KawaPlayer** の **Add** をクリック
4. Unity を開き、**GameObject > KawaPlayer > Main** メニューまたは `KawaPlayer.prefab` をシーンにドラッグして配置

> VCC で導入した場合、アップデートも VCC 上からワンクリックで実行できます。

### .unitypackage で導入

1. VRChat Worlds SDK (>=3.8.1) が導入済みの Unity プロジェクトを用意
2. [Releases ページ](https://github.com/Mega-Gorilla/KawaPlayer/releases) から `com.vhub.kawaplayer-x.x.x.unitypackage` をダウンロード
3. Unity メニュー: **Assets > Import Package > Custom Package** で `.unitypackage` をインポート
4. **GameObject > KawaPlayer > Main** メニュー、または `KawaPlayer.prefab` をシーンにドラッグして配置

### YamaPlayer からの移行

KawaPlayer は YamaPlayer と同じアセンブリ定義を使用しているため、**YamaPlayer を先に削除してから** KawaPlayer を導入してください。両方が同時に存在するとコンパイルエラーが発生します。

1. **Unity を閉じる**
2. **YamaPlayer を削除**
   - **VCC で導入した場合:** VCC の **Manage Project** でプロジェクトを開き、YamaPlayer の **Remove Package** をクリック
   - **.unitypackage で導入した場合:** Unity メニュー **Window > Package Manager** で YamaPlayer を選択し **Remove** をクリック
3. `Packages/net.kwxxw.yama-stream/` フォルダが残っていないことを確認
4. **Unity を開き**、コンパイルエラーがないことを確認
5. 上記の「VCC で導入」または「.unitypackage で導入」の手順で KawaPlayer を導入
6. シーン内の YamaPlayer プレハブを `KawaPlayer.prefab` に置き換え

### PlaylistLoader の設定

PlaylistLoader は KawaPlayer.prefab に組み込み済みです。デフォルトの Pool ID (`default`) がそのまま利用可能です。Pool ID を変更する場合のみ、PlaylistLoader の Inspector で Pool ID を設定し Generate Pool を実行してください。

### DefaultUrl の設定

DefaultUrl は KawaPlayer.prefab に組み込み済みです (v1.1.0 以降)。設定不要で、配置後そのまま VRChat 内 Settings UI から利用できます。Owner として URL を設定すると、次回以降も自動復元されます。

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
