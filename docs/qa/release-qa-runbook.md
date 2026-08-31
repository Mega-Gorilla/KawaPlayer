# リリース実機 QA ランブック (#68)

**#68 は「何を確認するか」、この文書は「どう実施し、どう記録するか」。**

#68 のチェックリストは**項目本文の唯一の出典**である。本書はそれを写さない。写すと集計が二重になり、必ず食い違う。

- **合否そのものは #68 のチェックボックスに付ける。**ゲートの数え方 (171 / ゲート対象 167) もあちらが持つ
- **本書に書くのは、チェックボックスでは残せないもの** — 誰がいつどのビルドで実施したか、項目が「記録する」ことを求めている実測値、落ちたときの詳細

---

## 開発者フェーズ — テスターに渡す前にここを全部埋める

**この節が終わるまでテスターは着手できない。**

### 1. 固定 SHA を確認してビルドする

- [ ] #68 の「検証対象 (固定 SHA)」を見て、**その SHA が現在の `develop` HEAD と一致している**ことを確認する
- [ ] 一致していなければ、**ビルドを始める前に #68 を更新する** (SHA が動いたら J-1 の再実施要否も判断すること → `playlist-editor-verification.md`)
- [ ] ワールドをビルド・アップロードする
- [ ] **blueprint ID を控える** — シーンの `VRCWorld` にある `PipelineManager` の `blueprintId` (`wrld_` で始まる)。SDK のビルドパネルにも出る

> **⚠ 実機からビルドを区別できない。**プレイヤー内の情報ページは「KawaPlayer v1.2.0」固定で、**どのビルドかは画面から分からない**。だから blueprint ID とアップロード日時を**開発者がここで控えて記録シートの先頭に書く**。テスターは「正しいワールドに入っているか」をその ID で確認する。

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

**差し替えと復旧** (`%TEMP%` = `YtdlpResolver.ExecutablePath` の置き場所)

```bash
dotnet build -c Release          # bin/Release/net8.0/ に yt-dlp.exe ほかが出る

# 差し替え (本物を退避してから、4 ファイルすべてを置く)
mv  "$TEMP/yt-dlp.exe" "$TEMP/yt-dlp.real.exe"
cp bin/Release/net8.0/yt-dlp.{exe,dll,runtimeconfig.json,deps.json} "$TEMP/"

# ... Unity で確認 ...

# 復旧
rm  "$TEMP"/yt-dlp.{exe,dll,runtimeconfig.json,deps.json}
mv  "$TEMP/yt-dlp.real.exe" "$TEMP/yt-dlp.exe"
```

> **framework-dependent ビルドなので `.dll` / `.runtimeconfig.json` / `.deps.json` も一緒に置くこと。**`.exe` だけでは起動しない。

**終了コード側の分岐**を出したいときは、Unity のプロセスに環境変数を入れてから実行する (子プロセスが継承する)。

```csharp
System.Environment.SetEnvironmentVariable("YTSTUB_EMITTED",  "5");
System.Environment.SetEnvironmentVariable("YTSTUB_NENTRIES", "5");   // 不足なし
System.Environment.SetEnvironmentVariable("YTSTUB_EXIT",     "1");   // 非 0 終了だけで partial
```

- [ ] **復旧を必ず確認する。**戻したあと、実際のプレイリスト ID で正常に取得できるところまで見ること

#### 6-c. ドラッグ&ドロップ取り込み (J-1 の 9 番目)

- [ ] **USharpVideo / IwaSync3 / VizVid / Kinel のいずれか**のプレイリストを持つ Prefab を 1 つ、テストプロジェクトに用意する

> `PlaylistImporter.ImportPlaylists` はこれらのコンポーネントを持つ GameObject しか受け付けない。**1 つも入っていないと、誰が実施しても不可能。**

---

## テスターフェーズ — セッション 3 本

**J-1 の 19 項目は Unity 側で完了済み**なので、実機で消化するのは **152 項目**。

| セッション | 体制 | セクション | 件数 |
| --- | --- | --- | --- |
| **S1** | **1 人 (A)** | E → F → G → H-1 → I-1 → I-2 | **58** |
| **S2** | **2 人 (A + B)** | A → B → C | **26** |
| **S3** | **2 人 + 途中参加 (C)** | I-3〜I-7 → H-2〜H-7 → D → J-2〜J-4 | **68** |

### 各セッションの開始前に

- [ ] **記録シートのヘッダを埋める** (下記)
- [ ] **正しいワールドか確認する** — blueprint ID が開発者の控えと一致すること
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
blueprint ID      : wrld_
アップロード日時  :
固定 SHA          :
VRChat クライアント版 :
プラットフォーム  : PC / Quest
実施者            : A =        B =        C =
開始時刻          :        終了時刻 :
```

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
- **全文は貼らない** (個人情報が混ざる)

### 3. どこに書くか

| 状況 | 起票先 |
| --- | --- |
| **#68 の項目が期待どおりでない** | **#68 のコメント。**セクション + 番号を先頭に書く |
| **#68 と無関係な不具合を見つけた** | **新規 Issue。**#68 からリンクする |
| **手順や期待結果のほうが間違っている** | **#68 のコメント**で指摘する。**本文は直接書き換えない** (確定した計画だけを本文に残す運用のため) |

### 4. 最低限添えるもの

```text
固定 SHA     :
blueprint ID :
項目         :
手順         : 1. ... 2. ...
期待         :
実際         :
再現性       : 毎回 / たまに / 1 回だけ
ログ         : (貼るか、無いと書く)
```

---

## 関連

- **#68** — チェックリスト本体。**項目本文と合否はあちらが持つ**
- `docs/qa/playlist-editor-verification.md` — J-1 (Editor 側) の実施結果と、Editor を自動操作する手順
- `docs/design/url-pool-playlist-loader.md` — 取り込んだ URL がリダイレクトスロットのままである理由
