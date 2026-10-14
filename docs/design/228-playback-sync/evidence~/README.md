# 確認の記録（ログの抜粋と、採取に使ったスクリプト）

[比較の文書](../comparison.md) の付録 D（Unity）と付録 E（VRChat）のもとになった記録です。結果を確かめ直したり、同じ手順で試し直したりするときに使います。フォルダーの名前が `~` で終わるのは、テスト用のプロジェクトがこのリポジトリを読み込んでも、Unity がこのフォルダーを取り込まないようにするためです（スクリプトの拡張子を `.txt` にしたのも同じ理由です）。

## clientsim：Unity（ClientSim、1 クライアント）

| ファイル | 中身 |
| --- | --- |
| `vvmw-v1.txt`・`vvmw-v2.txt`・`vvmw-v3.txt` | 付録 D の表 D2 の V1〜V3。値（位置を除く）が変わった行と、位置が飛んだ行だけを残した |
| `vv_probe.cs.txt` | VizVid（VVMW）を操作し、値を記録したエディターのスクリプト |
| `sy_probe.cs.txt` | KawaPlayer を操作し、値を読んだエディターのスクリプト（表 D1） |

- **動かし方：** どちらも Unity MCP の `execute_code` で、Play の中で実行した。先頭の `ACTION` と `ARG` を置き換えて使う。
- **採取の間隔：** `vv_probe` の `sample-start` は、エディターの更新（`EditorApplication.update`）のたびに値を読み、位置以外の値が変わったときに 1 行を足す。抜粋では、さらに位置が飛んだ行を残した。
- **列：** 先頭の数字は `Time.realtimeSinceStartup`（秒）。`state` は共有の状態（0 止まっている、1 読み込み中、2 再生、3 一時停止）、`time` は開始の時刻（ミリ秒）、`loading` と `localReload` は読み込み中と読み込み直しの印、`req` は予約の読み込みがあるか、`pos` はプレイヤーの位置。`BBB` は試した動画（Big Buck Bunny、archive.org）の URL である。
- **表 D1（KawaPlayer）：** 値はその場で読んで表に書いたもので、ファイルには残していない。

## vrchat：VRChat（Build & Test、同じ PC の 2〜3 クライアント）

| ファイル | 中身 |
| --- | --- |
| `SyncTestHarness.cs.txt` | 試すために、一時的なシーンにだけ置いた U# のスクリプト（2 回目のもの） |
| `run1-c1.txt`〜`run1-c3.txt` | 1 回目。VizVid の V2・V3、KawaPlayer の P3、P7 の 1 回目（付録 E の E1〜E4・E6） |
| `run2-c1.txt`〜`run2-c3.txt` | 2 回目。P7 の再現と、時計の比べ合わせ（付録 E の E5・E6） |
| `run1-c2-network-datetime.txt` | 1 回目の 2 つ目のクライアントの、VRChat 自身のログの 1 行（日時の同期の失敗） |

- **採取の間隔：** スクリプトは毎フレーム値を読み、共有の値が変わったとき（先頭が `K`・`V`）、位置が前のフレームから 0.5 秒を超えて飛んだとき（`KJUMP`・`VJUMP`）、操作したとき（`ACT`）に 1 行を書く。何も起きなければ 1 秒ごとに 1 行（`BEAT`）を書く。抜粋では、`BEAT` を 5 行に 1 行だけ残した。クライアントを閉じるときに VRChat のログで途中までしか書かれなかった最後の数行は、除いた。
- **列：** `role` は入った順（0 が最初）、`srv` は `Networking.GetServerTimeInSeconds()`（全員に共通で、クライアントをまたいで比べるのに使う）、`T` は操作の時刻表の起点からの秒。`K` が KawaPlayer（`own` オーナーか、`ss` 共有の状態、`st` 同期する位置、`tk` その時刻、`rl` 読み込み直しの印、`ld` 読み込み中、`kpos` 位置）、`V` が VizVid（表 D2 と同じ）。
- **2 回目だけの列：** `nd` は `Networking.GetNetworkDateTime()` と手元の時計（`DateTime.UtcNow`）の差（ミリ秒）、`sl` は `GetServerTimeInSeconds()` と手元の時計の差（秒）。3 つのクライアントは同じ PC で動くので、手元の時計を共通のものさしにできる。
- **1 回目のスクリプト：** 2 回目と同じ作りで、`nd`・`sl` の列がない。2 人がそろった時刻を起点に、最初のクライアントが 15 秒で VizVid を再生、45 秒で `LocalSync`、75・76.5・78 秒で `LocalSync`、110 秒で止め、115 秒で KawaPlayer を再生、145 秒で読み込み直し、175・176.5・178 秒で読み込み直しを押した。2 つ目のクライアントは 215 秒で `ErrorRetry` を呼んだが、再生中の `ErrorRetry` は何もしない作りなので、2 回目では先に自分のプレイヤーを一時停止するようにした。
- **個人の情報：** 抜粋は、スクリプトが書いた行だけで、表示名やアカウントの ID は含まない。
