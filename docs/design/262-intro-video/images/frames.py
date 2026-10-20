"""Draws the storyboard's frames (c01.webp, c02.webp, ...) for README.md.

The frames are sketches. The people are silhouettes: MAO (teal), who runs the player,
RIRIKA (pink, with twin tails), the friend who watches with her, and the guest
(lavender) who joins the world. What KawaPlayer shows is cut from
the guide's own pictures (docs/guide/images/original/), and the videos and pictures
on its screen are drawn here. Frames taken in Unity are not put in this repository:
they show the avatars, whose terms do not allow it. After changing a cut, run:

  python frames.py [c01 ...]

Needs Pillow. The font is the package's own Zen Maru Gothic (Assets/Fonts). Without
names, every frame is drawn again. The frames are 640 x 360 WebP, drawn at twice that
size and reduced, so their edges are smooth.
"""
import math
import os
import random
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
GUIDE = os.path.join(ROOT, "docs", "guide", "images", "original")
FONT = os.path.join(ROOT, "Assets", "Fonts", "ZenMaruGothic-Regular.ttf")
LOGO = os.path.join(ROOT, "Assets", "Images", "kawaplayer-logo.png")

W, H = 1280, 720
OUT = (640, 360)

WHITE = (255, 255, 255)
INK = (28, 32, 38)
ORANGE = (255, 102, 0)
MINT = (107, 235, 170)
TEAL = (63, 196, 198)
BLUE = (34, 166, 203)
A_COL = (46, 170, 180)  # MAO
B_COL = (226, 120, 166)  # RIRIKA
G_COL = (150, 132, 226)  # The guest who joins
NIGHT = dict(wall=(36, 42, 52), floor=(26, 30, 38), line=(70, 80, 94), sofa=(52, 58, 70))
DAY = dict(wall=(214, 220, 226), floor=(186, 192, 198), line=(150, 158, 166), sofa=(150, 156, 166))
SCREEN = (380, 110, 900, 403)  # The wall screen, 16:9.

_fonts = {}


def font(size):
    if size not in _fonts:
        _fonts[size] = ImageFont.truetype(FONT, size)
    return _fonts[size]


def new(bg):
    return Image.new("RGBA", (W, H), bg + (255,))


def overlay(im, draw):
    """Draws with transparency: draw(d) paints on a clear layer laid over im."""
    layer = Image.new("RGBA", im.size, (0, 0, 0, 0))
    draw(ImageDraw.Draw(layer))
    im.alpha_composite(layer)


def guide(rel, crop=None):
    src = Image.open(os.path.join(GUIDE, rel)).convert("RGBA")
    return src.crop(crop) if crop else src


def place(im, src, box, radius=0):
    x0, y0, x1, y1 = [int(v) for v in box]
    src = src.resize((x1 - x0, y1 - y0), Image.LANCZOS)
    if radius:
        mask = Image.new("L", src.size, 0)
        ImageDraw.Draw(mask).rounded_rectangle((0, 0, src.width - 1, src.height - 1), radius, fill=255)
        alpha = Image.new("L", src.size, 0)
        alpha.paste(src.getchannel("A"), mask=mask)
        src.putalpha(alpha)
    im.alpha_composite(src, (x0, y0))


def box_at(src, x, y, w=None, h=None):
    """The box for src placed at (x, y), w wide or h high, keeping its shape."""
    if w is None:
        w = h * src.width / src.height
    else:
        h = w * src.height / src.width
    return (x, y, x + w, y + h)


def text(im, xy, s, size, fill=WHITE, anchor="la", stroke=0, sfill=INK):
    ImageDraw.Draw(im).text(xy, s, font=font(size), fill=fill, anchor=anchor,
                            stroke_width=stroke, stroke_fill=sfill)


def gradient(w, h, stops, horizontal=False):
    """A gradient through the colours of stops, top to bottom (or left to right)."""
    g = Image.new("RGBA", (w, h))
    d = ImageDraw.Draw(g)
    n = w if horizontal else h
    for i in range(n):
        t = i / max(1, n - 1) * (len(stops) - 1)
        k = min(int(t), len(stops) - 2)
        f = t - k
        c = tuple(int(stops[k][j] + (stops[k + 1][j] - stops[k][j]) * f) for j in range(3))
        d.line((i, 0, i, h) if horizontal else (0, i, w, i), fill=c)
    return g


def darker(c, f=0.7):
    return tuple(int(v * f) for v in c[:3])


# ---------------------------------------------------------------- what is drawn


def arrow(im, p0, p1, col=ORANGE, width=7, head=24):
    d = ImageDraw.Draw(im)
    a = math.atan2(p1[1] - p0[1], p1[0] - p0[0])
    end = (p1[0] - head * 0.8 * math.cos(a), p1[1] - head * 0.8 * math.sin(a))
    d.line((p0, end), fill=col, width=width)
    left = (p1[0] - head * math.cos(a - 0.45), p1[1] - head * math.sin(a - 0.45))
    right = (p1[0] - head * math.cos(a + 0.45), p1[1] - head * math.sin(a + 0.45))
    d.polygon([p1, left, right], fill=col)


def dashed(im, box, col=ORANGE, width=5, dash=20):
    d = ImageDraw.Draw(im)
    x0, y0, x1, y1 = box
    for (ax, ay, bx, by) in ((x0, y0, x1, y0), (x1, y0, x1, y1), (x1, y1, x0, y1), (x0, y1, x0, y0)):
        length = math.hypot(bx - ax, by - ay)
        n = int(length // (dash * 2)) + 1
        for i in range(n):
            t0 = i * 2 * dash / length
            t1 = min(1, t0 + dash / length)
            if t0 >= 1:
                break
            d.line((ax + (bx - ax) * t0, ay + (by - ay) * t0, ax + (bx - ax) * t1, ay + (by - ay) * t1),
                   fill=col, width=width)


def zoom(im, box, inward=True):
    """A camera move: a dashed frame and arrows between it and the picture's corners."""
    dashed(im, box)
    x0, y0, x1, y1 = box
    m = 26
    for (cx, cy), (bx, by) in (((m, m), (x0, y0)), ((W - m, m), (x1, y0)),
                               ((W - m, H - m), (x1, y1)), ((m, H - m), (x0, y1))):
        if inward:
            arrow(im, (cx, cy), (cx + (bx - cx) * 0.8, cy + (by - cy) * 0.8), width=5, head=18)
        else:
            arrow(im, (cx + (bx - cx) * 0.8, cy + (by - cy) * 0.8), (cx, cy), width=5, head=18)


def note(im, s, xy=None):
    """A note for the camera or the staging, in orange at the top right."""
    f = font(26)
    d = ImageDraw.Draw(im)
    w = d.textlength(s, font=f)
    x1, y0 = (W - 20, 18) if xy is None else xy
    d.rounded_rectangle((x1 - w - 28, y0, x1, y0 + 44), 10, fill=ORANGE)
    d.text((x1 - w - 14, y0 + 22), s, font=f, fill=WHITE, anchor="lm")


def tag(im, cut):
    d = ImageDraw.Draw(im)
    overlay(im, lambda o: o.rounded_rectangle((18, 18, 112, 62), 10, fill=(0, 0, 0, 160)))
    d.text((65, 40), cut.upper(), font=font(26), fill=WHITE, anchor="mm")


def bubble(im, x, y, s, size=36, tail=None):
    """A speech bubble with its top-left at (x, y) and its tail pointing at tail."""
    d = ImageDraw.Draw(im)
    f = font(size)
    l, t, r, b = d.textbbox((0, 0), s, font=f)
    pad = 18
    box = (x, y, x + (r - l) + pad * 2, y + (b - t) + pad * 2)
    if tail:
        cx = min(max(tail[0], box[0] + 30), box[2] - 30)
        base_y = box[3] if tail[1] > box[3] else box[1]
        tri = [(cx - 16, base_y), (cx + 16, base_y), tail]
        d.polygon(tri, fill=WHITE, outline=INK, width=4)
    d.rounded_rectangle(box, 22, fill=WHITE, outline=INK, width=4)
    if tail:
        inset = -4 if base_y == box[3] else 4
        d.polygon([(cx - 13, base_y + inset), (cx + 13, base_y + inset), tail], fill=WHITE)
        d.line((tri[2], tri[0]), fill=INK, width=4)
        d.line((tri[2], tri[1]), fill=INK, width=4)
    d.text((box[0] + pad - l, box[1] + pad - t), s, font=f, fill=INK)


def sfx(im, xy, s, size=64, col=(255, 236, 120)):
    """A sound written into the picture, comic style."""
    text(im, xy, s, size, fill=col, anchor="mm", stroke=6, sfill=INK)


CHAPTERS = {"01": 12 / 79.5, "02": 28.5 / 79.5, "03": 50 / 79.5}


def seek_bar(im, at):
    """KawaPlayer's seek bar along the bottom, filled up to at (0-1), with a diamond at each chapter."""
    d = ImageDraw.Draw(im)
    x0, x1, y = 70, W - 70, H - 34
    overlay(im, lambda o: o.rounded_rectangle((x0, y - 3, x1, y + 3), 3, fill=(255, 255, 255, 90)))
    fill = gradient(int((x1 - x0) * at), 6, [MINT, TEAL, BLUE], horizontal=True)
    im.alpha_composite(fill, (x0, y - 3))
    for f in CHAPTERS.values():
        cx = x0 + (x1 - x0) * f
        d.polygon([(cx, y - 9), (cx + 9, y), (cx, y + 9), (cx - 9, y)], fill=WHITE if f <= at + 1e-6 else (150, 160, 170))
    kx = x0 + (x1 - x0) * at
    d.ellipse((kx - 11, y - 11, kx + 11, y + 11), fill=(90, 190, 240), outline=WHITE, width=3)
    return kx, y


def diamond(d, cx, cy, r, fill, outline=None):
    d.polygon([(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy)], fill=fill, outline=outline)


def heading(im, num, title, sub=None, en=None):
    """The chapter tag: it rises from the seek bar's chapter diamond, with the number in the
    logo's diamond, the feature's name, what it lets you do, and the same in English."""
    kx, by = seek_bar(im, CHAPTERS.get(num, 0.5))
    d = ImageDraw.Draw(im)
    lines = [(title, 50, WHITE)]
    if sub:
        lines.append((sub, 28, (228, 236, 240)))
    if en:
        lines.append((en, 20, (160, 200, 212)))
    tw = max(d.textlength(t, font=font(z)) for t, z, _ in lines)
    w, h = tw + 150, sum(z + 12 for _, z, _ in lines) + 34
    x = min(max(kx - 60, 30), W - 30 - w)
    y = by - 40 - h
    overlay(im, lambda o: o.rounded_rectangle((x, y, x + w, y + h), 26, fill=(8, 24, 36, 215)))
    d.rounded_rectangle((x, y, x + w, y + h), 26, outline=TEAL, width=3)
    d.polygon([(kx - 12, y + h), (kx + 12, y + h), (kx, by - 16)], fill=TEAL)
    diamond(d, x + 62, y + h / 2, 40, TEAL)
    diamond(d, x + 62, y + h / 2, 33, (8, 24, 36))
    d.text((x + 62, y + h / 2), num, font=font(30), fill=MINT, anchor="mm", stroke_width=1, stroke_fill=MINT)
    ty = y + 18
    for t, z, c in lines:
        d.text((x + 122, ty), t, font=font(z), fill=c, stroke_width=1 if z >= 28 else 0, stroke_fill=c)
        ty += z + 12


def fish_mark(im, x, y, size=34, col=MINT):
    """The logo's fish, small, swimming to the right: the mark at the end of a caption's wave."""
    d = ImageDraw.Draw(im)
    leaf(d, x, y, size, size * 0.42, 0, col)
    d.polygon([(x - size * 0.45, y), (x - size * 0.8, y - size * 0.22), (x - size * 0.8, y + size * 0.22)], fill=col)


def wave(im, x0, x1, y, amp=5, period=46, width=4):
    """A wave line in the logo's colours."""
    layer = Image.new("RGBA", im.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    pts = [(x, y + amp * math.sin((x - x0) / period * 2 * math.pi)) for x in range(int(x0), int(x1) + 1, 3)]
    d.line(pts, fill=WHITE, width=width, joint="curve")
    mask = layer.getchannel("A")
    colour = gradient(W, H, [MINT, TEAL, BLUE], horizontal=True)
    colour.putalpha(mask)
    im.alpha_composite(colour)


def telop(im, s, size=46, y=H - 84, en=None):
    """The caption line: it flows in from the left like the river, a wave draws itself under it,
    and the fish swims at the wave's end."""
    d = ImageDraw.Draw(im)
    if en:
        text(im, (W / 2, y + 52), en, 22, fill=(214, 226, 232), anchor="mm", stroke=4)
    text(im, (W / 2, y), s, size, anchor="mm", stroke=7)
    d.text((W / 2, y), s, font=font(size), fill=WHITE, anchor="mm", stroke_width=1, stroke_fill=WHITE)
    w = d.textlength(s, font=font(size))
    wave(im, W / 2 - w / 2, W / 2 + w / 2 - 20, y + size * 0.66)
    fish_mark(im, W / 2 + w / 2 + 10, y + size * 0.66)


CHARS = {"MAO": (A_COL, "M", "まお"), "RIRIKA": (B_COL, "R", "りりか"), "ゲスト": (G_COL, "G", "GUEST")}


def say(im, head, s, who, side="right", size=38):
    """Speech as a bubble of water: a pale bubble with the speaker's colour as its rim, a few small
    bubbles rising from their head, their name on the rim. It pops when they stop."""
    col, ini, _ = CHARS[who]
    d = ImageDraw.Draw(im)
    l, t, r, b = d.textbbox((0, 0), s, font=font(size))
    w, h = (r - l) + 60, (b - t) + 44
    hx, hy = head
    x = hx + 70 if side == "right" else hx - 70 - w
    y = hy - h - 70
    pale = tuple(int(c * 0.12 + 255 * 0.88) for c in col)
    for k, (fx, fy, rr) in enumerate(((0.12, 0.25, 6), (0.32, 0.55, 9), (0.55, 0.85, 12))):
        bx = hx + (x + (w * 0.2 if side == "right" else w * 0.8) - hx) * fy
        by = hy - 50 + (y + h - (hy - 50)) * fy
        d.ellipse((bx - rr, by - rr, bx + rr, by + rr), fill=pale, outline=col, width=3)
    d.rounded_rectangle((x, y, x + w, y + h), h / 2, fill=pale, outline=col, width=5)
    d.arc((x + 12, y + 8, x + h, y + h - 8), 150, 230, fill=WHITE, width=5)
    d.text((x + w / 2, y + h / 2), s, font=font(size), fill=INK, anchor="mm", stroke_width=1, stroke_fill=INK)
    nw = d.textlength(who, font=font(18)) + 20
    d.rounded_rectangle((x + 24, y - 14, x + 24 + nw, y + 14), 12, fill=col)
    d.text((x + 24 + nw / 2, y), who, font=font(18), fill=WHITE, anchor="mm")


def name_tag(im, who, at):
    """The name drop: a drop in the speaker's colour falls beside them, splashes, and the ripple
    becomes the ring around their initial; the name runs along a wave in their colour."""
    col, ini, kana = CHARS[who]
    d = ImageDraw.Draw(im)
    x, y = at
    def splash(o):
        for k in range(3):
            o.ellipse((x - 34 - k * 14, y + 26 - k * 3, x + 34 + k * 14, y + 42 + k * 3), outline=col + (150 - k * 45,), width=3)
    overlay(im, splash)
    d.polygon([(x, y - 46), (x + 22, y - 10), (x - 22, y - 10)], fill=col)
    d.ellipse((x - 26, y - 22, x + 26, y + 30), fill=col, outline=WHITE, width=3)
    d.text((x, y + 4), ini, font=font(24), fill=WHITE, anchor="mm", stroke_width=1, stroke_fill=WHITE)
    tx = x + 40
    text(im, (tx, y - 4), who, 32, anchor="lm", stroke=5)
    d.text((tx, y - 4), who, font=font(32), fill=WHITE, anchor="lm", stroke_width=1, stroke_fill=WHITE)
    nw = d.textlength(who, font=font(32))
    text(im, (tx + nw + 10, y), kana, 18, fill=col, anchor="lm", stroke=3)
    layer = Image.new("RGBA", im.size, (0, 0, 0, 0))
    ld = ImageDraw.Draw(layer)
    pts = [(xx, y + 24 + 4 * math.sin((xx - tx) / 30 * 2 * math.pi)) for xx in range(int(tx), int(tx + nw + 60), 3)]
    ld.line(pts, fill=col + (255,), width=4, joint="curve")
    im.alpha_composite(layer)


def action(im, s, at, side="right", rise=80):
    """The ripple note: a ripple where the finger touches, and a short line from it to a few words."""
    d = ImageDraw.Draw(im)
    x, y = at
    def ripples(o):
        for k in range(3):
            r = 22 + k * 20
            o.ellipse((x - r, y - r, x + r, y + r), outline=(170, 236, 248, 230 - k * 60), width=4)
    overlay(im, ripples)
    sgn = 1 if side == "right" else -1
    ex, ey = x + sgn * 90, y - rise
    d.line((x + sgn * 26, y - (22 if rise > 0 else -22), ex, ey, ex + sgn * 40, ey), fill=WHITE, width=3)
    diamond(d, ex + sgn * 40, ey, 7, MINT)
    anchor = "lm" if side == "right" else "rm"
    text(im, (ex + sgn * 56, ey), s, 30, anchor=anchor, stroke=6)
    d.text((ex + sgn * 56, ey), s, font=font(30), fill=WHITE, anchor=anchor, stroke_width=1, stroke_fill=WHITE)


def thing_label(im, x, y, title, sub):
    """⑥ A label on a thing: a dot on it, a thin line, a diamond and the label."""
    d = ImageDraw.Draw(im)
    ex, ey = x + 120, y - 120
    d.line((x, y, ex, ey), fill=WHITE, width=3)
    d.ellipse((x - 7, y - 7, x + 7, y + 7), fill=WHITE)
    diamond(d, ex, ey, 9, MINT)
    text(im, (ex + 22, ey - 18), title, 30, anchor="lm", stroke=6)
    text(im, (ex + 22, ey + 20), sub, 22, anchor="lm", stroke=5)


def now_card(im, x, y, num, name, step, done, parts):
    """⑦ The card at the top left: the chapter, what it shows now, and how far into the chapter it is."""
    overlay(im, lambda o: o.rounded_rectangle((x, y, x + 330, y + 118), 16, fill=(8, 22, 30, 200)))
    d = ImageDraw.Draw(im)
    diamond(d, x + 34, y + 36, 20, TEAL, outline=WHITE)
    d.text((x + 34, y + 36), num, font=font(20), fill=WHITE, anchor="mm")
    d.text((x + 66, y + 36), name, font=font(26), fill=WHITE, anchor="lm")
    d.text((x + 24, y + 78), step, font=font(22), fill=(200, 236, 236), anchor="lm")
    x0, x1, by = x + 24, x + 306, y + 104
    d.line((x0, by, x1, by), fill=(70, 90, 100), width=4)
    d.line((x0, by, x0 + (x1 - x0) * done / parts, by), fill=MINT, width=4)
    for k in range(parts):
        cx = x0 + (x1 - x0) * k / parts
        diamond(d, cx, by, 6, MINT if k < done else (70, 90, 100))


def captions():
    """The caption styles together, for the README."""
    im = new(NIGHT["wall"])
    room(im, screen=sample_video(), glow=True)
    im = im.filter(ImageFilter.GaussianBlur(8))
    now_card(im, 24, 24, "02", "タブレット", "スクリーンに送る", 3, 4)
    name_tag(im, "ゲスト", (1010, 150))
    action(im, "スクリーンに送る", (840, 300))
    say(im, (430, 420), "見て見て！", "RIRIKA")
    thing_label(im, 470, 210, "KawaPlayer タブレット", "手もとで、動画も写真も操作できる")
    heading(im, "02", "タブレット", "手元で見せて、みんなのスクリーンにも", "Show it in your hands, then on everyone's screen")
    telop(im, "手元の写真を、みんなのスクリーンへ", y=H - 330)
    d = ImageDraw.Draw(im)
    for (x, y), n in (((965, 150), "②"), ((840, 360), "③"), ((560, 250), "④"), ((70, 470), "①"), ((260, 360), "⑤"),
                      ((430, 215), "⑥"), ((190, 165), "⑦")):
        d.ellipse((x - 20, y - 20, x + 20, y + 20), fill=ORANGE)
        d.text((x, y), n, font=font(26), fill=WHITE, anchor="mm")
    return im


def room(im, mode=NIGHT, screen=None, glow=False, window=True, door=False):
    d = ImageDraw.Draw(im)
    d.rectangle((0, 0, W, H), fill=mode["wall"])
    d.rectangle((0, 560, W, H), fill=mode["floor"])
    d.line((0, 560, W, 560), fill=mode["line"], width=3)
    night = mode is NIGHT
    if window:
        wb = (60, 140, 290, 440)
        d.rectangle(wb, fill=(16, 22, 40) if night else (196, 226, 244), outline=mode["line"], width=8)
        d.line((175, 140, 175, 440), fill=mode["line"], width=6)
        d.line((60, 290, 290, 290), fill=mode["line"], width=6)
        if night:
            for sx, sy in ((90, 170), (140, 210), (230, 175), (260, 240), (110, 330), (210, 320),
                           (250, 380), (95, 400), (150, 260), (200, 410)):
                d.ellipse((sx - 3, sy - 3, sx + 3, sy + 3), fill=(230, 236, 255))
            d.ellipse((215, 190, 255, 230), fill=(240, 236, 210))
    if door:
        d.rectangle((1060, 200, 1200, 560), fill=darker(mode["wall"], 0.8), outline=mode["line"], width=6)
        d.ellipse((1074, 380, 1092, 398), fill=mode["line"])
    x0, y0, x1, y1 = SCREEN
    d.rectangle((x0 - 12, y0 - 12, x1 + 12, y1 + 12), fill=(10, 12, 16))
    if screen is None:
        d.rectangle(SCREEN, fill=(18, 20, 24))
    else:
        place(im, screen, SCREEN)
    if glow:
        overlay(im, lambda o: o.polygon([(x0, y1 + 12), (x1, y1 + 12), (x1 + 260, H), (x0 - 260, H)],
                                        fill=(150, 210, 230, 34)))


def sofa(im, mode=NIGHT):
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((350, 540, 930, 720), 34, fill=mode["sofa"])
    d.rounded_rectangle((310, 590, 400, 720), 26, fill=darker(mode["sofa"], 0.85))
    d.rounded_rectangle((880, 590, 970, 720), 26, fill=darker(mode["sofa"], 0.85))




def person(im, cx, base, h, col, label=None, twin=False, arm=None):
    """A standing figure, base at its feet and h tall. arm: None, 'up', 'wave', 'reach' or 'both'."""
    d = ImageDraw.Draw(im)
    dark = darker(col)
    r = h * 0.115
    hy = base - h + r * 1.1
    if twin:
        for s in (-1, 1):
            tx = cx + s * r * 1.5
            d.ellipse((tx - r * 0.5, hy - r * 0.3, tx + r * 0.5, hy + r * 2.7), fill=dark)
    d.ellipse((cx - r * 1.12, hy - r * 1.14, cx + r * 1.12, hy + r * 1.3), fill=dark)
    d.ellipse((cx - r * 0.92, hy - r * 0.8, cx + r * 0.92, hy + r * 1.0), fill=col)
    sh = hy + r * 1.35
    sw, ww, sk = h * 0.15, h * 0.085, h * 0.17
    wy, sy = sh + h * 0.25, sh + h * 0.43
    d.polygon([(cx - sw, sh + h * 0.035), (cx - sw * 0.6, sh - h * 0.012), (cx + sw * 0.6, sh - h * 0.012),
               (cx + sw, sh + h * 0.035), (cx + ww, wy), (cx + sk, sy), (cx - sk, sy), (cx - ww, wy)], fill=col)
    lw = h * 0.034
    for s in (-1, 1):
        d.rounded_rectangle((cx + s * h * 0.06 - lw, sy - 4, cx + s * h * 0.06 + lw, base), lw, fill=dark)
    aw = max(4, int(h * 0.045))
    hand = h * 0.035
    left = [(cx - sw * 0.92, sh + h * 0.04), (cx - sw * 1.12, sh + h * 0.17), (cx - sw * 1.05, wy + h * 0.07)]
    right = [(cx + sw * 0.92, sh + h * 0.04), (cx + sw * 1.12, sh + h * 0.17), (cx + sw * 1.05, wy + h * 0.07)]
    if arm == "up":
        right = [(cx + sw * 0.92, sh + h * 0.04), (cx + sw * 1.7, sh + h * 0.02), (cx + sw * 1.7, sh - h * 0.17)]
    elif arm == "wave":
        right = [(cx + sw * 0.92, sh + h * 0.04), (cx + sw * 1.5, sh - h * 0.06), (cx + sw * 1.95, sh - h * 0.26)]
    elif arm == "reach":
        right = [(cx + sw * 0.92, sh + h * 0.04), (cx + sw * 1.7, sh + h * 0.08), (cx + sw * 2.5, sh + h * 0.02)]
    elif arm == "both":
        left = [(cx - sw * 0.92, sh + h * 0.04), (cx - sw * 1.3, sh - h * 0.08), (cx - sw * 1.6, sh - h * 0.27)]
        right = [(cx + sw * 0.92, sh + h * 0.04), (cx + sw * 1.3, sh - h * 0.08), (cx + sw * 1.6, sh - h * 0.27)]
    for pts in (left, right):
        d.line(pts, fill=col, width=aw, joint="curve")
        ex, ey = pts[-1]
        d.ellipse((ex - hand, ey - hand, ex + hand, ey + hand), fill=col)
    if label:
        ly = hy - r * 1.14 - 30
        lw2 = d.textlength(label, font=font(24)) / 2 + 14
        d.rounded_rectangle((cx - lw2, ly - 22, cx + lw2, ly + 18), 12, fill=col, outline=WHITE, width=3)
        d.text((cx, ly - 2), label, font=font(24), fill=WHITE, anchor="mm")
    return (cx, hy)


def join_ring(im, cx, cy, rx, ry):
    """The sparkle of someone arriving."""
    def draw(o):
        for k in range(3):
            o.ellipse((cx - rx - k * 14, cy - ry - k * 14, cx + rx + k * 14, cy + ry + k * 14),
                      outline=(200, 240, 255, 150 - k * 40), width=5)
        for a in range(0, 360, 45):
            px = cx + (rx + 52) * math.cos(math.radians(a))
            py = cy + (ry + 52) * math.sin(math.radians(a))
            o.ellipse((px - 6, py - 6, px + 6, py + 6), fill=(255, 255, 255, 200))
    overlay(im, draw)


def hands(im, box, col=A_COL):
    """Two hands holding something by its sides, seen by the one holding it."""
    d = ImageDraw.Draw(im)
    x0, y0, x1, y1 = box
    my = y0 + (y1 - y0) * 0.62
    for s, x in ((-1, x0), (1, x1)):
        d.polygon([(x - s * 10, my + 40), (x + s * 120, H), (x + s * 330, H), (x + s * 70, my + 90)], fill=darker(col, 0.8))
        d.rounded_rectangle((x - 46, my - 70, x + 46, my + 110), 40, fill=col)
        d.rounded_rectangle((x - s * 54 - 20, my - 30, x - s * 54 + 20, my + 30), 18, fill=col)


def tablet_frame(im, box, radius=46, portrait=False):
    d = ImageDraw.Draw(im)
    d.rounded_rectangle(box, radius, fill=(8, 9, 11), outline=(120, 124, 130), width=5)
    x0, y0, x1, y1 = box
    inset = 32
    if portrait:
        screen = (x0 + inset, y0 + inset, x1 - inset, y1 - inset - 30)
        bx, by = (x0 + x1) / 2, y1 - inset - 2
    else:
        screen = (x0 + inset + 6, y0 + inset, x1 - inset - 6, y1 - inset - 34)
        bx, by = (x0 + x1) / 2, y1 - inset
    d.ellipse((bx - 15, by - 15, bx + 15, by + 15), fill=(30, 32, 36), outline=(120, 124, 130), width=3)
    return screen


def tablet_home(im, box):
    """The guide's picture of the tablet, without the table around it."""
    place(im, guide("tablet/home.webp", (36, 22, 1244, 786)), box, radius=56)


# --------------------------------------------------------- what the screen shows


def sample_video(w=640, h=360, time="01:23", bar=True):
    """A made-up clip: a sunset over the sea, with KawaPlayer's seek bar."""
    v = gradient(w, h, [(40, 52, 110), (180, 92, 140), (250, 160, 96)])
    d = ImageDraw.Draw(v)
    d.ellipse((w * 0.58, h * 0.42, w * 0.76, h * 0.74), fill=(255, 214, 140))
    sea = gradient(w, int(h * 0.36), [(70, 70, 120), (24, 30, 64)])
    v.alpha_composite(sea, (0, int(h * 0.64)))
    for k in range(6):
        y = h * 0.68 + k * h * 0.045
        d.line((w * 0.55, y, w * 0.8, y), fill=(255, 200, 140), width=max(1, int(h * 0.008)))
    d.text((w * 0.05, h * 0.06), "サンプル映像", font=font(max(12, int(h * 0.07))), fill=(255, 255, 255))
    if bar:
        d.rectangle((0, h * 0.86, w, h), fill=(10, 12, 18))
        d.line((w * 0.16, h * 0.93, w * 0.84, h * 0.93), fill=(110, 130, 150), width=max(2, int(h * 0.012)))
        d.line((w * 0.16, h * 0.93, w * 0.42, h * 0.93), fill=(90, 190, 240), width=max(2, int(h * 0.012)))
        rr = h * 0.022
        d.ellipse((w * 0.42 - rr, h * 0.93 - rr, w * 0.42 + rr, h * 0.93 + rr), fill=(90, 190, 240))
        d.text((w * 0.03, h * 0.93), time, font=font(max(10, int(h * 0.05))), fill=WHITE, anchor="lm")
        d.text((w * 0.97, h * 0.93), "04:34", font=font(max(10, int(h * 0.05))), fill=WHITE, anchor="rm")
    return v




def logo(size, col=WHITE):
    src = Image.open(LOGO).convert("RGBA").resize((size, size), Image.LANCZOS)
    tinted = Image.new("RGBA", src.size, col + (255,))
    tinted.putalpha(src.getchannel("A"))
    return tinted


def brand_background():
    g = gradient(W, H, [MINT, TEAL, BLUE], horizontal=True)
    overlay(g, lambda o: o.ellipse((-300, -400, 900, 500), fill=(255, 255, 255, 28)))
    return g




def leaf(d, cx, cy, length, width, angle, fill):
    """A leaf: a pointed oval along angle (degrees)."""
    a = math.radians(angle)
    pts = []
    for k in range(25):
        t = k / 24 * math.pi * 2
        x = math.cos(t) * length / 2
        y = math.sin(t) * width / 2 * (1 - 0.35 * math.cos(t))
        pts.append((cx + x * math.cos(a) - y * math.sin(a), cy + x * math.sin(a) + y * math.cos(a)))
    d.polygon(pts, fill=fill)


def foliage(im, spots, blur=5, fill=(18, 38, 32, 235)):
    """Leaves close to the camera, out of focus: spots are (x, y, length, angle)."""
    layer = Image.new("RGBA", im.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for x, y, length, angle in spots:
        leaf(d, x, y, length, length * 0.5, angle, fill)
    im.alpha_composite(layer.filter(ImageFilter.GaussianBlur(blur)))


def night_room_blur(im):
    """The room behind, out of focus: the window's city lights and the dark wall screen."""
    back = gradient(W, H, [(44, 50, 78), (62, 56, 96), (34, 34, 56)])
    d = ImageDraw.Draw(back)
    d.rectangle((860, 60, 1280, 520), fill=(52, 70, 110))
    for k in range(40):
        x = 880 + (k * 97) % 380
        y = 300 + (k * 53) % 200
        d.rectangle((x, y, x + 10, y + 6), fill=(230, 210, 150))
    d.rectangle((60, 140, 420, 360), fill=(16, 18, 26))
    im.alpha_composite(back.filter(ImageFilter.GaussianBlur(9)))


def c01():
    im = new(NIGHT["wall"])
    room(im)
    d = ImageDraw.Draw(im)
    # the desk by the window, the tablet on it
    d.rectangle((70, 470, 470, 486), fill=(120, 86, 64))
    for x in (90, 440):
        d.rectangle((x, 486, x + 14, 640), fill=(80, 58, 44))
    d.rounded_rectangle((300, 452, 380, 470), 4, fill=(20, 22, 26))
    person(im, 640, 660, 380, A_COL, "MAO")
    # the camera: low at her feet, out to her left rear, round to her right side
    pts = [(640 + 310 * math.cos(math.radians(a)), 445 + 240 * math.sin(math.radians(a))) for a in range(205, 346, 4)]
    d.line(pts, fill=ORANGE, width=7, joint="curve")
    arrow(im, pts[-3], pts[-1])
    text(im, (pts[0][0] - 10, pts[0][1] + 40), "足もとから", 26, fill=ORANGE, anchor="mm", stroke=4)
    text(im, (pts[-1][0] + 20, pts[-1][1] + 40), "右横へ", 26, fill=ORANGE, anchor="mm", stroke=4)
    foliage(im, [(1180, 560, 280, -40), (1250, 420, 260, 80)])
    d.rectangle((0, 0, W, 88), fill=(0, 0, 0))
    d.rectangle((0, H - 88, W, H), fill=(0, 0, 0))
    note(im, "長回し：足もと → 左後ろへ引く → 右横へ回る", (W - 20, 104))
    return im


def c02():
    im = new(NIGHT["wall"])
    room(im, window=False)
    d = ImageDraw.Draw(im)
    # her right shoulder and arm at the left edge; the tablet raised and turned up
    d.ellipse((-220, 80, 330, 900), fill=darker(A_COL, 0.55))
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    box = (430, 170, 1010, 510)
    tablet_home(layer, box)
    im.alpha_composite(layer.rotate(-8, resample=Image.BICUBIC, center=((box[0] + box[2]) / 2, (box[1] + box[3]) / 2)))
    tx, ty = 512, 422
    d.line((330, 640, tx - 6, ty + 18), fill=A_COL, width=46)

    def ripple(o):
        for k in range(3):
            r = 26 + k * 22
            o.ellipse((tx - r, ty - r, tx + r, ty + r), outline=(150, 235, 245, 210 - k * 55), width=5)
    overlay(im, ripple)
    d.rectangle((0, 0, W, 88), fill=(0, 0, 0))
    d.rectangle((0, H - 88, W, H), fill=(0, 0, 0))
    thing_label(im, 760, 300, "KawaPlayer タブレット", "手もとで、動画も写真も操作できる")
    note(im, "右横から・押したあと画面へ寄る", (W - 20, 580))
    return im


def c04():
    im = brand_background()
    place(im, logo(250), (515, 90, 765, 340))
    text(im, (640, 440), "KawaPlayer", 104, anchor="mm")
    text(im, (640, 530), "VRChat の動画プレイヤー", 38, anchor="mm")
    return im








def fish(im, cx, cy, size, angle, col=WHITE):
    """The logo's fish, swimming towards angle (degrees)."""
    d = ImageDraw.Draw(im)
    a = math.radians(angle)
    leaf(d, cx, cy, size, size * 0.42, angle, col)
    bx, by = cx - math.cos(a) * size * 0.45, cy - math.sin(a) * size * 0.45
    for s_ in (-1, 1):
        tx = bx - math.cos(a + s_ * 0.5) * size * 0.35
        ty = by - math.sin(a + s_ * 0.5) * size * 0.35
        d.polygon([(bx, by), (tx, ty), (bx - math.cos(a) * size * 0.12, by - math.sin(a) * size * 0.12)], fill=col)
    ex, ey = cx + math.cos(a) * size * 0.28 - math.sin(a) * size * 0.06, cy + math.sin(a) * size * 0.28 + math.cos(a) * size * 0.06
    d.ellipse((ex - size * 0.03, ey - size * 0.03, ex + size * 0.03, ey + size * 0.03), fill=INK)


def trail(im, pts, col=(255, 255, 255, 150)):
    """The dotted path something has swum along."""
    def draw(o):
        for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
            n = int(math.hypot(x1 - x0, y1 - y0) // 26)
            for k in range(n):
                t = k / max(1, n)
                x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
                o.ellipse((x - 4, y - 4, x + 4, y + 4), fill=col)
    overlay(im, draw)


def capsule(im, x, y, s="…/r/default/…"):
    """The copied URL, carried as a little tag."""
    d = ImageDraw.Draw(im)
    w = d.textlength(s, font=font(22)) + 28
    d.rounded_rectangle((x, y, x + w, y + 36), 18, fill=TEAL, outline=WHITE, width=3)
    d.text((x + w / 2, y + 18), s, font=font(22), fill=WHITE, anchor="mm")


TABLET_APP = (36, 22, 1244, 786)  # The tablet in the guide's picture of the KawaPlayer app.


def tablet_app_box(x, y, w):
    return (x, y, x + w, y + w * (TABLET_APP[3] - TABLET_APP[1]) / (TABLET_APP[2] - TABLET_APP[0]))


def at(box, px, py):
    """Where a point of the guide's tablet picture lands in box."""
    s = (box[2] - box[0]) / (TABLET_APP[2] - TABLET_APP[0])
    return (box[0] + (px - TABLET_APP[0]) * s, box[1] + (py - TABLET_APP[1]) * s)


def c05():
    im = Image.new("RGBA", (W, H))
    im.alpha_composite(gradient(W, H, [(12, 26, 44), (16, 58, 78), (22, 96, 116)], horizontal=True))
    d = ImageDraw.Draw(im)
    place(im, logo(190, (200, 230, 236)), (70, 110, 260, 300))
    card = (640, 40, 1240, 470)
    d.rounded_rectangle(card, 18, fill=WHITE)
    d.rounded_rectangle((card[0], card[1], card[2], card[1] + 46), 18, fill=(236, 239, 242))
    d.rectangle((card[0], card[1] + 26, card[2], card[1] + 46), fill=(236, 239, 242))
    d.text((card[0] + 24, card[1] + 23), "playlist.vrc-hub.com", font=font(22), fill=(100, 108, 116), anchor="lm")
    src = guide("vhub/detail.webp")
    b = box_at(src, card[0] + 40, card[1] + 56, h=card[3] - card[1] - 66)
    place(im, src, b)
    sc = (b[2] - b[0]) / src.width
    btn = (b[0] + 18 * sc, b[1] + 432 * sc, b[0] + 610 * sc, b[1] + 503 * sc)
    d.rounded_rectangle((btn[0] - 6, btn[1] - 6, btn[2] + 6, btn[3] + 6), 24, outline=ORANGE, width=6)
    trail(im, [(170, 210), (330, 260), (480, 420), (btn[0] - 40, btn[1] + 30)])
    fish(im, btn[0] - 14, btn[1] + 30, 110, 10)
    capsule(im, btn[0] - 130, btn[1] - 64)
    bubble(im, 380, 250, "コピー！", 36, tail=(btn[0] - 70, btn[1] - 10))
    text(im, (165, 330), "ロゴから魚が泳ぎ出る", 24, fill=ORANGE, anchor="mm", stroke=4)
    heading(im, "01", "VHub のプレイリスト", "VHub で作ったプレイリストを、URL ひとつで", "Play a whole VHub playlist from a single URL")
    note(im, "2D の空間")
    return im


def c06():
    im = new((40, 44, 70))
    night_room_blur(im)
    box = tablet_app_box(470, 120, 700)
    place(im, guide("tablet/kawaplayer.webp", TABLET_APP), box, radius=50)
    hands(im, box)
    lx, ly = at(box, 115, 295)
    trail(im, [(1240, 60), (1000, 150), (lx + 40, ly - 30)])
    fish(im, lx + 26, ly - 16, 70, 150)
    capsule(im, lx + 60, ly - 70)
    d = ImageDraw.Draw(im)
    d.ellipse((60, 230, 420, 620), fill=darker(A_COL, 0.55))
    d.ellipse((110, 250, 380, 560), fill=darker(A_COL, 0.7))
    d.polygon([(0, H), (0, 560), (200, 520), (460, 600), (520, H)], fill=A_COL)
    zoom(im, (box[0] + 30, box[1] + 30, box[2] - 30, box[3] - 60), inward=False)
    text(im, (240, 200), "MAO", 30, fill=A_COL, anchor="mm", stroke=4, sfill=WHITE)
    note(im, "タブレットの画面から、肩越しまで引く")
    return im


def c07():
    im = new((40, 44, 70))
    night_room_blur(im)
    box = tablet_app_box(150, 40, 980)
    place(im, guide("tablet/kawaplayer.webp", TABLET_APP), box, radius=60)
    d = ImageDraw.Draw(im)
    fx, fy = at(box, 300, 160)
    d.rounded_rectangle((fx - 60, fy - 40, fx + 640, fy + 40), 14, fill=(40, 44, 50), outline=(110, 190, 230), width=4)
    d.text((fx - 36, fy), "https://playlist.vrc-hub.com/r/default/…", font=font(30), fill=WHITE, anchor="lm")

    def ripple(o):
        for k in range(4):
            r = 40 + k * 55
            o.ellipse((fx - r, fy - r, fx + r, fy + r), outline=(150, 235, 245, 210 - k * 45), width=6)
    overlay(im, ripple)
    d.polygon([(W, H), (W, H - 160), (fx + 120, fy + 60), (fx + 60, fy + 120)], fill=darker(A_COL, 0.8))
    d.rounded_rectangle((fx + 30, fy + 30, fx + 170, fy + 150), 50, fill=A_COL)
    d.line((fx + 60, fy + 60, fx + 6, fy + 6), fill=A_COL, width=32)
    d.ellipse((fx - 10, fy - 10, fx + 20, fy + 20), fill=A_COL)
    action(im, "URL のボタンをタップ", (fx, fy))
    note(im, "MAO の指でタップ", (W - 20, 660))
    return im


def c08():
    back = new((40, 44, 70))
    night_room_blur(back)
    box = tablet_app_box(640, 300, 760)
    place(back, guide("tablet/kawaplayer.webp", TABLET_APP), box, radius=50)
    hands(back, box)
    cx, cy = at(box, 300, 160)
    im = back.copy()
    front = new(INK)
    place(front, guide("screen/playlist-tracks.webp"), (0, 0, W, H))
    mask = Image.new("L", (W, H), 0)
    r = 600
    ImageDraw.Draw(mask).ellipse((cx - r, cy - r, cx + r, cy + r), fill=255)
    im.paste(front, (0, 0), mask)

    def rings(o):
        for k in range(3):
            rr = r - k * 26
            o.ellipse((cx - rr, cy - rr, cx + rr, cy + rr), outline=(150, 235, 245, 220 - k * 60), width=8 - k * 2)
    overlay(im, rings)
    telop(im, "URL ひとつで、プレイリストを追加", en="One URL adds the whole playlist")
    note(im, "波紋が広がり、スクリーンの一覧へ")
    return im












def c09():
    im = new(NIGHT["wall"])
    room(im, screen=sample_video(), glow=True)
    ha = person(im, 560, 800, 470, A_COL, "MAO")
    hb = person(im, 730, 805, 460, B_COL, "RIRIKA", twin=True)
    sofa(im)
    sfx(im, (645, 300), "♪", 56, col=WHITE)
    x0, y0, x1, y1 = SCREEN
    dashed(im, (x0 - 20, y0 - 20, x1 + 20, y1 + 20))
    for (bx, by), (cx, cy) in (((x0 - 20, y0 - 20), (60, 40)), ((x1 + 20, y0 - 20), (W - 60, 40)),
                               ((x1 + 20, y1 + 20), (W - 60, H - 40)), ((x0 - 20, y1 + 20), (60, H - 40))):
        arrow(im, (bx, by), (bx + (cx - bx) * 0.7, by + (cy - by) * 0.7), width=5, head=18)
    name_tag(im, "RIRIKA", (880, 250))
    note(im, "スクリーンから、2 人の後ろまで引く")
    return im








def sample_photo_tall(w=360, h=640):
    """A made-up 9:16 picture: a lighthouse at dusk."""
    p = gradient(w, h, [(60, 90, 170), (240, 150, 140), (250, 200, 150)])
    d = ImageDraw.Draw(p)
    d.rectangle((0, h * 0.78, w, h), fill=(40, 70, 120))
    d.polygon([(w * 0.42, h * 0.3), (w * 0.58, h * 0.3), (w * 0.63, h * 0.8), (w * 0.37, h * 0.8)], fill=(245, 245, 240))
    for k in range(3):
        y = h * (0.42 + k * 0.12)
        d.rectangle((w * 0.38, y, w * 0.62, y + h * 0.05), fill=(220, 70, 70))
    d.rectangle((w * 0.44, h * 0.24, w * 0.56, h * 0.3), fill=(255, 230, 140))
    d.polygon([(w * 0.4, h * 0.24), (w * 0.6, h * 0.24), (w * 0.5, h * 0.18)], fill=(200, 60, 60))
    return p


def fit_into(im, src, box, bg=(0, 0, 0)):
    """src inside box, keeping its shape, with bars on the sides or the top."""
    x0, y0, x1, y1 = box
    ImageDraw.Draw(im).rectangle(box, fill=bg)
    s = min((x1 - x0) / src.width, (y1 - y0) / src.height)
    w, h = src.width * s, src.height * s
    place(im, src, ((x0 + x1 - w) / 2, (y0 + y1 - h) / 2, (x0 + x1 + w) / 2, (y0 + y1 + h) / 2))


def sparkle(im, x, y, r, col=(255, 240, 160)):
    ImageDraw.Draw(im).polygon([(x, y - r), (x + r * 0.25, y - r * 0.25), (x + r, y), (x + r * 0.25, y + r * 0.25),
                                (x, y + r), (x - r * 0.25, y + r * 0.25), (x - r, y), (x - r * 0.25, y - r * 0.25)], fill=col)


def sweat(im, x, y, r=16):
    d = ImageDraw.Draw(im)
    d.polygon([(x, y - r * 1.6), (x + r, y), (x - r, y)], fill=(150, 210, 250))
    d.ellipse((x - r, y - r, x + r, y + r), fill=(150, 210, 250))


def c10():
    im = new(NIGHT["wall"])
    room(im, screen=sample_video(), glow=True)
    im = im.filter(ImageFilter.GaussianBlur(6))
    hb = person(im, 760, 900, 760, B_COL, "RIRIKA", twin=True)
    tab = (560, 430, 960, 660)
    scr = tablet_frame(im, tab, radius=30)
    fit_into(im, sample_photo_tall(), scr)
    d = ImageDraw.Draw(im)
    for x in (tab[0] - 6, tab[2] + 6):
        d.ellipse((x - 34, 520, x + 34, 600), fill=B_COL)
    for x, y, r in ((520, 380, 26), (1010, 360, 20), (990, 470, 14), (500, 500, 16)):
        sparkle(im, x, y, r)
    say(im, hb, "見て見て！", "RIRIKA")
    person(im, 120, 1300, 1100, A_COL)
    text(im, (760, 690), "縦の写真を、横持ちのまま", 24, fill=ORANGE, anchor="mm", stroke=4)
    heading(im, "02", "タブレット", "手元で見せて、みんなのスクリーンにも", "Show it in your hands, then on everyone's screen")
    note(im, "RIRIKA に寄る")
    return im


def c11():
    im = new(NIGHT["wall"])
    room(im, screen=sample_video(), glow=True)
    im = im.filter(ImageFilter.GaussianBlur(6))
    ha = person(im, 420, 860, 700, A_COL, "MAO", arm="reach")
    d = ImageDraw.Draw(im)
    fx, fy = 420 + 700 * 0.15 * 2.5, 860 - 700 + 700 * 0.115 * 1.1 + 700 * 0.115 * 1.35 + 700 * 0.02
    d.arc((fx - 50, fy - 90, fx + 50, fy + 10), 200, 520, fill=ORANGE, width=6)
    arrow(im, (fx + 44, fy - 30), (fx + 30, fy - 2), width=5, head=16)
    tab = (1000, 380, 1330, 570)
    scr = tablet_frame(im, tab, radius=26)
    fit_into(im, sample_photo_tall(), scr)
    say(im, ha, "縦、縦！", "MAO")
    text(im, (fx, fy + 70), "くるっと回す仕草", 24, fill=ORANGE, anchor="mm", stroke=4)
    note(im, "MAO が指摘する")
    return im


def c12():
    im = new(NIGHT["wall"])
    room(im, screen=sample_video(), glow=True)
    im = im.filter(ImageFilter.GaussianBlur(8))
    tab = (520, 40, 880, 640)
    scr = tablet_frame(im, tab, radius=40, portrait=True)
    place(im, sample_photo_tall(), scr)
    hands(im, tab, col=B_COL)
    d = ImageDraw.Draw(im)
    d.ellipse((60, 260, 420, 680), fill=darker(B_COL, 0.6))
    d.ellipse((20, 360, 120, 720), fill=darker(B_COL, 0.6))
    d.ellipse((360, 360, 460, 720), fill=darker(B_COL, 0.6))
    sweat(im, 440, 260)
    sweat(im, 470, 330, 12)
    d.arc((880, 120, 1120, 360), 250, 30, fill=ORANGE, width=8)
    arrow(im, (1100, 320), (1080, 352))
    text(im, (1000, 400), "縦に回す", 28, fill=ORANGE, anchor="mm", stroke=4)
    telop(im, "横向きでも縦向きでも、画面がついてくる", en="Sideways or upright, the screen turns with it")
    note(im, "RIRIKA の肩越し・はずかしそうに")
    return im


def c13():
    im = new(NIGHT["wall"])
    room(im, glow=True)
    fit_into(im, sample_photo_tall(), SCREEN)
    ha = person(im, 1000, 690, 400, A_COL, "MAO", arm="wave")
    d = ImageDraw.Draw(im)
    for k in range(9):
        a = math.radians(-150 + k * 15)
        d.line((ha[0] + 80 * math.cos(a), ha[1] - 30 + 80 * math.sin(a), ha[0] + 130 * math.cos(a), ha[1] - 30 + 130 * math.sin(a)),
               fill=WHITE, width=5)
    say(im, ha, "おおー！", "MAO", side="left")
    # RIRIKA from behind at the lower left, the tablet in her left hand
    d.ellipse((60, 380, 380, 760), fill=darker(B_COL, 0.6))
    d.ellipse((20, 470, 110, 760), fill=darker(B_COL, 0.6))
    d.ellipse((330, 470, 420, 760), fill=darker(B_COL, 0.6))
    scr = tablet_frame(im, (400, 440, 560, 700), radius=20, portrait=True)
    place(im, sample_photo_tall(), scr)
    action(im, "スクリーンに送る", ((scr[0] + scr[2]) / 2, scr[1] + 20))
    arrow(im, (560, 470), (640, 420))
    telop(im, "手元の写真を、みんなのスクリーンへ", en="From your hands to everyone's screen")
    note(im, "1 本で撮る：背中越しのタップ → 引いてスクリーンへ")
    return im


def c14():
    im = new(NIGHT["wall"])
    room(im, screen=sample_video(), glow=True, door=True)
    join_ring(im, 1130, 470, 90, 210)
    hg = person(im, 1130, 690, 400, G_COL, "ゲスト")
    ha = person(im, 330, 700, 420, A_COL, "MAO", arm="wave")
    hb = person(im, 700, 640, 400, B_COL, "RIRIKA", twin=True, arm="both")
    d = ImageDraw.Draw(im)
    for k in range(3):
        d.line((650 + k * 50, 668, 650 + k * 50, 700), fill=(200, 210, 220), width=4)
    say(im, hb, "いらっしゃーい！", "RIRIKA", side="left")
    name_tag(im, "ゲスト", (1000, 150))
    note(im, "別の場面・入ってくる人を歓迎", (W - 20, 660))
    return im


def visitors_three():
    """The guide's picture of the visitors app, with MAO, RIRIKA and the guest who just came."""
    shot = guide("tablet/visitors.webp")
    g = ImageDraw.Draw(shot)
    bg = shot.getpixel((640, 500))[:3]
    g.rectangle((96, 140, 1184, 690), fill=bg)
    g.text((108, 166), "今いる人 3 人", font=font(22), fill=(190, 194, 200), anchor="lm")
    rows = (("MAO", A_COL, "20:58", "オーナー"), ("RIRIKA", B_COL, "21:02", None), ("ゲスト", G_COL, "21:10", None))
    for k, (name, col, t, badge) in enumerate(rows):
        y = 190 + k * 100
        g.rounded_rectangle((102, y, 1178, y + 87), 10, fill=(44, 46, 52))
        g.ellipse((114, y + 10, 180, y + 76), fill=col)
        g.text((200, y + 30), name, font=font(28), fill=WHITE, anchor="lm")
        g.text((232, y + 66), "PC・キーボード", font=font(18), fill=(170, 176, 182), anchor="lm")
        if badge:
            g.rounded_rectangle((300, y + 14, 400, y + 46), 16, fill=(40, 90, 120))
            g.text((350, y + 30), badge, font=font(18), fill=WHITE, anchor="mm")
        g.text((868, y + 25), "入室", font=font(18), fill=(170, 176, 182), anchor="lm")
        g.text((868, y + 59), t, font=font(26), fill=WHITE, anchor="lm")
    y = 190 + 2 * 100
    g.rounded_rectangle((96, y - 6, 1184, y + 93), 14, outline=ORANGE, width=6)
    g.text((560, y + 44), "NEW", font=font(30), fill=ORANGE, anchor="mm")
    return shot


def c15():
    im = new(NIGHT["wall"])
    room(im, screen=sample_video(), glow=True)
    im = im.filter(ImageFilter.GaussianBlur(10))
    box = (230, 50, 1050, 569)
    place(im, visitors_three().crop((36, 22, 1244, 786)), box, radius=56)
    hands(im, box, col=B_COL)
    telop(im, "誰が来たかも、ひと目で", en="See who's here at a glance")
    note(im, "画面転換で、RIRIKA のタブレットへ")
    return im






def prone(im, cx, cy, r, col, facing="up", twin=False, label=None):
    """Someone lying on their front, propped on their elbows, seen from above. facing "up":
    their back to the camera, head towards the top; "down": their face towards the camera."""
    d = ImageDraw.Draw(im)
    dark = darker(col)
    s = -1 if facing == "up" else 1
    d.ellipse((cx - r * 1.5, cy - s * r * 0.6 - r * 1.2, cx + r * 1.5, cy - s * r * 0.6 + r * 1.2), fill=dark)
    if facing == "up":
        d.rectangle((cx - r * 1.3, cy + r * 0.6, cx + r * 1.3, cy + r * 4), fill=dark)
    else:
        d.rectangle((cx - r * 1.3, cy - r * 4, cx + r * 1.3, cy - r * 0.6), fill=dark)
    for side in (-1, 1):
        ex, ey = cx + side * r * 1.35, cy - s * r * 0.5
        hx, hy = cx + side * r * (0.9 if facing == "up" else 0.45), cy + s * r * 1.25
        d.line((ex, ey, hx, hy), fill=col, width=int(r * 0.5))
        d.ellipse((hx - r * 0.3, hy - r * 0.3, hx + r * 0.3, hy + r * 0.3), fill=col)
    if twin:
        for side in (-1, 1):
            tx = cx + side * r * 1.25
            d.ellipse((tx - r * 0.45, cy - r * 0.6, tx + r * 0.45, cy + r * 1.5), fill=dark)
    d.ellipse((cx - r * 1.05, cy - r * 1.05, cx + r * 1.05, cy + r * 1.05), fill=dark if facing == "up" else col)
    if facing != "up":
        d.chord((cx - r * 1.05, cy - r * 1.05, cx + r * 1.05, cy + r * 1.05), 180, 360, fill=dark)
    if label:
        ly = cy - r * 1.05 - 30 if facing == "down" else cy + r * 1.3 + 30
        lw = d.textlength(label, font=font(24)) / 2 + 14
        d.rounded_rectangle((cx - lw, ly - 20, cx + lw, ly + 20), 12, fill=col, outline=WHITE, width=3)
        d.text((cx, ly), label, font=font(24), fill=WHITE, anchor="mm")


def settings_playback():
    """The guide's picture of the tablet's settings app, turned to the playback category, with
    the autoplay setting MAO has just set."""
    shot = guide("tablet/settings.webp")
    g = ImageDraw.Draw(shot)
    panel = shot.getpixel((240, 600))[:3]
    chosen = shot.getpixel((240, 190))[:3]
    card = shot.getpixel((600, 200))[:3]
    g.rounded_rectangle((102, 146, 385, 231), 12, fill=panel)
    g.rounded_rectangle((128, 172, 176, 206), 6, outline=WHITE, width=3)
    g.text((190, 189), "映像", font=font(28), fill=WHITE, anchor="lm")
    g.rounded_rectangle((102, 346, 385, 431), 12, fill=chosen)
    g.ellipse((130, 368, 174, 412), outline=WHITE, width=3)
    g.polygon([(146, 378), (146, 402), (164, 390)], outline=WHITE)
    g.text((190, 389), "再生", font=font(28), fill=WHITE, anchor="lm")
    g.rectangle((420, 130, 1190, 600), fill=panel)
    g.rounded_rectangle((428, 138, 1178, 330), 12, fill=card)
    strip = guide("screen/autoplay-owner-set.webp")
    place(shot, strip, (436, 150, 1170, 150 + 734 * strip.height / strip.width))
    return shot


def c16():
    im = new((58, 50, 56))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((300, 60, 1240, 700), 60, fill=(120, 74, 70))
    d.rounded_rectangle((330, 90, 1210, 670), 46, outline=(160, 104, 90), width=6)
    cx = 860
    tab = (cx - 190, 270, cx + 190, 500)
    scr = tablet_frame(im, tab, radius=28)
    place(im, settings_playback().crop((36, 22, 1244, 786)), scr)
    prone(im, cx, 185, 64, B_COL, facing="down", twin=True, label="RIRIKA")
    prone(im, cx, 640, 85, A_COL, facing="up")
    text(im, (cx, 692), "MAO", 26, anchor="mm", stroke=4, sfill=A_COL)
    d.arc((cx - 150, 140, cx - 90, 200), 120, 240, fill=ORANGE, width=5)
    d.arc((cx + 90, 140, cx + 150, 200), 300, 60, fill=ORANGE, width=5)
    text(im, (cx + 170, 170), "頭をゆらゆら", 24, fill=ORANGE, anchor="lm", stroke=4)
    text(im, (480, 130), "じゅうたん", 24, fill=(235, 205, 195), anchor="mm")
    heading(im, "03", "自動再生", "インスタンスに入ったときに流す動画を、決めておける", "Choose the video that plays when people join")
    note(im, "タブレットから少し引く・2 人は寝そべり", (700, 18))
    return im


def c17():
    im = new((120, 74, 70))
    d = ImageDraw.Draw(im)
    tab = (140, 40, 1140, 672)
    place(im, guide("tablet/settings.webp", (36, 22, 1244, 786)), tab, radius=60)
    sx = (tab[2] - tab[0]) / (1244 - 36)
    rb = (tab[0] + (102 - 36) * sx, tab[1] + (346 - 22) * sx, tab[0] + (385 - 36) * sx, tab[1] + (431 - 22) * sx)
    d.rounded_rectangle((rb[0] - 6, rb[1] - 6, rb[2] + 6, rb[3] + 6), 16, outline=ORANGE, width=6)
    fx, fy = (rb[0] + rb[2]) / 2 + 30, (rb[1] + rb[3]) / 2
    d.polygon([(fx + 60, H), (fx + 220, H), (fx + 40, fy + 70), (fx, fy + 40)], fill=darker(A_COL, 0.8))
    d.rounded_rectangle((fx - 10, fy + 20, fx + 110, fy + 140), 40, fill=A_COL)
    d.line((fx + 20, fy + 40, fx, fy + 4), fill=A_COL, width=30)
    d.ellipse((fx - 15, fy - 12, fx + 15, fy + 18), fill=A_COL)
    action(im, "ホーム → 設定 → 再生", (fx, fy))
    note(im, "真上から・MAO の指", (W - 20, 660))
    return im


def c18():
    im = new((120, 74, 70))
    tab = (50, 40, 1230, 640)
    scr = tablet_frame(im, tab, radius=60)
    crop = settings_playback().crop((420, 130, 1190, 600))
    place(im, crop, scr)
    k = (scr[2] - scr[0]) / crop.width
    st = 734 / 1280
    d = ImageDraw.Draw(im)
    y0 = scr[1] + (150 - 130 + 92 * st) * k
    y1 = scr[1] + (150 - 130 + 122 * st) * k
    d.rounded_rectangle((scr[0] + (436 - 420 + 18 * st) * k - 6, y0 - 6, scr[0] + (436 - 420 + 520 * st) * k + 6, y1 + 6),
                        10, outline=ORANGE, width=6)
    action(im, "流したい動画の URL を入れる", (scr[0] + (436 - 420 + 90 * st) * k, (y0 + y1) / 2 + 30), rise=-110)
    telop(im, "インスタンスに入ったときに流す動画を、決めておく", en="決められるのは、インスタンスを作った人")
    note(im, "タブレットに寄る", (W - 20, 660))
    return im








def specks(im, layer, frac, gather=False, seed=7):
    """The figures drawn on layer, breaking into blue specks: when leaving, the top frac of them is
    gone, scattered up and to the right; when gathering, the bottom frac has formed and the rest
    is still specks drawing in."""
    rnd = random.Random(seed)
    a = layer.getchannel("A")
    box = a.getbbox()
    if not box:
        return
    x0, y0, x1, y1 = box
    keep = Image.new("L", layer.size, 0)
    kd = ImageDraw.Draw(keep)
    parts = []
    step = 8
    for by in range(y0, y1, step):
        for bx in range(x0, x1, step):
            if a.getpixel((min(bx + step // 2, x1 - 1), min(by + step // 2, y1 - 1))) == 0:
                continue
            t = (by - y0) / max(1, y1 - y0)
            n = rnd.random() * 0.22
            gone = t < frac + n - 0.11 if not gather else t < 1 - frac + n - 0.11
            if not gone:
                kd.rectangle((bx, by, bx + step - 1, by + step - 1), fill=255)
                continue
            far = (abs(frac - t) + n) * 320 + rnd.random() * 80
            angle = rnd.uniform(-0.9, -0.2) if not gather else rnd.uniform(0, 2 * math.pi)
            parts.append((bx + math.cos(angle) * far, by + math.sin(angle) * far, rnd.randint(3, 7)))
    kept = Image.new("RGBA", layer.size, (0, 0, 0, 0))
    kept.paste(layer, (0, 0), ImageChops.multiply(a, keep))
    im.alpha_composite(kept)

    def draw(o):
        for x, y, r in parts:
            o.ellipse((x - r * 2, y - r * 2, x + r * 2, y + r * 2), fill=(90, 190, 255, 60))
        for x, y, r in parts:
            o.ellipse((x - r, y - r, x + r, y + r), fill=(170, 230, 255, 230))
    overlay(im, draw)


def two_of_them(arm_b=None, x=820, base=700, h=380):
    """MAO and RIRIKA standing side by side, on a clear layer."""
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    person(layer, x, base, h, A_COL)
    person(layer, x + h * 0.37, base - 10, h * 0.97, B_COL, twin=True, arm=arm_b)
    return layer


def c19():
    im = new(NIGHT["wall"])
    room(im, screen=sample_video(), glow=True)
    specks(im, two_of_them(x=600, base=640, h=470), 0.42)
    note(im, "2 人が青い粒子になって散る")
    return im


def c20():
    im = new(DAY["wall"])
    room(im, mode=DAY, screen=sample_video(time="00:03"), glow=True)
    sofa(im, DAY)
    specks(im, two_of_them(), 0.45, gather=True, seed=11)
    note(im, "朝の部屋・粒子が集まって 2 人に")
    return im


def c21():
    im = new(DAY["wall"])
    room(im, mode=DAY, screen=sample_video(time="00:05"), glow=True)
    sofa(im, DAY)
    im.alpha_composite(two_of_them(arm_b="both"))
    say(im, (960, 690 - 370 + 370 * 0.115 * 1.1), "もう流れてる！", "RIRIKA", side="left")
    sfx(im, (640, 470), "♪", 64, col=WHITE)
    telop(im, "入ったとたん、決めた動画が流れる", en="Step in, and it's already playing")
    return im


def c22():
    im = new(NIGHT["wall"])
    room(im, screen=sample_video(), glow=True)
    hb = person(im, 540, 800, 440, B_COL, "RIRIKA", twin=True, arm="wave")
    ha = person(im, 740, 800, 470, A_COL, "MAO", arm="wave")
    sofa(im)
    for (cx, cy), col in ((hb, (255, 186, 214)), (ha, (150, 236, 236))):
        for k in range(7):
            a = math.radians(200 + k * 24)
            sparkle(im, cx + 100 * math.cos(a), cy - 20 + 100 * math.sin(a), 12 + (k % 3) * 6, col)
    telop(im, "URL ひとつで、みんなの上映会。", 50, y=H - 90, en="One URL. Movie night for everyone.")
    note(im, "腰から振り向く → 2 人を中心にアイリスで閉じる")
    return im


def c23():
    im = brand_background()
    place(im, logo(170), (555, 50, 725, 220))
    text(im, (640, 280), "KawaPlayer", 84, anchor="mm")
    text(im, (640, 360), "無料 ・ VCC で導入", 40, anchor="mm")
    d = ImageDraw.Draw(im)
    overlay(im, lambda o: o.rounded_rectangle((250, 410, 1030, 560), 18, fill=(0, 40, 60, 90)))
    d.text((290, 450), "Booth", font=font(30), fill=WHITE, anchor="lm")
    d.text((470, 450), "booth.pm/ja/items/8155190", font=font(30), fill=WHITE, anchor="lm")
    d.text((290, 515), "入れ方", font=font(30), fill=WHITE, anchor="lm")
    d.text((470, 515), "playlist.vrc-hub.com/docs/developers", font=font(30), fill=WHITE, anchor="lm")
    return im


def c24():
    im = new((0, 0, 0))
    d = ImageDraw.Draw(im)
    rows = (("アバター", "（作者の指定の表記）"), ("ワールド・小物", "（購入した素材の名前と作者）"),
            ("シェーダー・フォント", "（名前と作者）"), ("BGM・効果音", "この動画のために作ったもの"),
            ("", "KawaPlayer は YamaPlayer（koorimizuw）を元にフォーク開発しています"))
    for k, (role, who) in enumerate(rows):
        y = 220 + k * 70 + (30 if not role else 0)
        if not role:
            d.text((640, y), who, font=font(30), fill=WHITE, anchor="mm")
            continue
        d.text((600, y), role, font=font(30), fill=(150, 156, 162), anchor="rm")
        d.text((640, y), who, font=font(30), fill=WHITE, anchor="lm")
    note(im, "暗転してから")
    return im


CUTS = {name: fn for name, fn in globals().items() if name[:1] == "c" and name[1:].isdigit()}
FIGURES = {"captions": captions}


def main(names):
    for name in names or sorted(CUTS) + sorted(FIGURES):
        im = CUTS[name]() if name in CUTS else FIGURES[name]()
        if name in CUTS:
            tag(im, name)
        im.convert("RGB").resize(OUT, Image.LANCZOS).save(os.path.join(HERE, name + ".webp"), "WEBP",
                                                         quality=82, method=6)
        print(name, os.path.getsize(os.path.join(HERE, name + ".webp")) // 1024, "KB")


if __name__ == "__main__":
    main(sys.argv[1:])
