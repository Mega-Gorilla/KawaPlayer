"""Draws the SVG figures of docs/analysis/device-connections.md, the record of how
KawaPlayer and the devices placed beside it (the Controller Bar, the Playlist Panel,
the Sub Screen, the tablet and the Global Reload Button) are connected today.

  python figures.py            # writes the SVGs next to this script

The diagrams are drawn here rather than in mermaid, which GitHub did not always draw,
as the figures of docs/guide/technical do. Each SVG carries its own light and dark
colours (prefers-color-scheme). The names in the figures are the code's own, so a
change to a class, a field or an event name has to be made here too.
"""
import os
import sys

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.dirname(os.path.abspath(__file__))

STYLE = """<style>
svg{--surface:#fcfcfb;--ink:#0b0b0b;--ink2:#52514e;--muted:#898781;--grid:#e1e0d9;--axis:#c3c2b7;--s1:#2a78d6;--s2:#eb6834;--wash:#f0efec;
font-family:system-ui,-apple-system,"Segoe UI","Yu Gothic UI","Hiragino Sans","Noto Sans JP",Meiryo,sans-serif}
@media (prefers-color-scheme:dark){svg{--surface:#1a1a19;--ink:#f0efec;--ink2:#c3c2b7;--muted:#898781;--grid:#2c2c2a;--axis:#383835;--s1:#3987e5;--s2:#d95926;--wash:#383835}}
.bg{fill:var(--surface)} .t1{fill:var(--ink)} .t2{fill:var(--ink2)} .tm{fill:var(--muted)} .ts1{fill:var(--s1)} .ts2{fill:var(--s2)}
.axis{stroke:var(--axis);stroke-width:1}
.box{fill:var(--surface);stroke:var(--axis);stroke-width:1.5} .grp{fill:var(--wash);stroke:none}
.ask{fill:var(--wash);stroke:var(--s1);stroke-width:1.5} .go{fill:var(--s1);fill-opacity:.12;stroke:var(--s1);stroke-width:1.5}
.hot{fill:var(--s2);fill-opacity:.12;stroke:var(--s2);stroke-width:1.5}
.arr{fill:none;stroke:var(--ink2);stroke-width:1.5} .arrd{fill:none;stroke:var(--muted);stroke-width:1.5;stroke-dasharray:5 4}
.arr1{fill:none;stroke:var(--s1);stroke-width:1.5} .arr2{fill:none;stroke:var(--s2);stroke-width:1.5;stroke-dasharray:5 4}
.head{fill:var(--ink2)} .headm{fill:var(--muted)} .head1{fill:var(--s1)} .head2{fill:var(--s2)}
.badge{fill:var(--s1)} .badge2{fill:var(--s2)} .bt{fill:var(--surface);font-size:11px;font-weight:600}
.f11{font-size:11px} .f12{font-size:12px} .f13{font-size:13px} .f14{font-size:14px;font-weight:600} .b{font-weight:600}
.mono{font-family:ui-monospace,"Cascadia Mono",Consolas,"Noto Sans Mono",monospace;font-size:11.5px}
</style>"""

DEFS = ('<defs>'
        + "".join(f'<marker id="{i}" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">'
                  f'<path class="{c}" d="M0,0 L10,5 L0,10 z"/></marker>'
                  for i, c in (("h", "head"), ("hm", "headm"), ("h1", "head1"), ("h2", "head2")))
        + '</defs>')

ARROWS = {"solid": ("arr", "h"), "dash": ("arrd", "hm"), "blue": ("arr1", "h1"), "orange": ("arr2", "h2")}


def svg(w, h, title, desc, body):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w} {h}" width="{w}" height="{h}" role="img" aria-labelledby="t d">'
            f'<title id="t">{title}</title><desc id="d">{desc}</desc>{STYLE}{DEFS}'
            f'<rect class="bg" x="0" y="0" width="{w}" height="{h}" rx="8"/>{body}</svg>\n')


def esc(s):
    return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def text(x, y, s, cls="t2 f12", anchor="start"):
    return f'<text x="{x:.1f}" y="{y:.1f}" class="{cls}" text-anchor="{anchor}">{esc(s)}</text>'


def node(x, y, w, h, lines, cls="box", tcls="t1 f13", step=18):
    out = [f'<rect class="{cls}" x="{x}" y="{y}" width="{w}" height="{h}" rx="6"/>']
    top = y + h / 2 - (len(lines) - 1) * step / 2 + 4
    for i, line in enumerate(lines):
        c = tcls
        if isinstance(line, tuple):
            line, c = line
        out.append(text(x + w / 2, top + i * step, line, c, "middle"))
    return "".join(out)


def card(x, y, w, h, title, rows, cls="box", row_cls="t2 f12", step=19, title_cls="t1 f13 b"):
    """A box with a title and rows of text, left-aligned."""
    out = [f'<rect class="{cls}" x="{x}" y="{y}" width="{w}" height="{h}" rx="6"/>', text(x + 12, y + 22, title, title_cls)]
    for i, row in enumerate(rows):
        c = row_cls
        if isinstance(row, tuple):
            row, c = row
        out.append(text(x + 12, y + 44 + i * step, row, c))
    return "".join(out)


def group(x, y, w, h, title, sub=None):
    out = [f'<rect class="grp" x="{x}" y="{y}" width="{w}" height="{h}" rx="8"/>', text(x + 14, y + 22, title, "t1 f13 b")]
    if sub:
        out.append(text(x + 14, y + 40, sub, "t2 f11"))
    return "".join(out)


def arrow(points, kind="solid", label=None, at=None, anchor="middle", lcls="t2 f12"):
    cls, mk = ARROWS[kind]
    d = "M" + " L".join(f"{x:.1f},{y:.1f}" for x, y in points)
    out = [f'<path class="{cls}" d="{d}" marker-end="url(#{mk})"/>']
    if label:
        out.append(text(at[0], at[1], label, lcls, anchor))
    return "".join(out)


def badge(x, y, s, cls="badge"):
    w = 10 + 7 * len(s)
    return (f'<rect class="{cls}" x="{x:.1f}" y="{y - 12:.1f}" width="{w}" height="16" rx="8"/>'
            + text(x + w / 2, y, s, "bt", "middle"))


def legend(x, y, items):
    out = []
    for kind, label in items:
        cls, mk = ARROWS[kind]
        out.append(f'<path class="{cls}" d="M{x},{y - 4} L{x + 26},{y - 4}" marker-end="url(#{mk})"/>' + text(x + 34, y, label, "t2 f12"))
        x += 44 + 13 * len(label)
    return "".join(out)


# Sequence diagrams: participants side by side, steps going down.
# A step is ("msg", from, to, label[, kind]), ("note", who, [lines]) or ("notes", [(who, [lines]), ...]).
def seq(W, title, sub, people, steps, desc, note_w=None):
    cols = len(people)
    left, right = 24, W - 24
    gap = (right - left) / cols
    xs = [left + gap * (i + 0.5) for i in range(cols)]
    bw = min(170, gap - 10)
    nw = note_w or min(190, gap * 1.6)
    top = 70
    b = [text(24, 30, title, "t1 f14"), text(24, 50, sub, "t2 f12")]
    y = top + 70
    body = []
    for st in steps:
        if st[0] == "msg":
            a, z, label = st[1], st[2], st[3]
            kind = st[4] if len(st) > 4 else "solid"
            x1, x2 = xs[a], xs[z]
            body.append(text((x1 + x2) / 2, y - 6, label, "t2 f12", "middle"))
            off = 4 if x2 > x1 else -4
            cls, mk = ARROWS[kind]
            body.append(f'<path class="{cls}" d="M{x1:.1f},{y} L{x2 - off:.1f},{y}" marker-end="url(#{mk})"/>')
            y += 36
        elif st[0] in ("note", "notes"):
            items = [(st[1], st[2])] if st[0] == "note" else st[1]
            h = max(12 + 17 * len(lines) for _, lines in items)
            w = nw if len(items) == 1 else min(nw, gap - 8)
            for who, lines in items:
                body.append(f'<rect class="ask" x="{xs[who] - w / 2:.1f}" y="{y - 12}" width="{w:.1f}" height="{h}" rx="6"/>')
                for i, line in enumerate(lines):
                    body.append(text(xs[who], y + 6 + 17 * i, line, "t1 f12", "middle"))
            y += h + 14
    bottom = y + 2
    for i, (name, sub2) in enumerate(people):
        b.append(f'<line class="axis" x1="{xs[i]:.1f}" x2="{xs[i]:.1f}" y1="{top + 44}" y2="{bottom:.1f}"/>')
        b.append(node(xs[i] - bw / 2, top, bw, 44 if sub2 else 34, [name] + ([(sub2, "t2 f11")] if sub2 else []), "box", "t1 f12", 17))
    b.extend(body)
    return svg(W, int(bottom + 18), title, desc, "".join(b))


# Figure 1: the player and the devices.
def fig_overview():
    W, H = 820, 470
    b = [text(24, 30, "KawaPlayer 本体とデバイス", "t1 f14"),
         text(24, 50, "デバイスは、自分の中の UIController（Sub Screen は YamaPlayerScreen）で本体の Controller を指す", "t2 f12")]
    b.append(group(24, 70, 310, 370, "KawaPlayer 本体", "KawaPlayer.prefab（根元に YamaPlayer）"))
    b.append(node(44, 112, 270, 62, ["Controller", ("再生の中心。再生の状態を同期する", "t2 f12")], "go"))
    b.append(node(44, 190, 130, 50, ["プレーヤー", ("AVPro・Unity・画像", "t2 f11")], "box", "t1 f12", 17))
    b.append(node(184, 190, 130, 50, ["プレイリスト", ("キュー・履歴・枠", "t2 f11")], "box", "t1 f12", 17))
    b.append(node(44, 256, 270, 50, ["Modules/", ("PlaylistLoader・DefaultUrl・権限 など", "t2 f11")], "box", "t1 f12", 17))
    b.append(node(44, 322, 270, 50, ["ScreenUI（UIController）", ("本体のスクリーンの UI", "t2 f11")], "box", "t1 f12", 17))
    b.append(text(44, 400, "本体のスクリーンの UI も、デバイスと", "t2 f11"))
    b.append(text(44, 416, "同じ UIController でつながる", "t2 f11"))

    b.append(group(486, 70, 310, 370, "デバイス", "根元に YamaPlayerSubController が付くプレハブ"))
    devs = [("Controller Bar", "UIController"), ("Playlist Panel", "UIController"),
            ("Sub Screen", "YamaPlayerScreen（ビルドで登録）"), ("Tablet", "UIController ×2・TabletScreen"),
            ("全員リロードボタン", "UIController・GlobalReload")]
    for i, (name, link) in enumerate(devs):
        y = 112 + i * 64
        b.append(node(506, y, 270, 50, [name, (link, "t2 f11")], "box", "t1 f12", 17))
    b.append(arrow([(486, 124), (316, 124)], "blue", "命令（public メソッド）", (402, 116)))
    b.append(arrow([(314, 162), (484, 162)], "orange", "出来事（After〜）", (402, 154)))
    b.append(arrow([(486, 300), (380, 300), (380, 170), (318, 170)], "dash"))
    b.append(text(388, 252, "_controller", "t2 mono"))
    b.append(text(388, 268, "で指す", "t2 f11"))
    b.append(legend(490, H - 14, [("blue", "命令"), ("orange", "出来事"), ("dash", "参照")]))
    return svg(W, H, "KawaPlayer 本体とデバイス",
               "本体はController、プレーヤー、プレイリスト、モジュール、ScreenUIからなる。Controller Bar、Playlist Panel、Sub Screen、Tablet、全員リロードボタンのデバイスは、自分の中のUIControllerかYamaPlayerScreenでControllerを指し、命令を送り、出来事を受け取る。", "".join(b))


# Figure 2: the three stages of wiring.
def fig_stages():
    W, H = 820, 420
    b = [text(24, 30, "つなぎ込みの 3 段", "t1 f14"),
         text(24, 50, "参照はエディターで書き、ビルドで足し、実行時の Start で登録する", "t2 f12")]
    cols = [("① エディター", "選んだとき・メニューで置いたとき", [
                "Sub Controller の YamaPlayer を選ぶ",
                "（置くメニューは、親か 1 つだけの本体）",
                "→ 下の UIController の _controller",
                "→ YamaPlayerScreen の controller",
                "→ YamaPlayerSpeaker の controller",
                "シーン（プレハブの上書き）に保存",
            ]),
            ("② ビルド", "ワールドのビルド・Play のとき", [
                "モジュールに _controller を入れる",
                "モジュールの UI を各 UIController に写す",
                "ボタンに GlobalReload を入れる",
                "つながっていない部品をログに出す",
                "URL 欄に PlaylistLoaderUI を差す",
                "Screen・Speaker を Controller に登録",
                "Controller をシーンの一番上へ移す",
            ]),
            ("③ 実行時", "Udon の Start 以降", [
                "Controller.AddListener(this)",
                "  UIController・モジュール・キュー",
                "UIController.AddListener(this)",
                "  モジュールの UI・TabletScreen",
                "GetComponentInParent で自分の",
                "  UIController を探す（モジュールの UI）",
            ])]
    x, w = 24, 244
    for i, (head, sub, rows) in enumerate(cols):
        cx = x + i * (w + 22)
        b.append(f'<rect class="{"go" if i == 1 else "box"}" x="{cx}" y="72" width="{w}" height="300" rx="8"/>')
        b.append(text(cx + 14, 96, head, "t1 f14"))
        b.append(text(cx + 14, 114, sub, "t2 f11"))
        for j, row in enumerate(rows):
            mono = row.strip().startswith(("Controller.", "UIController.")) or "_controller" in row
            b.append(text(cx + 14, 146 + j * 30, row, "t1 mono" if mono and not row.startswith("→") else "t1 f12"))
        if i < 2:
            b.append(arrow([(cx + w + 2, 222), (cx + w + 20, 222)]))
    b.append(text(24, H - 28, "①で選んだプレイヤーと合っているかは、②でも③でも確かめ直さない（未設定は、ビルドと Start が知らせる）。", "t2 f12"))
    b.append(text(24, H - 10, "②の配線は、①で書いた _controller を手がかりにする。", "t2 f12"))
    return svg(W, H, "つなぎ込みの3段",
               "エディターではSub Controllerのインスペクターと置くメニューが参照を書き、ビルドではビルド処理がモジュールやボタンを配線し、実行時にはStartで各部品が自分をControllerやUIControllerに登録する。", "".join(b))


# Figure 3: the endpoints.
def fig_endpoints():
    W, H = 820, 520
    b = [text(24, 30, "接続口（エンドポイント）の地図", "t1 f14"),
         text(24, 50, "実行時の接続口は Controller と UIController に集まり、エディターとビルドの接続口が配線する", "t2 f12")]
    ctl = [("E1 命令", "public メソッド・プロパティ（約 90）"), ("E2 出来事", "AfterXxx（型付き 29）"),
           ("E3 名前の出来事", "SendCustomVideoEvent(名前)"), ("E4 登録", "AddListener・AddScreen など")]
    ui = [("E5 前の確認", "BeforeUserXxx（名前 27）と中止"), ("E6 UI の知らせ", "AfterLanguageChanged"),
          ("E7 差し込み口", "_urlInterceptor・_playlistRefreshHandler"), ("E8 窓口", "ShowMessage・PlayImageUrl など"),
          ("E9 登録", "AddListener(UdonSharpBehaviour)")]
    b.append(f'<rect class="go" x="24" y="72" width="360" height="232" rx="8"/>' + text(40, 96, "Controller（本体に 1 つ・同期する）", "t1 f13 b"))
    for i, (k, v) in enumerate(ctl):
        y = 128 + i * 40
        b.append(badge(40, y, k.split()[0]) + text(78, y, k.split(" ", 1)[1], "t1 f12 b") + text(78, y + 17, v, "t2 f11"))
    b.append(f'<rect class="box" x="436" y="72" width="360" height="270" rx="8"/>' + text(452, 96, "UIController（UI ごと・同期しない）", "t1 f13 b"))
    for i, (k, v) in enumerate(ui):
        y = 128 + i * 42
        b.append(badge(452, y, k.split()[0]) + text(490, y, k.split(" ", 1)[1], "t1 f12 b") + text(490, y + 17, v, "t2 f11"))
    b.append(arrow([(436, 150), (386, 150)], "blue"))
    b.append(arrow([(386, 190), (436, 190)], "orange"))
    b.append(f'<rect class="box" x="24" y="368" width="772" height="104" rx="8"/>' + text(40, 392, "エディター・ビルドの接続口（ワールドを作るときだけ）", "t1 f13 b"))
    eb = [("E10", "インスペクター・置くメニュー", "下の部品に _controller を書く"),
          ("E11", "IYamaPlayerBuildProcess", "番号の順に動くビルド処理"),
          ("E12", "uiSlots", "パスで UIController の下に写す")]
    for i, (k, v, s) in enumerate(eb):
        x = 40 + i * 252
        b.append(badge(x, 424, k, "badge2") + text(x + 40, 424, v, "t1 f12 b") + text(x + 40, 442, s, "t2 f11"))
    b.append(arrow([(200, 368), (200, 306)], "dash", "配線する", (208, 340), "start"))
    b.append(arrow([(616, 368), (616, 344)], "dash"))
    b.append(legend(24, H - 16, [("blue", "UIController から Controller へ（E1）"), ("orange", "Controller から UIController へ（E2・E3）")]))
    return svg(W, H, "接続口の地図",
               "ControllerはE1命令、E2出来事、E3名前の出来事、E4登録を持つ。UIControllerはE5前の確認、E6 UIの知らせ、E7差し込み口、E8窓口、E9登録を持つ。エディターとビルドには、E10 Sub Controllerのインスペクター、E11ビルド処理、E12 uiSlotsがある。", "".join(b))


# Figure 4: an event going out (E2).
def fig_events():
    people = [("VRChat の", "動画プレーヤー"), ("BaseVideo", "PlayerHandler"), ("Controller", ""), ("UIController", "（各デバイス）"), ("モジュール", "")]
    steps = [
        ("msg", 0, 1, "OnVideoReady"),
        ("msg", 1, 2, "AfterVideoReady()"),
        ("note", 2, ["オーナーなら位置を送る", "そうでなければ位置を合わせる"]),
        ("msg", 2, 3, "AfterVideoReady()", "orange"),
        ("msg", 2, 4, "AfterVideoReady()", "orange"),
        ("notes", [(3, ["表示を更新する"]), (4, ["それぞれの処理"])]),
    ]
    return seq(820, "出来事が届くまで（E2）", "プレーヤーの知らせは Controller に集まり、Controller が登録した全員に配る（_listeners の順）", people, steps,
               "VRChatの動画プレーヤーの知らせをBaseVideoPlayerHandlerが受け、ControllerのAfterVideoReadyを呼ぶ。Controllerは同期の処理をしたあと、登録したUIControllerとモジュールに同じ出来事を配る。")


# Figure 5: a press that asks first (E5).
def fig_before():
    people = [("ボタン", "（押した人の UI）"), ("UIController", ""), ("PermissionManagementUI", "（UI の下に写された物）"), ("PermissionManagement", ""), ("Controller", "")]
    steps = [
        ("msg", 0, 1, "Pause()"),
        ("msg", 1, 2, "BeforeUserPauseVideo（名前で送る）"),
        ("msg", 2, 3, "PlayerPermission を読む"),
        ("note", 2, ["Editor 未満なら", "CancelCurrentAction() と", "ShowMessage（権限がない）"]),
        ("note", 1, ["中止されたら、ここで戻る"]),
        ("msg", 1, 4, "TakeOwnership()・Pause()", "blue"),
        ("note", 4, ["オーナーとして", "状態を全員に送る"]),
    ]
    return seq(820, "前の確認を通る操作（E5）", "UIController は操作の前に BeforeUserXxx を登録した全員へ送り、誰かが中止すれば Controller を呼ばない", people, steps,
               "ボタンでPauseを押すと、UIControllerがBeforeUserPauseVideoを名前で送る。PermissionManagementUIは権限を読み、Editor未満なら中止して案内を出す。中止されなければ、UIControllerがControllerの所有者になって一時停止する。")


# Figure 6: the URL field handed to PlaylistLoader (E7).
def fig_intercept():
    people = [("URL 欄", ""), ("UIController", ""), ("PlaylistLoaderUI", "（_urlInterceptor）"), ("PlaylistLoader", ""), ("VHub", "")]
    steps = [
        ("msg", 0, 1, "PlayUrl"),
        ("msg", 1, 2, "interceptUrl・interceptSource を書き、OnUrlSubmitted"),
        ("note", 2, ["VHub の VRChat用URL か？", "BeforeUserLoadPlaylist も通す"]),
        ("msg", 2, 3, "LoadPlaylistFromUrlWithFeedback（始めるだけ）"),
        ("msg", 2, 1, "interceptHandled = true（その場で返る）", "dash"),
        ("note", 1, ["false なら、そのまま", "動画として再生する"]),
        ("msg", 3, 4, "文字列のダウンロード（あとで）"),
        ("msg", 4, 3, "曲の一覧（JSON）", "dash"),
        ("msg", 3, 2, "OnLoadResult(結果)（非同期）"),
        ("msg", 2, 1, "ShowMessage（結果の案内）"),
    ]
    return seq(820, "URL 欄の差し込み口（E7）", "差し込みはその場で handled を返し、ダウンロードの結果はあとから OnLoadResult に届く。コアは VHub を知らない", people, steps,
               "URL欄で再生すると、UIControllerは_urlInterceptorのPlaylistLoaderUIにinterceptUrlとinterceptSourceを書いてOnUrlSubmittedを送る。PlaylistLoaderUIはVHubのURLなら読み込みを始めてinterceptHandledをtrueにし、その場で返る。falseなら、UIControllerが動画として再生する。ダウンロードの結果はあとからOnLoadResultに届き、UIControllerのShowMessageで案内する。")


# Figure 7: inside the tablet.
def fig_tablet():
    W, H = 820, 500
    b = [text(24, 30, "タブレットの中", "t1 f14"),
         text(24, 50, "タブレットは UIController を 2 つ持ち、画面の状態は自分の TabletScreen で同期する", "t2 f12")]
    b.append(group(24, 70, 566, 380, "Tablet.prefab", "根元に YamaPlayerSubController"))
    b.append(node(44, 112, 170, 50, ["TabletPickup", ("VRCPickup・VRCObjectSync", "t2 f11")], "box", "t1 f12", 17))
    b.append(node(300, 108, 270, 60, ["TabletScreen", ("同期：開いているアプリ・画像の URL・一覧", "t2 f11"), ("縦横の回転・ホームボタン", "t2 f11")], "go", "t1 f12", 16))
    apps = [("画像アプリ", "TabletImageApp"), ("設定アプリ", "SettingsPanel"), ("訪問者アプリ", "TabletVisitorsApp")]
    for i, (name, cls) in enumerate(apps):
        b.append(node(44, 202 + i * 60, 170, 46, [name, (cls, "t2 f11")], "box", "t1 f12", 17))
    b.append(node(300, 212, 270, 50, ["UIController（タブレットの）", ("ホーム画面・設定アプリのボタン", "t2 f11")], "box", "t1 f12", 17))
    b.append(node(300, 300, 270, 50, ["ScreenUI（UIController）", ("KawaPlayer アプリ。本体のスクリーンと同じ", "t2 f11")], "box", "t1 f12", 17))
    b.append(node(44, 388, 170, 46, ["TabletReturnButton", ("TabletPickup を戻す", "t2 f11")], "box", "t1 f12", 17))
    b.append(node(300, 384, 270, 50, ["TabletVisitorRecorder", ("ビルドでワールドに 1 つにまとめる", "t2 f11")], "box", "t1 f12", 17))
    b.append(node(640, 206, 156, 160, ["Controller", ("本体", "t2 f11")], "go", "t1 f13", 18))

    b.append(arrow([(214, 137), (300, 137)]))
    b.append(arrow([(435, 168), (435, 210)], "dash", "AddListener", (443, 196), "start", "t2 mono"))
    b.append(arrow([(214, 222), (300, 232)], "blue", "PlayImageUrl", (257, 216), "middle", "t2 mono"))
    b.append(arrow([(214, 284), (300, 248)], "blue"))
    b.append(arrow([(214, 345), (250, 345), (250, 409), (300, 409)]))
    b.append(arrow([(44, 411), (34, 411), (34, 137), (42, 137)], "dash"))
    b.append(arrow([(570, 229), (640, 229)], "blue"))
    b.append(arrow([(640, 247), (572, 247)], "orange"))
    b.append(arrow([(570, 317), (640, 317)], "blue"))
    b.append(arrow([(640, 335), (572, 335)], "orange"))
    b.append(legend(24, H - 16, [("blue", "命令"), ("orange", "出来事"), ("solid", "タブレットの中の参照"), ("dash", "登録・戻す")]))
    return svg(W, H, "タブレットの中",
               "TabletScreenが画面の状態を同期し、アプリはTabletScreenを通してタブレットのUIControllerを使う。画像アプリはPlayImageUrlで、設定アプリは直接つないだボタンで、タブレットのUIControllerを呼ぶ。KawaPlayerアプリはScreenUIで、本体と同じUIを持つ。どちらのUIControllerも本体のControllerを指す。", "".join(b))


# Figure 8: a press of the Global Reload Button, across the network.
def fig_global():
    people = [("GlobalReload", "Button"), ("UIController", "（ボタンの）"), ("GlobalReload", "（押した人）"), ("VRChat", ""), ("GlobalReload", "（各自）"), ("Controller", "（各自）")]
    steps = [
        ("msg", 0, 1, "BeforeUserGlobalReload"),
        ("note", 1, ["中止されなければ押せる", "（1 秒ごとにも試す）"]),
        ("msg", 0, 2, "Send()", "blue"),
        ("note", 2, ["ローカルモードなら", "送らずに Reload()"]),
        ("msg", 2, 3, "SendCustomNetworkEvent(All)"),
        ("msg", 3, 4, "OnGlobalReload（押した人にも）", "dash"),
        ("msg", 4, 5, "Reload()", "blue"),
        ("note", 5, ["自分の動画を読み込み直す", "（ReSync と同じ決まり）"]),
    ]
    return seq(820, "全員リロードボタン（ネットワークをまたぐ流れ）", "ボタンは UIController で権限を通し、モジュールの GlobalReload がネットワークのイベントを全員に送る", people, steps,
               "GlobalReloadButtonはボタンのUIControllerにBeforeUserGlobalReloadを送って権限を確かめる。押すとGlobalReloadのSendがネットワークのイベントを全員に送り、各自のGlobalReloadが自分のControllerのReloadを呼ぶ。",
               note_w=176)


# Figure 9: module UI copied into every UIController (E12).
def fig_slots():
    W, H = 820, 494
    b = [text(24, 30, "モジュールの UI の差し込み（E12）", "t1 f14"),
         text(24, 50, "ビルドで、同じ Controller を指すすべての UIController に、パスが見つかった所だけ写す（例は PermissionManagement）", "t2 f12")]
    b.append(card(24, 72, 326, 274, "YamaPlayerModuleDefinition.uiSlots", [
        ("targetPath", "t2 f11 b"),
        ("Canvas/User", "t1 mono"),
        ("Categories（設定から）", "t1 mono"),
        ("Pages（設定から）", "t1 mono"),
        ("Canvas/Display", "t1 mono"),
        ("Canvas/Modules", "t1 mono"),
        ("content：写す元のテンプレート", "t2 f11"),
        ("siblingIndex：写した先での並び", "t2 f11"),
        ("inSettings：設定（SettingsPanel）から探す", "t2 f11"),
    ], "box", step=24))
    b.append(node(372, 150, 136, 96, ["YamaPlayerModule", "BuildProcess", ("transform.Find", "t2 mono"), ("(targetPath)", "t2 mono")], "go", "t1 f12", 18))
    b.append(arrow([(350, 198), (372, 198)]))
    uis = [("ScreenUI（本体・タブレットのアプリ）", "Canvas/User ほか", True),
           ("タブレットの UIController", "Canvas/Display", True),
           ("全員リロードボタン・Playlist Panel", "Canvas/Modules（空で用意）", True),
           ("Controller Bar", "合うパスがない → 写さない", False)]
    for i, (name, path, ok) in enumerate(uis):
        y = 80 + i * 70
        b.append(node(544, y, 252, 56, [name, (path, "t2 f11")], "box" if ok else "hot", "t1 f12", 17))
        b.append(arrow([(508, 198), (526, 198), (526, y + 28), (544, y + 28)], "solid" if ok else "dash"))
    b.append(text(24, 376, "inSettings の口は、UIController の下の設定（スクリーンの設定のページ、タブレットの設定アプリ）から探すので、1 つで両方に入る。", "t2 f12"))
    b.append(text(24, 398, "写した物は、実行時に GetComponentInParent<UIController>() で自分の UIController を見つけて登録する。", "t2 f12"))
    b.append(text(24, 420, "写した物の中の参照は、写した先の物に付け替える。テンプレートの外（モジュール本体）への参照はそのまま残る。", "t2 f12"))
    b.append(text(24, 442, "パスが見つからない UI は、何も言わずに飛ばす。写し終えたら、テンプレートは消す。", "t2 f12"))
    return svg(W, H, "モジュールのUIの差し込み",
               "モジュールの定義のuiSlotsにはtargetPathと写す元がある。ビルド処理は、同じControllerを指すUIControllerごとにパスを探し（inSettingsの口はその設定から）、見つかった所に写す。ScreenUI、タブレットのUIController、全員リロードボタンとPlaylist PanelのUIControllerには写され、Controller Barには合うパスがないので写されない。", "".join(b))


# Figure 10: three ways a part finds its player.
def fig_find():
    W, H = 820, 380
    b = [text(24, 30, "部品がプレイヤーを見つける 3 つの方法", "t1 f14"),
         text(24, 50, "同じ「どのプレイヤーに従うか」を、場所ごとに別の方法で決めている", "t2 f12")]
    cols = [("(a) エディターで書く", "box", [
                ("YamaPlayerSubControllerEditor", "t1 mono"),
                "選んだとき・メニューで置いたときに、",
                "下の UIController・",
                "Screen・Speaker へ書く",
                ("使う：デバイスの UI・画面・音", "t2 f11"),
                ("選び直さないと、後で足した物は", "ts2 f11"),
                ("つながらない", "ts2 f11")]),
            ("(b) 親をたどる", "box", [
                ("GetComponentInParent<…>()", "t1 mono"),
                "モジュール → Controller",
                "モジュールの UI → UIController",
                ("使う：ビルドのモジュール配線、", "t2 f11"),
                ("実行時のモジュールの UI", "t2 f11"),
                ("置き場所が決まっている物だけ", "ts2 f11")]),
            ("(c) シーン全体を探す", "box", [
                ("FindObjectsByType<UIController>", "t1 mono"),
                "_controller が同じ物を全部",
                "集める",
                ("使う：uiSlots、PlaylistLoader の", "t2 f11"),
                ("差し込み", "t2 f11"),
                ("(a) で書いた値が手がかり", "ts2 f11")])]
    w = 244
    for i, (head, cls, rows) in enumerate(cols):
        x = 24 + i * (w + 22)
        b.append(card(x, 72, w, 230, head, rows, cls, step=26, title_cls="t1 f13 b"))
    b.append(text(24, 336, "正の値は各部品の _controller で、書くのは (a) だけ（インスペクターと置くメニュー）。(b)・(c) は読む側である。", "t2 f12"))
    b.append(text(24, 358, "YamaPlayerSubController.YamaPlayer は、選んだときに使われるだけで、配線には使われない（ビルドは警告の文に使う）。", "t2 f12"))
    return svg(W, H, "部品がプレイヤーを見つける3つの方法",
               "エディターで書く方法、親をたどる方法、シーン全体を探す方法がある。正の値は各部品の_controllerで、書くのはエディター（Sub Controllerのインスペクターと置くメニュー）だけである。", "".join(b))


os.makedirs(OUT, exist_ok=True)
for name, f in (("overview.svg", fig_overview), ("stages.svg", fig_stages), ("endpoints.svg", fig_endpoints),
                ("events.svg", fig_events), ("before.svg", fig_before), ("intercept.svg", fig_intercept),
                ("tablet.svg", fig_tablet), ("global-reload.svg", fig_global), ("slots.svg", fig_slots), ("find.svg", fig_find)):
    with open(os.path.join(OUT, name), "w", encoding="utf-8", newline="\n") as fh:
        fh.write(f())
    print("wrote", name)
