# KawaPlayer

VRChat の動画プレイヤー [YamaPlayer](https://github.com/koorimizuw/YamaPlayer) のフォーク。**ワールド作成者が VRChat 内から直接プレイリストやデフォルト動画URL を設定できる**機能を追加しています。

> **YamaPlayer を置き換えて使用するパッケージです。** 同時インストール不可。既存 YamaPlayer ユーザは [移行手順](#yamaplayer-からの移行) を参照してください。

---

## YamaPlayer との違い

| 機能 | YamaPlayer | KawaPlayer |
|---|---|---|
| 動画再生 (YouTube / Twitch / HLS / 直リンク) | ✓ | ✓ (継承) |
| カラオケ / 区間リピート / 再生速度 / 最大解像度 | ✓ | ✓ (継承) |
| AudioLink / LTCGI / Light Volume 連携 | ✓ | ✓ (継承) |
| 多言語対応 (9 言語) | ✓ | ✓ (継承) |
| **外部プレイリスト URL の読込** (`playlist.vrc-hub.com`) | — | ✅ NEW |
| **ワールドのデフォルト動画URL を VRChat 内から設定** | — | ✅ NEW |
| **デフォルト動画URL の永続化** (再 join で自動復元) | — | ✅ NEW |

KawaPlayer は YamaPlayer の全機能を継承しつつ、**ワールド作成者が VRChat 内で動画/プレイリストを直接管理できる**機能を追加しています。

---

## クイックスタート (VCC 推奨)

1. VCC にリポジトリを追加 — 以下のいずれかの方法で:
   - **VCC を直接開く**: 下記 URL をブラウザのアドレスバーに貼り付けて Enter (VCC の Add Repository ダイアログが起動)

     ```
     vcc://vpm/addRepo?url=https://mega-gorilla.github.io/vpm-repos/index.json
     ```

   - **手動追加**: VCC の **Settings > Packages > Add Repository** に下記 URL を入力

     ```
     https://mega-gorilla.github.io/vpm-repos/index.json
     ```
2. **Manage Project** で対象プロジェクトを開き、**KawaPlayer** の **Add** をクリック
3. Unity で `KawaPlayer.prefab` をシーンにドラッグ (または **GameObject > KawaPlayer > Main** メニュー)

これで KawaPlayer の全機能 (PlaylistLoader / DefaultUrl 含む) が **追加設定なしで** 使えます。

> アップデートも VCC からワンクリックで実行できます。

---

## 主要機能 (KawaPlayer 独自)

### 外部プレイリスト読込 (PlaylistLoader)

[playlist.vrc-hub.com](https://playlist.vrc-hub.com) で作成したプレイリストを、VRChat 内から動画プレイヤーに読み込む機能です。

**使い方:**
1. ワールド内の KawaPlayer UI でプレイリスト URL を入力
2. プレイリストの全トラックが Queue に追加される
3. 停止中なら自動再生開始

外部プレイリストを世界の事前ビルドに焼き込む必要なく、**VRChat に居ながら好みのプレイリストを切替できます**。

### デフォルト動画URL 機能 (DefaultUrl)

ワールドの **「入室時に自動再生される動画 / プレイリスト URL」** を、Instance Owner が VRChat 内から設定できる機能です。

> **⚠ Public インスタンスでは利用できません。**Public インスタンスには Instance Owner が存在しないため、誰も設定できず、保存した URL も復元されません。**Friends / Friends+ / Invite / Invite+ など、自分で作成したインスタンス**でお使いください。

**使い方 (Owner として):**
1. ワールドに Instance Owner として入室
2. KawaPlayer UI を開く → **Settings → Playback** タブ
3. 末尾の **Default URL** セクションで URL を入力 → **保存** をクリック
4. プレイヤーが停止中であれば即時再生開始 (再生中の場合は中断せず、次回ロード時に反映)
5. 保存した URL は **次回入室時にも自動復元** されます

**機能特性:**
- **動画 URL / プレイリスト URL の自動判定** — `playlist.vrc-hub.com` を含めばプレイリスト経路、それ以外は動画再生経路
- **Owner 限定編集** — Instance Owner 以外には、設定欄も現在の URL も表示されず、**利用できない旨の案内のみ**が出ます
- **マルチプレイヤー同期** — Owner の設定は instance 内全 player に同期、後続 joiner も自動再生される
- **永続化** — Owner の VRChat アカウントに紐づき、次回入室時に自動復元

---

## YamaPlayer からの移行

KawaPlayer は YamaPlayer と同じアセンブリ定義を使用しているため、**両方を同時にインストールすると C# コンパイルエラーが発生**します。下記手順で置き換えてください。

1. **Unity を閉じる**
2. **YamaPlayer を削除**
   - **VCC で導入の場合:** VCC の **Manage Project** から YamaPlayer の **Remove Package**
   - **.unitypackage で導入の場合:** Unity **Window > Package Manager** で YamaPlayer を選択 → **Remove**
3. `Packages/net.kwxxw.yama-stream/` フォルダが残っていないことを確認
4. **Unity を開き直し**、コンパイルエラーがないことを確認
5. 上記の [クイックスタート](#クイックスタート-vcc-推奨) で KawaPlayer を導入
6. シーン内の YamaPlayer プレハブを `KawaPlayer.prefab` に置き換え

---

## YamaPlayer 機能 (継承)

YamaPlayer の動画プレイヤーとしての全機能 — YouTube/Twitch/HLS 再生、カラオケモード、再生キュー / 履歴 / プレイリスト、ネットワーク同期、AudioLink / LTCGI 連携、多言語対応 (9 言語)、再生速度 / 区間リピート / 最大解像度設定 等 — をすべて継承しています。

詳細は [本家 YamaPlayer](https://github.com/koorimizuw/YamaPlayer) を参照してください。

---

## .unitypackage で導入 (VCC を使わない場合)

1. VRChat Worlds SDK (>=3.8.1) 導入済みの Unity プロジェクトを用意
2. [Releases ページ](https://github.com/Mega-Gorilla/KawaPlayer/releases) から `com.vhub.kawaplayer-x.x.x.unitypackage` をダウンロード
3. Unity メニュー **Assets > Import Package > Custom Package** でインポート
4. **GameObject > KawaPlayer > Main** または `KawaPlayer.prefab` をシーンにドラッグ

---

## 利用規約

本家 YamaPlayer の利用規約に準じます。

許可:
- 改変
- ワールドの一部として VRChat での公開
- 有償・無償問わず販売ワールドアセットへの取り組み

クレジット記載は任意ですが、**販売ワールドアセットに取り組む場合はクレジット記載が必須**です。

## クレジット

- [YamaPlayer](https://github.com/koorimizuw/YamaPlayer) by [kwxxw](https://yamadev.booth.pm)
- プレイリストサーバー: [playlist.vrc-hub.com](https://playlist.vrc-hub.com)
