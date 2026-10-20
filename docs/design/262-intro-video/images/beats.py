"""Draws the shooting script's beat strips (beats-c01.webp, ...) for script.md.

Each strip shows a cut, or a few cuts, as 2-3 panels in the order they happen: the wind-up, the
move, the hold, with the time in the cut and what happens under each panel. Like the storyboard's
frames (frames.py, whose drawing helpers these use), they are sketches with silhouettes: MAO
(teal), RIRIKA (pink, twin tails), the guest (lavender). Nothing filmed is used, as the avatars'
terms do not allow it in this repository. After changing a strip, run:

  python beats.py [c01 ...]

Needs Pillow and the package's Zen Maru Gothic (see frames.py). Panels are drawn at 1280 x 720
and the strip is saved at half that size.
"""
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFilter

import frames as F
from frames import W, H, WHITE, INK, ORANGE, MINT, TEAL, BLUE, A_COL, B_COL, G_COL, NIGHT, DAY, SCREEN

YELLOW = (255, 226, 110)
GAP = 16


# ---------------------------------------------------------------- helpers


def band(im, n, t, s):
    """Under a panel: its number, the time in the cut, and what happens."""
    F.overlay(im, lambda o: o.rectangle((0, H - 92, W, H), fill=(8, 12, 18, 215)))
    d = ImageDraw.Draw(im)
    d.ellipse((24, H - 74, 80, H - 18), fill=TEAL)
    d.text((52, H - 46), str(n), font=F.font(32), fill=WHITE, anchor="mm")
    d.text((100, H - 46), t, font=F.font(32), fill=MINT, anchor="lm")
    tw = d.textlength(t, font=F.font(32))
    d.text((100 + tw + 26, H - 46), s, font=F.font(32), fill=WHITE, anchor="lm")


def letterbox(im, open_=0.0):
    """2.35:1 bars, open_ from 0 (closed to 2.35:1) to 1 (gone)."""
    bar = int(88 * (1 - open_))
    if bar > 0:
        d = ImageDraw.Draw(im)
        d.rectangle((0, 0, W, bar), fill=(0, 0, 0))
        d.rectangle((0, H - bar, W, H), fill=(0, 0, 0))


def star(im, x, y, r=34):
    F.sparkle(im, x, y, r, YELLOW)
    F.sparkle(im, x + r * 1.3, y - r * 0.9, r * 0.45, WHITE)


def burst(im, cx, cy, r0, r1, n=14, col=WHITE, width=6):
    """Surprise lines around a point."""
    d = ImageDraw.Draw(im)
    for k in range(n):
        a = k / n * math.tau + 0.2
        d.line((cx + r0 * math.cos(a), cy + r0 * math.sin(a), cx + r1 * math.cos(a), cy + r1 * math.sin(a)), fill=col, width=width)


def curve_arrow(im, cx, cy, r, a0, a1, col=ORANGE, width=7):
    d = ImageDraw.Draw(im)
    pts = [(cx + r * math.cos(math.radians(a)), cy + r * math.sin(math.radians(a))) for a in range(int(a0), int(a1), 4 if a1 > a0 else -4)]
    d.line(pts, fill=col, width=width, joint="curve")
    F.arrow(im, pts[-3], pts[-1], col=col, width=width, head=24)


def tilted(im, draw_fn, box, angle):
    """Draws something by draw_fn(layer, box) on its own layer and turns it by angle about its centre."""
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw_fn(layer, box)
    cx, cy = (box[0] + box[2]) / 2, (box[1] + box[3]) / 2
    im.alpha_composite(layer.rotate(angle, resample=Image.BICUBIC, center=(cx, cy)))


def tablet_with(src):
    def draw(layer, box):
        scr = F.tablet_frame(layer, box, radius=30)
        F.fit_into(layer, src, scr, bg=(14, 16, 20))
    return draw


def lofi_frame(w=1280, h=720, ui=False):
    """The study clip (よふかしスタディ) as a sketch: MAO at the desk by the night window."""
    v = F.gradient(w, h, [(20, 26, 48), (34, 40, 70), (22, 24, 40)])
    d = ImageDraw.Draw(v)
    d.rectangle((w * 0.16, h * 0.05, w * 0.8, h * 0.62), fill=(26, 40, 82))
    for k in range(70):
        x, y = w * (0.17 + (k * 37 % 63) / 100), h * (0.07 + (k * 29 % 50) / 100)
        d.ellipse((x - 3, y - 3, x + 3, y + 3), fill=(230, 236, 255))
    d.polygon([(w * 0.16, h * 0.62), (w * 0.35, h * 0.4), (w * 0.55, h * 0.55), (w * 0.8, h * 0.38), (w * 0.8, h * 0.62)], fill=(30, 46, 80))
    d.line((w * 0.42, h * 0.05, w * 0.42, h * 0.62), fill=(14, 16, 24), width=int(w * 0.012))
    d.rectangle((0, h * 0.66, w, h), fill=(110, 58, 32))
    d.polygon([(w * 0.02, h * 0.12), (w * 0.14, h * 0.05), (w * 0.17, h * 0.18), (w * 0.06, h * 0.24)], fill=(236, 226, 200))
    F.overlay(v, lambda o: o.ellipse((-w * 0.1, -h * 0.1, w * 0.35, h * 0.55), fill=(255, 200, 130, 40)))
    d.rounded_rectangle((w * 0.06, h * 0.5, w * 0.14, h * 0.66), 6, fill=(60, 150, 150))
    d.rounded_rectangle((w * 0.18, h * 0.58, w * 0.26, h * 0.7), 10, fill=(236, 230, 214))
    d.polygon([(w * 0.32, h * 0.68), (w * 0.5, h * 0.66), (w * 0.52, h * 0.72), (w * 0.33, h * 0.74)], fill=(246, 242, 230))
    F.person(v, w * 0.7, h * 1.25, h * 1.05, A_COL)
    d.ellipse((w * 0.665, h * 0.27, w * 0.69, h * 0.33), fill=(170, 250, 245), outline=WHITE, width=3)
    d.line((w * 0.6, h * 0.55, w * 0.47, h * 0.69), fill=A_COL, width=int(w * 0.03))
    if ui:
        kawaplayer_ui(v, (0, 0, w, h))
    return v


def kawaplayer_ui(im, box):
    """KawaPlayer's UI over a playing video: the title, the seek bar, the list with the first track lit."""
    x0, y0, x1, y1 = box
    w, h = x1 - x0, y1 - y0
    F.overlay(im, lambda o: o.rectangle(box, fill=(0, 0, 0, 90)))
    d = ImageDraw.Draw(im)
    s = w / 1280
    d.text((x0 + 40 * s, y0 + 50 * s), "よふかしスタディ", font=F.font(max(12, int(34 * s))), fill=WHITE)
    by = y0 + h * 0.86
    d.line((x0 + 40 * s, by, x0 + w * 0.6, by), fill=(200, 210, 220), width=max(2, int(5 * s)))
    d.line((x0 + 40 * s, by, x0 + w * 0.22, by), fill=TEAL, width=max(2, int(5 * s)))
    d.ellipse((x0 + w * 0.22 - 9 * s, by - 9 * s, x0 + w * 0.22 + 9 * s, by + 9 * s), fill=TEAL)
    lx = x0 + w * 0.68
    F.overlay(im, lambda o: o.rectangle((lx, y0 + h * 0.08, x1 - 20 * s, y0 + h * 0.8), fill=(10, 14, 20, 160)))
    for k in range(6):
        ry = y0 + h * (0.14 + k * 0.1)
        col = MINT if k == 0 else (200, 206, 214)
        d.line((lx + 30 * s, ry, lx + w * 0.22, ry), fill=col, width=max(2, int(8 * s)))


def strip(name, panels):
    n = len(panels)
    out = Image.new("RGBA", (W * n + GAP * (n - 1), H), (12, 14, 18, 255))
    for k, p in enumerate(panels):
        out.alpha_composite(p, (k * (W + GAP), 0))
    path = os.path.join(F.HERE, "beats-" + name + ".webp")
    out.convert("RGB").resize((out.width // 2, out.height // 2), Image.LANCZOS).save(path, "WEBP", quality=82, method=6)
    print(name, os.path.getsize(path) // 1024, "KB")


def desk(im, x0, x1, y):
    d = ImageDraw.Draw(im)
    d.rectangle((x0, y, x1, y + 22), fill=(120, 70, 40))
    for x in (x0 + 20, x1 - 40):
        d.rectangle((x, y + 22, x + 20, H), fill=(40, 30, 26))


# ---------------------------------------------------------------- the strips


def c01():
    """The opening as filmed (v3.1): one take from her feet, out to her left rear, round to her right side; C02b
    goes on from there to the taps."""
    p1 = F.new(NIGHT["floor"])
    d = ImageDraw.Draw(p1)
    d.rectangle((0, 0, W, 300), fill=NIGHT["wall"])
    for k in range(3):
        x = 220 + k * 330
        F.overlay(p1, lambda o, x=x: o.polygon([(x, 430), (x + 210, 430), (x + 330, 720), (x + 90, 720)], fill=(150, 170, 230, 70)))
    for (x, top, lift) in ((520, -40, 0), (700, -40, 40)):
        d.rounded_rectangle((x - 46, top, x + 46, 560 - lift), 30, fill=(28, 38, 50))
        d.rounded_rectangle((x - 70, 540 - lift, x + 90, 600 - lift), 26, fill=(232, 236, 240))
    F.foliage(p1, [(90, 520, 260, 30), (160, 650, 300, -20)], blur=8)
    letterbox(p1)
    F.note(p1, "床から 10 cm・左前から歩きに合わせて", (W - 20, 104))
    band(p1, 1, "C01 0.0〜1.5", "窓の光の縞を歩く足もと")
    p2 = F.new(NIGHT["wall"])
    F.room(p2)
    desk(p2, 70, 470, 470)
    F.person(p2, 640, 660, 380, A_COL, "MAO")
    curve_arrow(p2, 640, 520, 330, 205, 345)
    F.note(p2, "左後ろへ引き、背中越しを通って右横へ", (W - 20, 104))
    letterbox(p2)
    band(p2, 2, "C01 1.5〜3.1", "引きながら回る（全身と机・窓が見える）")
    p3 = F.new(NIGHT["wall"])
    F.room(p3, window=False)
    d = ImageDraw.Draw(p3)
    d.ellipse((-220, 80, 330, 900), fill=(24, 40, 46))
    d.ellipse((-60, -120, 340, 300), fill=(150, 120, 150))
    box = (430, 180, 1010, 520)
    tilted(p3, lambda layer, b: F.tablet_home(layer, b), box, -8)
    tx, ty = 512, 432
    d.line((330, 640, tx - 6, ty + 18), fill=(40, 52, 60), width=46)
    d.ellipse((tx - 34, ty - 34, tx + 34, ty + 34), outline=(170, 240, 255), width=6)
    F.note(p3, "右横・固定 → 押したあと画面へ寄る", (W - 20, 104))
    letterbox(p3)
    band(p3, 3, "C01 3.4〜 / C02b 1.25", "持ち上げる → 起こす → KawaPlayer を押す")
    return [p1, p2, p3]


def c06():
    p1 = Image.new("RGBA", (W, H))
    p1.alpha_composite(F.gradient(W, H, [(40, 46, 74), (58, 52, 92), (30, 30, 50)]))
    F.person(p1, 820, 1100, 900, A_COL)
    p1 = p1.filter(ImageFilter.GaussianBlur(10))
    d = ImageDraw.Draw(p1)
    d.polygon([(420, H), (560, 330), (640, 310), (690, 360), (600, H)], fill=(238, 214, 200))
    d.ellipse((560, 290, 700, 380), fill=(242, 220, 206))
    F.capsule(p1, 560, 230, "…/r/default/…")
    F.fish(p1, 860, 190, 120, 200)
    F.trail(p1, [(1240, 80), (1050, 140), (900, 190)])
    F.note(p1, "画面いっぱいから肩越しへ引いたあと")
    band(p1, 1, "C06 1.2〜1.85", "指を立てる（沈んで上がり、行き過ぎて戻る）→ 札が指先に")
    p2 = F.new((20, 22, 28))
    box = (140, 60, 1140, 620)
    F.tablet_frame(p2, box, radius=40)
    d = ImageDraw.Draw(p2)
    d.rounded_rectangle((210, 150, 330, 230), 16, fill=(40, 48, 60), outline=WHITE, width=3)
    d.text((270, 190), "URL", font=F.font(34), fill=WHITE, anchor="mm")
    for k in range(5):
        d.line((780, 170 + k * 70, 1060, 170 + k * 70), fill=(150, 160, 170), width=10)
    d.polygon([(250, H), (300, 290), (340, 280), (370, 300), (360, H)], fill=(238, 214, 200))
    d.ellipse((296, 252, 376, 312), fill=(242, 220, 206))
    F.note(p2, "MAO の目線", (620, 250))
    band(p2, 2, "C07 0.85", "URL のボタン（リンクの印）を押す → URL が入る")
    p3 = F.new((20, 22, 28))
    d = ImageDraw.Draw(p3)
    d.rectangle((0, 0, W, H), fill=(26, 30, 38))
    for k in range(5):
        y = 120 + k * 110
        col = TEAL if k == 2 else (60, 66, 78)
        d.rounded_rectangle((180, y, 1100, y + 84), 14, fill=col)
        d.text((220, y + 42), "よるのまったりBGM" if k == 2 else "プレイリスト", font=F.font(36), fill=WHITE, anchor="lm")
    for r in (60, 90, 120):
        d.ellipse((640 - r * 4, 382 - r, 640 + r * 4, 382 + r), outline=(170, 250, 220), width=4)
    F.sfx(p3, (1060, 230), "ポン", 70)
    F.zoom(p3, (180, 150, 1100, 560))
    band(p3, 3, "C07 1.55〜", "「プレイリストを追加しました」→ 一覧にポン")
    return [p1, p2, p3]


def c09():
    p1 = lofi_frame()
    F.note(p1, "スクリーンの正面・画面いっぱい")
    band(p1, 1, "C09 0.0〜0.45", "よふかしスタディの中の MAO から")
    p2 = F.new(NIGHT["wall"])
    F.room(p2, screen=lofi_frame(520, 293, ui=True), glow=True)
    F.zoom(p2, (380, 110, 900, 403), inward=False)
    F.note(p2, "2 人の頭の上を越えて引く・UI は出したまま")
    band(p2, 2, "C09 0.45〜2.7", "KawaPlayer の画面だったと分かる")
    p3 = F.new(NIGHT["wall"])
    F.room(p3, screen=lofi_frame(520, 293, ui=True), glow=True)
    F.person(p3, 330, 760, 360, A_COL, "MAO")
    hb = F.person(p3, 450, 760, 330, B_COL, "RIRIKA", twin=True)
    F.sfx(p3, (420, 330), "ひそひそ", 40, col=WHITE)
    F.note(p3, "2 人は左寄り：映像の MAO を隠さない")
    band(p3, 3, "C09 2.3〜3.5", "RIRIKA が身を寄せてささやき、MAO が応える")
    return [p1, p2, p3]


def c10():
    photo = F.sample_photo_tall()
    land = Image.new("RGBA", (640, 360), (14, 16, 20, 255))
    F.fit_into(land, photo, (0, 0, 640, 360), bg=(14, 16, 20))
    p1 = F.new(NIGHT["wall"])
    F.room(p1)
    p1 = p1.filter(ImageFilter.GaussianBlur(6))
    F.person(p1, 640, 980, 760, B_COL, twin=True)
    tilted(p1, tablet_with(land), (300, 300, 980, 680), 0)
    F.arrow(p1, (640, 700), (640, 640), width=6)
    F.note(p1, "C10a：MAO の目線・広角で少し傾ける")
    band(p1, 1, "C10a", "少し引いてから、MAO の目の前へ突き出す")
    p2 = F.new(NIGHT["wall"])
    F.room(p2)
    p2 = p2.filter(ImageFilter.GaussianBlur(6))
    F.person(p2, 640, 980, 760, B_COL, twin=True)
    tilted(p2, tablet_with(land), (300, 300, 980, 680), 0)
    star(p2, 760, 150, 44)
    F.bubble(p2, 840, 60, "見て見て！", 40, tail=(720, 190))
    F.note(p2, "C10f の笑顔の寄りのあと、C10b の肩越し", (W - 20, 210))
    band(p2, 2, "C10f・C10b", "笑顔 → 胸もとに戻してウインクと星")
    p3 = F.new(NIGHT["wall"])
    F.room(p3)
    p3 = p3.filter(ImageFilter.GaussianBlur(6))
    hx, hy = F.person(p3, 600, 1000, 820, A_COL)
    d = ImageDraw.Draw(p3)
    d.line((760, 560, 820, 330), fill=A_COL, width=36)
    curve_arrow(p3, 820, 290, 60, 200, 520)
    F.bubble(p3, 880, 120, "縦、縦！", 40, tail=(820, 260))
    F.note(p3, "目をぎゅっとつぶって笑いながら")
    band(p3, 3, "C11 1.35", "指をくるくる → 顔の横で 2 回突き上げ、拍で寄る")
    return [p1, p2, p3]


def c12():
    photo = F.sample_photo_tall()
    land = Image.new("RGBA", (640, 360), (14, 16, 20, 255))
    F.fit_into(land, photo, (0, 0, 640, 360), bg=(14, 16, 20))

    def shoulder(im):
        d = ImageDraw.Draw(im)
        d.ellipse((-200, 300, 300, 1100), fill=F.darker(B_COL, 0.75))
        d.ellipse((60, 40, 360, 380), fill=F.darker(B_COL, 0.6))

    p1 = F.new(NIGHT["wall"])
    F.room(p1)
    p1 = p1.filter(ImageFilter.GaussianBlur(5))
    tilted(p1, tablet_with(land), (420, 220, 980, 540), 10)
    shoulder(p1)
    curve_arrow(p1, 700, 380, 300, -60, -20)
    F.note(p1, "溜め：逆へ 10°")
    band(p1, 1, "C12", "回す前に逆へ引く（0.15 秒）")
    p2 = F.new(NIGHT["wall"])
    F.room(p2)
    p2 = p2.filter(ImageFilter.GaussianBlur(5))
    tilted(p2, tablet_with(land), (420, 220, 980, 540), -8)
    shoulder(p2)
    curve_arrow(p2, 700, 380, 300, -10, -40)
    F.note(p2, "8° 行き過ぎて、0.3 秒で戻る")
    band(p2, 2, "C12", "額縁を返すように自分へ向ける")
    p3 = F.new(NIGHT["wall"])
    F.room(p3)
    p3 = p3.filter(ImageFilter.GaussianBlur(5))

    def portrait(layer, box):
        scr = F.tablet_frame(layer, box, radius=30, portrait=True)
        F.fit_into(layer, photo, scr, bg=(14, 16, 20))

    tilted(p3, portrait, (560, 90, 840, 600), 0)
    shoulder(p3)
    F.sweat(p3, 380, 120, 20)
    F.note(p3, "手のひらは縁、指は画面の裏、親指は表の縁")
    band(p3, 3, "C12 1.5〜", "汗 → 縦に回す → 写真が大きく")
    return [p1, p2, p3]


def c13():
    """C13 as filmed (v3.1): one take over RIRIKA's right shoulder, closing in on the tablet first; she taps the
    picture and "KawaPlayer に送る", and the camera pulls back round to the screen for MAO's double take."""
    photo = F.sample_photo_tall()
    shot = Image.new("RGBA", (520, 293), (14, 16, 20, 255))
    F.fit_into(shot, photo, (0, 0, 520, 293), bg=(14, 16, 20))

    def tablet(im, box):
        scr = F.tablet_frame(im, box, radius=30, portrait=True)
        F.fit_into(im, photo, scr, bg=(14, 16, 20))
        d = ImageDraw.Draw(im)
        bx = (scr[2] - 210, scr[1] + 14, scr[2] - 14, scr[1] + 62)
        d.rounded_rectangle(bx, 16, fill=(16, 20, 26), outline=(200, 206, 214), width=2)
        d.text(((bx[0] + bx[2]) / 2, (bx[1] + bx[3]) / 2), "KawaPlayer に送る", font=F.font(22), fill=WHITE, anchor="mm")
        return scr, bx

    p1 = F.new(NIGHT["wall"])
    F.room(p1, window=False)
    tablet(p1, (560, 110, 860, 640))
    d = ImageDraw.Draw(p1)
    d.ellipse((60, 40, 470, 520), fill=(214, 186, 170))
    for x in (110, 410):
        d.rounded_rectangle((x - 50, 260, x + 50, 720), 46, fill=(214, 186, 170))
    d.rounded_rectangle((330, 520, 640, 720), 80, fill=B_COL)
    F.zoom(p1, (500, 80, 920, 680))
    F.note(p1, "背中越し・まずタブレットへ寄る")
    band(p1, 1, "C13 0.0〜0.55", "背中越しにタブレットへ寄る")
    p2 = F.new((20, 22, 28))
    scr, bx = tablet(p2, (380, 30, 900, 660))
    d = ImageDraw.Draw(p2)
    fx, fy = bx[0] + 60, (bx[1] + bx[3]) / 2
    d.line((1060, H + 40, 960, 520, fx + 30, fy + 40), fill=B_COL, width=70, joint="curve")
    d.line((fx + 30, fy + 40, fx, fy), fill=(240, 214, 200), width=22)
    d.ellipse((fx - 30, fy - 30, fx + 30, fy + 30), outline=(170, 240, 255), width=6)
    F.note(p2, "押す腕は画面の右の縁に沿わせる")
    band(p2, 2, "C13 0.9〜1.35", "写真をタップ → KawaPlayer に送る")
    p3 = F.new(NIGHT["wall"])
    F.room(p3, screen=shot, glow=True)
    p3 = p3.filter(ImageFilter.GaussianBlur(4))
    hx, hy = F.person(p3, 600, 1180, 980, A_COL)
    burst(p3, hx, hy, 170, 260)
    F.bubble(p3, 820, 90, "おおー！", 44, tail=(hx + 90, hy - 60))
    F.zoom(p3, (200, 60, 1080, 560))
    F.note(p3, "引きながらスクリーンへ回る → 二度見で寄る")
    band(p3, 3, "C13 1.55〜3.05", "スクリーンに写真、MAO の二度見")
    return [p1, p2, p3]


def c14():
    def scene(rh, rbase, arms, guest=True):
        im = F.new(NIGHT["wall"])
        F.room(im, door=True)
        if guest:
            F.join_ring(im, 1130, 380, 70, 150)
            F.person(im, 1130, 560, 330, G_COL, "ゲスト")
        F.person(im, 380, 700, 400, A_COL, "MAO", arm="wave")
        F.person(im, 680, rbase, rh, B_COL, "RIRIKA", twin=True, arm=arms)
        return im

    p1 = scene(300, 700, None)
    F.note(p1, "固定・1 秒に 1.5% 寄る")
    band(p1, 1, "C14", "ゲストが入る。RIRIKA がしゃがんで溜め、MAO は手を振る")
    p2 = scene(380, 600, "both")
    d = ImageDraw.Draw(p2)
    for k in range(3):
        d.line((620 + k * 60, 640, 620 + k * 60, 700), fill=WHITE, width=5)
    F.bubble(p2, 760, 120, "いらっしゃーい！", 40, tail=(700, 240))
    band(p2, 2, "C14", "両手を上げて跳ぶ（頂点で目をぎゅっと）")
    p3 = scene(350, 700, "both")
    d = ImageDraw.Draw(p3)
    d.arc((560, 660, 800, 740), 200, 340, fill=WHITE, width=5)
    F.note(p3, "着地で沈んで、もう 1 回小さく弾む")
    band(p3, 3, "C14", "着地 → 小さく弾む → 落ち着く")
    return [p1, p2, p3]


def c16():
    p1 = F.new((40, 32, 30))
    d = ImageDraw.Draw(p1)
    d.polygon([(0, 250), (W, 250), (W, H), (0, H)], fill=(150, 120, 110))
    # RIRIKA across the tablet, on her elbows, her feet up behind her and kicking in turn
    for x0, x1 in ((610, 560), (670, 735)):
        d.line((x0, 250, x1, 120), fill=F.darker(B_COL, 0.7), width=30)
    d.arc((520, 80, 600, 160), 200, 320, fill=ORANGE, width=5)
    d.arc((700, 80, 780, 160), 220, 340, fill=ORANGE, width=5)
    d.rounded_rectangle((520, 250, 760, 340), 46, fill=F.darker(B_COL, 0.8))
    d.ellipse((570, 150, 710, 280), fill=B_COL)
    # the tablet on the carpet between them
    d.polygon([(500, 380), (780, 380), (830, 470), (450, 470)], fill=(14, 16, 20), outline=(130, 136, 144))
    # MAO from behind, in the foreground
    d.rounded_rectangle((180, 560, 840, 800), 120, fill=F.darker(A_COL, 0.7))
    d.ellipse((390, 420, 630, 660), fill=F.darker(A_COL, 0.55))
    F.zoom(p1, (420, 340, 860, 500), inward=False)
    F.note(p1, "タブレットから MAO の背中越しへ引く")
    band(p1, 1, "C16", "寝そべってのぞく・RIRIKA の足がパタパタ")
    p2 = F.new((20, 22, 28))
    box = (240, 80, 1040, 530)
    F.tablet_home(p2, box)
    F.note(p2, "真上から")
    d = ImageDraw.Draw(p2)
    for k, s in enumerate(("ホーム", "設定", "再生")):
        x = 330 + k * 230
        d.rounded_rectangle((x, 560, x + 190, 610), 14, fill=TEAL if k == 2 else (60, 66, 78))
        d.text((x + 95, 585), s, font=F.font(28), fill=WHITE, anchor="mm")
        if k < 2:
            F.arrow(p2, (x + 196, 585), (x + 226, 585), width=5, head=16)
    band(p2, 2, "C17 0.65・1.45・2.25", "ホーム → 設定 → 再生")
    return [p1, p2]


def c19():
    p1 = F.new(NIGHT["wall"])
    F.room(p1)
    F.person(p1, 520, 700, 380, A_COL)
    F.person(p1, 780, 700, 350, B_COL, twin=True)

    def dots(o):
        for k in range(420):
            x = (k * 151) % W
            y = 80 + (k * 97) % 560
            r = 4 + (k % 5) * 3
            o.ellipse((x - r, y - r, x + r, y + r), fill=(120, 200, 255, 150))
    F.overlay(p1, dots)
    F.note(p1, "光の粒がレンズの前を上る → 白い光で朝へ")
    band(p1, 1, "C19", "頭から粒子になって消える → 光の粒のワイプ")
    day = F.sample_video(520, 293)

    def morning(look, joy=False):
        im = F.new(DAY["wall"])
        F.room(im, mode=DAY, screen=day, glow=True)
        hx, hy = F.person(im, 500, 760, 380, A_COL, "MAO")
        rx, ry = F.person(im, 790, 740 if joy else 760, 350, B_COL, "RIRIKA", twin=True, arm="both" if joy else None)
        return im, (hx, hy), (rx, ry)

    p2, _, (rx, ry) = morning("screen")
    F.arrow(p2, (rx, ry - 40), (rx - 60, ry - 200))
    F.arrow(p2, (rx - 40, ry + 20), (rx - 200, ry + 30))
    F.note(p2, "スクリーン → MAO → スクリーンへ戻る")
    band(p2, 2, "C21 0.0〜0.95", "RIRIKA の二度見")
    p3, (hx, hy), _ = morning("screen", joy=True)
    d = ImageDraw.Draw(p3)
    d.rounded_rectangle((hx - 80, hy + 90, hx + 80, hy + 130), 18, fill=F.darker(A_COL, 0.85))
    star(p3, hx + 90, hy - 80, 36)
    F.bubble(p3, 840, 80, "もう流れてる！", 40, tail=(810, 300))
    band(p3, 3, "C21 1.0〜", "跳んで喜ぶ、MAO は腰に手を当てて胸を張る")
    return [p1, p2, p3]


def c22():
    """C22 as filmed (v3.1): the two on the sofa from behind; they turn round from the hips, MAO with a peace sign
    and RIRIKA waving, with sparkles in their colours; the iris closes on them and opens on the end card's logo."""
    p1 = F.new(NIGHT["wall"])
    F.room(p1, screen=F.sample_video(520, 293), glow=True)
    F.person(p1, 540, 720, 280, (46, 34, 40), twin=True)
    F.person(p1, 720, 720, 300, (30, 40, 46))
    F.sofa(p1)
    F.note(p1, "ソファの後ろ・ゆっくり引いて止まる")
    band(p1, 1, "C22 0.0〜1.9", "2 人でスクリーンを観る")
    p2 = F.new(NIGHT["wall"])
    F.room(p2, screen=F.sample_video(520, 293), glow=True)
    mx, my = F.person(p2, 720, 720, 300, A_COL, "MAO", arm="wave")
    rx, ry = F.person(p2, 540, 720, 280, B_COL, "RIRIKA", twin=True, arm="wave")
    F.sofa(p2)
    for (cx, cy, col) in ((mx, my, (150, 236, 236)), (rx, ry, (255, 186, 214))):
        for k in range(7):
            a = math.radians(200 + k * 24)
            F.sparkle(p2, cx + 92 * math.cos(a), cy - 20 + 92 * math.sin(a), 12 + (k % 3) * 6, col)
    F.telop(p2, "URL ひとつで、みんなの上映会。", 44, y=H - 140)
    F.note(p2, "腰から振り向く・キラキラと専用の効果音")
    band(p2, 2, "C22 1.9〜2.9", "振り向いて、ピースと手を振る")
    p3 = F.new((0, 0, 0))
    F.overlay(p3, lambda o: o.ellipse((340, 40, 940, 600), fill=(255, 255, 255, 255)))
    p3c = F.brand_background()
    mask = Image.new("L", (W, H), 0)
    ImageDraw.Draw(mask).ellipse((340, 40, 940, 600), fill=255)
    p3.paste(p3c, (0, 0), mask)
    F.place(p3, F.logo(260, WHITE), (510, 190, 770, 450))
    F.note(p3, "2 人を中心に閉じ、穴からロゴが開く")
    band(p3, 3, "C22 3.15〜 / C23", "アイリスで終わりの画面へ")
    return [p1, p2, p3]


STRIPS = {name: fn for name, fn in globals().items() if name[:1] == "c" and name[1:].isdigit()}


def main(names):
    for name in names or sorted(STRIPS):
        strip(name, STRIPS[name]())


if __name__ == "__main__":
    main(sys.argv[1:])
