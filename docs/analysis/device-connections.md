# KawaPlayer とデバイスのつながり方（現状の分析）

| 項目 | 内容 |
| --- | --- |
| 文書の種類 | 現状の分析（実装の記録） |
| 読む人 | KawaPlayer の開発者、レビューをする人 |
| 調べた版 | develop `55fd86d`（2026-10-16、#254 のマージのあと） |
| 目的 | これから増えるデバイスに合わせて、KawaPlayer の接続口（エンドポイント）を整理するための、現状の確認 |
| 関連する issue | #108（タブレット）、#236・#248（全員リロードボタン）、#82・#84・#91（URL の差し込みとプレイリストの更新）、#130（ダイアログ）、#159（言語）、#206 |
| 図の元 | [images/device-connections/figures.py](images/device-connections/figures.py)（`python figures.py` で SVG を書き直す） |

## 要旨

KawaPlayer の本体（`KawaPlayer.prefab`）の横には、Controller Bar・Playlist Panel・Sub Screen・タブレット・全員リロードボタンの 5 種類のデバイスを置ける。この文書は、デバイスが本体とどうつながり、どんな流れで命令や出来事をやりとりしているかを、コードの名前と図で記録する。つなぎ込みは 3 段に分かれる。エディターでは Sub Controller のインスペクターが参照を書き、ビルドではビルド処理がモジュールやボタンを配線し、実行時には各部品が `Start` で自分を登録する。実行時の接続口は `Controller` と `UIController` に集まり、型の決まった呼び出し、名前の文字列で送るイベント、欄に値を書いて呼ぶ差し込み口が混ざっている。部品が「どのプレイヤーに従うか」を見つける方法は 3 通りあり、タブレットにしかない設定が共通のインスペクターに入っている。最後に、接続口を整理するときの論点を 8 つ挙げる。

**キーワード：** デバイス、接続口、Controller、UIController、YamaPlayerSubController、リスナー、ビルド処理、モジュール

## 目次

1. はじめに
2. 構成
3. つなぎ込みの 3 段
4. 接続口の一覧
5. 実行時の流れ
6. デバイスごとのつながり
7. モジュールの UI の差し込み
8. ネットワーク
9. 観察：接続口を整理するときの論点
10. まとめ
11. 参考文献

---

## 1. はじめに

### 1.1 目的

KawaPlayer には、これからもタブレットやボタンのようなデバイスが増えていく。今は、デバイスごとに本体へのつなぎ方が少しずつ違い、新しいデバイスを足すたびに、共通のインスペクターやビルド処理に手を入れている。この文書は、接続口を整理する設計の前に、今どうつながっているかを一か所にまとめる。直し方は決めない。それは別の設計の文書（`docs/design/`）で扱う。

### 1.2 範囲

- **入れるもの：** 本体とデバイスのあいだの参照、イベント、命令、ビルドでの配線、ネットワークでの同期。デバイスの中の部品どうしのつながり（タブレットのアプリなど）は、本体とのつながりに関わるところだけ書く。
- **入れないもの：** 再生の同期のくわしい決まり（[playback-sync.md](playback-sync.md)）、URL から再生までの流れ（[url-to-playback-pipeline.md](url-to-playback-pipeline.md)）、プレイリストのしくみ（[playlist-pipeline.md](playlist-pipeline.md)）。

### 1.3 用語

**表 1** この文書で使う言葉

| 言葉 | 意味 |
| --- | --- |
| 本体 | `KawaPlayer.prefab`。根元に `YamaPlayer`、中に `Controller`・プレーヤー・プレイリスト・`Modules/`・本体のスクリーンの UI（`ScreenUI`）がある |
| デバイス | 本体とは別に置くプレハブ。根元に `YamaPlayerSubController` が付く（§2） |
| 接続口 | 本体の外の部品が、本体を呼んだり、本体から知らせを受けたりする入り口。この文書では E1〜E12 と番号を付ける（§4） |
| リスナー | 出来事を受け取るために登録した部品。`Controller` に登録する物（`YamaPlayerListener`）と、`UIController` に登録する物（`UdonSharpBehaviour`）の 2 種類がある |
| 前の確認 | `UIController` が操作の前に送る `BeforeUserXxx` のイベント。受けた部品が中止できる（§4.4） |

---

## 2. 構成

本体の中心は `Controller` で、再生の状態を同期する唯一の部品である（キュー・履歴・プレイリストの枠の中身は、それぞれの部品が同期する。§8）。デバイスは、自分の中の `UIController`（Sub Screen は `YamaPlayerScreen`）の参照で `Controller` を指す。本体のスクリーンの UI（`ScreenUI`）も、デバイスと同じ `UIController` でつながる。

![KawaPlayer 本体と 5 種類のデバイス。デバイスは UIController か YamaPlayerScreen で本体の Controller を指し、命令を送り、出来事を受け取る](images/device-connections/overview.svg)

**図 1** KawaPlayer 本体とデバイス。

**表 2** デバイスの一覧

| デバイス | プレハブ | 本体を指す部品 | 持てる | 自分で同期する物 | 権限の差し込み先 |
| --- | --- | --- | --- | --- | --- |
| Controller Bar | `Prefabs/ControlBar.prefab` | `UIController` | — | 無し | 無し（合うパスがない） |
| Playlist Panel | `Prefabs/PlaylistPanel.prefab` | `UIController` | — | 無し | 無し（合うパスがない） |
| Sub Screen | `Prefabs/SubScreen.prefab` | `YamaPlayerScreen` | 持てる（`VRCPickup`） | 置き場所（`VRCObjectSync`、選んだとき） | — |
| タブレット | `Prefabs/Tablet.prefab` | `UIController` ×2 | 持てる（`TabletPickup`） | `TabletScreen`、`TabletVisitorRecorder` | `Canvas/Display` と、KawaPlayer アプリの `Canvas/User` |
| 全員リロードボタン | `Modules/GlobalReload/GlobalReloadButton.prefab` | `UIController` | — | 無し（モジュールがネットワークのイベントを送る） | `Canvas/Modules`（空で用意） |

`YamaPlayerSubController`（`Runtime/Components/YamaPlayerSubController.cs`）は、`YamaPlayer` の欄が 1 つあるだけの `MonoBehaviour` で、実行時の処理を持たない。YamaPlayer から引き継いだもので、KawaPlayer はタブレットと全員リロードボタンにも使っている。

---

## 3. つなぎ込みの 3 段

デバイスと本体のつながりは、エディター・ビルド・実行時の 3 段で作られる。

![エディターでは Sub Controller のインスペクターが参照を書き、ビルドではビルド処理が配線し、実行時には Start で登録する](images/device-connections/stages.svg)

**図 2** つなぎ込みの 3 段。

### 3.1 エディター：参照を書く

`YamaPlayerSubControllerEditor`（`Editor/Inspector/YamaPlayerSubControllerEditor.cs`）は、インスペクターで `YamaPlayer` が選ばれた瞬間に、根元の下の部品へ本体の `Controller` を書く（`ProcessYamaPlayerChange`）。

**表 3** インスペクターが書く参照

| 書く先 | 欄 | 数 |
| --- | --- | --- |
| `UIController` | `_controller` | 下にあるすべて（YamaPlayer は最初の 1 つだけだった。タブレットのために KawaPlayer が変えた） |
| `YamaPlayerScreen` | `controller` | 下にあるすべて |
| `YamaPlayerSpeaker` | `controller` | 下にあるすべて |

書いた値は、シーンのプレハブの上書きとして保存される。`YamaPlayerSubController.YamaPlayer` そのものは、ビルドでも実行時でも読まれない。インスペクターは、ほかに色の組・既定の言語・待ち受け画像を下の UI にそろえ、持てるデバイスには持つ・同期の欄、タブレットには戻す設定と訪問者の顔写真の欄を出す。

### 3.2 ビルド：配線する

ワールドのビルドと Play のとき、`YamaPlayerBuildProcess` が `IYamaPlayerBuildProcess` を実装した処理を、`callbackOrder` の小さい順に動かす（`TypeCache` で集める）。

**表 4** デバイスのつながりに関わるビルド処理（順番の順）

| 順番 | 処理 | すること | 相手の見つけ方 |
| --- | --- | --- | --- |
| -3000 | `YamaPlayerModuleBuildProcess` | 有効なモジュールに `_controller` を入れる。モジュールの `uiSlots` を、同じ `Controller` を指す UIController に写す（§7）。無効なモジュールは `EditorOnly` にする | モジュール：親をたどって `Controller`。UI：シーン全体から `_controller` が同じ物。写す先は `transform.Find(targetPath)` |
| -2900 | `GlobalReloadBuildProcess` | ボタンに、そのプレイヤーの `GlobalReload` を入れる。見つからないボタンは外してログに出す | ボタンの親の `UIController` の `_controller` |
| -2600 | `DynamicPlaylistBuildProcess` | 各 UIController に、有効な動的プレイリストの枠の一覧（`_dynamicPlaylists`）を入れる | UIController の `_controller` |
| -2500 | `PlaylistLoaderBuildProcess` | URL 欄のある UIController に `_urlInterceptor` と `_urlHintKey` を、すべての UIController に `_playlistRefreshHandler` を入れる。`PlaylistLoaderUI` に `_uiController` を入れる | シーン全体から `_controller` が同じ UIController |
| -2000 | `YamaPlayerComponentBuildProcess` | `YamaPlayerScreen`・`YamaPlayerSpeaker` を、`Controller.AddScreen`・`AddAudioSource` で本体の配列に足す | 各部品の `controller` |
| -2000 | `TabletVisitorsBuildProcess` | ワールドに 1 つの `TabletVisitorRecorder` を残し、シーンの一番上へ移し、すべての訪問者アプリをそこへ向ける | シーン全体 |
| -1 | `YamaPlayerPostProcess` | `Controller` に版の番号を入れ、`Controller` をシーンの一番上へ移す | 各 `YamaPlayer` |

このほかに、翻訳（-10000）、プレイリスト（-9000）、`[RegisterEvent]` の配線（-8000）、配色（-2000）、プレーヤーの並び（-2000）、AudioLink・LTCGI・PitchShifter・Persistence のモジュールのビルド処理と、AutoPlay の From Playlist のプレイリストの番号を、ワールドに入るプレイリストの中の番号に直す処理（-2900、issue #209）がある。

### 3.3 実行時：登録する

Udon の `Start` で、各部品が自分を相手のリストに足す。リストは実行時の配列で、保存はされない。

**表 5** 実行時の登録

| 登録する側 | 登録先 | 受け取るもの | 見つけ方 |
| --- | --- | --- | --- |
| `UIController` | `Controller.AddListener` | 出来事（E2・E3） | 欄 `_controller`（§3.1 で書いた物） |
| モジュール（`YamaPlayerModule` を継ぐ 13 個） | `Controller.AddListener` | 出来事（E2・E3） | 欄 `_controller`（§3.2 で入れた物） |
| `QueueList`・`HistoryList`・`DynamicPlaylist` | `Controller.AddListener` | 出来事（E2・E3） | 欄 `_controller` |
| モジュールの UI（`PermissionManagementUI`・`AudioLinkAdaptorUI`・`DefaultUrlSettingsUI`・`PitchShifterUI`・`SlideShowerUI`） | `UIController.AddListener` | 前の確認（E5）、UI の知らせ（E6） | `GetComponentInParent<UIController>()` |
| `TabletScreen` | `UIController.AddListener`（タブレットの） | UI の知らせ（E6） | 欄 `_uiController` |

---

## 4. 接続口の一覧

実行時の接続口は `Controller` と `UIController` に集まり、エディターとビルドの接続口がそれらを配線する。

![Controller は E1〜E4、UIController は E5〜E9、エディターとビルドは E10〜E12 の接続口を持つ](images/device-connections/endpoints.svg)

**図 3** 接続口（エンドポイント）の地図。

**表 6** 接続口の一覧

| 番号 | 接続口 | 置き場所 | 結び方 | 向き | 使う側 |
| --- | --- | --- | --- | --- | --- |
| E1 | 命令 | `Controller` の public メソッド・プロパティ（約 90） | 型（直接呼ぶ） | 外 → 本体 | UIController、モジュール（タブレットのアプリは UIController を通す） |
| E2 | 出来事 | `YamaPlayerListener` の `AfterXxx`（29） | 型（仮想メソッド） | 本体 → 外 | UIController、モジュール、キュー・履歴 |
| E3 | 名前の出来事 | `Controller.SendCustomVideoEvent(名前)` | 名前の文字列 | だれでも → 本体のリスナー全員 | キュー・履歴・枠、`VideoInfoDownloader`、言語の変更 |
| E4 | 本体への登録 | `AddListener`・`AddScreen`・`AddAudioSource`・`AddPlaylist` | 型 | 外 → 本体 | 実行時の各部品、ビルド処理 |
| E5 | 前の確認 | `UIController.InvokeBeforeEvent("BeforeUserXxx")`・`CancelCurrentAction` | 名前の文字列 | UI → UI のリスナー | 権限、全員リロードボタン |
| E6 | UI の知らせ | `AfterLanguageChanged` | 名前の文字列 | UI → UI のリスナー | モジュールの UI、`TabletScreen` |
| E7 | 差し込み口 | `UIController._urlInterceptor`・`_playlistRefreshHandler` | 欄に値を書き、名前で呼ぶ | UI → モジュール | PlaylistLoader |
| E8 | 窓口 | `ShowMessage`・`ShowConfirm`・`GetTranslation`・`PlayImageUrl` など | 型 | 外 → UI | モジュールの UI、タブレットのアプリ |
| E9 | UI への登録 | `UIController.AddListener(UdonSharpBehaviour)` | 型 | 外 → UI | モジュールの UI、`TabletScreen` |
| E10 | インスペクター | `YamaPlayerSubControllerEditor` | 参照を書く | エディター | デバイスの UI・画面・音 |
| E11 | ビルド処理 | `IYamaPlayerBuildProcess`（`callbackOrder` の順） | 型（インターフェース） | ビルド | 本体とモジュールの Editor |
| E12 | UI の差し込み | `YamaPlayerModuleDefinition.uiSlots` | パスの文字列 | ビルド | モジュール（§7） |

### 4.1 命令（E1）

UI やモジュールは、`Controller` の public メソッドを直接呼ぶ。同期する操作は、呼ぶ前に `Controller.TakeOwnership()` でオーナーになる。

**表 7** `Controller` の public メンバーの分類（代表的なもの）

| 分類 | メンバー |
| --- | --- |
| 再生の操作 | `Play`・`Pause`・`Stop`・`SetTime`・`Reload`・`Forward`・`Backward`・`PlayTrack`・`PlayTrackFromQueue`・`PlayTrackFromHistory`・`PlayRandomTrack` |
| 設定 | `Volume`・`Mute`・`Loop`・`ShufflePlay`・`Speed`・`Repeat`・`LocalDelay`・`MirrorFlip`・`Brightness`・`MaxResolution`・`SetPlayerType`・`SetUILanguage` |
| 状態（読むだけ） | `Stopped`・`Paused`・`IsPlaying`・`IsLoading`・`IsError`・`IsLive`・`Duration`・`VideoTime`・`SyncedState`・`CanReload`・`IsLocal`・`Track`・`Queue`・`History`・`Playlists`・`Handler` |
| エラーの記録 | `ErrorLogCount`・`GetErrorTitle`・`GetErrorUrl` など 12 個（エラーの詳細の画面が読む） |
| 登録（E4） | `AddListener`・`AddScreen`・`AddAudioSource`・`AddPlaylist`・`EventListeners`・`SendCustomVideoEvent` |

### 4.2 出来事（E2・E3）

`Controller` は `YamaPlayerListener` を継いでいて、プレーヤー（`BaseVideoPlayerHandler` など）の知らせを受け、自分の処理をしてから、登録したリスナー全員に同じ出来事を配る（§5.1）。

**表 8** 型の決まった出来事（`YamaPlayerListener`、29 個）

| 分類 | 数 | 例 |
| --- | --- | --- |
| 再生の出来事 | 8 | `AfterVideoReady`・`AfterVideoStarted`・`AfterVideoErrorOccurred`・`AfterVideoStopped` |
| 再生の設定 | 7 | `AfterPlayerHandlerChanged`・`AfterTimeChanged`・`AfterSpeedChanged`・`AfterLocalDelayChanged` |
| 画面 | 4 | `AfterTextureUpdated`・`AfterMirrorFlipChanged`・`AfterBrightnessChanged`・`AfterMaxResolutionChanged` |
| 音 | 2 | `AfterVolumeChanged`・`AfterMuteChanged` |
| そのほか | 8 | `AfterTrackUpdated`・`AfterQueueUpdated`・`AfterPlaylistsUpdated`・`AfterOwnerChanged`・`AfterVideoRetry` |

E3 の `SendCustomVideoEvent(名前)` は、リスナー全員に `SendCustomEvent(名前)` を送る。型のある出来事を本体の外から起こすときと、型にない出来事を配るときに使われている。

**表 9** 名前で配られる出来事（E3）

| 名前 | 送る側 | 型にあるか |
| --- | --- | --- |
| `AfterQueueUpdated` | `QueueList`、`VideoInfoDownloader` | ある |
| `AfterHistoryUpdated` | `HistoryList` | ある |
| `AfterPlaylistsUpdated` | `DynamicPlaylist` | ある |
| `AfterTrackUpdated` | `VideoInfoDownloader` | ある |
| `_OnUILanguageChanged` | `Controller.SetUILanguage` | ない（受けるのは `UIController`、§4.5） |

### 4.3 本体への登録（E4）

実行時に使われる登録は `AddListener` だけである。`AddScreen`・`AddAudioSource` は、ビルドで画面と音を本体の配列に焼き込むのに使われる（§3.2）。`AddPlaylist` はどこからも呼ばれていない（プレイリストは、`Controller` が実行時に自分の下から集める）。外す口（`RemoveListener` など）は無い。

### 4.4 前の確認（E5）

`UIController` は、操作の前に `InvokeBeforeEvent("BeforeUserXxx")` で、登録したリスナー全員に名前のイベントを送る。受けた部品が `CancelCurrentAction()` を呼ぶと、残りのリスナーには送らず、`false` を返し、操作をやめる（§5.2）。

**表 10** 前の確認のイベント（26 個）と、権限（PermissionManagement）が見るか

| 権限が見る（18 個） | 権限が見ない（8 個。自分だけの設定） |
| --- | --- |
| `BeforeUserPlayTrack`・`BeforeUserPauseVideo`・`BeforeUserStopVideo`・`BeforeUserSetTime`・`BeforeUserForward`・`BeforeUserBackward`・`BeforeUserChangeLoop`・`BeforeUserChangeShufflePlay`・`BeforeUserChangeSpeed`・`BeforeUserChangeRepeat`・`BeforeUserChangePlayerHandler`・`BeforeUserAddTrackToQueue`・`BeforeUserRemoveTrackFromQueue`・`BeforeUserMoveTrackUp`・`BeforeUserMoveTrackDown`・`BeforeUserLoadPlaylist`・`BeforeUserDeletePlaylist`・`BeforeUserGlobalReload` | `BeforeUserChangeVolume`・`BeforeUserChangeMute`・`BeforeUserChangeMirrorFlip`・`BeforeUserChangeBrightness`・`BeforeUserChangeMaxResolution`・`BeforeUserChangeLocalDelay`・`BeforeUserChangeLanguage`・`BeforeUserReloadVideo` |

送るのは `UIController`（`UIController.cs`・`.Localization.cs`・`.Playlist.cs`）のほか、`GlobalReloadButton` と `PlaylistLoaderUI` で、どちらも自分の UIController の `InvokeBeforeEvent` を使う。イベント名の一覧はコードのどこにもなく、送る側と受ける側が同じ文字列を書いている。

### 4.5 UI の知らせ（E6）と、UI どうしの調整

同じプレイヤーの UI は、言語を共有する（#159）。ある UI で言語を選ぶと、`Controller.SetUILanguage` が `_OnUILanguageChanged` を本体のリスナー全員に配り（E3）、受けた各 `UIController` が訳を直してから、自分のリスナーに `AfterLanguageChanged` を送る（E6）。UI どうしは直接つながらず、`Controller` を通して調整する。

### 4.6 差し込み口（E7）

コア（`Yamadev.YamaStream.Runtime`）は、モジュールの型を参照できない（参照の向きはモジュールからコアだけ）。そこで `UIController` は、ビルドで欄に入れられた `UdonSharpBehaviour` に、`SetProgramVariable` で値を渡し、`SendCustomEvent` で名前を呼ぶ（§5.3）。

**表 11** 差し込み口

| 欄 | 渡す値 | 呼ぶ名前 | 返す値 | 入れる処理 |
| --- | --- | --- | --- | --- |
| `_urlInterceptor` | `interceptUrl`・`interceptSource` | `OnUrlSubmitted` | `interceptHandled`（`true` なら UIController は動画として再生しない） | `PlaylistLoaderBuildProcess`（URL 欄のある UI だけ） |
| `_playlistRefreshHandler` | `refreshUrl`・`refreshSource` | `OnPlaylistRefreshRequested` | 無し | `PlaylistLoaderBuildProcess`（すべての UI） |

### 4.7 窓口（E8）とモジュールの自前の配る先

モジュールの UI やタブレットは、`UIController` の public メソッドを使う。

- **ダイアログ：** `ShowMessage`・`ShowConfirm`・`HasModalDialog`・`IsModalBusy`
- **翻訳：** `GetTranslation`
- **再生：** `PlayImageUrl`
- **表示の占有：** `ProgressHeld`
- **前の確認：** `InvokeBeforeEvent`・`CancelCurrentAction`
- **登録：** `AddListener`

`SlideShowerUI` だけは、`UIController` の非公開の欄（`_currentTimeText`・`_totalDurationText`・`_progressSlider`・`_progressSliderHelper`・`_progressDrag`）を `GetProgramVariable` で直接読む。

いくつかのモジュールは、`Controller` とは別に、自分のリスナーのリストを持つ。

**表 12** モジュールが自前で持つ配る先

| モジュール | 配る名前 | 受ける側 |
| --- | --- | --- |
| `PermissionManagement` | `AfterPermissionChanged` | `PermissionManagementUI` |
| `PitchShifter` | `AfterSemitonesChanged` | `PitchShifterUI` |
| `SlideShower` | `AfterSlideModeChanged`・`AfterSlideSecondsChanged` | `SlideShowerUI` |
| `AudioLinkAdaptor` | `AfterAudioLinkStateChanged` | `AudioLinkAdaptorUI` |

---

## 5. 実行時の流れ

### 5.1 出来事が届くまで

![VRChat の動画プレーヤーの知らせを BaseVideoPlayerHandler が受け、Controller が同期の処理をしてから UIController とモジュールに配る](images/device-connections/events.svg)

**図 4** 出来事が届くまで（E2）。配る順は、登録した順（`Start` の順）である。

### 5.2 前の確認を通る操作

![ボタンで Pause を押すと、UIController が BeforeUserPauseVideo を送り、権限の UI が中止しなければ Controller の所有者になって一時停止する](images/device-connections/before.svg)

**図 5** 前の確認を通る操作（E5）。`PermissionManagementUI` は、ビルドで UIController の下に写された物で（§7）、Editor 未満の人の操作を中止し、案内を出す。

### 5.3 URL 欄の差し込み口

![URL 欄の URL を、UIController が PlaylistLoaderUI に渡す。VHub の URL ならプレイリストとして読み込み、結果を案内する。そうでなければ動画として再生する](images/device-connections/intercept.svg)

**図 6** URL 欄の差し込み口（E7）。差し込みは同期の呼び出しで、`PlaylistLoaderUI` は読み込みを始めるとその場で `interceptHandled` を返す。ダウンロードの結果は、あとから `OnLoadResult` に届く。読み込む前には、`BeforeUserLoadPlaylist` で前の確認も通す。

---

## 6. デバイスごとのつながり

### 6.1 Controller Bar・Playlist Panel

中に `UIController` が 1 つあり、`_controller` で本体を指す。モジュールの UI を差し込むパスを持たないので、権限の確認は無い（§7）。Playlist Panel は自分のダイアログ（`Modal`）を持たず、削除と更新のボタンは、制作者がダイアログを指したときだけ出る。

### 6.2 Sub Screen

中に `YamaPlayerScreen` があり、ビルドで `Controller.AddScreen` によって本体の画面の配列に足される（§3.2）。実行時に本体を呼ぶことはなく、本体が映像を書き込むだけである。持てる設定にすると、インスペクターが `VRCObjectSync` を付け外しする。

### 6.3 タブレット

![タブレットは TabletScreen で画面の状態を同期し、アプリは TabletScreen を通してタブレットの UIController を使う。KawaPlayer アプリは ScreenUI で、本体と同じ UI を持つ](images/device-connections/tablet.svg)

**図 7** タブレットの中。

- **UIController が 2 つある。** ホーム画面と設定アプリのボタンのための物と、KawaPlayer アプリの `ScreenUI`（本体のスクリーンと同じ UI）である。インスペクターは、どちらの `_controller` も書く。モジュールの UI は、パスが合う方に写される（`Canvas/Display` と `Canvas/User`）。
- **本体への命令は、タブレットの UIController を通す。**
  - 画像アプリは `_screen.UIController.PlayImageUrl` を呼ぶ。
  - 設定アプリのボタンは、タブレットの UIController のメソッドに、直接つないである。

  どちらも UIController の前の確認（E5）を通るので、権限がかかる。
- **画面の状態は自分で同期する。** `TabletScreen`（`Manual`）が、開いているアプリ・画像の URL・一覧・画像を見ているかを同期する。本体の `Controller` の同期とは別である。
- **ワールドに 1 つの物がある。** `TabletVisitorRecorder` は、ビルドで 1 つにまとめられ、シーンの一番上へ移される。`TabletReturnButton` は、`_tablets` に並べた `TabletPickup` を元の場所へ戻す。

### 6.4 全員リロードボタン

![ボタンは UIController で前の確認を通し、GlobalReload がネットワークのイベントを全員に送り、各自の GlobalReload が自分の Controller の Reload を呼ぶ](images/device-connections/global-reload.svg)

**図 8** 全員リロードボタン（ネットワークをまたぐ流れ）。

- **3 つの部品に分かれる。** ボタン（`GlobalReloadButton`）、そのボタンだけを持つ `UIController`、本体のモジュール（`GlobalReload`）である。ボタンとモジュールは、ビルドで `GlobalReloadBuildProcess` が結ぶ（§3.2）。
- **ボタンは状態を自分で調べる。** 1 秒ごとに `BeforeUserGlobalReload` を試し、押せるかと見た目を決める。
- **コアは何も知らない。** `Controller` と `UIController` には、このボタンのためのコードが無い（#248）。

---

## 7. モジュールの UI の差し込み

モジュールは、定義（`YamaPlayerModuleDefinition`）の `uiSlots` に「写す先のパス」と「写す元のテンプレート」を並べる。ビルドで `YamaPlayerModuleBuildProcess` が、同じ `Controller` を指す UIController ごとに `transform.Find(targetPath)` で探し、見つかった所に写す。

![PermissionManagement の 5 つの targetPath が、ScreenUI・タブレットの UIController・全員リロードボタンの UIController に写され、Controller Bar と Playlist Panel には合うパスがないので写されない](images/device-connections/slots.svg)

**図 9** モジュールの UI の差し込み（E12）。

**表 13** 各モジュールの `uiSlots` の写す先

| モジュール | 写す先（`targetPath`） | 当たるデバイス |
| --- | --- | --- |
| PermissionManagement | `Canvas/User`、`Canvas/User/Main/Pages/Settings/PageToggles`、`Canvas/User/Main/Pages/Settings/PageView`、`Canvas/Display`、`Canvas/Modules` | 本体とタブレットの `ScreenUI`、タブレット、全員リロードボタン |
| AudioLinkAdaptor | `Canvas/User/Main/Pages/Settings/PageView/VideoView/Viewport/Content`、`Canvas/Display/Apps/Settings/Pages/Audio/Viewport/Content` | `ScreenUI`、タブレットの設定アプリ |
| PitchShifter | `Canvas/User/Main/Pages/Settings/PageView/VideoView/Viewport/Content` | `ScreenUI` |
| SlideShower | `Canvas/User`、`Canvas/User/Main/Pages/Settings/PageView/PlaybackView/Viewport/Content`（2 つ） | `ScreenUI` |
| DefaultUrl | `Canvas/Display/Apps/Settings/Pages/Playback/Viewport/Content` | タブレットの設定アプリ（スクリーンのカードは `ScreenUI` に直接置いてある） |

- 写した物の中の参照は、写した先の物に付け替える。テンプレートの外（モジュール本体）への参照は、そのまま残る。
- 写し終えたら、テンプレートは消す。
- パスが見つからない UI は、何も言わずに飛ばす。
- 写した物は、実行時に `GetComponentInParent<UIController>()` で自分の UIController を見つけて登録する（表 5）。

---

## 8. ネットワーク

本体とデバイスのうち、同期する部品は次のとおりである。再生の状態は `Controller` が、キュー・履歴・プレイリストの枠の中身はそれぞれの部品が、別々に同期する。キューと履歴は、受け取ると自分の `OnDeserialization` で曲を組み立て直し、`Controller` のリスナーに知らせる（E3）。`UIController` とモジュールの UI は同期しない（`None`）ので、表示や前の確認は、見ている人ごとに動く。

**表 14** 同期する部品

| 部品 | 同期の方式 | 同期するもの |
| --- | --- | --- |
| `Controller` | `Manual` | 再生の状態・曲・位置と時刻・速度など（[playback-sync.md](playback-sync.md)） |
| `QueueList` | `Manual` | キューの曲（プレーヤーの種類・題・URL・拡張データ）。受け取ると曲を組み立て直し、`AfterQueueUpdated` を配る |
| `HistoryList` | `Manual` | 履歴の曲（同じ形）。受け取ると `AfterHistoryUpdated` を配る |
| `DynamicPlaylist` | `Manual` | 読み込んだプレイリストの枠の中身 |
| `TabletScreen` | `Manual` | 開いているアプリ、画像の URL と一覧、画像を見ているか |
| `TabletVisitorRecorder` | `Manual` | 訪問者の記録（ワールドに 1 つ） |
| `TabletPickup` | `NoVariableSync` | 変数は無し。位置は `VRCObjectSync`（持てる・同期を選んだとき） |
| `GlobalReload` | `Manual` | 変数は無し。ネットワークのイベント（`OnGlobalReload`）を全員に送る |
| `PermissionManagement`・`DefaultUrlController`・`OwnerDefaultUrlStorage` | `Manual` | 権限、自動再生の URL |
| `PitchShifter`・`SlideShower` | `Manual` | それぞれの設定。オーナーを `Controller` に合わせる（`IsSyncedModule`） |

出荷したプレハブで、U# の部品がすでにある物に部品を足したり外したりすると、そのワールドはビルドできなくなる（Network ID。CLAUDE.md の Key Constraints）。新しい部品は子の物に置く。この制約は、接続口を整理するときにも変わらない。

---

## 9. 観察：接続口を整理するときの論点

![部品がプレイヤーを見つける方法は、エディターで書く、親をたどる、シーン全体を探す、の 3 つがある](images/device-connections/find.svg)

**図 10** 部品がプレイヤーを見つける 3 つの方法。

ここに挙げるのは、今の作りから読み取れる論点で、直し方は決めていない。

1. **O1. プレイヤーを見つける方法が 3 通りある（図 10）。** 正の値は各部品の `_controller` だが、書くのは Sub Controller のインスペクターだけである。プレハブを更新して部品が増えても、欄を選び直すまでつながらない。ビルドは、それを手がかりに配線するだけで、Sub Controller で選んだプレイヤーと合っているかは確かめ直さない。未設定だけは、`UIController.Start`・`YamaPlayerModule.Start` がログに出し、全員リロードボタンのビルド処理が警告する。
2. **O2. 共通のインスペクターが、特定のデバイスを知っている。** `YamaPlayerSubControllerEditor` が `TabletPickup`・`TabletReturnButton`・`TabletVisitorRecorder` を直接扱う。新しいデバイスの設定を足すと、このファイルを書き換えることになる（#206 もここ）。
3. **O3. 名前の文字列の接続口に、一覧が無い。** 前の確認の 26 個、名前の出来事、差し込み口の値と名前は、送る側と受ける側が同じ文字列を書いている。打ち間違いは、コンパイルでもビルドでも見つからない。コアがモジュールの型を参照できないので、名前で呼ぶこと自体には理由がある。
4. **O4. 差し込みの場所が、UI の階層のパス。** `uiSlots` は `Canvas/User/Main/Pages/...` のような深いパスで写す先を決める。デバイスの側は、そのパスを用意しておく必要がある（全員リロードボタンの空の `Canvas/Modules`）。合わないパスは、何も言わずに飛ばされる。
5. **O5. 配る先のリストが何重にもあり、食い違いやすい。** `Controller` と `UIController` のほかに、4 つのモジュールが自前のリストを持つ（表 12）。外す口はどのリストにも無い。食い違いの例が `AudioLinkAdaptor` だった。状態の変化を `Controller` のリスナーに送っていた（E3）が、受ける `AudioLinkAdaptorUI` は `AudioLinkAdaptor` の自前のリストと `UIController` にしか登録していなかった。そのため、ある UI で AudioLink を切り替えても、ほかの UI（スクリーンとタブレットの設定アプリ）のトグルは追いつかなかった。2026-10-16 に ClientSim で確かめ、自前のリストに送るように直した（#260）。
6. **O6. 本体の UI の中を、外から直接読んでいる所がある。** `SlideShowerUI` が `UIController` の非公開の欄を読む（§4.7）。UI の作りを変えると、壊れる。
7. **O7. 本体への命令の入り口が 2 つある。** UI を通す（前の確認と権限がかかる）道と、`Controller` を直接呼ぶ道がある。デバイスが直接呼べば、権限を通らない。今のデバイスは、どれも UI を通している。
8. **O8. デバイスの「持てる・戻す・同期」が、デバイスごとの作り。** Sub Screen は `VRCPickup` と `VRCObjectSync`、タブレットは `TabletPickup` と `TabletReturnButton` と、似た機能を別々に持つ。

---

## 10. まとめ

- **3 段のつなぎ込み：** デバイスは、エディター（Sub Controller のインスペクター）・ビルド（`IYamaPlayerBuildProcess`）・実行時（`Start` での登録）の 3 段で本体につながる。
- **接続口の集まり方：** 実行時の接続口は `Controller`（E1〜E4）と `UIController`（E5〜E9）に集まる。結び方は、型、名前の文字列、欄への書き込みが混ざる。
- **デバイスの共通点：** 本体への命令は `UIController` を通し、前の確認で権限がかかる。ただし Controller Bar と Playlist Panel には権限の UI が写されないので、権限はかからない（§6.1）。モジュールの UI は、ビルドでパスに写され、実行時に自分の UIController を探して登録する。
- **同期の分担：** 再生の状態は `Controller` が、キュー・履歴・プレイリストの枠の中身はそれぞれの部品が同期する。タブレットの画面や全員リロードボタンの合図も、それぞれの部品が別に送る。
- **整理の論点：** プレイヤーの見つけ方（O1）、デバイスを知っている共通のインスペクター（O2）、名前の一覧（O3）、パスでの差し込み（O4）、何重ものリスト（O5）、UI の中への直接の読み出し（O6）、命令の入り口（O7）、持てる・戻す・同期の作り（O8）。

---

## 11. 参考文献

**コード**

1. `Runtime/Components/YamaPlayerSubController.cs`、`YamaPlayer.cs`、`YamaPlayerScreen.cs`、`YamaPlayerSpeaker.cs`
2. `Editor/Inspector/YamaPlayerSubControllerEditor.cs`（§3.1、O2）
3. `Editor/Build/YamaPlayerBuildProcess.cs`、`YamaPlayerComponentBuildProcess.cs`、`YamaPlayerPostProcess.cs`、`TabletVisitorsBuildProcess.cs`（§3.2）
4. `Editor/Module/YamaPlayerModuleBuildProcess.cs`、`Runtime/Module/YamaPlayerModule.cs`、`YamaPlayerModuleDefinition.cs`（§3.2、§7）
5. `Runtime/Internal/Controller.cs`・`Controller.Events.cs` ほかの partial、`Runtime/Internal/YamaPlayerListener.cs`（§4.1〜§4.3）
6. `Runtime/Internal/UI/UIController.cs`・`UIController.Localization.cs`・`UIController.Playlist.cs`（§4.4〜§4.7）
7. `Runtime/Internal/Tablet/`（§6.3）
8. `Modules/GlobalReload/`（§6.4）、`Modules/PlaylistLoader/`（§4.6、§5.3）、`Modules/PermissionManagement/`（§5.2）

**文書**

9. [playback-sync.md](playback-sync.md)（再生の同期の仕様）
10. [url-to-playback-pipeline.md](url-to-playback-pipeline.md)（URL から再生まで）
11. [playlist-pipeline.md](playlist-pipeline.md)（プレイリストのしくみ）
12. [../guide/tablet.md](../guide/tablet.md)、[../guide/global-reload.md](../guide/global-reload.md)（制作者向けの使い方）

**issue**

13. #108 タブレット、#153 訪問者アプリ、#181 画像アプリ、#183 タブレットの回転
14. #236・#248 全員リロードボタンとそのモジュール化
15. #82・#84・#91 URL の差し込み口とプレイリストの更新
16. #130 ダイアログが質問を上書きしないこと、#159 UI の言語の共有
17. #206 タブレットの戻す設定の案内（共通のインスペクター）
