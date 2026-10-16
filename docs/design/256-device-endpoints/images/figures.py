"""Draws the SVG figures of docs/design/256-device-endpoints: those of comparison.md
(figure 1, the layers; figure 2, how a link is settled; figure 3, adding a part; figure 4,
how events go out; figure 5, commands and permission) and of README.md (figure 1, the
proposed wiring), comparing KawaPlayer with VizVid (VVMW).

  python figures.py            # writes the SVGs next to this script

The helpers are those of docs/analysis/images/device-connections/figures.py, so the two
sets of figures look the same. Each SVG carries its own light and dark colours
(prefers-color-scheme). The names in the figures are the code's own.
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


# comparison.md, figure 1: the layers.
def fig_layers():
    W, H = 820, 452
    b = [text(24, 30, "部品の層", "t1 f14"),
         text(24, 50, "どちらも、同期する本体と、UI ごとの部品と、足す部品からなる。VVMW は、プレイリストとロックを FrontendHandler に分けている", "t2 f12")]
    b.append(group(24, 70, 374, 340, "KawaPlayer"))
    b.append(node(44, 100, 334, 54, ["Controller", ("再生・同期・プレイリスト・キュー", "t2 f11")], "go", "t1 f12", 17))
    b.append(node(44, 172, 270, 50, ["モジュール", ("PlaylistLoader・権限・GlobalReload など", "t2 f11")], "box", "t1 f12", 17))
    b.append(node(44, 240, 334, 50, ["UIController（UI ごと）", ("本体のスクリーン・タブレット・パネル・ボタン", "t2 f11")], "box", "t1 f12", 17))
    b.append(node(44, 308, 334, 62, ["デバイス（根元に YamaPlayerSubController）", ("タブレット・全員リロードボタン・Sub Screen・", "t2 f11"), ("Controller Bar・Playlist Panel", "t2 f11")], "box", "t1 f12", 16))
    b.append(arrow([(346, 240), (346, 156)], "blue"))
    b.append(arrow([(179, 172), (179, 156)], "blue"))

    b.append(group(422, 70, 374, 340, "VizVid（VVMW）"))
    b.append(node(442, 100, 334, 54, ["Core", ("再生・同期", "t2 f11")], "go", "t1 f12", 17))
    b.append(node(442, 172, 334, 50, ["FrontendHandler（Core に 1 つ）", ("プレイリスト・キュー・ロック", "t2 f11")], "box", "t1 f12", 17))
    b.append(node(442, 240, 334, 50, ["UIHandler（UI ごと）", ("いくつでも置ける", "t2 f11")], "box", "t1 f12", 17))
    b.append(node(442, 308, 334, 62, ["追加の部品", ("オーバーレイ・持てる画面・Resync ボタン・", "t2 f11"), ("近づくと再生・BGM の音量 など", "t2 f11")], "box", "t1 f12", 16))
    b.append(arrow([(609, 240), (609, 224)], "blue"))
    b.append(arrow([(609, 172), (609, 156)], "blue"))
    b.append(legend(24, H - 16, [("blue", "命令を出す向き")]))
    return svg(W, H, "部品の層",
               "KawaPlayerはController、モジュール、UIController、デバイスからなる。VVMWはCore、FrontendHandler、UIHandler、追加の部品からなり、プレイリストとロックはFrontendHandlerが持つ。", "".join(b))


# comparison.md, figure 2: how a link is settled, by stage.
def fig_linking():
    W, H = 820, 430
    b = [text(24, 30, "つなぐ相手が決まるまで", "t1 f14"),
         text(24, 50, "KawaPlayer はインスペクターが書いた値をビルドが手がかりにする。VVMW は部品の欄の属性を、ビルドが読んで埋める", "t2 f12")]
    cols = ["エディター", "ビルド（Play も）", "実行時"]
    x0, cw, gap = 150, 210, 12
    for i, c in enumerate(cols):
        b.append(text(x0 + i * (cw + gap) + cw / 2, 82, c, "t1 f13 b", "middle"))
    rows = [("KawaPlayer", [
                ["Sub Controller で選ぶと、", "下の部品の _controller に書く", ("正の値はここで書かれる", "ts2 f11")],
                ["ビルド処理が _controller を", "手がかりに、モジュール・", "ボタン・URL 欄を配線", ("選んだ値と合うかは確かめない", "ts2 f11")],
                ["Start で AddListener", "（Controller・UIController）", ("外す口は無い", "t2 f11")]]),
            ("VizVid", [
                ["[Locatable]：いちばん近い", "Core を探して欄に入れる", ("正の値は部品の欄", "ts1 f11")],
                ["[Resolve]：handler.core を埋める", "[BindUdonSharpEvent]：", "配り先に自分を足す（名前ごと）"],
                ["SendEvent(名前) が", "namedTargets に配る", ("実行時の登録はほぼ無い", "t2 f11")]])]
    for r, (name, cells) in enumerate(rows):
        y = 96 + r * 150
        b.append(node(24, y, 114, 130, [name], "go" if r else "box", "t1 f13", 18))
        for i, lines in enumerate(cells):
            x = x0 + i * (cw + gap)
            b.append(node(x, y, cw, 130, lines, "box", "t1 f12", 20))
            if i < 2:
                b.append(arrow([(x + cw + 1, y + 65), (x + cw + gap - 1, y + 65)]))
    b.append(text(24, H - 20, "VVMW の値は付録 B で確かめた：UIHandler.core はエディターでは空で、Play の前処理で handler.core から埋まった。", "t2 f12"))
    return svg(W, H, "つなぐ相手が決まるまで",
               "KawaPlayerは、エディターでSub Controllerが_controllerを書き、ビルド処理がそれを手がかりに配線し、実行時にStartで登録する。VVMWは、エディターでLocatableがいちばん近いCoreを探し、ビルドでResolveとBindUdonSharpEventが参照と配り先を埋め、実行時はSendEventが名前ごとの配り先に配る。", "".join(b))


# comparison.md, figure 3: adding a part.
def fig_adding():
    W, H = 820, 330
    b = [text(24, 30, "部品の足し方", "t1 f14"),
         text(24, 50, "KawaPlayer は置いたあとにプレイヤーを選ぶ。VVMW は置くとき、いちばん近い本体に結ぶ", "t2 f12")]
    flows = [("KawaPlayer", [["GameObject >", "KawaPlayer > Tablet"], ["選んでいる物の", "下に置く"], ["インスペクターで", "プレイヤーを選ぶ"], ["下の部品の", "_controller に書く"]],
              "選ぶまでは、どこにもつながらない", "ts2 f12"),
             ("VizVid", [["GameObject >", "VizVid > …"], ["選んでいる物の", "下に置く"], ["Locate：いちばん近い", "Core を探す"], ["欄に入れる", "（無ければ Core を置く）"]],
              "プレイヤーを選んで足せば、そのプレイヤーにつながる", "ts1 f12")]
    for r, (name, steps, note, ncls) in enumerate(flows):
        y = 82 + r * 120
        b.append(text(24, y + 34, name, "t1 f13 b"))
        x = 130
        for i, st in enumerate(steps):
            b.append(node(x, y, 150, 56, st, "go" if i == 2 else "box", "t1 f12", 18))
            if i < 3:
                b.append(arrow([(x + 151, y + 28), (x + 170, y + 28)]))
            x += 172
        b.append(text(130, y + 80, note, ncls))
    return svg(W, H, "部品の足し方",
               "KawaPlayerは、メニューで選んでいる物の下に置き、インスペクターでプレイヤーを選ぶと下の部品に書く。VVMWは、メニューで置くと、Locateがいちばん近いCoreを探して欄に入れ、無ければCoreを置く。", "".join(b))


# comparison.md, figure 4: how events go out.
def fig_events():
    W, H = 820, 470
    b = [text(24, 30, "イベントの配り方", "t1 f14"),
         text(24, 50, "VVMW は共通の基底クラスが、ビルドで作った名前ごとの配り先に配る。KawaPlayer は送り手ごとにリストを持つ", "t2 f12")]
    b.append(group(24, 70, 772, 168, "VizVid（UdonSharpEventSender を継ぐ送り手）"))
    b.append(node(44, 110, 190, 106, ["Core", ("SendEvent(名前)", "t2 mono"), ("19 種類の名前", "t2 f11")], "go", "t1 f12", 20))
    b.append(card(270, 100, 330, 126, "namedTargets（ビルドで作る）", [
        ("_onVideoReady → FrontendHandler, UIHandler", "t1 mono"),
        ("_OnPerformerChange → UIHandler", "t1 mono"),
        ("_OnTitleData → FrontendHandler", "t1 mono"),
        ("…（付録 B では 15 個の名前）", "t2 f11")], "box", step=20))
    b.append(node(630, 110, 146, 106, ["受け手", ("[BindUdonSharpEvent(", "t2 mono"), ("nameof(_OnXxx))]", "t2 mono")], "box", "t1 f12", 20))
    b.append(arrow([(234, 163), (270, 163)]))
    b.append(arrow([(600, 163), (630, 163)], "orange"))

    b.append(group(24, 254, 772, 170, "KawaPlayer（送り手ごとのリスト）"))
    b.append(node(44, 296, 230, 108, ["Controller._listeners", ("型付き AfterXxx（29）と", "t2 f11"), ("SendCustomVideoEvent(名前)", "t2 mono")], "go", "t1 f12", 19))
    b.append(node(294, 296, 230, 108, ["UIController._listeners", ("BeforeUserXxx（26）と", "t2 f11"), ("AfterLanguageChanged", "t2 mono")], "box", "t1 f12", 19))
    b.append(node(544, 296, 232, 108, ["モジュールの自前のリスト", ("PermissionManagement・PitchShifter・", "t2 f11"), ("SlideShower・AudioLinkAdaptor", "t2 f11")], "box", "t1 f12", 19))
    b.append(text(44, H - 14, "どのリストも、受け手が実行時の Start で AddListener を呼んで入る。名前は、送る側も受ける側も文字列。", "t2 f12"))
    return svg(W, H, "イベントの配り方",
               "VVMWでは、CoreなどUdonSharpEventSenderを継ぐ送り手がSendEventで名前を送り、ビルドでBindUdonSharpEventから作った名前ごとの配り先に届く。KawaPlayerでは、Controller、UIController、4つのモジュールが、それぞれ実行時に登録されたリストに配る。", "".join(b))


# comparison.md, figure 5: commands and permission.
def fig_permission():
    W, H = 820, 470
    b = [text(24, 30, "命令と権限", "t1 f14"),
         text(24, 50, "KawaPlayer は UI が操作の前に聞く。VVMW は受ける側が 5 つの命令を断り、ほかは UI が押せなくする", "t2 f12")]
    b.append(group(24, 70, 772, 176, "KawaPlayer"))
    b.append(node(44, 112, 120, 50, ["ボタン"], "box", "t1 f12"))
    b.append(node(196, 112, 160, 50, ["UIController", ("InvokeBeforeEvent", "t2 mono")], "box", "t1 f12", 17))
    b.append(node(392, 100, 200, 74, ["PermissionManagementUI", ("権限が足りなければ", "t2 f11"), ("CancelCurrentAction()", "t2 mono")], "ask", "t1 f12", 16))
    b.append(node(630, 112, 146, 50, ["Controller", ("Pause() など", "t2 mono")], "go", "t1 f12", 17))
    b.append(arrow([(164, 137), (196, 137)]))
    b.append(arrow([(356, 126), (392, 126)], label="BeforeUserXxx", at=(374, 94)))
    b.append(arrow([(276, 162), (276, 198), (703, 198), (703, 164)], "blue", "中止されなければ", (490, 192)))
    b.append(text(44, 228, "Controller を直接呼べば、権限を通らない。権限（PermissionManagement）は全員で共有する。", "ts2 f12"))

    b.append(group(24, 262, 772, 186, "VizVid"))
    b.append(node(44, 300, 140, 56, ["外の権限の", "仕組み"], "box", "t1 f12"))
    b.append(node(230, 292, 196, 72, ["FrontendHandler", ("locked（見る人ごと）", "t2 f11"), ("_Play など 5 つは断る", "t2 f11")], "ask", "t1 f12", 17))
    b.append(node(470, 300, 160, 56, ["UIHandler", ("Locked を見て", "t2 f11"), ("URL 欄などを止める", "t2 f11")], "box", "t1 f12", 15))
    b.append(node(670, 300, 106, 56, ["Core"], "go", "t1 f12"))
    b.append(arrow([(184, 328), (230, 328)]))
    b.append(text(207, 290, "_OnUnlock", "t2 mono", "middle"))
    b.append(text(207, 376, "_Lock", "t2 mono", "middle"))
    b.append(arrow([(470, 316), (428, 316)], "blue", "_Play", (449, 310)))
    b.append(arrow([(426, 344), (470, 344)], "orange"))
    b.append(arrow([(328, 364), (328, 400), (723, 400), (723, 358)], "blue", "locked でなければ", (526, 394)))
    b.append(text(44, 432, "PlayUrl・PlayAt（リストの選択）は受ける側で確かめず、UI が押せなくするだけ。", "ts2 f12"))
    return svg(W, H, "命令と権限",
               "KawaPlayerでは、ボタンを押すとUIControllerがBeforeUserXxxを送り、PermissionManagementUIが中止しなければControllerを呼ぶ。VVMWでは、外の権限の仕組みが_OnUnlockと_Lockを送り、FrontendHandlerがロック中は5つの命令を断り、UIHandlerはURL欄などを押せなくする。", "".join(b))


# README.md, figure 1: the proposed wiring.
def fig_proposal():
    W, H = 820, 470
    b = [text(24, 30, "整理したあとのつなぎ方（案）", "t1 f14"),
         text(24, 50, "どのプレイヤーに従うかはデバイスの根元の 1 か所。ビルドがそこから参照を書き直す。デバイスのことはデバイスの側に置く", "t2 f12")]
    b.append(group(24, 70, 270, 236, "デバイス", "根元に YamaPlayerSubController"))
    b.append(node(44, 120, 230, 50, ["YamaPlayer（正の値）", ("どのプレイヤーに従うか", "t2 f11")], "go", "t1 f12", 17))
    b.append(node(44, 186, 230, 50, ["UIController・Screen・Speaker", ("参照はビルドが書き直す", "t2 f11")], "box", "t1 f12", 17))
    b.append(node(44, 246, 230, 50, ["デバイスの区画とビルド処理", ("デバイスの側のフォルダーに置く", "t2 f11")], "box", "t1 f12", 17))
    b.append(node(330, 120, 220, 116, ["デバイスの処理（ビルド・新）", ("下の参照を書き直す", "t2 f11"), ("食い違いをログに出す", "t2 f11"), ("未設定のデバイスは外す", "t2 f11")], "ask", "t1 f12", 18))
    b.append(node(586, 120, 210, 116, ["本体", ("Controller", "t1 f12"), ("モジュール", "t1 f12")], "go", "t1 f13", 20))
    b.append(arrow([(274, 145), (330, 160)]))
    b.append(arrow([(330, 200), (276, 211)], "dash"))
    b.append(arrow([(274, 222), (306, 222), (306, 284), (691, 284), (691, 238)], "dash", "_controller で指す", (498, 278), "middle"))
    b.append(group(24, 326, 380, 120, "UI の差し込み先に名前"))
    b.append(text(44, 372, "モジュールの uiSlots は、パスでなく名前で指す", "t1 f12"))
    b.append(text(44, 394, "UI のプレハブに、名前の印（ビルドで消える）を置く", "t1 f12"))
    b.append(text(44, 416, "どこにも写されなかったら、ログに出す", "t1 f12"))
    b.append(group(416, 326, 380, 120, "接続口の名前と配る仕組み"))
    b.append(text(436, 372, "名前のイベントを 1 つの定数の一覧にする", "t1 f12"))
    b.append(text(436, 394, "配り先の扱いを共通の補助にそろえる", "t1 f12"))
    b.append(text(436, 416, "名前と受け手のメソッドをテストで確かめる", "t1 f12"))
    b.append(legend(24, H - 12, [("solid", "読む"), ("dash", "書き直す・指す")]))
    return svg(W, H, "整理したあとのつなぎ方（案）",
               "デバイスの根元のYamaPlayerを正の値とし、ビルドのデバイスの処理が下のUIController、Screen、Speakerの参照を書き直し、食い違いをログに出し、未設定のデバイスを外す。デバイスの区画とビルド処理はデバイスの側に置く。モジュールのUIは名前の差し込み先に写し、名前のイベントは定数の一覧にまとめる。", "".join(b))


os.makedirs(OUT, exist_ok=True)
for name, f in (("layers.svg", fig_layers), ("linking.svg", fig_linking), ("adding.svg", fig_adding),
                ("events.svg", fig_events), ("permission.svg", fig_permission), ("proposal.svg", fig_proposal)):
    with open(os.path.join(OUT, name), "w", encoding="utf-8", newline="\n") as fh:
        fh.write(f())
    print("wrote", name)
