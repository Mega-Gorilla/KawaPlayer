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

権限にも違いがある。VizVid は、再生の命令の 5 つを受ける側（`FrontendHandler`）が断り、URL の入力などは UI が押せなくする。KawaPlayer は、操作の前に UI が確認のイベントを送り、権限のモジュールが中止する。

VizVid の前処理と配り先の作られ方は、テスト用の Unity（ClientSim）で確かめた（付録 B）。この文書は事実の記録で、直す方針は [README.md](README.md) にまとめる。

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

**表 2** つなぎ方の比較（機能ごと）

| 機能 | KawaPlayer | VVMW | 節 |
| --- | --- | --- | --- |
| 部品の層 | `Controller`（再生と同期）、`UIController`（UI ごと）、モジュール、デバイス | `Core`（再生と同期）、`FrontendHandler`（プレイリストとロック、Core に 1 つ）、`UIHandler`（UI ごと）、追加の部品 | §4 |
| つなぐ相手の正の値 | 各部品の `_controller`。書くのは Sub Controller のインスペクターだけ | 各部品の欄。部品が自分で持つ | §5 |
| 相手の見つけ方 | 3 通り（エディターで書く・親をたどる・シーン全体を探す） | エディターでいちばん近い物を探す（`[Locatable]`）、ビルドで欄からたどる（`[Resolve]`） | §5 |
| 部品の足し方 | `GameObject > KawaPlayer > …` で置き、インスペクターでプレイヤーを選ぶ | `GameObject > VizVid > …` で置くと、いちばん近い Core に結ばれる。無ければ Core を置く | §6 |
| イベントの配り方 | `Controller` は型付き（29）と名前（E3）、`UIController` は名前、モジュール 4 つが自前のリスト | 共通の基底クラス `UdonSharpEventSender` の `SendEvent(名前)` | §7 |
| 購読の時 | 実行時の `Start` で `AddListener` | ビルドで `[BindUdonSharpEvent]`。受ける名前を絞れる | §7 |
| 名前の確かめ | 送る側も受ける側も文字列 | 受ける側は `nameof` で自分のメソッドを指す | §7 |
| 命令 | UI が `Controller` を直接呼ぶ | 再生系は UI が `FrontendHandler` を呼び、見る人ごとの設定は `Core` を呼ぶ | §8 |
| 権限 | UI が操作の前に `BeforeUserXxx` を送り、権限のモジュールが中止する | 受ける側が 5 つの命令を断り、URL の入力などは UI が押せなくする。外の仕組みは `_OnUnlock`・`_Lock` を送る | §8 |
| ビルド時の処理の置き場所 | `IYamaPlayerBuildProcess` を実装したクラス。デバイスごとのクラスもある | 汎用の前処理（属性を読む）と、部品自身の前処理（`ISelfPreProcess`） | §9 |
| ワールドに 1 つの部品 | タブレットの訪問者の記録を、専用のビルド処理が 1 つにまとめる | `ISingleton` を付けた部品を、汎用の前処理が 1 つにまとめる | §9 |
| 部品ごとの設定 | 共通の Sub Controller のインスペクターに、タブレットの欄がある | 部品が自分のインスペクターを持つ。Core のインスペクターは、Core を操作する部品の設定を埋め込む | §10 |
| 画面と音 | `YamaPlayerScreen`・`YamaPlayerSpeaker` を、ビルドで `Controller` に登録する | Core が画面と音の配列を持ち、エディターの部品（`ScreenConfigurator`）がそれを書き換える | §11 |
| 同期 | `Controller`、キュー・履歴・枠、`TabletScreen` などが、それぞれ同期する。`UIController` は同期しない | `Core` と `FrontendHandler` が同期する。`UIHandler` は変数を同期しない | §12 |

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

**表 3** VVMW の、つなぎ方の属性

| 属性 | 働く時 | 中身 |
| --- | --- | --- |
| `[Locatable]` | エディター | 欄の横に「探す」ボタンを出す。階層でいちばん近い型の物を探し、無ければ指定したプレハブを置く（[LocatableAttribute.cs](https://github.com/JLChnToZ/vrcw-foundation/blob/2aa8623832453f8493f18743a6f448f00775f1dc/Packages/idv.jlchntoz.vrcw-foundation/Runtime/Attributes%20Markers/LocatableAttribute.cs#L9)） |
| `[Resolve("handler.core")]` | ビルド | 欄の値を、ほかの欄からたどって埋める。たどれるなら、インスペクターに欄を出さない（[ResolveAttribute.cs](https://github.com/JLChnToZ/vrcw-foundation/blob/2aa8623832453f8493f18743a6f448f00775f1dc/Packages/idv.jlchntoz.vrcw-foundation/Runtime/Attributes%20Markers/ResolveAttribute.cs#L15)） |
| `[BindUdonSharpEvent(名前…)]` | ビルド | 欄が指す送り手の配り先に、自分を足す。名前を書けば、その名前のイベントだけを受ける（[UdonSharpEventSender.cs L16](https://github.com/JLChnToZ/vrcw-foundation/blob/2aa8623832453f8493f18743a6f448f00775f1dc/Packages/idv.jlchntoz.vrcw-foundation/Runtime/UdonSharpEventSender.cs#L16)） |
| `[SingletonCoreControl]` | エディター | この型が「Core を操作する部品」だと示す。Core のインスペクターが、その部品の設定を埋め込む（`FrontendHandler.core` に付いている） |

- **正の値：** 部品が自分の欄で持つ。たどれる物（`UIHandler.core`）は、ビルドで決まるので、手で書いた値と食い違うことがない。
- **確かめたこと（付録 B）：** エディターでは `UIHandler.core` は空で、Play の前処理で `handler.core` から埋まった。

---

## 6. 部品の足し方

![KawaPlayer は、メニューで置いてからインスペクターでプレイヤーを選ぶ。VVMW は、メニューで置くと、いちばん近い Core に結ばれ、無ければ Core を置く](images/adding.svg)

**図 3** 部品の足し方。

- **KawaPlayer：** `GameObject > KawaPlayer > Tablet` などで、選んでいる物の下にプレハブを置く（`YamaPlayerMenu.cs`）。つなぐプレイヤーは、置いたあとにインスペクターで選ぶ。選ぶまでは、どこにもつながらない。
- **VVMW：** `GameObject > VizVid > …`（ヒエラルキーで右クリックしても出る）で置くと、メニューの処理が `LocatableAttributeDrawer.Locate` を呼び、いちばん近い Core（UI なら `FrontendHandler`）を欄に入れる（[MenuUtil.cs L51](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Editor/VVMW/MenuUtil.cs#L51)）。シーンに無ければ、Core のプレハブを置く。プレイヤーの物を選んで足せば、そのプレイヤーにつながる。

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

**表 4** 権限の確かめ方

| | KawaPlayer | VVMW |
| --- | --- | --- |
| 確かめる場所 | UI。操作の前に `InvokeBeforeEvent("BeforeUserXxx")` を送り、権限の UI（`PermissionManagementUI`）が `CancelCurrentAction()` を呼ぶ | 受ける側。`FrontendHandler` の `_Play`・`_Pause`・`_Stop`・`_Skip`・`_GlobalSync` が、`if (locked) return;` で断る（[FrontendHandler.cs L237](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Runtime/VVMW/FrontendHandler.cs#L237)） |
| 受ける側で確かめない命令 | すべて（`Controller` を直接呼べば、権限を通らない） | `PlayUrl`・`PlayAt`（リストの選択）。UI がロック中に URL の入力・リスト・リピート・シャッフルなどを押せなくする（[UIHandler.cs L447–L556](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Runtime/VVMW/UIHandler.cs#L447-L556)） |
| 権限の状態 | `PermissionManagement` のモジュールが持ち、同期する | `FrontendHandler.locked`。同期しない、見る人ごとの値 |
| 外の仕組みとのつなぎ | 権限はモジュールの中で決まる | 外の仕組みが `FrontendHandler` に `_OnUnlock`・`_Lock` を送る。VVMW の文書は、自分でロックを作るときも `_OnUnlock` を送ればよいと書いている（[FrontendHandler.cs L493–L504](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Runtime/VVMW/FrontendHandler.cs#L493-L504)） |
| 権限の細かさ | 26 個の操作ごとに、権限のモジュールが答える（見る 18 個、見ない 8 個） | ロックしているか、していないかの 2 つ |

- **確かめたこと（付録 B）：** Play 中に `FrontendHandler` へ `_Lock` を送ると、UI の URL 欄が押せなくなり、`_OnUnlock` で戻った。

---

## 9. ビルド時の処理

**表 5** ビルド時の処理の組み立て

| | KawaPlayer | VVMW |
| --- | --- | --- |
| 入り口 | `YamaPlayerBuildProcess`（`IProcessSceneWithReport`、順番 -100000）が、`IYamaPlayerBuildProcess` を実装したクラスを `callbackOrder` の順に動かす | `BuildPreprocessorManager`（`IProcessSceneWithReport`、順番 -100）が、`IPreprocessor` を実装したクラスと、シーンの中で `ISelfPreProcess` を実装した部品を、`Priority` の順に動かす |
| 処理の種類 | 目的ごとのクラス（モジュール、全員リロードボタン、PlaylistLoader、画面と音、タブレットの訪問者など） | 汎用の処理（属性を読む `Resolve`・`BindUdonSharpEvent`、重複をまとめる `SingletonBehaviourCombiner`、エディター専用の物を消す）と、部品自身の処理 |
| デバイスに固有の処理 | 共通の処理か、デバイスのビルド処理のクラスに書く（全員リロードボタンはモジュールの Editor、タブレットの訪問者は共通の Editor） | 部品が `ISelfPreProcess` を実装して、自分の処理を持つ（例：`OverlayControl` はシーンのすべての Core の配り先に自分を足す） |
| ワールドに 1 つの部品 | `TabletVisitorsBuildProcess` が、最初の記録を残し、残りを止め、参照を付け替える | `ISingleton<T>` を付けた部品を、汎用の前処理がまとめ、参照を付け替える |

VVMW の前処理の順番は、`Resolve` と重複のまとめが -1、部品自身の処理が -10 と 0、イベントの結び付けが 0、エディター専用の物を消すのが 100 である。

---

## 10. 部品ごとの設定とインスペクター

- **KawaPlayer：**
  - デバイスの設定は、根元の Sub Controller のインスペクターにまとまっている。
  - そのインスペクターが、`TabletPickup`・`TabletReturnButton`・`TabletVisitorRecorder` を直接扱う（O2）。新しいデバイスの設定を足すと、この共通のファイルを書き換える（#206 もここ）。
- **VVMW：**
  - 各部品が、自分のインスペクターを持つ。
  - Core のインスペクターは、エディターを開いたときに、すべての型を調べて `[SingletonCoreControl]` の付いた Core の欄を持つ型を集める（[EditorBase.cs L93](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Editor/VVMW/EditorBase.cs#L93)）。シーンの中で自分を指すその部品（`FrontendHandler`）のインスペクターを、Core のインスペクターの中に埋め込む（[CoreEditor.cs L1185](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Editor/VVMW/CoreEditor.cs#L1185)）。
  - Core のインスペクターは、どの部品も個別には知らない。

---

## 11. 画面と音

- **KawaPlayer：** 画面と音の部品（`YamaPlayerScreen`・`YamaPlayerSpeaker`）が `controller` を持ち、ビルドで `Controller.AddScreen`・`AddAudioSource` によって本体の配列に足される。
- **VVMW：** Core が、画面（`screenTargets` など）と音（`audioSources`）の配列を持つ。画面に置くエディターの部品（`ScreenConfigurator`）が Core の配列を書き換え、外すときは配列から消す（[ScreenConfigurator.cs L256](https://github.com/JLChnToZ/VVMW/blob/834bd5ec453297b713399a5fbba1bd52a81b4f82/Packages/idv.jlchntoz.vvmw/Runtime/VVMW/Designer/ScreenConfigurator.cs#L256)）。音は、Core のインスペクターの「Setup Speakers」ボタンで集める。
- **違い：** KawaPlayer は画面の側に正の値を持ち、ビルドで本体に集める。VVMW は本体の側に正の値を持ち、エディターの部品がそれを直す。

---

## 12. 同期の分担

**表 6** 同期する部品

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
- イベントの配り方が 1 つの基底クラスにまとまり、受ける名前をビルドで絞れる。受ける側の名前は、`nameof` でコンパイル時に確かめられる。
- デバイスに固有の処理（ビルド・インスペクター）を、部品の側に置ける。共通のコードは、個別の部品を知らない。
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

確かめていないこと：VRChat の Build & Test（複数のクライアント）、ロック中に `PlayUrl` を直接呼んだときの動き（コードからは、受ける側で断らないと読める）。
