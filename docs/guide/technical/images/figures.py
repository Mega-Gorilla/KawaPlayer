"""Draws the SVG figures of docs/guide/technical/sync.md: figure 1 (the whole
picture), figure 2 (where the video should be), figure 3 (the check once a second),
figure 4 (how a drift is put right, a model) and figure 7 (when ReSync loads).
Figures 1 and 3 are drawn here rather than in mermaid, whose flowcharts GitHub did
not always draw.

  python figures.py            # writes the SVGs next to this script

Figure 4 comes from the small model below, not from a measurement: one viewer, a
network stall, and a fixed re-buffer after every seek. The check copies KawaPlayer's
rules: once a second while playing, no seek while the video has not moved since the
last check, and a seek only when it is 0.9 s off. Each SVG carries its own light and
dark colours (prefers-color-scheme), as the charts of docs/design/228-playback-sync do.
"""
import os
import sys

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.dirname(os.path.abspath(__file__))

STYLE = """<style>
svg{--surface:#fcfcfb;--ink:#0b0b0b;--ink2:#52514e;--muted:#898781;--grid:#e1e0d9;--axis:#c3c2b7;--s1:#2a78d6;--s2:#eb6834;--wash:#f0efec;
font-family:system-ui,-apple-system,"Segoe UI","Yu Gothic UI","Hiragino Sans","Noto Sans JP",Meiryo,sans-serif}
@media (prefers-color-scheme:dark){svg{--surface:#1a1a19;--ink:#f0efec;--ink2:#c3c2b7;--muted:#898781;--grid:#2c2c2a;--axis:#383835;--s1:#3987e5;--s2:#d95926;--wash:#383835}}
.bg{fill:var(--surface)} .t1{fill:var(--ink)} .t2{fill:var(--ink2)} .tm{fill:var(--muted)}
.grid{stroke:var(--grid);stroke-width:1} .axis{stroke:var(--axis);stroke-width:1}
.l1{fill:none;stroke:var(--s1);stroke-width:2;stroke-linejoin:round;stroke-linecap:round}
.l2{fill:none;stroke:var(--s2);stroke-width:2;stroke-linejoin:round;stroke-linecap:round}
.dash{fill:none;stroke:var(--muted);stroke-width:1.5;stroke-dasharray:5 4}
.d1{fill:var(--s1);stroke:var(--surface);stroke-width:2} .d2{fill:var(--s2);stroke:var(--surface);stroke-width:2}
.h1{fill:var(--surface);stroke:var(--s1);stroke-width:2} .h2{fill:var(--surface);stroke:var(--s2);stroke-width:2}
.s1{fill:var(--s1)} .s2{fill:var(--s2)} .w1{fill:var(--s1);fill-opacity:.12} .w2{fill:var(--s2);fill-opacity:.12} .wash{fill:var(--wash)}
.thr{stroke:var(--muted);stroke-width:1} .lead{stroke:var(--muted);stroke-width:1;fill:none}
.tick{stroke:var(--muted);stroke-width:1.5;stroke-linecap:round}
.press{stroke:var(--ink2);stroke-width:2;stroke-linecap:round}
.box{fill:var(--surface);stroke:var(--axis);stroke-width:1.5} .grp{fill:var(--wash);stroke:none}
.ask{fill:var(--wash);stroke:var(--s1);stroke-width:1.5} .go{fill:var(--s1);fill-opacity:.12;stroke:var(--s1);stroke-width:1.5}
.arr{fill:none;stroke:var(--ink2);stroke-width:1.5} .arrd{fill:none;stroke:var(--muted);stroke-width:1.5;stroke-dasharray:5 4}
.head{fill:var(--ink2)} .headm{fill:var(--muted)}
.f12{font-size:12px} .f13{font-size:13px} .f14{font-size:14px;font-weight:600} .num{font-variant-numeric:tabular-nums}
</style>"""


DEFS = ('<defs><marker id="h" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">'
        '<path class="head" d="M0,0 L10,5 L0,10 z"/></marker>'
        '<marker id="hm" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">'
        '<path class="headm" d="M0,0 L10,5 L0,10 z"/></marker></defs>')


def svg(w, h, title, desc, body):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w} {h}" width="{w}" height="{h}" role="img" aria-labelledby="t d">'
            f'<title id="t">{title}</title><desc id="d">{desc}</desc>{STYLE}{DEFS}'
            f'<rect class="bg" x="0" y="0" width="{w}" height="{h}" rx="8"/>{body}</svg>\n')


def node(x, y, w, h, lines, cls="box", tcls="t1 f13"):
    out = [f'<rect class="{cls}" x="{x}" y="{y}" width="{w}" height="{h}" rx="6"/>']
    top = y + h / 2 - (len(lines) - 1) * 9 + 4
    for i, line in enumerate(lines):
        out.append(text(x + w / 2, top + i * 18, line, tcls, "middle"))
    return "".join(out)


def arrow(points, dashed=False, label=None, at=None, anchor="middle"):
    d = "M" + " L".join(f"{x:.1f},{y:.1f}" for x, y in points)
    out = [f'<path class="{"arrd" if dashed else "arr"}" d="{d}" marker-end="url(#{"hm" if dashed else "h"})"/>']
    if label:
        out.append(text(at[0], at[1], label, "t2 f12", anchor))
    return "".join(out)


def text(x, y, s, cls="t2 f12", anchor="start"):
    return f'<text x="{x:.1f}" y="{y:.1f}" class="{cls}" text-anchor="{anchor}">{s}</text>'


# Figure 1: the whole picture.
def fig_overview():
    W, H = 820, 372
    b = [text(24, 30, "同期の全体像", "t1 f14"),
         text(24, 50, "共有するのは値だけで、動画は一人ひとりが読み込み、位置はサーバーの時刻から一人ひとりが出す", "t2 f12")]
    b.append('<rect class="grp" x="24" y="76" width="176" height="150" rx="8"/>' + text(112, 96, "オーナー（操作した人）", "t1 f13", "middle"))
    b.append('<rect class="grp" x="262" y="76" width="196" height="150" rx="8"/>' + text(360, 96, "VRChat", "t1 f13", "middle"))
    b.append('<rect class="grp" x="520" y="76" width="276" height="196" rx="8"/>' + text(658, 96, "ほかの人（一人ひとり）", "t1 f13", "middle"))
    b.append(node(40, 120, 144, 56, ["曲・状態・位置と", "その時刻を送る"]))
    b.append(node(278, 110, 164, 52, ["値を全員に配る", "（後から入った人にも）"]))
    b.append(node(278, 176, 164, 36, ["サーバーの時刻"]))
    b.append(node(540, 110, 236, 32, ["① 値を受け取る"]))
    b.append(node(540, 160, 236, 32, ["② 動画を自分で読み込む"]))
    b.append(node(540, 210, 236, 48, ["③ 今あるべき位置を出して、", "そこから再生する"]))
    b.append(node(290, 292, 210, 44, ["動画のサイト", "（YouTube・VHub など）"], "box", "t2 f12"))
    b.append(arrow([(184, 136), (278, 136)]))
    b.append(arrow([(442, 126), (540, 126)]))
    b.append(arrow([(658, 142), (658, 160)]))
    b.append(arrow([(658, 192), (658, 210)]))
    b.append(arrow([(442, 194), (496, 194), (496, 234), (540, 234)], dashed=True))
    b.append(arrow([(776, 176), (808, 176), (808, 314), (500, 314)], dashed=True, label="動画を取りに行く", at=(650, 306)))
    b.append(arrow([(112, 176), (112, 314), (290, 314)], dashed=True, label="オーナーも自分で読み込む", at=(122, 306), anchor="start"))
    lx, ly = 560, H - 12
    b.append(f'<path class="arr" d="M{lx},{ly - 4} L{lx + 26},{ly - 4}" marker-end="url(#h)"/>' + text(lx + 34, ly, "値", "t2 f12"))
    b.append(f'<path class="arrd" d="M{lx + 70},{ly - 4} L{lx + 96},{ly - 4}" marker-end="url(#hm)"/>' + text(lx + 104, ly, "時刻・動画", "t2 f12"))
    return svg(W, H, "同期の全体像",
               "オーナーは曲・状態・位置とその時刻を送り、VRChatが全員に配る。ほかの人は値を受け取り、動画を自分で読み込み、サーバーの時刻から今あるべき位置を出して再生する。", "".join(b))


# Figure 3: the check once a second.
def fig_check():
    W, H = 820, 330
    b = [text(24, 30, "1 秒ごとの確かめ", "t1 f14"),
         text(24, 50, "合わせ直すのは、動画が進んでいて、0.9 秒以上ずれているときだけ", "t2 f12")]
    y, h = 86, 64
    b.append(node(24, y + 8, 120, 48, ["1 秒ごとに", "比べる"]))
    b.append(node(180, y, 170, h, ["ライブ配信か、", "止まっているか？"], "ask"))
    b.append(node(390, y, 180, h, ["前に比べたときから", "動画が進んでいるか？"], "ask"))
    b.append(node(610, y, 186, h, ["ずれが 0.9 秒以上か？", "（実際の時間で）"], "ask"))
    yb = 260
    b.append(node(180, yb, 390, 44, ["何もしない"]))
    b.append(node(610, yb, 186, 44, ["今あるべき位置へシークする"], "go"))
    b.append(arrow([(144, y + 32), (180, y + 32)]))
    b.append(arrow([(350, y + 32), (390, y + 32)], label="いいえ", at=(370, y + 24)))
    b.append(arrow([(570, y + 32), (610, y + 32)], label="はい", at=(590, y + 24)))
    b.append(arrow([(265, y + h), (265, yb)], label="はい", at=(273, (y + h + yb) / 2 + 4), anchor="start"))
    b.append(arrow([(470, y + h), (470, yb)], label="いいえ", at=(462, (y + h + yb) / 2 - 6), anchor="end"))
    b.append(text(462, (y + h + yb) / 2 + 12, "（読み込み中・バッファ中）", "t2 f12", "end"))
    b.append(arrow([(650, y + h), (650, 232), (540, 232), (540, yb)], label="いいえ", at=(596, 226)))
    b.append(arrow([(756, y + h), (756, yb)], label="はい", at=(764, (y + h + yb) / 2 + 4), anchor="start"))
    return svg(W, H, "1秒ごとの確かめ",
               "ライブ配信か止まっていれば何もしない。動画が進んでいなければ（読み込み中・バッファ中）何もしない。ずれが0.9秒以上のときだけ、今あるべき位置へシークする。", "".join(b))


# Figure 2: the shared position and time, and where everyone works out the video should be.
# The owner plays at 2 s, pauses at 10 s, plays again at 14 s and sets the speed to 2 at 20 s.
def fig_position():
    W, H = 820, 450
    L, R, T, B = 84, 790, 84, 360
    xmax, ymax = 30.0, 36.0
    sx = lambda t: L + (R - L) * t / xmax
    sy = lambda p: B - (B - T) * p / ymax
    shared = [(2, 0, "① 再生", "start", 18), (10, 8, "② 一時停止", "start", 18), (14, 8, "③ 再生", "start", 18), (20, 14, "④ 速度を 2 倍に", "end", -12)]
    line = [(0, 0), (2, 0), (10, 8), (14, 8), (20, 14), (30, 34)]
    b = [text(24, 30, "共有の位置と時刻から、全員が「今あるべき位置」を出す", "t1 f14"),
         text(24, 50, "オーナーが 2 秒に再生、10 秒に一時停止、14 秒に再生、20 秒に速度を 2 倍にした例", "t2 f12")]
    for p in range(0, 37, 6):
        b.append(f'<line class="{"axis" if p == 0 else "grid"}" x1="{L}" x2="{R}" y1="{sy(p):.1f}" y2="{sy(p):.1f}"/>')
        b.append(text(L - 8, sy(p) + 4, f"{p}", "tm f12 num", "end"))
    for t in range(0, 31, 5):
        b.append(f'<line class="grid" x1="{sx(t):.1f}" x2="{sx(t):.1f}" y1="{T}" y2="{B}"/>')
        b.append(text(sx(t), B + 18, f"{t}", "tm f12 num", "middle"))
    b.append(text((L + R) / 2, B + 38, "VRChat のサーバーの時刻（秒）", "tm f12", "middle"))
    b.append(text(L - 40, T - 12, "再生位置（秒）", "tm f12"))
    b.append(f'<rect class="wash" x="{sx(10):.1f}" y="{T}" width="{sx(14) - sx(10):.1f}" height="{B - T}"/>')
    b.append(text((sx(10) + sx(14)) / 2, T + 16, "一時停止", "t2 f12", "middle"))
    b.append(text((sx(10) + sx(14)) / 2, T + 32, "進まない", "t2 f12", "middle"))
    pts = " ".join(f"{sx(t):.1f},{sy(p):.1f}" for t, p in line)
    b.append(f'<polyline class="l1" points="{pts}"/>')
    b.append(text(sx(6), sy(4) - 14, "傾き 1（速度 1）", "t2 f12", "end"))
    b.append(text(sx(22.5), sy(19) + 24, "傾き 2（速度 2）", "t2 f12", "start"))
    for t, p, label, anchor, dy in shared:
        b.append(f'<circle class="d2" cx="{sx(t):.1f}" cy="{sy(p):.1f}" r="6"/>')
        dx = 10 if anchor == "start" else -10
        b.append(text(sx(t) + dx, sy(p) + dy, label, "t1 f12", anchor))
    # how one point is worked out: at 26 s, from the last shared point (20 s, 14 s)
    t, p = 26, 14 + 2 * (26 - 20)
    b.append(f'<line class="dash" x1="{sx(20):.1f}" y1="{sy(14):.1f}" x2="{sx(t):.1f}" y2="{sy(14):.1f}"/>')
    b.append(f'<line class="dash" x1="{sx(t):.1f}" y1="{sy(14):.1f}" x2="{sx(t):.1f}" y2="{sy(p):.1f}"/>')
    b.append(f'<circle class="h1" cx="{sx(t):.1f}" cy="{sy(p):.1f}" r="5"/>')
    b.append(text(sx(t) - 10, sy(p) - 10, "26 秒には 14 ＋ 6 × 2 ＝ 26", "t2 f12", "end"))
    b.append(text((sx(20) + sx(t)) / 2, sy(14) + 16, "過ぎた時間 6 秒", "tm f12", "middle"))
    lx, ly = L, H - 12
    b.append(f'<circle class="d2" cx="{lx + 5}" cy="{ly - 4}" r="5"/>' + text(lx + 16, ly, "オーナーが送った位置と時刻", "t2 f12"))
    b.append(f'<line class="l1" x1="{lx + 200}" y1="{ly - 4}" x2="{lx + 226}" y2="{ly - 4}"/>' + text(lx + 234, ly, "今あるべき位置（全員が同じ計算で出す）", "t2 f12"))
    return svg(W, H, "共有の位置と時刻から、今あるべき位置を出す",
               "オーナーが送った位置と時刻から、過ぎた時間と再生速度で今あるべき位置を出す。一時停止のあいだは進まず、速度が2倍なら2倍で進む。", "".join(b))


# Figure 4: the model of a stall and the check once a second.
STALL = (8.0, 12.0)
SEEK_COST = 0.6
DT = 0.01
END = 24.0


def run():
    pos, frozen_until, t = 0.0, 0.0, 0.0
    next_check, last_checked_pos = 1.0, None
    trace, seeks, freezes, checks = [], [], [], []
    while t <= END + 1e-9:
        if t >= next_check - 1e-9:
            next_check += 1.0
            moved = last_checked_pos is None or abs(pos - last_checked_pos) > 1e-9
            seek = moved and abs(t - pos) >= 0.9
            checks.append((t, "seek" if seek else ("wait" if not moved else "ok")))
            if seek:
                seeks.append((t, t - pos)); pos = t
                frozen_until = max(frozen_until, t + SEEK_COST)
                freezes.append((t, frozen_until))
            last_checked_pos = pos
        stalled = STALL[0] <= t < STALL[1] or t < frozen_until
        trace.append((t, t - pos))
        if not stalled: pos += DT
        t = round(t + DT, 10)
    return trace, seeks, freezes, checks


def fig_drift():
    W, H = 820, 380
    L, R, T = 84, 790, 84
    ph = 200
    ymax, xmax = 4.5, END
    sx = lambda t: L + (R - L) * t / xmax
    sy = lambda v: T + ph - ph * v / ymax
    tr, seeks, freezes, checks = run()
    b = [text(24, 30, "通信が止まったあとの時刻合わせ（モデル。実測ではない）", "t1 f14"),
         text(24, 50, f"通信が 8〜12 秒で止まり、シーク 1 回ごとに {SEEK_COST} 秒の読み直しがかかると仮定した、1 人の見る人のずれ", "t2 f12")]
    b.append(f'<rect class="wash" x="{sx(STALL[0]):.1f}" y="{T}" width="{sx(STALL[1]) - sx(STALL[0]):.1f}" height="{ph}"/>')
    b.append(text((sx(STALL[0]) + sx(STALL[1])) / 2, T + 16, "通信が止まる", "t2 f12", "middle"))
    b.append(text((sx(STALL[0]) + sx(STALL[1])) / 2, T + 32, "（バッファ中）", "t2 f12", "middle"))
    for v in (0, 1, 2, 3, 4):
        b.append(f'<line class="{"axis" if v == 0 else "grid"}" x1="{L}" x2="{R}" y1="{sy(v):.1f}" y2="{sy(v):.1f}"/>')
        b.append(text(L - 8, sy(v) + 4, f"{v}", "tm f12 num", "end"))
    b.append(f'<line class="thr" x1="{L}" x2="{R}" y1="{sy(0.9):.1f}" y2="{sy(0.9):.1f}"/>')
    b.append(text(L + 6, sy(0.9) - 5, "合わせ直す境目 0.9 秒", "tm f12"))
    for a, z in freezes:
        b.append(f'<rect class="s2" x="{sx(a):.1f}" y="{T + ph - 7}" width="{max(3.0, sx(z) - sx(a)):.1f}" height="6" rx="2"/>')
    pts = " ".join(f"{sx(t):.1f},{sy(min(l, ymax)):.1f}" for i, (t, l) in enumerate(tr) if i % 5 == 0)
    b.append(f'<polyline class="l1" points="{pts}"/>')
    for t, l in seeks:
        b.append(f'<circle class="d2" cx="{sx(t):.1f}" cy="{sy(min(l, ymax)):.1f}" r="6"/>')
    # the checks once a second, under the chart
    cy = T + ph + 26
    b.append(text(L - 8, cy + 4, "確かめ", "tm f12", "end"))
    for t, kind in checks:
        if kind == "seek":
            b.append(f'<circle class="d2" cx="{sx(t):.1f}" cy="{cy}" r="5"/>')
        elif kind == "wait":
            b.append(f'<circle class="h1" cx="{sx(t):.1f}" cy="{cy}" r="4"/>')
        else:
            b.append(f'<line class="tick" x1="{sx(t):.1f}" x2="{sx(t):.1f}" y1="{cy - 5}" y2="{cy + 5}"/>')
    s_t, s_l = seeks[0]
    b.append(text(sx(s_t) + 12, sy(min(s_l, ymax)) + 4, f"動き出してから 1 回だけシーク（{s_l:.1f} 秒のずれ）", "t2 f12"))
    b.append(text(sx(15.5), sy(0.6) - 26, f"そのあとは {SEEK_COST} 秒遅れのまま落ち着く", "t2 f12"))
    b.append(text(sx(15.5), sy(0.6) - 10, "（境目の 0.9 秒より小さいので、直さない）", "t2 f12"))
    for t in range(0, int(END) + 1, 4):
        b.append(text(sx(t), cy + 26, f"{t}", "tm f12 num", "middle"))
    b.append(text((L + R) / 2, cy + 44, "経過時間（秒）", "tm f12", "middle"))
    b.append(text(L - 8, T - 12, "ずれ（秒）", "tm f12", "end"))
    lx, ly = L, H - 8
    b.append(f'<line class="tick" x1="{lx + 4}" x2="{lx + 4}" y1="{ly - 9}" y2="{ly + 1}"/>' + text(lx + 14, ly, "確かめて、そのまま", "t2 f12"))
    b.append(f'<circle class="h1" cx="{lx + 150}" cy="{ly - 4}" r="4"/>' + text(lx + 160, ly, "動画が進んでいないので待つ", "t2 f12"))
    b.append(f'<circle class="d2" cx="{lx + 344}" cy="{ly - 4}" r="5"/>' + text(lx + 355, ly, "シークした", "t2 f12"))
    b.append(f'<rect class="s2" x="{lx + 440}" y="{ly - 7}" width="14" height="6" rx="2"/>' + text(lx + 460, ly, "シークのあとの読み直し", "t2 f12"))
    return svg(W, H, "通信が止まったあとの時刻合わせ（モデル）",
               "通信が止まっているあいだは確かめても待ち、動き出してから1回だけシークする。シークのあとの0.6秒の遅れは境目の0.9秒より小さいので、そのまま落ち着く。", "".join(b))


# Figure 7: when ReSync loads. (a) presses while waiting make one load, 5.1 s after the
# last one; (b) a load under way is left alone for 15 s.
def fig_reload():
    W, H = 820, 400
    L, R = 210, 790
    b = [text(24, 30, "ReSync を押したときに、いつ読み込むか", "t1 f14"),
         text(24, 50, "VRChat は、1 人が新しく読み込めるのを 5 秒に 1 回に限る。KawaPlayer は前の読み込みから 5.1 秒あけ、読み込み中は 15 秒待つ", "t2 f12")]

    # (a) spacing the loads
    xmax = 12.0
    sx = lambda t: L + (R - L) * t / xmax
    top = 74
    b.append(text(24, top + 14, "(a) 読み込みの間隔", "t1 f13"))
    lanes = {"load": top + 50, "press": top + 96}
    for t in range(0, 13):
        b.append(f'<line class="grid" x1="{sx(t):.1f}" x2="{sx(t):.1f}" y1="{top + 28}" y2="{top + 116}"/>')
        if t % 2 == 0:
            b.append(text(sx(t), top + 134, f"{t}", "tm f12 num", "middle"))
    b.append(text(24, lanes["load"] + 4, "読み込み", "t2 f12"))
    b.append(text(24, lanes["press"] + 4, "ReSync を押した", "t2 f12"))
    y = lanes["load"]
    b.append(f'<rect class="w1" x="{sx(0):.1f}" y="{y - 13}" width="{sx(5.1) - sx(0):.1f}" height="26" rx="4"/>')
    b.append(f'<circle class="d1" cx="{sx(0):.1f}" cy="{y}" r="6"/>')
    b.append(text(sx(0) + 10, y - 18, "曲を始めた", "t2 f12"))
    b.append(f'<circle class="d2" cx="{sx(5.1):.1f}" cy="{y}" r="6"/>')
    b.append(text(sx(5.1) + 10, y + 4, "5.1 秒に、1 回だけ読み込む", "t2 f12"))
    yp = lanes["press"]
    for t in (2.5, 3.5, 4.5):
        b.append(f'<line class="press" x1="{sx(t):.1f}" x2="{sx(t):.1f}" y1="{yp - 9}" y2="{yp + 9}"/>')
        b.append(f'<path class="lead" d="M{sx(t):.1f},{yp - 11} Q{(sx(t) + sx(5.1)) / 2:.1f},{yp - 30} {sx(5.1) - 4:.1f},{y + 8}"/>')
    b.append(text(sx(6.0), yp + 4, "待つあいだに何回押しても、1 回にまとめる", "t2 f12"))

    # (b) a load under way
    xmax2 = 20.0
    sx2 = lambda t: L + (R - L) * t / xmax2
    top2 = 236
    b.append(text(24, top2 + 14, "(b) 読み込み中の 15 秒", "t1 f13"))
    lanes2 = {"load": top2 + 50, "btn": top2 + 92}
    for t in range(0, 21, 2):
        b.append(f'<line class="grid" x1="{sx2(t):.1f}" x2="{sx2(t):.1f}" y1="{top2 + 28}" y2="{top2 + 108}"/>')
        b.append(text(sx2(t), top2 + 126, f"{t}", "tm f12 num", "middle"))
    b.append(text(24, lanes2["load"] + 4, "自分の画面", "t2 f12"))
    b.append(text(24, lanes2["btn"] + 4, "ReSync のボタン", "t2 f12"))
    y = lanes2["load"]
    b.append(f'<rect class="w1" x="{sx2(0):.1f}" y="{y - 13}" width="{sx2(17) - sx2(0):.1f}" height="26" rx="4"/>')
    b.append(f'<circle class="d1" cx="{sx2(0):.1f}" cy="{y}" r="6"/>')
    b.append(text(sx2(0) + 10, y + 4, "読み込みを始めたが、進まない", "t2 f12"))
    b.append(f'<circle class="d2" cx="{sx2(17):.1f}" cy="{y}" r="6"/>')
    b.append(text(sx2(17) - 10, y - 18, "押すと、すぐ読み直す", "t2 f12", "end"))
    yb = lanes2["btn"]
    b.append(f'<rect class="wash" x="{sx2(0):.1f}" y="{yb - 11}" width="{sx2(15) - sx2(0):.1f}" height="22" rx="4"/>')
    b.append(text((sx2(0) + sx2(15)) / 2, yb + 4, "押せない（遅いだけの読み込みを最初からにしない）", "t2 f12", "middle"))
    b.append(f'<rect class="w2" x="{sx2(15):.1f}" y="{yb - 11}" width="{sx2(20) - sx2(15):.1f}" height="22" rx="4"/>')
    b.append(text((sx2(15) + sx2(20)) / 2, yb + 4, "押せる", "t2 f12", "middle"))
    b.append(f'<line class="press" x1="{sx2(17):.1f}" x2="{sx2(17):.1f}" y1="{yb - 11}" y2="{y + 8}"/>')
    b.append(text((L + R) / 2, H - 8, "経過時間（秒）", "tm f12", "middle"))
    return svg(W, H, "ReSync を押したときに、いつ読み込むか",
               "前の読み込みから5.1秒たつまでは待ち、そのあいだに押した回数は1回の読み込みにまとめる。読み込み中は、始めてから15秒たつまでボタンを押せない。", "".join(b))


os.makedirs(OUT, exist_ok=True)
for name, f in (("overview.svg", fig_overview), ("position.svg", fig_position), ("check.svg", fig_check),
                ("drift.svg", fig_drift), ("reload.svg", fig_reload)):
    with open(os.path.join(OUT, name), "w", encoding="utf-8", newline="\n") as fh:
        fh.write(f())
    print("wrote", name)
