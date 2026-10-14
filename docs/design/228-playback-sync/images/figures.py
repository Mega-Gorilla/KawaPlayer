"""Draws the SVG figures of docs/design/228-playback-sync/README.md (figures 7, 8 and 11).

  python figures.py            # writes the SVGs next to this script

Figure 8 comes from the small model below, not from a measurement: one viewer,
a network stall, and a fixed re-buffer after every seek. The checks copy the
rules of KawaPlayer (Controller.Update, EnsureVideoTime) and of VVMW
(Core_Timing._AutoSyncTime, SyncTime). Each SVG carries its own light and dark
colours (prefers-color-scheme), so it reads on GitHub in either theme.
"""
import os
import sys

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.dirname(os.path.abspath(__file__))

# The model: a 4 s network stall, and every seek costing a re-buffer of SEEK_COST seconds.
STALL = (8.0, 12.0)
SEEK_COST = 0.6
DT = 0.01
END = 30.0

def run(kind):
    pos, frozen_until, t = 0.0, 0.0, 0.0
    last_sync = 0.0                      # KawaPlayer: _lastSync
    next_check = 0.5                     # VVMW: _AutoSyncTime every 0.5 s
    last_raw, buffering = 0.0, False     # VVMW: lastSyncRawTime, isBuffering
    trace, seeks, freezes = [], [], []
    while t <= END + 1e-9:
        if kind == "kawa":
            if t - last_sync > 5.0:
                if abs(t - pos) >= 0.3:
                    seeks.append((t, t - pos)); pos = t
                    freezes.append((t, max(frozen_until, t + SEEK_COST)))
                    frozen_until = max(frozen_until, t + SEEK_COST)
                last_sync = t
        else:
            if t >= next_check - 1e-9:
                next_check += 0.5
                target, t2 = t, pos
                forced = False
                if abs(t2 - last_raw) > 1e-9:
                    if buffering:
                        catch = t2 - last_raw
                        if catch > 0.9: target += catch
                        buffering = False
                    last_raw = t2
                    forced = abs(t2 - target) >= 0.9
                else:
                    buffering = True
                if forced:
                    seeks.append((t, t - pos)); pos = target; last_raw = pos; buffering = False
                    freezes.append((t, max(frozen_until, t + SEEK_COST)))
                    frozen_until = max(frozen_until, t + SEEK_COST)
        stalled = STALL[0] <= t < STALL[1] or t < frozen_until
        trace.append((t, t - pos))
        if not stalled: pos += DT
        t = round(t + DT, 10)
    return trace, seeks, freezes


STYLE = """<style>
svg{--surface:#fcfcfb;--ink:#0b0b0b;--ink2:#52514e;--muted:#898781;--grid:#e1e0d9;--axis:#c3c2b7;--s1:#2a78d6;--s2:#eb6834;--wash:#f0efec;
font-family:system-ui,-apple-system,"Segoe UI","Yu Gothic UI","Hiragino Sans","Noto Sans JP",Meiryo,sans-serif}
@media (prefers-color-scheme:dark){svg{--surface:#1a1a19;--ink:#f0efec;--ink2:#c3c2b7;--muted:#898781;--grid:#2c2c2a;--axis:#383835;--s1:#3987e5;--s2:#d95926;--wash:#383835}}
.bg{fill:var(--surface)} .t1{fill:var(--ink)} .t2{fill:var(--ink2)} .tm{fill:var(--muted)}
.grid{stroke:var(--grid);stroke-width:1} .axis{stroke:var(--axis);stroke-width:1}
.l1{fill:none;stroke:var(--s1);stroke-width:2;stroke-linejoin:round;stroke-linecap:round}
.l2{fill:none;stroke:var(--s2);stroke-width:2;stroke-linejoin:round;stroke-linecap:round}
.d1{fill:var(--s1);stroke:var(--surface);stroke-width:2} .d2{fill:var(--s2);stroke:var(--surface);stroke-width:2}
.h1{fill:var(--surface);stroke:var(--s1);stroke-width:2} .h2{fill:var(--surface);stroke:var(--s2);stroke-width:2}
.x1{stroke:var(--s1);stroke-width:2;stroke-linecap:round}
.s1{fill:var(--s1)} .s2{fill:var(--s2)} .w1{fill:var(--s1);fill-opacity:.12} .w2{fill:var(--s2);fill-opacity:.12} .wash{fill:var(--wash)}
.thr{stroke:var(--muted);stroke-width:1} .lead{stroke:var(--muted);stroke-width:1;fill:none}
.press{stroke:var(--ink2);stroke-width:2;stroke-linecap:round}
.f12{font-size:12px} .f13{font-size:13px} .f14{font-size:14px;font-weight:600} .num{font-variant-numeric:tabular-nums}
</style>"""


def svg(w, h, title, desc, body):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w} {h}" width="{w}" height="{h}" role="img" aria-labelledby="t d">'
            f'<title id="t">{title}</title><desc id="d">{desc}</desc>{STYLE}'
            f'<rect class="bg" x="0" y="0" width="{w}" height="{h}" rx="8"/>{body}</svg>\n')


def text(x, y, s, cls="t2 f12", anchor="start", rotate=None):
    tr = f' transform="rotate({rotate} {x:.1f} {y:.1f})"' if rotate is not None else ""
    return f'<text x="{x:.1f}" y="{y:.1f}" class="{cls}" text-anchor="{anchor}"{tr}>{s}</text>'


# Figure 7: the time-correction model, as two panels sharing the time axis.
def fig_drift():
    W, H = 820, 500
    L, R = 84, 790
    ph = 140
    ymax, xmax = 4.5, 30.0
    panels = [
        ("kawa", "l1", "d1", "w1", 0.3, "KawaPlayer（5 秒ごとに確かめ、0.3 秒以上ずれたらシーク）", 72),
        ("vvmw", "l2", "d2", "w2", 0.9, "VVMW（0.5 秒ごとに確かめ、0.9 秒以上でシーク。バッファ中はしない）", 270),
    ]
    sx = lambda t: L + (R - L) * t / xmax
    b = [text(24, 30, "バッファのあとの時刻合わせ（モデル。実測ではない）", "t1 f14"),
         text(24, 50, f"通信が 8〜12 秒で止まり、シーク 1 回ごとに {SEEK_COST} 秒の読み直しがかかると仮定した、1 人の見る人のずれ", "t2 f12")]
    for kind, lc, dc, wc, thr, title, top in panels:
        tr, seeks, freezes = run(kind)
        y0 = top + 24
        sy = lambda v, y0=y0: y0 + ph - ph * v / ymax
        b.append(text(L, top + 12, title, "t1 f13"))
        b.append(f'<rect class="wash" x="{sx(STALL[0]):.1f}" y="{y0}" width="{sx(STALL[1]) - sx(STALL[0]):.1f}" height="{ph}"/>')
        for a, z in freezes:
            b.append(f'<rect class="{lc.replace("l", "s")}" x="{sx(a):.1f}" y="{y0 + ph - 7}" width="{max(3.0, sx(z) - sx(a)):.1f}" height="6" rx="2"/>')
        for v in (0, 1, 2, 3, 4):
            y = sy(v)
            b.append(f'<line class="{"axis" if v == 0 else "grid"}" x1="{L}" x2="{R}" y1="{y:.1f}" y2="{y:.1f}"/>')
            b.append(text(L - 8, y + 4, f"{v}", "tm f12 num", "end"))
        y = sy(thr)
        b.append(f'<line class="thr" x1="{L}" x2="{R}" y1="{y:.1f}" y2="{y:.1f}"/>')
        b.append(text(L + 6, y - 5, f"シークする境目 {thr} 秒", "tm f12"))
        pts = " ".join(f"{sx(t):.1f},{sy(min(l, ymax)):.1f}" for i, (t, l) in enumerate(tr) if i % 5 == 0)
        b.append(f'<polyline class="{lc}" points="{pts}"/>')
        for t, l in seeks:
            b.append(f'<circle class="{dc}" cx="{sx(t):.1f}" cy="{sy(min(l, ymax)):.1f}" r="5"/>')
        if kind == "kawa":
            b.append(text(sx(15.6), sy(3.6), "シークのたびに 0.6 秒止まり、また 0.6 秒遅れる", "t2 f12"))
            b.append(text(sx(15.6), sy(3.6) + 16, "→ 5 秒ごとに止まる繰り返し", "t2 f12"))
        else:
            b.append(text(sx(15.6), sy(3.6), "バッファ中は待ち、明けてから 1 回だけシーク", "t2 f12"))
            b.append(text(sx(15.6), sy(3.6) + 16, "→ 0.6 秒遅れのまま落ち着く（境目の 0.9 秒より小さい）", "t2 f12"))
        b.append(text(L - 8, y0 - 2, "ずれ（秒）", "tm f12", "end"))
    base = 270 + 24 + ph
    for t in range(0, 31, 5):
        b.append(text(sx(t), base + 18, f"{t}", "tm f12 num", "middle"))
    b.append(text((L + R) / 2, base + 38, "経過時間（秒）", "tm f12", "middle"))
    lx, ly = L, H - 12
    b.append(f'<rect class="wash" x="{lx}" y="{ly - 10}" width="14" height="12"/>' + text(lx + 20, ly, "通信が止まっている", "t2 f12"))
    b.append(f'<circle class="d1" cx="{lx + 160}" cy="{ly - 4}" r="5"/><circle class="d2" cx="{lx + 174}" cy="{ly - 4}" r="5"/>' + text(lx + 186, ly, "シークした時点", "t2 f12"))
    b.append(f'<rect class="s1" x="{lx + 300}" y="{ly - 7}" width="7" height="6" rx="2"/><rect class="s2" x="{lx + 307}" y="{ly - 7}" width="7" height="6" rx="2"/>' + text(lx + 320, ly, "シークのあとの読み直し（止まっている）", "t2 f12"))
    return svg(W, H, "バッファのあとの時刻合わせ（モデル）",
               "通信が4秒止まり、シークごとに0.6秒の読み直しがかかる仮定で、KawaPlayerは5秒ごとにシークを繰り返し、VVMWは1回のシークで0.6秒遅れのまま落ち着く。", "".join(b))


# Figure 6: three presses of reload, with and without spacing the loads.
def fig_ratelimit():
    W, H = 820, 310
    L, R = 200, 790
    xmax = 12.0
    sx = lambda t: L + (R - L) * t / xmax
    lanes = {"press": 100, "none": 162, "with": 230}
    b = [text(24, 30, "読み込みの間引き（読み込み直しを 1.5 秒おきに 3 回押した場合）", "t1 f14"),
         text(24, 50, "VRChat は、1 人が新しい URL を読めるのを、すべての動画プレイヤーを合わせて 5 秒に 1 回に限る", "t2 f12")]
    for t in range(0, 13):
        b.append(f'<line class="grid" x1="{sx(t):.1f}" x2="{sx(t):.1f}" y1="80" y2="262"/>')
        if t % 2 == 0:
            b.append(text(sx(t), 280, f"{t}", "tm f12 num", "middle"))
    b.append(text((L + R) / 2, 300, "経過時間（秒）", "tm f12", "middle"))
    b.append(text(24, lanes["press"] + 4, "押した時刻", "t1 f13"))
    b.append(text(24, lanes["none"] - 2, "間引きなし（仮定）", "t1 f13"))
    b.append(text(24, lanes["none"] + 14, "#215 で入口を緩めただけ", "t2 f12"))
    b.append(text(24, lanes["with"] - 2, "間引きあり", "t1 f13"))
    b.append(text(24, lanes["with"] + 14, "VVMW の RateLimitResolver", "t2 f12"))
    for t in (0, 1.5, 3.0):
        b.append(f'<line class="press" x1="{sx(t):.1f}" x2="{sx(t):.1f}" y1="{lanes["press"] - 9}" y2="{lanes["press"] + 9}"/>')
    y = lanes["none"]
    b.append(f'<rect class="w1" x="{sx(0):.1f}" y="{y - 14}" width="{sx(5) - sx(0):.1f}" height="28" rx="4"/>')
    b.append(f'<circle class="d1" cx="{sx(0):.1f}" cy="{y}" r="6"/>')
    for t in (1.5, 3.0):
        b.append(f'<line class="x1" x1="{sx(t) - 6:.1f}" y1="{y - 6}" x2="{sx(t) + 6:.1f}" y2="{y + 6}"/>')
        b.append(f'<line class="x1" x1="{sx(t) - 6:.1f}" y1="{y + 6}" x2="{sx(t) + 6:.1f}" y2="{y - 6}"/>')
    b.append(text(sx(5) + 10, y - 2, "2 回目と 3 回目は断られる（RateLimited）", "t2 f12"))
    b.append(text(sx(5) + 10, y + 14, "読めるのはリトライのあと（断られた回数しだい）", "t2 f12"))
    y = lanes["with"]
    b.append(f'<rect class="w2" x="{sx(0):.1f}" y="{y - 14}" width="{sx(5.05) - sx(0):.1f}" height="28" rx="4"/>')
    b.append(f'<rect class="w2" x="{sx(5.05) + 2:.1f}" y="{y - 14}" width="{sx(10.1) - sx(5.05) - 2:.1f}" height="28" rx="4"/>')
    b.append(f'<circle class="d2" cx="{sx(0):.1f}" cy="{y}" r="6"/>')
    for t in (1.5, 3.0):
        b.append(f'<circle class="h2" cx="{sx(t):.1f}" cy="{y}" r="5"/>')
        b.append(f'<path class="lead" d="M{sx(t) + 6:.1f},{y - 5} Q{(sx(t) + sx(5.05)) / 2:.1f},{y - 30} {sx(5.05) - 7:.1f},{y - 5}"/>')
    b.append(f'<circle class="d2" cx="{sx(5.05):.1f}" cy="{y}" r="6"/>')
    b.append(text(sx(5.05) + 10, y + 30, "5.05 秒に 1 回だけ読む（最後に頼まれた URL）", "t2 f12"))
    b.append(text(sx(1.5), y + 30, "待つ", "t2 f12", "middle"))
    lx, ly = L, 80 - 14
    b.append(f'<rect class="w1" x="{lx}" y="{ly - 10}" width="7" height="12"/><rect class="w2" x="{lx + 7}" y="{ly - 10}" width="7" height="12"/>' + text(lx + 20, ly, "次の URL を読めない 5 秒", "t2 f12"))
    b.append(f'<circle class="d1" cx="{lx + 196}" cy="{ly - 4}" r="5"/><circle class="d2" cx="{lx + 210}" cy="{ly - 4}" r="5"/>' + text(lx + 222, ly, "読む", "t2 f12"))
    b.append(f'<line class="x1" x1="{lx + 262}" y1="{ly - 9}" x2="{lx + 272}" y2="{ly + 1}"/><line class="x1" x1="{lx + 262}" y1="{ly + 1}" x2="{lx + 272}" y2="{ly - 9}"/>' + text(lx + 280, ly, "断られる", "t2 f12"))
    b.append(f'<circle class="h2" cx="{lx + 346}" cy="{ly - 4}" r="5"/>' + text(lx + 358, ly, "頼んで待つ", "t2 f12"))
    return svg(W, H, "読み込みの間引き",
               "間引きがないと、5秒以内の2回目と3回目の読み込みはVRChatに断られる。VVMWの方式では、2回目と3回目の頼みは5.05秒の枠の1回にまとまる。", "".join(b))


# Figure 11: the problems placed by impact and difficulty (three-step estimates).
def fig_priority():
    W, H = 820, 492
    L, R, T, B = 110, 790, 86, 416
    cw, chh = (R - L) / 3, (B - T) / 3
    cx = lambda i: L + cw * (i + 0.5)
    cy = lambda j: B - chh * (j + 0.5)
    b = [text(24, 30, "問題ごとの影響と、直す難しさ（目安）", "t1 f14"),
         text(24, 50, "どちらも 3 段階の目安。左上（影響が大きく、手間が小さい）ほど先に直す価値が高い", "t2 f12")]
    for i in range(4):
        x = L + cw * i
        y = T + chh * i
        b.append(f'<line class="grid" x1="{x:.1f}" x2="{x:.1f}" y1="{T}" y2="{B}"/>')
        b.append(f'<line class="grid" x1="{L}" x2="{R}" y1="{y:.1f}" y2="{y:.1f}"/>')
    b.append(f'<line class="axis" x1="{L}" x2="{R}" y1="{B}" y2="{B}"/><line class="axis" x1="{L}" x2="{L}" y1="{T}" y2="{B}"/>')
    for i, s in enumerate(("小", "中", "大")):
        b.append(text(cx(i), B + 20, s, "tm f12", "middle"))
        b.append(text(L - 12, cy(i) + 4, s, "tm f12", "end"))
    b.append(text((L + R) / 2, B + 42, "直す難しさ（実装と確認）", "tm f12", "middle"))
    b.append(text(L - 12, T - 10, "影響", "tm f12", "end"))
    pts = [  # id, label, difficulty, impact, dx, dy, group (1: confirmed in code, 2: needs a repro)
        ("P1", "自分だけ止まって戻せない", 1, 2, -55, -16, 1),
        ("P7", "オーナー交代で先頭に戻る", 1, 2, 55, 18, 2),
        ("P4", "読み込み直しに権限が要る", 0, 1, -40, -16, 1),
        ("P5", "間引きがリトライだけ", 0, 1, 40, 18, 1),
        ("P8", "全員を読み直す手段がない", 1, 1, 0, 0, 1),
        ("P6", "バッファ中の時刻合わせ", 2, 1, 0, 0, 2),
        ("P2", "押せるのに何も起きない", 0, 0, -40, -16, 1),
        ("P3", "オーナーの読み直しが送る", 0, 0, 40, 18, 1),
    ]
    for pid, label, d, im, dx, dy, g in pts:
        x, y = cx(d) + dx, cy(im) + dy
        b.append(f'<circle class="{"d1" if g == 1 else "d2"}" cx="{x:.1f}" cy="{y:.1f}" r="7"/>')
        b.append(text(x, y - 13, pid, "t1 f12", "middle"))
        b.append(text(x, y + 23, label, "t2 f12", "middle"))
    lx, ly = L, H - 12
    b.append(f'<circle class="d1" cx="{lx + 6}" cy="{ly - 4}" r="6"/>' + text(lx + 18, ly, "コードで確かめた", "t2 f12"))
    b.append(f'<circle class="d2" cx="{lx + 170}" cy="{ly - 4}" r="6"/>' + text(lx + 182, ly, "実機での再現が要る（位置は仮）", "t2 f12"))
    return svg(W, H, "問題ごとの影響と直す難しさ",
               "P1は影響大・難しさ中、P7は影響大・難しさ中で要再現、P4とP5は影響中・難しさ小、P8は影響中・難しさ中、P6は影響中・難しさ大で要再現、P2とP3は影響小・難しさ小。", "".join(b))


os.makedirs(OUT, exist_ok=True)
for name, f in (("rate-limit.svg", fig_ratelimit), ("drift-model.svg", fig_drift), ("priority.svg", fig_priority)):
    with open(os.path.join(OUT, name), "w", encoding="utf-8", newline="\n") as fh:
        fh.write(f())
    print("wrote", name)
