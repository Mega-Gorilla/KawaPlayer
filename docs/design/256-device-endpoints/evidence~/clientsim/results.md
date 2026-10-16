# ClientSim での確認（2026-10-16）

VizVid（VVMW、配布の 1.8.2。接続のしくみに関わるファイルは GitHub の `834bd5e` と同じ）と vrcw-foundation 0.0.68（`2aa8623`）が入ったテスト用プロジェクト（Unity 2022.3、VRChat Worlds SDK 3.10.4、ClientSim）で、一時的なシーンに `VVMW (Separated Controls).prefab` だけを置いて確かめた。Room N は変えていない。

スクリプトは、テスト用プロジェクトの Unity で `execute_code` として実行した（`runcs.py`）。順番は次のとおり。

1. `ep_scene.cs`：`Assets/__EpTest/EpTest.unity` を作り、`VVMW (Separated Controls).prefab` を置く。
2. `ep_desc.cs`・`ep_spawn.cs`：ClientSim が始まるように、`VRCSceneDescriptor` と出現位置を足す（出現位置が無いと Play が止まった）。
3. `ep_read.cs`（エディター）：前処理の前の値を読む。
4. Play に入る（ビルドと同じ前処理 `BuildPreprocessorManager` が動く）。
5. `ep_read.cs`・`ep_named.cs`（Play）：前処理のあとの値と、名前付きの配り先を読む。
6. `ep_lock.cs`（`lock` と `unlock`）：外の権限の仕組みが送るのと同じ `_Lock`・`_OnUnlock` を送り、`ep_read.cs` で UI を読む。
7. Play を止め、`ep_cleanup.cs` で Room N に戻り、`Assets/__EpTest` を消す。

## 結果

### エディター（前処理の前）

```
Core.targets=0[]
Core.namedTargets=0 names:
FrontendHandler.targets=0[]
FrontendHandler.core=Core@__VVMWTest locked=False
UIHandler@Default UI: handler=FrontendHandler@Playlist Queue Handler core=null urlInput.interactable=True playButton=True/True
```

### Play（前処理のあと）

```
Core.targets=0[]
Core.namedTargets=15 names: _OnRangeLoopToggled,_OnScreenSharedPropertiesChanged,_OnSpeedChange,_OnSyncOffsetChange,_OnTitleData,_OnVideoBeginLoad,_OnVideoError,_OnVolumeChange,_onVideoReady,_onVideoStart,OnVideoPlay,OnVideoPause,_onVideoEnd,_OnPerformerChange,_OnRangeLoopChange
FrontendHandler.targets=0[]
FrontendHandler.core=Core@__VVMWTest locked=False
UIHandler@Default UI: handler=FrontendHandler@Playlist Queue Handler core=Core@__VVMWTest urlInput.interactable=True playButton=False/True
```

`ep_named.cs`：

```
Core: targets=0 named=15
  _OnRangeLoopToggled -> FrontendHandler
  _OnScreenSharedPropertiesChanged -> FrontendHandler+UIHandler
  _OnSpeedChange -> FrontendHandler+UIHandler
  _OnSyncOffsetChange -> FrontendHandler+UIHandler
  _OnTitleData -> FrontendHandler
  _OnVideoBeginLoad -> FrontendHandler
  _OnVideoError -> FrontendHandler
  _OnVolumeChange -> FrontendHandler+UIHandler
  _onVideoReady -> FrontendHandler+UIHandler
  _onVideoStart -> FrontendHandler+UIHandler
  OnVideoPlay -> FrontendHandler+UIHandler
  OnVideoPause -> FrontendHandler+UIHandler
  _onVideoEnd -> FrontendHandler+UIHandler
  _OnPerformerChange -> UIHandler
  _OnRangeLoopChange -> UIHandler
FrontendHandler: targets=0 named=1
  _OnUIUpdate -> UIHandler
```

### ロック（Play）

```
lock: locked=True
UIHandler@Default UI: ... urlInput.interactable=False playButton=False/True
unlock: locked=False
UIHandler@Default UI: ... urlInput.interactable=True playButton=False/True
```

## 読み取れること

- **配り先：** エディターでは空で、Play の前処理で、名前ごとに埋まる。`targets`（全部のイベントを受ける配り先）は空のままで、どの部品も、受けるイベントを名前で絞っている。
- **UI の `core`：** エディターでは空で、前処理で `handler.core` からたどって埋まる（`[Resolve]`）。
- **ロック：** 外から `_Lock`・`_OnUnlock` を送るだけで、UI の URL 欄が押せなくなり、外すと戻る。

## 片付け

`Assets/__EpTest` を消し、Room N（未保存の変更なし）に戻した。最初の Play は、出現位置が無くて ClientSim が止めた。そのとき、ローカルのプレイヤーがいないまま動いた Udon が例外を出した（`LanguageManager.cs`・`Core_Persistence.cs`・`UIHandler_OmniInput.cs`）。出現位置を足したあとの Play で、上の値を読んだ。
