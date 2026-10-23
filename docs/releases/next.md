**KawaPlayer X.Y.Z** — YYYY-MM-DD

置き忘れたタブレットに、門限ができました（設定すれば、ですが）。

1.3.2 のすぐあとの、小さな改善の版です。曲の一覧の色が配色になじむようになったほかは、Unity のインスペクターとビルドのログの改善です。

## 更新する前に、これだけは読んでね

* **ワールドを作り直すと、曲の一覧の曲名の色が配色に合います**（下の「直したこと」）。作り直さなければ、1.3.2 のときのまま動きます。同期する欄は変わっていないので、PC と Quest で 1.3.2 と混ざっても、そのまま一緒に遊べます。
* **VRChat SDK を 3.10.5 に上げる人へ（KawaPlayer の変更ではありません）：** SDK のサンプルの **VRCWorld** を置いたワールドは、3.10.5 に上げると、アップロードの前のビルドが `AssignSceneNetworkIDs`（`Failed to assign network IDs`）で止まることがあります。
  * 3.10.5 で、VRCWorld に UdonBehaviour（WorldQualitySettings）が 1 つ増えたためです。シーンに記録されたネットワーク ID の部品の数と、合わなくなります。
  * **VRChat SDK > Utilities > Network ID Import and Export Utility** で、VRCWorld の記録を今の部品に合わせると直ります。ID は変えなくて大丈夫です。
  * KawaPlayer と一緒に SDK を上げた人が多そうなので、念のため書いておきます。

## 変わったこと

* **タブレットのインスペクターの「放置で元の位置に戻す (秒)」が、チェックと分の欄になりました。** 「0 のときは無効」という、知る人ぞ知る決まりはおしまいです。（#289）
  * **放置したら元の位置に戻す：** オンにすると、置き忘れたタブレットが、しばらくしたら置いた位置に戻ります。初めはオフです。
  * **戻すまでの時間 (分)：** オンのときだけ出ます。オンにしたときは 5 分で、1 分以上にできます。
  * 前に秒で入れていた値は、そのまま分で表示されます（たとえば 120 秒は 2 分）。動きも変わりません。
  * 9 言語の文言と、使い方ガイドの[タブレット](https://github.com/Mega-Gorilla/KawaPlayer/blob/develop/docs/guide/tablet.md)のページも合わせました。
* **KawaPlayer につながっていない部品があると、ビルドのログが、どの部品かと直し方を教えてくれます。** これまでは `Controller not found on UIController` と、犯人の名前を伏せた警告でした。（#295）
  * Sub Screen・Playlist Panel・Controller Bar・タブレットのインスペクターで、**KawaPlayer** の欄を空のままにしたとき：部品ごとに 1 件、Hierarchy のパスと「インスペクターで KawaPlayer を選んで」が出ます。
  * 欄は入っているのに、中の一部がつながっていないとき（プレハブの更新で部品が増えたときなど）：どこが空かと、「いったん空にしてから選び直して」が出ます。同じ KawaPlayer を選び直すだけでは直らないので、ご注意を。
  * Play や VRChat のログのエラーにも、部品のパスが出ます。
  * 全員リロードボタンは、これまでどおり自分の警告を出します。

## 直したこと

* **曲の一覧の、再生中でない曲名が、配色の文字色になりました。** これまでは真っ白で、プレハブの透明度（75%）まで消えていました。真っ白に燃え尽きていた曲名も、これで配色に戻ってきます。再生中の曲名は、これまでどおり主色です。スクリーンと Playlist Panel の両方です。（#293）
* **権限の説明文の役割名が、インスペクターで選んだ役割の色で出ます。** これまでは色が決め打ちで、色を変えても印だけが変わっていました。（#293）
* **ビルドが、UIController のない自作の UI にも、配色の色を塗るようになりました。** 「今すぐ反映」では塗られるのに、ビルドだけが素通りしていました。同梱の UI の見た目は変わりません。（#293）
* **インスペクターの文字を直しました。**（#293）
  * KawaPlayer やデバイスのインスペクターで、カラーセットがないときの案内が、そこにない「追加」ボタンを指していました。「編集」を指すようにしました。
  * プレイモード中の案内、再生間隔の「秒」、更新の確認の「OK」が、エディタの言語（9 言語）に合わせて出ます。

## 困ったら

「こんなことが起きた！」があれば、気軽に [Discord サーバー](https://discord.gg/tkrHek6PvN) で教えてください。あなたの報告が、次のリリースを作ります。

## 入手方法

VCC（VRChat Creator Companion）から入れます。更新の前に、上の「更新する前に、これだけは読んでね」を見てください。

KawaPlayer のリポジトリを追加済みなら、X.Y.Z はもう更新候補に出ています。まだの人は、下の URL をブラウザのアドレスバーに貼って Enter を押してください。VCC が開き、リポジトリが追加されます。あとは **Manage Project** からプロジェクトに **Add** するだけです。

```
vcc://vpm/addRepo?url=https://mega-gorilla.github.io/vpm-repos/index.json
```

VCC が開かない場合は、**Settings > Packages > Add Repository** に `https://mega-gorilla.github.io/vpm-repos/index.json` を入力してください。

## 全部の変更

1.3.2 からの変更は、[1.3.2...X.Y.Z](https://github.com/Mega-Gorilla/KawaPlayer/compare/1.3.2...X.Y.Z) で見られます。

---

*English:*

A small update right after 1.3.2: the track list now follows your colour set, and the Unity inspector and build log got friendlier.

**Read this before you update:**

* Rebuild your world to get the track list's new title colour (see What's fixed). Without a rebuild, the world keeps working as on 1.3.2. No synced fields changed, so PC and Quest builds on 1.3.2 still play together.
* **Updating the VRChat SDK to 3.10.5?** This is not a KawaPlayer change. A world with the SDK's sample **VRCWorld** may stop at `AssignSceneNetworkIDs` (`Failed to assign network IDs`) before upload. 3.10.5 adds an UdonBehaviour (WorldQualitySettings) to VRCWorld, so it no longer matches the network IDs recorded in the scene. Bring VRCWorld's record up to date in **VRChat SDK > Utilities > Network ID Import and Export Utility**; the IDs can stay the same.

**What's changed:**

* The tablet's "Return when idle (s)", where 0 meant off, is now a **Return when left alone** toggle with a **Time before return (min)** field under it. It starts off, and turning it on starts at 5 minutes (1 or more). Seconds you set before show up as minutes and work as before.
* A device not connected to a player (its **KawaPlayer** field left empty, or a part added after the player was chosen) is now named in the build log, once per device, with its path and how to fix it. The nameless `Controller not found on UIController` warning is gone, and the run-time error shows the UI's path.

**What's fixed:**

* Track titles that are not playing now take the colour set's text colour at 75% opacity instead of plain white, on the screen and the Playlist Panel. The playing one keeps the primary colour.
* The role names in the permission description take the role colours set in the inspector.
* The build now colours a UI of your own that has no UIController, as Apply Colors Now already did. The bundled UIs look the same.
* Inspector text: with no colour set to choose, the player's and the devices' inspectors now point to Edit, not to an Add button they do not have. The play-mode notice, the interval's unit and the update dialog's OK follow the editor language.

Anything odd? Tell us on our [Discord server](https://discord.gg/tkrHek6PvN).
