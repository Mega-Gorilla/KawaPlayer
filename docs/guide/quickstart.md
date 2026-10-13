# クイックスタート

KawaPlayer をワールドに置いて、ワールドの中で動画を流すまでを、いちばん短い手順で説明します。細かい設定は、[使い方ガイドの目次](README.md) から、カテゴリーごとのページを開いてください。

## 用意するもの

- VRChat Creator Companion（VCC）で作った、ワールドのプロジェクト
- VRChat SDK - Worlds 3.10.2 以上。古いときは、先に VCC の **Manage Project** で SDK を新しくしてください。

## 1. VCC に KawaPlayer のリポジトリを追加する

1. 次のリンクを開きます: **[KawaPlayer の VCC 用のページ（mega-gorilla.github.io/vpm-repos）](https://mega-gorilla.github.io/vpm-repos/)**
2. ページの上のほう、**Copy** の左にある **Add to VCC** を押します。

   ![VCC 用のページ。「VHub Packages」の見出しの下、Copy の左にある「Add to VCC」に印がある](images/vcc/listing-page.webp)

3. VCC が開いてリポジトリの追加を聞かれるので、**I Understand, Add Repository** を押します。

VCC が開かないときは、[インストールと更新](install.md#リポジトリを追加するほかの方法) のほかの方法（B・C）で追加してください。

## 2. プロジェクトに KawaPlayer を入れる

VCC の **Manage Project** でワールドのプロジェクトを開きます。パッケージの一覧で **KawaPlayer** の行を探し、右端の **＋**（Add）を押します。

![VCC のパッケージの一覧の KawaPlayer の行。Not Installed と出ていて、右端の ＋ に印がある](images/vcc/add-package.webp)

画像の版の数字は、撮ったときのものです。

## 3. シーンに KawaPlayer を置く

1. Hierarchy の何もないところを押して、何も選んでいない状態にします。
2. Unity のメニューの **GameObject**（①）> **KawaPlayer**（②）> **Main**（③）を選びます。

   ![Unity の GameObject メニュー。① GameObject、② KawaPlayer、③ Main に印がある](images/unity/gameobject-menu.webp)

3. 置かれた KawaPlayer を、Scene で好きな場所に動かします。

## 4. タブレットを置く

手に持って KawaPlayer を操作できるタブレットです。

1. もう一度、Hierarchy の何もないところを押します。置いたばかりの KawaPlayer が選ばれたままだと、タブレットがその中に置かれてしまいます。
2. メニューの **GameObject > KawaPlayer > Tablet** を選びます。何台置いてもかまいません。
3. インスペクターの **KawaPlayer** の欄（①）に、手順 3 で置いた KawaPlayer を入れます。

   ![タブレットのインスペクター。一番上の、操作する KawaPlayer を入れる欄に ① の印がある](images/unity/tablet-player-field.webp)

**タブレットが KawaPlayer の中に入ってしまったとき:** Hierarchy で **Tablet** が **KawaPlayer** の下に、一段下げて並んでいたら、KawaPlayer の中に置かれています。中に入ったままだと、KawaPlayer を動かしたときにタブレットも一緒に動きます。直し方は、次のどちらかです。

- **A. 取り消してやり直す:** タブレットが消えるまで Ctrl+Z（Mac は Cmd+Z）を押して取り消し、上の 1. からやり直します。
- **B. 外に出す:** Hierarchy で **Tablet** をつかみ、一番下の何もないところまでドラッグして離します。**KawaPlayer** と同じ高さ（一段下げない位置）に並べば、外に出ています。

## 5. ビルドして確かめる

メニューの **VRChat SDK > Show Control Panel** で開くパネルの **Builder** で、**Build Type**（①）を選んで、その下のボタン（②）を押します。上の **Review Any Alerts** に問題が出ているときは、先に直してください。

![VRChat SDK の Builder。① Build Type の欄と、② その下のビルドのボタンに印がある](images/unity/build.webp)

| Build Type（①） | すること |
| --- | --- |
| **Build & Test Your World** | 自分の PC だけで、ワールドを試す |
| **Build & Publish Your World Online** | ワールドをアップロードする |

## 6. Allow Untrusted URLs をオンにする

VHub のプレイリストと曲は、VRChat の設定で **Allow Untrusted URLs**（信頼されていない URL を許可）をオンにしないと読み込めません。VHub（`playlist.vrc-hub.com`）は、VRChat が初めから信頼しているサイトではないためです。この設定は一人ひとりのもので、聞く人それぞれがオンにします。

1. クイックメニューを開き、下の並びの右端の **歯車**（①）を押します。

   ![クイックメニュー。下の並びの右端の歯車に ① の印がある](images/vrchat/quick-menu.webp)

2. 設定が開いたら、右上の **全画面** のアイコン（②）を押して、設定を大きく開きます。

   ![クイックメニューの設定。右上の全画面のアイコンに ② の印がある](images/vrchat/quick-menu-settings.webp)

3. 左の **快適性とセーフティ**（Comfort & Safety、③）を選びます。
4. **セーフティ** の **信頼されていない URL を許可**（Allow Untrusted URLs、④）をオンにします。

   ![設定の快適性とセーフティ。③ 左の快適性とセーフティと、④ 信頼されていない URL を許可のスイッチに印がある](images/vrchat/untrusted-urls.webp)

5. ワールドに入り直します。

オンにすると、VRChat が信頼していないサイトからも、ワールドが動画や画像を読み込めるようになります。気になるときは、使い終わったらオフに戻してください。

## 7. ワールドの中で動画を流す

1. スクリーンを指して、左上の **URL を入力**（①）を押します。デスクトップでは視線（画面の中央）を、VR ではコントローラーのレーザーを向けてください。
2. 動画の URL（YouTube など）か、[VHub Playlist](https://playlist.vrc-hub.com) のプレイリストの共有 URL を貼って、確定します。

![「プレイリストを追加しました」の表示。『作業用BGM』の 12 曲を読み込んだ。URL を入れるボタンに印がある](images/screen/playlist-loaded.webp)

## 次に読むページ

| 知りたいこと | ページ |
| --- | --- |
| 色・言語・音量などの初期値を変えたい | [KawaPlayer の設定](settings.md) |
| 最初からプレイリストを入れておきたい | [プレイリスト](playlists.md) |
| 入室時に動画を自動で流したい、機能を足したい | [モジュール](modules.md) |
| ワールドの中での操作を知りたい | [ワールドの中で使う](playing.md) |

困ったときは、気軽に [Discord サーバー](https://discord.gg/tkrHek6PvN) で聞いてください。
