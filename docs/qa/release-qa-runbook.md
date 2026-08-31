# リリース実機 QA ランブック (#68)

**#68 は「何を確認するか」、この文書は「どう実施し、どう記録するか」。**

#68 のチェックリストは**項目本文の唯一の出典**である。本書はそれを写さない。写すと集計が二重になり、必ず食い違う。

- **合否そのものは #68 のチェックボックスに付ける。**ゲートの数え方 (171 / ゲート対象 167) もあちらが持つ
- **本書に書くのは、チェックボックスでは残せないもの** — 誰がいつどのビルドで実施したか、項目が「記録する」ことを求めている実測値、落ちたときの詳細

---

## 開発者フェーズ — テスターに渡す前にここを全部埋める

**この節が終わるまでテスターは着手できない。**

### 1. 「固定 SHA の中身」をビルドしたことを確かめられる形にする

**HEAD が固定 SHA と一致していても、それだけではビルド内容が一致した証明にならない。**未コミットの差分はそのままアップロードに入る。

- [ ] **作業ツリーが clean であることを確認する**

  ```bash
  git rev-parse HEAD          # 固定 SHA と突き合わせる
  git status --short          # 何も出ないこと
  ```

  > **⚠ このリポジトリでは UdonSharp の `.asset` が 30 件以上 churn することがある。**放置するとビルドに入る。破棄したうえで、**破棄後は `UdonSharpCompilerV1.CompileSync()` を必ず実行する** (しないと Play 時に "Field for X does not exist" が出る)。

- [ ] **固定 SHA との差分が `docs/` だけであることを確認する**

  ```bash
  git diff --name-only <固定SHA> HEAD    # docs/ 以外が出たら固定 SHA を見直す
  ```

  > **固定 SHA は「コード候補」を指す。**文書だけの PR が入って HEAD が進むたびに固定し直すのは無駄なので、**`docs/` だけの差分は許容し、上のコマンドでそれを確認する**運用にしている。`docs/` 以外が出たなら、それはコードが変わったということなので **#68 の固定 SHA を更新し、J-1 の再実施要否も判断する** (→ `playlist-editor-verification.md`)。

**順番が重要。**marker はシーンに入れてからビルドしないと、アップロード済みのワールドには載らない。

- [ ] **run ID を決める** — 固定 SHA の先頭 7 桁 + 連番 (例: `53363c5-run1`)。**同じ SHA を撮り直したときに区別できる**ようにするため
- [ ] **build marker をシーンへ置く** — **run ID** を書いたテキストを、**入室してすぐ見える場所**に出す
  - **アップロード日時は marker に入れない。**ビルド前には確定しない。日時は記録シートへ書く
- [ ] **marker を含めて**ビルド・アップロードする
- [ ] **実際のアップロード日時**を控える
- [ ] **アップロード完了後に新しいインスタンスを作る**
- [ ] 入室して、**ワールド内の marker が run ID と一致する**ことを自分の目で確認する

- [ ] 記録用に控える (記録シートと不具合報告の両方で使う)
  - **run ID**
  - **アップロード日時**
  - **固定 SHA**
  - **blueprint ID** — シーンの `VRCWorld` にある `PipelineManager` の `blueprintId` (`wrld_` で始まる)。SDK のビルドパネルにも出る

> **⚠ blueprint ID ではビルドを区別できない。**blueprint ID は**そのワールドの ID**で、アップロードのたびに変わるものではない。旧ビルドと今回の候補は**同じ `wrld_...`** になる。プレイヤー内の情報ページも「KawaPlayer v1.2.0」固定なので、**画面からビルドを見分ける手段が標準では無い**。だから **run ID の marker を自分で置く**。**blueprint ID はワールドの取り違えを防ぐためのもので、ビルドの新旧は判別しない。**
>
> **⚠ 更新前から動いているインスタンスは古いワールドのまま続く。**アップロードしても、既存インスタンスがその場で入れ替わるわけではない。**必ずアップロード完了後に新しく作ったインスタンス**で検証すること。

### 2. VHub プレイリストを用意する

**1 プレイリスト 100 曲が VHub 側の上限。**これを超える「超過用」プレイリストは作れないので、代わりに fixture を使う (下記)。

作り方: playlist.vrc-hub.com でプレイリストを作り、**共有用 URL** (`/r/default/{id}`) をコピーする。ブラウザのアドレスバーに出る `/playlists/{id}` とは**別物**で、そちらは G の「案内が出ること」の確認に使う。

- [ ] **プレイリスト①** — 通常のもの
- [ ] **プレイリスト②** — ①と別のもの
- [ ] **プレイリスト混合** — **mode 0 / 1 / 2 を全部含む** (Unity 用動画・AVPro 用動画・画像)
- [ ] **プレイリスト混在** — **YouTube の曲 / 非 YouTube の曲 / `provider` なしの曲**が区別できるように混ぜたもの
- [ ] **プレイリスト長名** — 極端に長い名前
- [ ] **プレイリスト大** — **100 曲** (上限いっぱい)
- [ ] **プレイリスト使い捨て** — I-3 の更新失敗用。消してよいもの
- [ ] H-4 の LRU 上書き確認用に、**異なる共有 URL が 6 本以上**あること (動的スロットが 5 個のため)

### 3. 検証データ表を埋める

**⛔ ここが空だと E / A / H-4 / I-3 / J-2 に着手できない。**

`provider` や `mode` は**画面から判別できない**。**どの曲がどの条件なのかを表で教える**必要がある。#68 の「検証データ表」に直接書き込むこと。

**公開リポジトリなので、URL を Issue に書いてよいかは判断してから。**共有したくなければ、表には呼び名だけを残し、実 URL は別の場所 (共有ドキュメント等) に置いて**そのリンクだけ**を書く。

### 4. テストワールドに構成を並べる

#68 の「テストワールドに用意しておく構成」の表どおりに配置する。

- [ ] **それぞれの前に看板を立てて、表の「置くもの」の名前をそのまま書く**

> 「通常」「Modal なし」「PlaylistLoader なし」「Permission なし」は**入っただけでは見分けられない**。看板が無いとテスターはどれを触っているのか判断できず、結果が信用できなくなる。

### 5. hosts 遮断を一度試しておく

- [ ] #68 の「VHub だけを遮断する手順」を**本番前に一度通しで試す**
- [ ] 遮断中に **VRChat 本体・YouTube・直接動画 URL は通常どおり動く**ことを確認する (= VHub だけが落ちている)

### 6. fixture を用意する

#### 6-a. partial 経路 (H-4 用) — 上限を絞ったテスト構成

**VHub は 100 曲上限なので、既定値 (`_maxTracks` = 200) のままでは超過を起こせない。**

- [ ] テスト専用の KawaPlayer を 1 台置き、`PlaylistLoader` のインスペクタで次のどちらかを小さくする
  - `Max Tracks` — `Range(1, 500)` 既定 200
  - `Max Sync Bytes` — `Range(4096, 65536)` 既定 32768
- [ ] 通常のプレイリスト (例: プレイリスト①) を読み込み、**モーダルに「{N} 曲を追加しました ({K} 曲はスキップ)。」が出る**ことを確認する
- [ ] 看板に**絞った値**を書いておく (例: 「Max Tracks = 5」)

#### 6-b. yt-dlp の部分成功 (J-1b の 3 番目 — Unity 上で実施) — スタブ実行ファイル

**公開プレイリストではこの経路を出せない。**`--flat-playlist` の `n_entries` は YouTube 側が既に除外したあとの件数を返すので、announce と出力行数が常に一致し、exit code も 0 になる。**yt-dlp を差し替えるのが唯一確実な方法。**

`YtdlpResolver` の判定はこうなっている。

```csharp
result.Success   = result.JsonLines.Count > 0;
result.IsPartial = result.Success
  && (result.ExitCode != 0
      || (result.ExpectedCount.HasValue && result.JsonLines.Count < result.ExpectedCount.Value));
```

つまり **「announce より少なく出す」か「非 0 で終了する」**のどちらかを作ればよい。次のスタブは環境変数で両方を切り替えられる。

**`ytstub.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>disable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <AssemblyName>yt-dlp</AssemblyName>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>
</Project>
```

**`Program.cs`**

```csharp
using System;
using System.Text;

internal static class Program
{
  private static int Main(string[] args)
  {
    Console.OutputEncoding = new UTF8Encoding(false);

    int emitted   = ReadInt("YTSTUB_EMITTED",  3);   // 実際に出す行数
    int announced = ReadInt("YTSTUB_NENTRIES", 5);   // n_entries として名乗る件数
    int exitCode  = ReadInt("YTSTUB_EXIT",     0);   // 終了コード

    Console.Error.WriteLine("[ytstub] argv: " + string.Join(" ", args));
    Console.Error.WriteLine(
      "[ytstub] emitting " + emitted + " of " + announced + " entries, exiting " + exitCode);

    for (int i = 0; i < emitted; i++)
    {
      Console.WriteLine(
        "{\"title\":\"ytstub track " + (i + 1) + "\"," +
        "\"url\":\"https://example.com/ytstub/" + (i + 1) + "\"," +
        "\"playlist\":\"YTSTUB PLAYLIST\"," +
        "\"duration\":" + (i + 1) + "," +
        "\"n_entries\":" + announced + "}");
    }
    return exitCode;
  }

  private static int ReadInt(string name, int fallback)
  {
    string raw = Environment.GetEnvironmentVariable(name);
    int value;
    return int.TryParse(raw, out value) ? value : fallback;
  }
}
```

**差し替えと復旧 (PowerShell)**

**本物を戻せなくなるのが一番怖い**ので、`try` / `finally` で囲み、中断しても必ず復旧するようにする。`%TEMP%` を使わず **`YtdlpResolver` と同じ `[System.IO.Path]::GetTempPath()` から組み立てる** (両者が一致しない環境がある)。

```powershell
dotnet --version                     # net8.0 をターゲットにできる SDK が要る (9.0.312 で確認済み)
dotnet build -c Release              # bin/Release/net8.0/ に yt-dlp.exe ほかが出る

$temp   = [System.IO.Path]::GetTempPath()
$real   = Join-Path $temp 'yt-dlp.exe'
$backup = Join-Path $temp 'yt-dlp.real.exe'
"target : $real"
"backup : $backup"

# 前回の復旧に失敗している状態で上書きしないための門番
if (Test-Path $backup) { throw "前回のバックアップが残っている。先に復旧すること: $backup" }
if (-not (Test-Path $real)) { throw "yt-dlp が無い。Playlist Editor でダウンロードしてから実行すること" }

$before = (Get-FileHash $real -Algorithm MD5).Hash
"original md5 : $before"

$stub = 'bin/Release/net8.0'
$files = 'yt-dlp.exe','yt-dlp.dll','yt-dlp.runtimeconfig.json','yt-dlp.deps.json'

# スタブ 4 ファイルが揃っているか (足りないと差し替え途中で止まる)
foreach ($f in $files) {
  if (-not (Test-Path (Join-Path $stub $f))) { throw "スタブが足りない: $f" }
}
# TEMP に前回の残骸が無いか。あると finally が「元からあったファイル」を消しかねない
foreach ($f in $files | Where-Object { $_ -ne 'yt-dlp.exe' }) {
  if (Test-Path (Join-Path $temp $f)) { throw "TEMP に残骸がある。先に片付けること: $f" }
}

try {
  Move-Item $real $backup
  foreach ($f in $files) { Copy-Item (Join-Path $stub $f) (Join-Path $temp $f) }
  Read-Host "Unity 側の確認が終わったら Enter (Ctrl+C で中断しても復旧します)"
}
finally {
  foreach ($f in $files) { Remove-Item (Join-Path $temp $f) -ErrorAction SilentlyContinue }
  if (Test-Path $backup) { Move-Item $backup $real }
  $after = (Get-FileHash $real -Algorithm MD5).Hash
  if ($after -ne $before) { throw "復旧に失敗: $after != $before" }
  "restored ok : $after"
}
```

> **framework-dependent ビルドなので `.dll` / `.runtimeconfig.json` / `.deps.json` も一緒に置くこと。**`.exe` だけでは起動しない。

**終了コード側の分岐**を出したいときは、Unity のプロセスに環境変数を入れてから実行する (子プロセスが継承する)。**確認が終わったら必ず消す** — 残すと以降の取得がすべて partial になる。

```csharp
// 設定
System.Environment.SetEnvironmentVariable("YTSTUB_EMITTED",  "5");
System.Environment.SetEnvironmentVariable("YTSTUB_NENTRIES", "5");   // 不足なし
System.Environment.SetEnvironmentVariable("YTSTUB_EXIT",     "1");   // 非 0 終了だけで partial

// 後始末 (忘れない)
foreach (var k in new[] { "YTSTUB_EMITTED", "YTSTUB_NENTRIES", "YTSTUB_EXIT" })
  System.Environment.SetEnvironmentVariable(k, null);
```

- [ ] **復旧を必ず確認する。**ハッシュが一致することに加えて、**実際のプレイリスト ID で正常に取得できる**ところまで見ること

#### 6-c. ドラッグ&ドロップ取り込み (J-1 の 9 番目 — Unity 上で実施) — スタブコンポーネント

`PlaylistImporter.ImportPlaylists` は **USharpVideo / IwaSync3 / VizVid / Kinel / ProTV** のコンポーネントを持つ GameObject しか受け付けない。**1 つも入っていない環境では実施できない。**

対応パッケージを入れてもよいが、`ImportFromScript` は**型名で振り分けて `tracks` と `playlistUrl` をリフレクションで読むだけ**なので、**同じ名前と同じ 2 フィールドを持つコンポーネント**があれば実経路をそのまま動かせる。

**テストプロジェクト側**に置く (**KawaPlayer には入れない**)。

```csharp
namespace HoshinoLabs.IwaSync3
{
  [System.Serializable]
  public class Track
  {
    public int mode;
    public string title;
    public string url;
  }

  public class Playlist : UnityEngine.MonoBehaviour
  {
    public Track[] tracks;
    public string playlistUrl;
  }
}
```

> **確認が終わったら消すこと。**本物のパッケージが持つ型名を占有するので、後から IwaSync3 を入れると衝突する。

ドロップの手つきは `EditorWindow.SendEvent` で作れる。

まず fixture の GameObject を作る (`tracks` は `SerializedObject` で埋める)。

```csharp
var go = new UnityEngine.GameObject("QA IwaSync3 Fixture");
go.AddComponent(System.Type.GetType("HoshinoLabs.IwaSync3.Playlist, Assembly-CSharp"));
// so.FindProperty("tracks") を arraySize = 2 にして mode / title / url を入れる
```

そのうえでドロップの手つきを送る。**`HandleDragEvent` の rect 判定と `DragAndDrop.AcceptDrag()` を含めて本物の経路が動く。**

```csharp
var flags = System.Reflection.BindingFlags.NonPublic
          | System.Reflection.BindingFlags.Instance
          | System.Reflection.BindingFlags.Public;
var window = UnityEngine.Resources
  .FindObjectsOfTypeAll<Yamadev.YamaStream.Editor.PlaylistEditorWindow>()[0];
var fixtureGameObject = UnityEngine.GameObject.Find("QA IwaSync3 Fixture");

UnityEditor.DragAndDrop.PrepareStartDrag();
UnityEditor.DragAndDrop.objectReferences = new UnityEngine.Object[] { fixtureGameObject };

// SendEvent は internal なのでリフレクション経由
var send = window.GetType().GetMethod(
  "SendEvent", flags, null, new[] { typeof(UnityEngine.Event) }, null);
send.Invoke(window, new object[] { new UnityEngine.Event {
  type = UnityEngine.EventType.DragUpdated,
  mousePosition = new UnityEngine.Vector2(120f, 260f) } });   // 左カラムの内側
send.Invoke(window, new object[] { new UnityEngine.Event {
  type = UnityEngine.EventType.DragPerform,
  mousePosition = new UnityEngine.Vector2(120f, 260f) } });
```

- [ ] 取り込まれたプレイリストの **mode / title / url が fixture と一致する**ことを確認する
- [ ] **fixture のスクリプトと GameObject を消す** (シーンは保存せず読み直せば GameObject は消える)

---

## テスターフェーズ — セッション 3 本

**J-1 は Unity 側で完了済み** (**ゲート対象 18 項目すべて合格**。残る 1 項目は macOS / Linux での確認で、任意・ゲート外)。実機で消化するのは **152 項目**。

| セッション | 体制 | セクション | 件数 |
| --- | --- | --- | --- |
| **S1** | **1 人 (A)** | E → F → G → H-1 → I-1 → I-2 | **58** |
| **S2** | **2 人 (A + B)** | A → B → C | **26** |
| **S3** | **2 人 + 途中参加 (C)** | I-3〜I-7 → H-2〜H-7 → D → J-2〜J-4 | **68** |

### 各セッションの開始前に

- [ ] **記録シートのヘッダを埋める** (下記)
- [ ] **正しいビルドか確認する** — ワールド内の **build marker が run ID と一致する**こと。**blueprint ID の一致だけでは足りない** (旧ビルドでも同じ ID になる)
- [ ] **アップロード後に作られたインスタンス**であることを確認する (既存インスタンスは古いワールドのまま続く)
- [ ] **#68 の「標準リセット」を実行する**
- [ ] 手元に **#68 の検証データ表**を開いておく (呼び名 → 実 URL / 曲名の対応表)

### 【開発者】の同席が要る 5 項目

**先に段取りを決めておかないとセッションが止まる。**

| 項目 | セッション | 開発者が何をするか |
| --- | --- | --- |
| **A-2** | S2 | fallback handler を設定し、`PlayerError` を作り分ける |
| **A (B 側で復元されない)** | S2 | **B 単独の新規インスタンス**を用意する |
| **H-4** | S3 | VRChat の出力ログから `success` / `byteCount` を読む |
| **I-3** | S3 | VHub 側でプレイリストを消す / hosts で遮断する |
| **J-2** | S3 | hosts で遮断・復旧する |

### 中断と再開

- **セクションの途中で止めない。**セクション単位で区切る
- 再開するときは**必ず標準リセットからやり直す**。前のセクションの状態が残ると結果が変わる
- 記録シートに**どこまで終わったか**を書いてから閉じる

---

## 記録シート (コピーして使う)

### ヘッダ

```text
実施日            :
セッション        : S1 / S2 / S3
開始時刻          :        終了時刻 :

--- ビルドの同一性 (開発者が埋める) ---
blueprint ID      : wrld_
アップロード日時  :
固定 SHA          :
build marker      : (ワールド内の表示と一致したか  はい / いいえ)
インスタンス      : アップロード後に新規作成したものか  はい / いいえ

--- クライアント (使った分だけ) ---
      | 名前 | 端末           | VR / Desktop | VRChat 版 |
  A   |      |                |              |           |
  B   |      |                |              |           |
  C   |      |                |              |           |
```

> **クライアントごとに VR / Desktop を残すこと。**F はタブ操作を VR とデスクトップの両方で見るなど、**モードによって結果が変わる項目がある**。「PC / Quest」だけでは足りない。

### 実測値の記録 (項目が「記録する」ことを求めているもの)

**#68 のチェックボックスでは残せない。ここに書く。**

```text
[E] YouTube 障害が届く VideoError 種別  :
[E] 情報ページのプレイヤーバージョン    :
[E] 使用中の動画プレイヤー              :
[D-2] プレイリスト大が全クライアントへ行き渡るまでの秒数 :
[H-4] 出力ログの success                :
[H-4] 出力ログの byteCount              :
[F]  タブ切替で気づいた違和感 (空欄なら合格扱い) :
```

### 不合格・気づいた点

**合格はここに書かない (#68 のチェックボックスで足りる)。**

| 項目 | 症状 (期待 → 実際) | ログ | 起票先 |
| --- | --- | --- | --- |
| 例: E-3 | 案内行が出るはず → 出ない | 有 | #68 コメント |
|  |  |  |  |
|  |  |  |  |

### セッション終了時

```text
消化した項目数 :        / 未実施 :
次に再開する場所 :
```

---

## 不具合が出たときの報告手順

### 1. その場で押さえる

- **どの項目か** (セクション + 上から何番目か。例: 「I-3 の 4 番目」)
- **期待と実際**
- **再現するか** — 標準リセット後にもう一度やって同じになるか
- **誰の画面で起きたか** (A / B / C)

### 2. VRChat の出力ログを取る

```text
%USERPROFILE%\AppData\LocalLow\VRChat\VRChat\output_log_*.txt
```

- **最新のファイル**を開き、**発生時刻の前後**を貼る
- `[KawaPlayer]` / `[PlaylistLoader]` / `Udon` を含む行は特に残す
- **全文は貼らない。**抜粋にも次が混ざるので、貼る前に消す

| 消すもの | 見た目 |
| --- | --- |
| **インスタンス ID / 招待 URL** | `wrld_...:12345~private(usr_...)` / `vrchat://launch?...` |
| **ユーザー ID・表示名** | `usr_xxxxxxxx-...` / ログイン名・フレンド名 |
| **認証情報** | `authToken` / `Cookie` / `apiKey` を含む行 |
| **非公開のプレイリスト URL** | 公開していない `/r/default/{id}` や `/playlists/{id}` |

### 3. どこに書くか

| 状況 | 起票先 |
| --- | --- |
| **#68 の項目が期待どおりでない** | **#68 のコメント。**セクション + 番号を先頭に書く |
| **#68 と無関係な不具合を見つけた** | **新規 Issue。**#68 からリンクする |
| **手順や期待結果のほうが間違っている** | **#68 のコメント**で指摘する。**本文は直接書き換えない** (確定した計画だけを本文に残す運用のため) |

### 4. 最低限添えるもの

```text
固定 SHA       :
run ID         : (ワールド内の build marker と同じもの)
アップロード日時 :
blueprint ID   :
新規インスタンス : アップロード後に作ったものか  はい / いいえ
クライアント   : A / B / C のどれで起きたか  +  VR / Desktop  +  VRChat 版
項目           :
手順           : 1. ... 2. ...
期待           :
実際           :
再現性         : 毎回 / たまに / 1 回だけ
ログ           : (貼るか、無いと書く)
```

> **記録シートのヘッダと同じ識別情報を持たせること。**どのビルドで起きたのか分からない報告は、追いかけようがない。

---

## 関連

- **#68** — チェックリスト本体。**項目本文と合否はあちらが持つ**
- `docs/qa/playlist-editor-verification.md` — J-1 (Editor 側) の実施結果と、Editor を自動操作する手順
- `docs/design/url-pool-playlist-loader.md` — 取り込んだ URL がリダイレクトスロットのままである理由
