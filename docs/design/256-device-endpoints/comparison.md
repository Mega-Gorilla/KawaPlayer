# デバイスのつなぎ方：KawaPlayer と VizVid（VVMW）の機能ごとの比較

| 項目 | 内容 |
| --- | --- |
| 文書の種類 | 仕様と比較の記録（調べた時点のもの） |
| 時点 | KawaPlayer develop `95f818f`、VizVid（VVMW）develop `834bd5e`（1.8.2 の 4 コミットあと）、vrcw-foundation 0.0.68（`2aa8623`） |
| 関連 | #256。KawaPlayer の現状は [docs/analysis/device-connections.md](../../analysis/device-connections.md)（#255）。この比較をもとにした方針は [README.md](README.md) |
| 日付 | 2026-10-16 |
| 注意 | #256 の実装が入ると、KawaPlayer のつなぎ方は変わる。変わったあとの仕様は、実装の PR で `docs/analysis/device-connections.md` に書き直す |

## 要旨

KawaPlayer と VizVid（VVMW）が、本体と、その横に置く部品（UI・画面・ボタン・パネル）をどうつないでいるかを、両者のコードを読んで機能ごとに比べた記録である。

比べた機能は、部品の層、つなぐ相手の決め方、部品の足し方、イベント、命令と権限、ビルド時の処理、部品ごとの設定、画面と音、同期の分担の 9 つである。

いちばん大きな違いは、つなぎ方を書く場所にある。

- **VizVid：** 部品が自分の欄に属性（`[Locatable]`・`[Resolve]`・`[BindUdonSharpEvent]`）を付けて宣言し、ビルドの前処理がそれを読んで、参照を埋め、イベントの配り先を名前ごとに作る。
- **KawaPlayer：** Sub Controller のインスペクターが、選んだときに参照を書く。ビルドではデバイスごとのビルド処理が配線し、実行時に各部品が自分を登録する。

命令の受け口と権限にも違いがある。VizVid は、再生・リスト・同期の命令を、本体ごとに 1 つの `FrontendHandler` で受け、そのうち 5 つをロック中に断る。シーク・速度・音量などは `Core` が直接受け、URL の入力などは UI が押せなくする。KawaPlayer は、UI ごとの `UIController` が受け口で、操作の前に確認のイベントを送り、権限のモジュールが中止する。権限のモジュールは UI ごとに写すので、写す場所の無い UI には権限がかからない（§8.4）。

用語の対応（表 2）と、KawaPlayer の接続口 E1〜E12 ごとの VVMW の対応（表 4）も並べた。VizVid の前処理と配り先の作られ方は、テスト用の Unity（ClientSim）で確かめた（付録 B）。この文書は事実の記録で、直す方針は [README.md](README.md) にまとめる。

**キーワード：** デバイス、接続口、VizVid、UdonSharp、ビルド時の前処理、イベント、権限

## 目次

1. はじめに
2. 前提：U# で部品をつなぐ道具
3. 比較の一覧
4. 部品の層
5. つなぐ相手の決め方
6. 部品の足し方
7. イベント
8. 命令と権限
9. ビルド時の処理
10. 部品ごとの設定とインスペクター
11. 画面と音
12. 同期の分担
13. まとめ：それぞれの良いところ
14. 参考文献
15. 付録 A：コードの場所 ／ 付録 B：Unity での確認

---

## 1. はじめに

### 1.1 目的

KawaPlayer のデバイスはこれからも増える。#255 で、今のつなぎ方を分析し、整理するときの論点を O1〜O8 として挙げた。VizVid（以下 VVMW）は、つなぎ方を部品の側で宣言し、ビルドで結ぶ作りをしている。この文書は、両者のつなぎ方を機能ごとに並べて記録する。何をどう直すかの判断は、[README.md](README.md)（方針）に分けて書く。

### 1.2 範囲

- **入れるもの：** 本体と部品のあいだの参照、イベント、命令、権限、ビルド時の処理、エディターでの部品の足し方、部品ごとの設定、同期の分担。
- **入れないもの：** 再生の同期の決まり（[#228 の比較](../228-playback-sync/comparison.md)）、プレイリストの中身の扱い、見た目と翻訳。

### 1.3 調べ方

- **KawaPlayer：** develop `95f818f` のコードと、#255 の分析（[device-connections.md](../../analysis/device-connections.md)）。この文書では、KawaPlayer の接続口を #255 の番号（E1〜E12）で呼ぶ。
- **VVMW：** develop `834bd5e` のコード（GitHub の最新。1.8.2 の配布のあとの 4 つのコミットを含む）。テスト用プロジェクトの `Packages/idv.jlchntoz.vvmw`（配布の 1.8.2）と比べると、92 本の C# のうち 87 本はファイルのハッシュが一致した。違う 5 本（`Core.cs`・`VideoPlayerHandler.cs`・`Designer/MaterialUtil.cs`・`Editor/VVMW/ScreenMeshUtils.cs`・`Editor/VVMW/VideoMaterialEmbeddedEditor.cs`）は、動画の終わりの知らせのまとめ方、プレーヤーが有効・無効になったときの扱い、画面の素材とメッシュの扱いの違いで、つなぎ方には関わらない。この文書の行番号は `834bd5e` のものである。
- **vrcw-foundation：** VVMW が使う、同じ作者の土台のパッケージ。0.0.68（`2aa8623`）で、テスト用プロジェクトの物とファイルのハッシュが一致した。VVMW の属性・前処理・イベントの基底クラスは、このパッケージにある。どちらも MIT ライセンスである。
- **確かめたこと：** VVMW の前処理の前とあとの値を、テスト用の Unity（ClientSim）で読んだ（付録 B）。VRChat の Build & Test では確かめていない。この文書では、コードから読めることと、確かめたことを分けて書く。

---

## 2. 前提：U# で部品をつなぐ道具

VRChat のワールドの U#（UdonSharp）では、部品どうしをつなぐ道具が限られている。両者とも、この中から選んでいる。

**表 1** U# で部品をつなぐ道具

| 道具 | 中身 | 型の確かめ |
| --- | --- | --- |
| 参照の欄 | 別の U# の部品を、型付きの欄で持つ。メソッドを直接呼べる | コンパイル時 |
| 名前のイベント | `SendCustomEvent("名前")` で、相手のメソッドを名前で呼ぶ。引数は渡せない | 無し（打ち間違いは実行時に何も起きない） |
| 欄への書き込み | `SetProgramVariable("欄", 値)` で相手の欄に書き、名前のイベントで呼ぶ | 無し |
| ネットワークのイベント | `SendCustomNetworkEvent` で、全員か所有者の部品の名前を呼ぶ | 無し |
| 同期 | `[UdonSynced]` の値を、所有者が送る | コンパイル時 |
| ビルド時の前処理 | `IProcessSceneWithReport` で、ビルドと Play の前にシーンを書き換える（参照を埋める・物を消す） | エディターの C# なので、自由 |

- **型の制約：** U# の部品は、別の U# の部品の型を、同じ側の、または依存しているアセンブリの中でしか参照できない。コアからモジュールの型は参照できないので、名前のイベントや欄への書き込みが要る。
- **インターフェース：** U# の部品は、実行時のインターフェースを持てない。VVMW は、`#if !COMPILER_UDONSHARP` の partial クラスで、エディターだけのインターフェース（`IVizVidCompoonent`・`ISelfPreProcess`）を部品に付けている。
- **Network ID：** 出荷したプレハブで、U# の部品がすでにある物に部品を足したり外したりすると、その版で作ったワールドを更新したときにビルドできなくなる。どちらの作りでも、この制約は変わらない。

---

## 3. 比較の一覧

### 3.1 用語の対応

同じ役の部品でも、両者で名前が違う。この文書では、次の対応で読む。

**表 2** 用語の対応

| 役 | KawaPlayer | VVMW |
| --- | --- | --- |
| 本体（再生と同期） | `Controller` | `Core` |
| プレイリスト・キュー | `Controller` と `QueueList`・`HistoryList`・`DynamicPlaylist` | `FrontendHandler` |
| UI | `UIController` | `UIHandler` |
| 外の部品の根元 | `YamaPlayerSubController` | 無し（部品ごとに Core や `FrontendHandler` の欄を持つ） |
| 出来事を受ける基底 | `YamaPlayerListener`（型付きのメソッド 29 個） | 無し（名前のメソッドを持つ U# の部品なら受けられる） |
| 出来事を配る | `Controller.AddListener`・`UIController.AddListener` | `UdonSharpEventSender` の `_AddListener` と `[BindUdonSharpEvent]` |
| 権限 | `PermissionManagement`（モジュール）と `BeforeUserXxx` | `FrontendHandler.locked` と `_OnUnlock`・`_Lock` |
| URL の差し込み口 | `UIController._urlInterceptor`（名前で呼ぶ） | `InputFilterBase`（抽象の基底クラス、型で呼ぶ） |
| ビルド時の処理 | `IYamaPlayerBuildProcess` | `IPreprocessor`・`ISelfPreProcess` |
| ワールドに 1 つの部品 | `TabletVisitorsBuildProcess`（専用） | `ISingleton<T>`（汎用） |
| つなぐ相手を探す | Sub Controller のインスペクター | `[Locatable]`・`Locate` |
| 部品を足すメニュー | `GameObject > KawaPlayer > …` | `GameObject > VizVid > …` |

### 3.2 機能ごとの比較

**表 3** つなぎ方の比較（機能ごと）

| 機能 | KawaPlayer | VVMW | 節 |
| --- | --- | --- | --- |
| 部品の層 | `Controller`（再生と同期）、`UIController`（UI ごと）、モジュール、デバイス | `Core`（再生と同期）、`FrontendHandler`（プレイリストとロック、Core に 1 つ）、`UIHandler`（UI ごと）、追加の部品 | §4 |
| つなぐ相手の正の値 | 各部品の `_controller`。書くのは Sub Controller のインスペクターだけ | 各部品の欄。部品が自分で持つ | §5 |
| 相手の見つけ方 | 3 通り（エディターで書く・親をたどる・シーン全体を探す） | エディターでいちばん近い物を探す（`[Locatable]`）、ビルドで欄からたどる（`[Resolve]`） | §5 |
| 部品の足し方 | `GameObject > KawaPlayer > …` で置き、インスペクターでプレイヤーを選ぶ | `GameObject > VizVid > …` で置くと、いちばん近い Core（UI は `FrontendHandler`）に結ばれる。相手が無いとき、本体を置くのは一部の部品だけで、UI では置かない | §6 |
| イベントの配り方 | `Controller` は型付き（29）と名前（E3）、`UIController` は名前、モジュール 4 つが自前のリスト | 共通の基底クラス `UdonSharpEventSender` の `SendEvent(名前)` | §7 |
| 購読の時 | 実行時の `Start` で `AddListener` | ビルドで `[BindUdonSharpEvent]`。受ける名前を絞れる | §7 |
| 名前の確かめ | 送る側も受ける側も文字列 | 受ける側は `nameof` で自分のメソッドを指す | §7 |
| 命令 | UI が確かめてから `Controller` を直接呼ぶ。受け口は UI の数だけある | 再生・リスト・同期は UI が `FrontendHandler` を呼び、シーク・速度・音量などは `Core` を呼ぶ。受け口は本体ごとに 1 つ | §8 |
| 権限 | UI が操作の前に `BeforeUserXxx` を送り、権限のモジュールが中止する | 受ける側が 5 つの命令を断り、URL の入力などは UI が押せなくする。外の仕組みは `_OnUnlock`・`_Lock` を送る | §8 |
| ビルド時の処理の置き場所 | `IYamaPlayerBuildProcess` を実装したクラス。デバイスごとのクラスもある | 汎用の前処理（属性を読む）と、部品自身の前処理（`ISelfPreProcess`） | §9 |
| ワールドに 1 つの部品 | タブレットの訪問者の記録を、専用のビルド処理が 1 つにまとめる | `ISingleton` を付けた部品を、汎用の前処理が 1 つにまとめる | §9 |
| 部品ごとの設定 | 共通の Sub Controller のインスペクターに、タブレットの欄がある | 部品が自分のインスペクターを持つ。Core のインスペクターは、Core を操作する部品の設定を埋め込む | §10 |
| 画面と音 | `YamaPlayerScreen`・`YamaPlayerSpeaker` を、ビルドで `Controller` に登録する | Core が画面と音の配列を持ち、エディターの部品（`ScreenConfigurator`）がそれを書き換える | §11 |
| 同期 | `Controller`、キュー・履歴・枠、`TabletScreen` などが、それぞれ同期する。`UIController` は同期しない | `Core` と `FrontendHandler` が同期する。`UIHandler` は変数を同期しない | §12 |

### 3.3 KawaPlayer の接続口と、VVMW の対応

#255 の接続口（E1〜E12）ごとに、VVMW で同じ役をする物を並べる。

**表 4** KawaPlayer の接続口（E1〜E12）と VVMW の対応

| 番号 | KawaPlayer の接続口 | VVMW で同じ役をする物 | 結び方の違い |
| --- | --- | --- | --- |
| E1 | 命令（`Controller` の public メソッド） | `Core` と `FrontendHandler` の public メソッド | VVMW は再生系の命令を `FrontendHandler` に分ける |
| E2 | 型付きの出来事（`AfterXxx`、29 個） | 無し | VVMW は型付きの出来事を持たず、すべて名前で配る |
| E3 | 名前の出来事（`SendCustomVideoEvent`） | `SendEvent(名前)`（Core 19 種類、`FrontendHandler` 11 種類） | 同じく名前。VVMW は受ける名前をビルドで絞る |
| E4 | 本体への登録（`AddListener`・`AddScreen` など） | `_AddListener`、ビルドの `[BindUdonSharpEvent]`、画面と音は Core の配列 | VVMW の購読は、ほぼすべてビルドで決まる |
| E5 | 前の確認（`BeforeUserXxx`、26 個） | 無し（ロックの値を見る） | VVMW は操作ごとには聞かない |
| E6 | UI の知らせ（`AfterLanguageChanged`） | `FrontendHandler` の `_OnUIUpdate` | どちらも名前 |
| E7 | 差し込み口（`_urlInterceptor`・`_playlistRefreshHandler`） | `InputFilterBase`（URL の差し込み） | KawaPlayer は欄に書いて名前で呼ぶ。VVMW は抽象の基底クラスで型として呼ぶ（§8.3） |
| E8 | 窓口（`ShowMessage`・`PlayImageUrl` など） | `UIHandler`・`Core` の public メソッド | どちらも型 |
| E9 | UI への登録（`UIController.AddListener`） | 無し（`UIHandler` は送り手ではない。送り手は `FrontendHandler`） | — |
| E10 | Sub Controller のインスペクター | `[Locatable]` と、Core のインスペクターへの埋め込み | KawaPlayer は根元で 1 回書く。VVMW は部品の欄ごと |
| E11 | ビルド処理（`IYamaPlayerBuildProcess`） | `IPreprocessor`・`ISelfPreProcess` | VVMW は部品自身もビルド処理を持てる（§9） |
| E12 | UI の差し込み（`uiSlots`、パス） | 無し（UI は自分のプレハブを持つ） | — |

---

## 4. 部品の層

![KawaPlayer は Controller・UIController・モジュール・デバイス、VVMW は Core・FrontendHandler・UIHandler・追加の部品からなる](images/layers.svg)

**図 1** 部品の層。

- **KawaPlayer：** 本体の中心は `Controller` で、再生と同期を持つ。UI ごとに `UIController` があり、本体のスクリーン・タブレット・パネルの UI は、どれも同じ `UIController` の形である。プレイリストの読み込みや権限などは、モジュール（`YamaPlayerModule`）として足す。
- **VVMW：** 本体の中心は `Core` で、再生と同期を持つ。プレイリスト・キュー・ロックは `FrontendHandler` に分かれていて、Core に 1 つだけ置く。UI は `UIHandler` で、いくつでも置ける。オーバーレイ・持てる画面・Resync ボタン・近づくと再生などは、追加の部品として足す。
- **違い：** KawaPlayer の `Controller` は、プレイリストとキューも持つ。VVMW は、それを `FrontendHandler` に分けているので、`UIHandler` は `FrontendHandler` を指せば足り、`Core` は欄からたどって決まる（§5）。

---

## 5. つなぐ相手の決め方

![KawaPlayer は、エディターで書き、ビルドで配線し、実行時に登録する。VVMW は、部品の欄に属性を付けて宣言し、ビルドの前処理が参照と配り先を埋める](images/linking.svg)

**図 2** つなぐ相手が決まるまで。

### 5.1 KawaPlayer

- **正の値：** 各部品の `_controller`（`UIController`）と `controller`（`YamaPlayerScreen`・`YamaPlayerSpeaker`）である。
- **書く時：** Sub Controller のインスペクターで `YamaPlayer` を選んだ瞬間に、根元の下の部品へ書く（[YamaPlayerSubControllerEditor.cs L132](https://github.com/Mega-Gorilla/KawaPlayer/blob/95f818ff67074a9d67de8975077c0d4aa17d947b/Editor/Inspector/YamaPlayerSubControllerEditor.cs#L132)）。`YamaPlayerSubController.YamaPlayer` そのものは、ビルドでも実行時でも読まれない。
- **ほかの見つけ方：**
  - モジュールは、親をたどって `Controller` を探す。
  - モジュールの UI は、ビルドで UI の下に写され、実行時に `GetComponentInParent<UIController>()` で自分の UI を探す。
  - ビルド処理は、シーン全体から `_controller` が同じ `UIController` を探す。
- **確かめ：** Sub Controller で選んだプレイヤーと合っているかは、ビルドでも実行時でも確かめ直さない。

### 5.2 VVMW

部品は、つなぐ相手を自分の欄で持ち、欄に属性を付ける。`UIHandler` の例を示す（[UIHandler.cs L24–L47](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Runtime/VVMW/UIHandler.cs#L24-L47)）。

```csharp
[SerializeField, BindUdonSharpEvent(nameof(_OnPerformerChange), /* ほか 10 個 */ nameof(OnVideoStart))]
[Resolve(nameof(handler) + "." + nameof(FrontendHandler.core), HideInInspectorIfResolvable = true)]
[Locatable(InstaniatePrefabPath = "Packages/idv.jlchntoz.vvmw/VVMW (No Controls).prefab", ...)]
Core core;

[Locatable(...), BindUdonSharpEvent(nameof(_OnUIUpdate))]
public FrontendHandler handler;
```

**表 5** VVMW の、つなぎ方の属性

| 属性 | 働く時 | 中身 |
| --- | --- | --- |
| `[Locatable]` | エディター | 欄の横に「探す」ボタンを出す。階層でいちばん近い型の物を探し、無ければ指定したプレハブを置ける（置くかは、呼ぶ側が決める。§6）（[LocatableAttribute.cs](https://github.com/JLChnToZ/vrcw-foundation/blob/2aa8623832453f8493f18743a6f448f00775f1dc/Packages/idv.jlchntoz.vrcw-foundation/Runtime/Attributes%20Markers/LocatableAttribute.cs#L9)） |
| `[Resolve("handler.core")]` | ビルド | 欄の値を、ほかの欄からたどって埋める。たどれるなら、インスペクターに欄を出さない（[ResolveAttribute.cs](https://github.com/JLChnToZ/vrcw-foundation/blob/2aa8623832453f8493f18743a6f448f00775f1dc/Packages/idv.jlchntoz.vrcw-foundation/Runtime/Attributes%20Markers/ResolveAttribute.cs#L15)） |
| `[BindUdonSharpEvent(名前…)]` | ビルド | 欄が指す送り手の配り先に、自分を足す。名前を書けば、その名前のイベントだけを受ける（[UdonSharpEventSender.cs L16](https://github.com/JLChnToZ/vrcw-foundation/blob/2aa8623832453f8493f18743a6f448f00775f1dc/Packages/idv.jlchntoz.vrcw-foundation/Runtime/UdonSharpEventSender.cs#L16)） |
| `[SingletonCoreControl]` | エディター | この型が「Core を操作する部品」だと示す。Core のインスペクターが、その部品の設定を埋め込む（`FrontendHandler.core` に付いている） |

- **正の値：** 部品が自分の欄で持つ。たどれる物（`UIHandler.core`）は、ビルドで決まるので、手で書いた値と食い違うことがない。
- **確かめたこと（付録 B）：** エディターでは `UIHandler.core` は空で、Play の前処理で `handler.core` から埋まった。

---

## 6. 部品の足し方

![KawaPlayer は、メニューで置いてからインスペクターでプレイヤーを選ぶ。VVMW は、メニューで置くと、いちばん近い Core か FrontendHandler に結ばれる。相手が無いとき本体を置くのは一部の部品だけで、UI は空のままになる](images/adding.svg)

**図 3** 部品の足し方。

- **KawaPlayer：** `GameObject > KawaPlayer > Tablet` などで、選んでいる物の下にプレハブを置く（`YamaPlayerMenu.cs`）。つなぐプレイヤーは、置いたあとにインスペクターで選ぶ。選ぶまでは、どこにもつながらない。
- **VVMW：** `GameObject > VizVid > …`（ヒエラルキーで右クリックしても出る）で置くと、メニューの処理が、置いたプレハブの部品を型ごとに見て、いちばん近い相手を欄に入れる（[MenuUtil.cs L44–L99](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Editor/VVMW/MenuUtil.cs#L44-L99)）。プレイヤーの物を選んで足せば、そのプレイヤーにつながる。相手が無いときの扱いは、部品の型で違う。

**表 6** VVMW のメニューが、置いた部品の欄を埋めるしかた

| 部品 | 埋める欄 | 相手がシーンに無いとき |
| --- | --- | --- |
| `FrontendHandler`・オーバーレイ（`OverlayControl`）・Resync ボタン（`ResyncButtonConfigurator`）・`ActiveRegionConfig` | `core` | Core のプレハブを置く（`Locate` の `createPrefab` が true、[L51・L55・L57・L59](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Editor/VVMW/MenuUtil.cs#L51-L59)） |
| UI（`UIHandler`）・近づくと再生（`AutoPlayOnNearV2`）・`StreamLinkAssigner` | `handler`（`StreamLinkAssigner` は `frontendHandler`）と `core` | 置かない。どちらも見つからなければ空のまま（3 つとも `Resolve` を呼び（[L53・L61・L73](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Editor/VVMW/MenuUtil.cs#L53-L73)）、`autoCreate` を渡さないので既定の false になる（[L81–L95](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Editor/VVMW/MenuUtil.cs#L81-L95)）） |
| `Core` | `audioLink`・`yttl` | 置かない（`createPrefab` が false、[L48–L49](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Editor/VVMW/MenuUtil.cs#L48-L49)） |

`[Locatable]` 属性は、無ければプレハブを置く力を持つが、それを使うかはメニューが部品ごとに決めている。たとえば、追加の UI（`Modules/Separated Controls`、`Prefabs/Default UI.prefab`）を、プレイヤーの無いシーンに置くと、UI の欄は空のままになる。

---

## 7. イベント

![KawaPlayer は、Controller が型付きのメソッドで、UIController とモジュールが名前で、それぞれのリストに配る。VVMW は、共通の基底クラスが、ビルドで作った名前ごとの配り先に配る](images/events.svg)

**図 4** イベントの配り方。

### 7.1 KawaPlayer

- **本体の出来事：** `Controller` が、`YamaPlayerListener` の型付きのメソッド（29 個）で配る（E2）。外の部品は、`Controller.SendCustomVideoEvent(名前)` で、本体のリスナーに名前でも配れる（E3）。
- **UI の前の確認と知らせ：** `UIController` が、自分のリスナーに名前で送る（E5・E6）。
- **モジュールの自前のリスト：** `PermissionManagement`・`PitchShifter`・`SlideShower`・`AudioLinkAdaptor` が、それぞれ自分のリストを持つ。
- **購読：** 各部品が実行時の `Start` で `AddListener(this)` を呼ぶ。外す口は無い。
- **名前：** 送る側も受ける側も文字列で書く。#255 で、`AudioLinkAdaptor` の知らせが、受ける UI の登録先と食い違っていることを見つけた（O5）。

### 7.2 VVMW

- **共通の基底：** 配る部品（`Core`・`FrontendHandler`・リストやスライダーなど）は、どれも `UdonSharpEventSender` を継ぐ（[UdonSharpEventSender.cs](https://github.com/JLChnToZ/vrcw-foundation/blob/2aa8623832453f8493f18743a6f448f00775f1dc/Packages/idv.jlchntoz.vrcw-foundation/Runtime/UdonSharpEventSender.cs#L100)）。
  - `SendEvent(名前)` は、まず名前ごとの配り先（`namedTargets`）に、次に全部を受ける配り先（`targets`）に、`SendCustomEvent(名前)` を送る。
  - 実行時の `_AddListener` もあるが、VVMW の中で実行時に呼ぶのは、`ButtonEntry` だけだった。
- **配るイベント：**
  - `Core` は、`_OnVideoBeginLoad`・`_OnTextureChanged`・`_OnVolumeChange` など、19 種類の名前で配る。
  - `FrontendHandler` は、`_OnUIUpdate`・`_OnPlay` など、11 種類の名前で配る。
- **購読：** ビルドの前処理（`BindUdonSharpEventPreprocessor`）が、`[BindUdonSharpEvent]` の付いた欄を読み、送り手の `targets` か `namedTargets` に書き込む。Core のインスペクターには、`targets` の一覧が出る。
- **名前：** 受ける側は `nameof(自分のメソッド)` で書くので、受ける側の打ち間違いはコンパイルで見つかる。VRChat の動画のイベントの一部（`OnVideoEnd` など）は、前処理が Udon の内部の名前（`_onVideoEnd`）に直す（付録 B の一覧では、`OnVideoPlay`・`OnVideoPause` は元の名前のまま）。
- **確かめたこと（付録 B）：** エディターでは Core の配り先は空だった。Play の前処理のあと、Core の `namedTargets` に 15 個の名前が入り、それぞれ `FrontendHandler` と `UIHandler` に配られていた。`targets` は空のままで、どの部品も、受ける名前を絞っていた。

---

## 8. 命令と権限

![KawaPlayer は、UI が操作の前に確認のイベントを送り、権限のモジュールが中止する。VVMW は、受ける側が 5 つの命令を断り、UI はロック中に URL の入力などを押せなくする。外の仕組みは _OnUnlock と _Lock を送る](images/permission.svg)

**図 5** 命令と権限。

### 8.1 命令

- **KawaPlayer：** UI は `Controller` の public メソッドを直接呼ぶ（E1）。タブレットのアプリも、タブレットの `UIController` を通して呼ぶ。
- **VVMW：** UI は、再生系の命令（`_Play`・`_Pause`・`_Stop`・`_Skip`・`PlayUrl`）を `FrontendHandler` に、音量・速度など見る人ごとの設定を `Core` に送る。`FrontendHandler` が無い UI は、`Core` を直接呼ぶ（[UIHandler_OmniInput.cs L80・L92](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Runtime/VVMW/UIHandler_OmniInput.cs#L80-L92)）。

### 8.2 権限

**表 7** 権限の確かめ方

| | KawaPlayer | VVMW |
| --- | --- | --- |
| 確かめる場所 | UI。操作の前に `InvokeBeforeEvent("BeforeUserXxx")` を送り、権限の UI（`PermissionManagementUI`）が `CancelCurrentAction()` を呼ぶ | 受ける側。`FrontendHandler` の `_Play`・`_Pause`・`_Stop`・`_Skip`・`_GlobalSync` が、`if (locked) return;` で断る（[FrontendHandler.cs L237](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Runtime/VVMW/FrontendHandler.cs#L237)） |
| 受ける側で確かめない命令 | すべて（`Controller` を直接呼べば、権限を通らない） | `PlayUrl`・`PlayAt`（リストの選択）。UI がロック中に URL の入力・リスト・リピート・シャッフルなどを押せなくする（[UIHandler.cs L447–L556](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Runtime/VVMW/UIHandler.cs#L447-L556)） |
| 権限の状態 | `PermissionManagement` のモジュールが持ち、同期する | `FrontendHandler.locked`。同期しない、見る人ごとの値 |
| 外の仕組みとのつなぎ | 権限はモジュールの中で決まる | 外の仕組みが `FrontendHandler` に `_OnUnlock`・`_Lock` を送る。VVMW の文書は、自分でロックを作るときも `_OnUnlock` を送ればよいと書いている（[FrontendHandler.cs L493–L504](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Runtime/VVMW/FrontendHandler.cs#L493-L504)） |
| 権限の細かさ | 26 個の操作ごとに、権限のモジュールが答える（見る 18 個、見ない 8 個） | ロックしているか、していないかの 2 つ |

- **確かめたこと（付録 B）：** Play 中に `FrontendHandler` へ `_Lock` を送ると、UI の URL 欄が押せなくなり、`_OnUnlock` で戻った。

**表 8** 操作ごとの権限のかかり方

| 操作 | KawaPlayer（権限のモジュールが見るか） | VVMW（ロック中） |
| --- | --- | --- |
| 再生・一時停止・停止 | 見る | 受ける側が断る。UI もボタンを出さない |
| 次の曲 | 見る | 受ける側が断る（`_Skip`） |
| 全員の読み込み直し・同期 | 見る（全員リロードボタン） | 受ける側が断る（`_GlobalSync`） |
| シーク | 見る | UI がスライダーを止める（Core は断らない） |
| URL を入れる | 見る | UI が URL 欄を止める（`PlayUrl` は断らない） |
| リストから選ぶ・キューの並べ替え・削除 | 見る | UI がリストを止める（`PlayAt` は断らない） |
| リピート・シャッフル | 見る | UI がボタンを止める |
| 速度 | 見る | 関係ない |
| 音程（ピッチシフター）・スライドショーのモードと秒数 | 見ない。全員で共有する設定なのに、権限を確かめない（[#211](https://github.com/Mega-Gorilla/KawaPlayer/issues/211)、未解決） | — |
| 自分の読み込み直し（ReSync・Resync） | 見ない | 関係ない（`_LocalSync`） |
| 音量・ミュート・ずれの補正 | 見ない | 関係ない |
| 明るさ・解像度・左右反転・言語 | 見ない | — |

### 8.3 URL の差し込み口

URL を、動画として読む前に別の部品に渡す口は、両者にある。作りが違う。

- **KawaPlayer（E7）：** `UIController` が、欄 `_urlInterceptor` に入った U# の部品に、`SetProgramVariable` で `interceptUrl`・`interceptSource` を書き、`SendCustomEvent("OnUrlSubmitted")` で呼ぶ。答えは、相手の欄 `interceptHandled` を読む。欄の名前もイベントの名前も文字列で、型の確かめは無い。入れるのは、PlaylistLoader のビルド処理である。
- **VVMW：**
  - 本体に抽象の基底クラス `InputFilterBase` があり、`pcUrl`・`questUrl` の欄と、抽象のメソッド `_ValidateUrls()` を持つ（[InputFilterBase.cs L12・L37](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Runtime/VVMW/InputFilterBase.cs#L12-L37)）。
  - `Core` は、その型の欄 `urlInputFilter` を持つ（[Core.cs L41](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Runtime/VVMW/Core.cs#L41)）。`FrontendHandler` があれば、それを引き取り、`PlayUrl` の中で、欄に URL を書いてから `_ValidateUrls()` を型として呼ぶ。拒むときは、受けた側が URL を空にする。
  - 外の人は、`InputFilterBase` を継いだ部品を作り、Core の欄に入れればよい。VVMW の中には、継いだ部品は無い。
- **違い：** VVMW の差し込み口は型付きなので、欄やメソッドの名前の打ち間違いはコンパイルで見つかる。U# の継承は 1 本なので、差し込み口を継ぐ部品は、ほかの基底を継げない。

### 8.4 命令の受け口

利用者の操作が、本体に届くまでにどこを通るかを比べる。

- **KawaPlayer：**
  - 受け口は、UI ごとの `UIController` である。`UIController` は、確認のイベント（`BeforeUserXxx`）を自分のリスナーに送り、中止されなければ `Controller` を呼ぶ。
  - 確認のイベントは 26 種類あり、`UIController`（3 つの部分クラス）・`GlobalReloadButton`・`PlaylistLoaderUI` の 38 か所で送る。権限のモジュール（`PermissionManagementUI`）が答えるのは、全員で共有する操作の 18 種類である。音量・ミュート・左右反転・明るさ・ずれの補正・解像度・言語・自分の読み込み直しの 8 種類には答えない。
  - `UIController` が本体（`Controller` とそのキュー）を変える呼び出しは、62 か所ある。内訳は、所有権を取る呼び出し（`TakeOwnership`）25 か所、命令 21 か所（17 種類）、設定の書き込み 16 か所（10 種類）である。
  - 権限のモジュールは、UI ごとに写される（`uiSlots`）。写す場所の無い Controller Bar と Playlist Panel では、確認のイベントは送られるが、答える物がいない。この 2 つと全員リロードボタンには、断ったことを知らせるダイアログ（`_modalDialog`）も無い。
  - モジュールの UI のうち、`PitchShifterUI` と `SlideShowerUI` は、確認のイベントを送らずに、自分のモジュールの共有の設定を変える（#211）。`DefaultUrlSettingsUI` は、モジュール自身の決まり（インスタンスのオーナーだけ）で確かめる。
- **VVMW：**
  - 受け口は 2 つに分かれる。再生・リスト・リピート・同期の命令は `FrontendHandler` に、シーク・速度・音量・ずれ・範囲のループは `Core` に送る（表 9）。`FrontendHandler` が無い UI は、すべて `Core` に送る。
  - ロック中に `FrontendHandler` が断るのは、`_Play`・`_Pause`・`_Stop`・`_Skip`・`_GlobalSync` の 5 つである（[FrontendHandler.cs L237–L297](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Runtime/VVMW/FrontendHandler.cs#L237-L297)）。`PlayUrl`・`PlayAt`・リピート・シャッフルは断らず、UI が押せなくする（§8.2 の表 8）。
  - オーバーレイ・Resync ボタン・`ActiveRegionConfig` は、`Core` を直接指す（§6 の表 6）。

**表 9** VVMW の UI が命令を送る先（`FrontendHandler` があるとき）

| 操作 | 送る先 | ロック中 | コード |
| --- | --- | --- | --- |
| 再生・一時停止・停止 | `FrontendHandler`（`_Play`・`_Pause`・`_Stop`） | 断る | [UIHandler.cs L271–L300](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Runtime/VVMW/UIHandler.cs#L271-L300) |
| 次の曲 | `FrontendHandler`（`_Skip`） | 断る | [UIHandler.cs L309](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Runtime/VVMW/UIHandler.cs#L309) |
| リピート・シャッフル | `FrontendHandler`（`NoRepeat`・`RepeatOne`・`RepeatAll`・`Shuffle`） | 断らない（UI が止める） | [UIHandler.cs L318–L368](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Runtime/VVMW/UIHandler.cs#L318-L368) |
| 自分の同期 | `FrontendHandler`（`_LocalSync`） | 断らない | [UIHandler.cs L376](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Runtime/VVMW/UIHandler.cs#L376) |
| 全員の同期 | `FrontendHandler`（`_GlobalSync`） | 断る | [UIHandler.cs L387](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Runtime/VVMW/UIHandler.cs#L387) |
| URL を入れる・リストから選ぶ・キューから消す | `FrontendHandler`（`PlayUrl`・`PlayAt`） | 断らない（UI が止める） | [UIHandler_OmniInput.cs L80–L84](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Runtime/VVMW/UIHandler_OmniInput.cs#L80-L84)、[UIHandler_Playlist.cs L255–L268](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Runtime/VVMW/UIHandler_Playlist.cs#L255-L268) |
| シーク | `Core`（`Progress`） | 断らない（UI がスライダーを止める） | [UIHandler_Progress.cs L11](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Runtime/VVMW/UIHandler_Progress.cs#L11) |
| 範囲のループ | `Core`（`SetRangeLoop`） | — | [UIHandler_Progress.cs L74](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Runtime/VVMW/UIHandler_Progress.cs#L74) |
| 速度・ずれ | `Core`（`Speed`・`SyncOffset`） | — | [UIHandler_Adjustment.cs L21–L74](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Runtime/VVMW/UIHandler_Adjustment.cs#L21-L74) |
| 音量・ミュート | `Core`（`Volume`・`Muted`） | — | [UIHandler.cs L397・L404](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Runtime/VVMW/UIHandler.cs#L397-L404) |

- **違い：**
  - KawaPlayer の受け口は UI の数だけあり、権限を確かめる部品も UI ごとに要る。
  - VVMW の受け口は本体ごとに 1 つ（`FrontendHandler`）だが、すべての命令がそこを通るわけではなく、ロックで断るのも 5 つだけである。

---

## 9. ビルド時の処理

**表 10** ビルド時の処理の組み立て

| | KawaPlayer | VVMW |
| --- | --- | --- |
| 入り口 | `YamaPlayerBuildProcess`（`IProcessSceneWithReport`、順番 -100000）が、`IYamaPlayerBuildProcess` を実装したクラスを `callbackOrder` の順に動かす | `BuildPreprocessorManager`（`IProcessSceneWithReport`、順番 -100）が、`IPreprocessor` を実装したクラスと、シーンの中で `ISelfPreProcess` を実装した部品を、`Priority` の順に動かす |
| 処理の種類 | 目的ごとのクラス（モジュール、全員リロードボタン、PlaylistLoader、画面と音、タブレットの訪問者など） | 汎用の処理（属性を読む `Resolve`・`BindUdonSharpEvent`、重複をまとめる `SingletonBehaviourCombiner`、エディター専用の物を消す）と、部品自身の処理 |
| デバイスに固有の処理 | 共通の処理か、デバイスのビルド処理のクラスに書く（全員リロードボタンはモジュールの Editor、タブレットの訪問者は共通の Editor） | 部品が `ISelfPreProcess` を実装して、自分の処理を持つ（例：`OverlayControl` はシーンのすべての Core の配り先に自分を足す） |
| ワールドに 1 つの部品 | `TabletVisitorsBuildProcess` が、最初の記録を残し、残りを止め、参照を付け替える | `ISingleton<T>` を付けた部品を、汎用の前処理がまとめ、参照を付け替える |

![KawaPlayer の YamaPlayerBuildProcess（-100000）が、翻訳・プレイリスト・モジュール・全員リロードボタン・PlaylistLoader・画面と音・後処理の順に動き、そのあと VizVid の土台の BuildPreprocessorManager（-100）が、部品自身の処理・Resolve・ISingleton・BindUdonSharpEvent・エディター専用の物の削除の順に動く](images/build-order.svg)

**図 6** ビルド時の処理の順番。Unity は、シーンの前処理を `callbackOrder` の小さい順に呼ぶので、KawaPlayer の処理がすべて済んでから、VVMW の土台の処理が動く。

VVMW の前処理の順番は、`Resolve` と重複のまとめが -1、部品自身の処理が -10 と 0、イベントの結び付けが 0、エディター専用の物を消すのが 100 である。

---

## 10. 部品ごとの設定とインスペクター

- **KawaPlayer：**
  - デバイスの設定は、根元の Sub Controller のインスペクターにまとまっている。
  - そのインスペクターが、`TabletPickup`・`TabletReturnButton`・`TabletVisitorRecorder` を直接扱う（O2）。新しいデバイスの設定を足すと、この共通のファイルを書き換える（#206 もここ）。
- **VVMW：**
  - 各部品が、自分のインスペクターを持つ。
  - Core のインスペクターは、エディターを開いたときに、すべての型を調べて `[SingletonCoreControl]` の付いた Core の欄を持つ型を集める（[EditorBase.cs L93](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Editor/VVMW/EditorBase.cs#L93)）。シーンの中で自分を指すその部品（`FrontendHandler`）のインスペクターを、Core のインスペクターの中に埋め込む（[CoreEditor.cs L1185](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Editor/VVMW/CoreEditor.cs#L1185)）。
  - Core のインスペクターは、どの部品も個別には知らない。ただし、部品を足すメニュー（`MenuUtil`）は、部品の型ごとに分かれている（§6 の表 6）。

---

## 11. 画面と音

- **KawaPlayer：** 画面と音の部品（`YamaPlayerScreen`・`YamaPlayerSpeaker`）が `controller` を持ち、ビルドで `Controller.AddScreen`・`AddAudioSource` によって本体の配列に足される。
- **VVMW：** Core が、画面（`screenTargets` など）と音（`audioSources`）の配列を持つ。画面に置くエディターの部品（`ScreenConfigurator`）が Core の配列を書き換え、外すときは配列から消す（[ScreenConfigurator.cs L256](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Runtime/VVMW/Designer/ScreenConfigurator.cs#L256)）。音は、Core のインスペクターの「Setup Speakers」ボタンで集める。
- **違い：** KawaPlayer は画面の側に正の値を持ち、ビルドで本体に集める。VVMW は本体の側に正の値を持ち、エディターの部品がそれを直す。

---

## 12. 同期の分担

**表 11** 同期する部品

| | KawaPlayer | VVMW |
| --- | --- | --- |
| 再生の状態 | `Controller`（Manual） | `Core`（Manual） |
| プレイリスト・キュー | `QueueList`・`HistoryList`・`DynamicPlaylist`（それぞれ Manual） | `FrontendHandler`（Manual） |
| UI | `UIController` は同期しない（None） | `UIHandler` は変数を同期しない（NoVariableSync） |
| デバイスの状態 | `TabletScreen`・`TabletVisitorRecorder`（Manual）、`GlobalReload`（ネットワークのイベント） | `OverlayControl` は変数を同期しない。持てる画面は、見る人ごと |
| 権限 | `PermissionManagement`（Manual） | `FrontendHandler.locked` は同期しない |

---

## 13. まとめ：それぞれの良いところ

**KawaPlayer**

- 権限が細かい。26 個の操作ごとに答えられ、権限の状態は全員で共有する。
- デバイスに、自分の同期する状態を持たせている（タブレットの画面、訪問者の記録）。
- 本体の出来事を、型付きのメソッドで配る。受ける側で、引数（`VideoError` など）を型付きで受け取れる。

**VVMW**

- つなぎ方を、部品の欄で宣言する。正の値が部品の側にあり、たどれる値はビルドで決まるので、食い違わない。
- 部品を足すと、いちばん近い本体に結ばれる。
- 再生・リスト・同期の命令の受け口が、本体ごとに 1 つ（`FrontendHandler`）にまとまる。ただし、シーク・速度・音量などは `Core` が直接受ける。
- イベントの配り方が 1 つの基底クラスにまとまり、受ける名前をビルドで絞れる。受ける側の名前は、`nameof` でコンパイル時に確かめられる。
- デバイスに固有の処理（ビルド・インスペクター）を、部品の側に置ける。前処理と Core のインスペクターは、個別の部品を知らない（部品を足すメニューは、型ごとに分かれる）。
- 権限の仕組みとは、2 つの名前のイベント（`_OnUnlock`・`_Lock`）だけでつながる。

**両方に共通する限界**

- 名前のイベントの送る側は、文字列のままである（VVMW も送る側は文字列か、自分のメソッドの `nameof`）。
- 出荷したプレハブの Network ID の制約は、どちらの作りでも変わらない。

---

## 14. 参考文献

1. Mega-Gorilla, KawaPlayer, develop `95f818f`. <https://github.com/Mega-Gorilla/KawaPlayer/tree/95f818ff67074a9d67de8975077c0d4aa17d947b>
2. KawaPlayer とデバイスのつながり方（現状の分析）, #255. [docs/analysis/device-connections.md](../../analysis/device-connections.md)
3. JLChnToZ, VizVid（VVMW）, develop `834bd5e`（1.8.2 の 4 コミットあと）, MIT. <https://github.com/JLChnToZ/VVMW/tree/834bd5ec453297b713399a5fbba1bd52a81b4f82>
4. JLChnToZ, vrcw-foundation, 0.0.68（`2aa8623`）, MIT. <https://github.com/JLChnToZ/vrcw-foundation/tree/2aa8623832453f8493f18743a6f448f00775f1dc>
5. VizVid の文書（部品の足し方、権限の仕組みとのつなぎ方）. <https://xtlcdn.github.io/VizVid/docs/>
6. 再生の同期：KawaPlayer と VizVid の比較, #228. [../228-playback-sync/comparison.md](../228-playback-sync/comparison.md)

---

## 付録 A：コードの場所

**表 A1** VVMW（`834bd5e`）

| 中身 | 場所 |
| --- | --- |
| Core（同期、イベントの送り手） | [Core.cs L24・L29](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Runtime/VVMW/Core.cs#L24-L29) |
| FrontendHandler（同期、Core の欄、ロック） | [FrontendHandler.cs L13–L36](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Runtime/VVMW/FrontendHandler.cs#L13-L36) |
| ロックで断る命令 | [FrontendHandler.cs L237](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Runtime/VVMW/FrontendHandler.cs#L237) |
| 外の仕組みとのつなぎ（`_OnUnlock`・`_Lock`） | [FrontendHandler.cs L493–L504](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Runtime/VVMW/FrontendHandler.cs#L493-L504) |
| UIHandler の欄と属性 | [UIHandler.cs L18–L47](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Runtime/VVMW/UIHandler.cs#L18-L47) |
| ロック中に押せなくする UI | [UIHandler.cs L447–L556](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Runtime/VVMW/UIHandler.cs#L447-L556) |
| 部品自身の前処理（すべての Core に自分を足す） | [OverlayControl.cs L399–L405](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Runtime/VVMW/OverlayControl.cs#L399-L405) |
| エディターだけのインターフェース | [VizVidBehaviour.cs L15](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Runtime/VVMW/VizVidBehaviour.cs#L15) |
| Core を操作する部品を集める | [EditorBase.cs L93](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Editor/VVMW/EditorBase.cs#L93)、[CoreEditor.cs L1185](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Editor/VVMW/CoreEditor.cs#L1185) |
| 部品を足すメニュー | [MenuUtil.cs L26・L51](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Editor/VVMW/MenuUtil.cs#L26-L51) |

**表 A2** vrcw-foundation（`2aa8623`）

| 中身 | 場所 |
| --- | --- |
| イベントの送り手（`targets`・`namedTargets`・`_AddListener`・`SendEvent`） | [UdonSharpEventSender.cs L37–L100](https://github.com/JLChnToZ/vrcw-foundation/blob/2aa8623832453f8493f18743a6f448f00775f1dc/Packages/idv.jlchntoz.vrcw-foundation/Runtime/UdonSharpEventSender.cs#L37-L100) |
| ビルドでの購読 | [BindUdonSharpEventPreprocessor.cs](https://github.com/JLChnToZ/vrcw-foundation/blob/2aa8623832453f8493f18743a6f448f00775f1dc/Packages/idv.jlchntoz.vrcw-foundation/Editor/Preprocessors/BindUdonSharpEventPreprocessor.cs) |
| 前処理の入り口と順番 | [BuildPreprocessorManager.cs](https://github.com/JLChnToZ/vrcw-foundation/blob/2aa8623832453f8493f18743a6f448f00775f1dc/Packages/idv.jlchntoz.vrcw-foundation/Editor/Preprocessors/BuildPreprocessorManager.cs) |
| ワールドに 1 つの部品をまとめる | [SingletonBehaviourCombiner.cs](https://github.com/JLChnToZ/vrcw-foundation/blob/2aa8623832453f8493f18743a6f448f00775f1dc/Packages/idv.jlchntoz.vrcw-foundation/Editor/Preprocessors/SingletonBehaviourCombiner.cs) |
| エディターだけのインターフェース（`ISingleton`・`ISelfPreProcess`） | [Interfaces.cs](https://github.com/JLChnToZ/vrcw-foundation/blob/2aa8623832453f8493f18743a6f448f00775f1dc/Packages/idv.jlchntoz.vrcw-foundation/Runtime/Attributes%20Markers/Interfaces.cs) |

KawaPlayer（`95f818f`）のコードの場所は、[device-connections.md](../../analysis/device-connections.md) の参考文献と付録にある。

---

## 付録 B：Unity での確認（2026-10-16）

テスト用プロジェクト（Unity 2022.3、VRChat Worlds SDK 3.10.4、ClientSim、VVMW は配布の 1.8.2）で、一時的なシーンに `VVMW (Separated Controls).prefab` だけを置き、前処理の前とあとの値を読んだ。手順・スクリプト・出力は [evidence~/clientsim/results.md](evidence~/clientsim/results.md) にある。

**表 B1** 確かめたこと

| 番号 | 確かめたこと | 結果 |
| --- | --- | --- |
| B1 | 前処理の前（エディター）の配り先 | Core・FrontendHandler とも、`targets` も `namedTargets` も空 |
| B2 | 前処理のあと（Play）の配り先 | Core の `namedTargets` に 15 個の名前。`FrontendHandler` と `UIHandler` に、受ける名前だけが配られる。`targets` は空のまま |
| B3 | `[Resolve]` | `UIHandler.core` が、エディターでは空、Play では `handler.core` から埋まる |
| B4 | 外からのロック | `_Lock` で UI の URL 欄が押せなくなり、`_OnUnlock` で戻る |

![Core の 15 個の名前のうち、動画の出来事・音量・速度など 9 個は FrontendHandler と UIHandler の両方に、題名や読み込みの始まりなど 4 個は FrontendHandler だけに、演者と範囲のループの 2 個は UIHandler だけに届く。FrontendHandler の _OnUIUpdate は UIHandler に届く](images/targets.svg)

**図 7** Play の前処理のあとの、名前ごとの配り先（B2）。

確かめていないこと：VRChat の Build & Test（複数のクライアント）、ロック中に `PlayUrl` を直接呼んだときの動き（コードからは、受ける側で断らないと読める）。
