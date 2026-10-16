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
.d1{fill:var(--s1);stroke:var(--surface);stroke-width:2} .badge{fill:var(--s1)} .badge2{fill:var(--s2)} .bt{fill:var(--surface);font-size:11px;font-weight:600}
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




# comparison.md, figure 6: the order of build-time processing.
def fig_build_order():
    W, H = 820, 540
    b = [text(24, 30, "ビルド時の処理の順番", "t1 f14"),
         text(24, 50, "Unity はシーンの前処理を callbackOrder の小さい順に呼ぶ。KawaPlayer の処理がすべて済んでから、VizVid の土台の処理が動く", "t2 f12")]
    b.append(group(24, 70, 380, 430, "KawaPlayer（YamaPlayerBuildProcess、-100000）", "IYamaPlayerBuildProcess を callbackOrder の順に"))
    kp = [("-10000", "翻訳"), ("-9000", "プレイリスト"), ("-8000", "[RegisterEvent] の配線"),
          ("-3000", "モジュール（_controller、uiSlots の写し）"), ("-2900", "全員リロードボタン・AudioLink・LTCGI"),
          ("-2800", "PitchShifter"), ("-2600", "動的プレイリストの枠"), ("-2500", "PlaylistLoader（URL 欄の差し込み）"),
          ("-2000", "配色・プレーヤーの並び・画面と音・訪問者の記録"), ("-100", "Persistence"), ("-1", "後処理（版の番号、Controller を一番上へ）")]
    for i, (o, name) in enumerate(kp):
        y = 136 + i * 32
        b.append(f'<rect class="box" x="40" y="{y - 16}" width="348" height="26" rx="5"/>')
        b.append(text(52, y + 2, o, "ts1 f12 b"))
        b.append(text(110, y + 2, name, "t1 f12"))
    b.append(group(436, 70, 360, 430, "VizVid の土台（BuildPreprocessorManager、-100）", "主なもの。IPreprocessor と ISelfPreProcess を Priority の順に"))
    vv = [("-10", "部品自身（版の表示、プレーヤー、Resync ボタン）"), ("-1", "[Resolve]（欄からたどって埋める）"),
          ("-1", "ISingleton（1 つにまとめる）"), ("0", "[BindUdonSharpEvent]（配り先を作る）"),
          ("0", "部品自身（配色、オーバーレイ、一覧など）"), ("0", "素材"), ("99", "Core の設定"), ("100", "エディター専用の物を消す")]
    for i, (o, name) in enumerate(vv):
        y = 136 + i * 32
        b.append(f'<rect class="box" x="452" y="{y - 16}" width="328" height="26" rx="5"/>')
        b.append(text(464, y + 2, o, "ts1 f12 b"))
        b.append(text(500, y + 2, name, "t1 f12"))
    b.append(text(452, 412, "同じ Priority の中の順番は、決まっていない", "t2 f11"))
    b.append(text(452, 430, "（-10 の部品自身の処理は、Resolve より先に動く）", "t2 f11"))
    b.append(arrow([(404, 310), (436, 310)]))
    b.append(text(24, H - 20, "KawaPlayer はデバイスごと・目的ごとの処理を並べる。VizVid は、属性を読む汎用の処理と、部品自身の処理を並べる。", "t2 f12"))
    return svg(W, H, "ビルド時の処理の順番",
               "KawaPlayerのYamaPlayerBuildProcess（-100000）が、翻訳からモジュール、全員リロードボタン、PlaylistLoader、画面と音、後処理までをcallbackOrderの順に動かす。そのあと、VizVidの土台のBuildPreprocessorManager（-100）が、部品自身の処理、Resolve、ISingleton、BindUdonSharpEvent、エディター専用の物の削除をPriorityの順に動かす。", "".join(b))


# comparison.md, figure 7: who each named event goes to after the build (appendix B).
def fig_targets():
    rows = [("_onVideoReady", 1, 1), ("_onVideoStart", 1, 1), ("OnVideoPlay", 1, 1), ("OnVideoPause", 1, 1), ("_onVideoEnd", 1, 1),
            ("_OnVolumeChange", 1, 1), ("_OnSpeedChange", 1, 1), ("_OnSyncOffsetChange", 1, 1), ("_OnScreenSharedPropertiesChanged", 1, 1),
            ("_OnTitleData", 1, 0), ("_OnVideoBeginLoad", 1, 0), ("_OnVideoError", 1, 0), ("_OnRangeLoopToggled", 1, 0),
            ("_OnPerformerChange", 0, 1), ("_OnRangeLoopChange", 0, 1)]
    W = 820
    top = 104
    rh = 22
    H = top + rh * (len(rows) + 2) + 60
    b = [text(24, 30, "Play の前処理のあとの、名前ごとの配り先", "t1 f14"),
         text(24, 50, "テスト用の Unity（ClientSim）で読んだ。エディターでは、どの配り先も空だった（付録 B の B1・B2）", "t2 f12")]
    cx = [520, 660]
    b.append(text(24, top - 12, "送り手と名前", "t2 f12 b"))
    b.append(text(cx[0], top - 12, "FrontendHandler", "t2 f12 b", "middle"))
    b.append(text(cx[1], top - 12, "UIHandler", "t2 f12 b", "middle"))
    for i, (name, f, u) in enumerate(rows):
        y = top + i * rh + 8
        if i % 2 == 0:
            b.append(f'<rect class="grp" x="24" y="{y - 15}" width="772" height="{rh}" rx="3"/>')
        b.append(text(40, y, "Core", "tm f11"))
        b.append(text(90, y, name, "t1 mono"))
        for k, on in enumerate((f, u)):
            if on:
                b.append(f'<circle class="d1" cx="{cx[k]}" cy="{y - 4}" r="6"/>')
            else:
                b.append(text(cx[k], y, "—", "tm f12", "middle"))
    y = top + len(rows) * rh + 8 + 8
    b.append(f'<rect class="grp" x="24" y="{y - 15}" width="772" height="{rh}" rx="3"/>')
    b.append(text(40, y, "Frontend", "tm f11"))
    b.append(text(90, y, "_OnUIUpdate", "t1 mono"))
    b.append(text(cx[0], y, "（送り手）", "tm f11", "middle"))
    b.append(f'<circle class="d1" cx="{cx[1]}" cy="{y - 4}" r="6"/>')
    b.append(text(24, H - 30, "Core は 15 個、FrontendHandler は 1 個の名前に、配り先を持った。全部を受ける配り先（targets）は、どちらも空だった。", "t2 f12"))
    b.append(f'<circle class="d1" cx="30" cy="{H - 12}" r="5"/>' + text(42, H - 8, "その名前のイベントが届く", "t2 f12"))
    return svg(W, H, "Playの前処理のあとの、名前ごとの配り先",
               "CoreのnamedTargetsには15個の名前があり、動画の出来事・音量・速度など9個はFrontendHandlerとUIHandlerの両方に、題名や読み込みの始まりなど4個はFrontendHandlerだけに、演者と範囲のループの2個はUIHandlerだけに届く。FrontendHandlerの_OnUIUpdateはUIHandlerに届く。", "".join(b))


# README.md, figure 2: now and after, by stage.
def fig_before_after():
    W, H = 820, 430
    b = [text(24, 30, "今と、整理したあと", "t1 f14"),
         text(24, 50, "3 段のつなぎ込みのうち、エディターとビルドが変わる。実行時の登録は今のまま", "t2 f12")]
    cols = ["エディター", "ビルド", "実行時"]
    x0, cw, gap = 150, 210, 12
    for i, c in enumerate(cols):
        b.append(text(x0 + i * (cw + gap) + cw / 2, 82, c, "t1 f13 b", "middle"))
    rows = [("今", "box", [
                (["選んだときだけ、Sub Controller が", "下の部品の _controller に書く", "タブレットの欄も共通の所にある"], "box"),
                (["デバイスごとの処理が配線する", "選んだ値と合うかは確かめない", "モジュールの UI はパスで写す"], "box"),
                (["Start で AddListener", "名前は送る側も受ける側も", "文字列"], "box")]),
            ("整理したあと", "go", [
                (["置くときにプレイヤーを自動で選ぶ", "デバイスの設定は、デバイスの", "区画として描く（D4・D8）"], "go"),
                (["デバイスの処理が、正の値から", "書き直す・確かめる・外す（D2・D3）", "モジュールの UI は名前の印に写す（D6）"], "go"),
                (["Start で AddListener（今のまま）", "名前は 1 つの一覧の定数（D5）", "差し込み口は型付き（D5）"], "go")])]
    for r, (name, cls, cells) in enumerate(rows):
        y = 96 + r * 150
        b.append(node(24, y, 114, 130, [name], cls, "t1 f13", 18))
        for i, (lines, ccls) in enumerate(cells):
            x = x0 + i * (cw + gap)
            b.append(node(x, y, cw, 130, lines, ccls, "t1 f12", 21))
            if i < 2:
                b.append(arrow([(x + cw + 1, y + 65), (x + cw + gap - 1, y + 65)]))
    b.append(text(24, H - 20, "変えないもの：権限の確認の場所（UI、D7）、実行時の登録のしかた、同期のしかた。", "t2 f12"))
    return svg(W, H, "今と、整理したあと",
               "今は、エディターで選んだときだけ参照を書き、ビルドはデバイスごとに配線し、モジュールのUIをパスで写す。整理したあとは、置くときにプレイヤーを自動で選び、ビルドのデバイスの処理が正の値から参照を書き直して確かめ、モジュールのUIは名前の印に写す。実行時の登録は今のままで、名前は定数の一覧にする。", "".join(b))


# README.md, figure 3: the device build step (D2, D3).
def fig_device_build():
    W, H = 820, 440
    b = [text(24, 30, "デバイスの処理（ビルド、新）の流れ", "t1 f14"),
         text(24, 50, "モジュールの処理より前に、デバイスごとに 1 回動く。下の参照を正の値にそろえてから、ほかの処理に渡す", "t2 f12")]
    b.append(node(24, 90, 170, 56, ["デバイスの根元", ("YamaPlayerSubController", "t2 mono")], "box", "t1 f12", 18))
    b.append(node(234, 84, 190, 68, ["YamaPlayer は", "選ばれているか？"], "ask", "t1 f12", 20))
    b.append(node(464, 84, 190, 68, ["下の部品の参照は、", "そのプレイヤーか？"], "ask", "t1 f12", 20))
    b.append(node(234, 230, 190, 72, ["デバイスを外す", ("EditorOnly にして", "t2 f11"), ("ログに出す（D3）", "t2 f11")], "hot", "t1 f12", 18))
    b.append(node(464, 230, 190, 72, ["書き直す", ("違っていた部品を", "t2 f11"), ("ログに出す（D2）", "t2 f11")], "go", "t1 f12", 18))
    b.append(node(640, 336, 156, 64, ["モジュールの処理", ("（-3000）ほか", "t2 f11")], "box", "t1 f12", 18))
    b.append(arrow([(194, 118), (234, 118)]))
    b.append(arrow([(424, 118), (464, 118)], label="はい", at=(444, 110)))
    b.append(arrow([(329, 152), (329, 230)], label="いいえ", at=(337, 196), anchor="start"))
    b.append(arrow([(559, 152), (559, 230)], label="いいえ", at=(567, 196), anchor="start"))
    b.append(arrow([(654, 118), (718, 118), (718, 336)], label="はい", at=(686, 110)))
    b.append(arrow([(559, 302), (559, 368), (640, 368)]))
    b.append(text(24, 336, "下の部品：", "t2 f12 b"))
    b.append(text(24, 356, "UIController._controller", "t1 mono"))
    b.append(text(24, 374, "YamaPlayerScreen.controller", "t1 mono"))
    b.append(text(24, 392, "YamaPlayerSpeaker.controller", "t1 mono"))
    b.append(text(24, H - 16, "本体（KawaPlayer.prefab）の ScreenUI は、YamaPlayer の下にあるので、同じ決まりでそのプレイヤーにそろえる。", "t2 f12"))
    return svg(W, H, "デバイスの処理（ビルド、新）の流れ",
               "デバイスの根元のYamaPlayerが選ばれていなければ、デバイスを外してログに出す。選ばれていれば、下のUIController、YamaPlayerScreen、YamaPlayerSpeakerの参照がそのプレイヤーかを見て、違えば書き直してログに出す。そのあと、モジュールの処理などに渡す。", "".join(b))


# README.md, figure 4: slots by path and by name (D6).
def fig_slot_names():
    W, H = 820, 420
    b = [text(24, 30, "UI の差し込み先：パスと名前の印", "t1 f14"),
         text(24, 50, "今はモジュールが UI の階層のパスを知っている。整理したあとは、UI が名前の印を置き、モジュールは名前だけを知る", "t2 f12")]
    b.append(group(24, 70, 772, 150, "今：パスで写す"))
    b.append(node(44, 112, 236, 80, ["モジュールの uiSlots", ("Canvas/User/Main/Pages/", "t2 mono"), ("Settings/PageView", "t2 mono")], "box", "t1 f12", 19))
    b.append(node(320, 112, 180, 80, ["transform.Find", ("(targetPath)", "t2 mono")], "ask", "t1 f12", 20))
    b.append(node(540, 112, 236, 80, ["UI の階層", ("作りを変えると外れる", "ts2 f11"), ("見つからなければ黙って飛ばす", "ts2 f11")], "hot", "t1 f12", 19))
    b.append(arrow([(280, 152), (320, 152)]))
    b.append(arrow([(500, 152), (540, 152)]))
    b.append(group(24, 236, 772, 150, "整理したあと：名前で写す（D6）"))
    b.append(node(44, 278, 236, 80, ["モジュールの uiSlots", ("名前：Settings.Video", "t2 mono"), ("（パスは知らない）", "t2 f11")], "box", "t1 f12", 19))
    b.append(node(320, 278, 180, 80, ["名前の印を探す", ("同じ名前の印の下に写す", "t2 f11")], "ask", "t1 f12", 20))
    b.append(node(540, 278, 236, 80, ["UI のプレハブ", ("印：Settings.Video", "t2 mono"), ("どこにも無ければログに出す", "ts1 f11")], "go", "t1 f12", 19))
    b.append(arrow([(280, 318), (320, 318)]))
    b.append(arrow([(500, 318), (540, 318)]))
    b.append(text(24, H - 14, "印は U# でない部品で、ビルドで消える。デバイスは、受け付ける差し込み先に印を置けばよい。", "t2 f12"))
    return svg(W, H, "UIの差し込み先：パスと名前の印",
               "今は、モジュールのuiSlotsがUIの階層のパスを持ち、transform.Findで探すので、UIの作りを変えると黙って外れる。整理したあとは、UIのプレハブに名前の印を置き、モジュールは名前で写す先を指し、どこにも無ければログに出す。", "".join(b))


# README.md, figure 5: the plan over versions.
def fig_rollout():
    W, H = 820, 330
    b = [text(24, 30, "進め方", "t1 f14"),
         text(24, 50, "不具合と #206 は 1.3.0 で先に直し、整理は 1.3.0 のあとに子の issue 1〜4 の順で入れる（D9・D10）", "t2 f12")]
    b.append(group(24, 72, 190, 220, "1.3.0（今）"))
    b.append(node(40, 120, 158, 56, ["AudioLink の", "食い違い（新しい issue）"], "hot", "t1 f12", 18))
    b.append(node(40, 196, 158, 56, ["#206", "タブレットの戻す設定"], "box", "t1 f12", 18))
    b.append(group(234, 72, 562, 220, "1.3.0 のあと（1.4.0）"))
    steps = [("1", "正の値と", "足し方", "D2・D3・D8"), ("2", "区画と", "ビルド処理", "D4"), ("3", "名前の一覧と", "配る仕組み", "D5"), ("4", "差し込み先の", "名前", "D6")]
    for i, (n, l1, l2, d) in enumerate(steps):
        x = 250 + i * 136
        b.append(node(x, 120, 118, 92, [f"子の issue {n}", l1, l2, (d, "t2 f11")], "go", "t1 f12", 19))
        if i < 3:
            b.append(arrow([(x + 119, 166), (x + 135, 166)]))
    b.append(text(250, 244, "1 が土台：ほかの子の issue が「どのプレイヤーか」を頼れるようにする", "t2 f12"))
    b.append(text(250, 264, "子の issue ごとに、device-connections.md と図を書き直す", "t2 f12"))
    b.append(arrow([(214, 166), (234, 166)], "dash"))
    return svg(W, H, "進め方",
               "1.3.0では、AudioLinkの食い違いと#206を先に直す。1.3.0のあとに、子のissue1（正の値と足し方）、2（区画とビルド処理）、3（名前の一覧と配る仕組み）、4（差し込み先の名前）の順に入れる。", "".join(b))


os.makedirs(OUT, exist_ok=True)
for name, f in (("layers.svg", fig_layers), ("linking.svg", fig_linking), ("adding.svg", fig_adding),
                ("events.svg", fig_events), ("permission.svg", fig_permission), ("build-order.svg", fig_build_order),
                ("targets.svg", fig_targets), ("proposal.svg", fig_proposal), ("before-after.svg", fig_before_after),
                ("device-build.svg", fig_device_build), ("slot-names.svg", fig_slot_names), ("rollout.svg", fig_rollout)):
    with open(os.path.join(OUT, name), "w", encoding="utf-8", newline="\n") as fh:
        fh.write(f())
    print("wrote", name)
