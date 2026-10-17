"""Draws the storyboard's frames (c01.webp, c02.webp, ...) for README.md.

The frames are sketches. The people are silhouettes: MAO (teal), who runs the player,
and RIRIKA (pink, with twin tails), the friend who joins. What KawaPlayer shows is cut from
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
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

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


def heading(im, num, title, sub=None, en=None):
    """The chapter heading at the bottom left."""
    d = ImageDraw.Draw(im)
    x = 52
    nw = d.textlength(num, font=font(76)) + 22 if num else 0
    lines = [(title, 46, WHITE)]
    if sub:
        lines.append((sub, 28, (222, 228, 232)))
    if en:
        lines.append((en, 22, (160, 172, 182)))
    widths = [d.textlength(s, font=font(z)) for s, z, _ in lines]
    hgt = sum(z + 12 for _, z, _ in lines)
    top = H - 48 - hgt
    overlay(im, lambda o: o.rounded_rectangle((x - 22, top - 20, x + nw + max(widths) + 30, H - 28), 18,
                                              fill=(10, 14, 20, 185)))
    if num:
        d.text((x, top + 2), num, font=font(76), fill=TEAL)
        d.line((x, top + 92, x + nw - 26, top + 92), fill=TEAL, width=4)
    y = top
    for s, z, c in lines:
        d.text((x + nw, y), s, font=font(z), fill=c)
        y += z + 12


def telop(im, s, size=44, y=H - 66, en=None):
    if en:
        text(im, (W / 2, y + 30), en, 24, fill=(214, 222, 228), anchor="mm", stroke=4)
        y -= 22
    text(im, (W / 2, y), s, size, anchor="mm", stroke=6)


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
    """A standing figure, base at its feet and h tall. arm: None, 'up', 'wave' or 'reach'."""
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


def sample_photo(w=640, h=360):
    """A made-up picture: mountains under a blue sky."""
    p = gradient(w, h, [(120, 180, 240), (200, 230, 250)])
    d = ImageDraw.Draw(p)
    d.ellipse((w * 0.72, h * 0.12, w * 0.84, h * 0.33), fill=(255, 246, 200))
    d.polygon([(0, h), (0, h * 0.62), (w * 0.22, h * 0.36), (w * 0.42, h * 0.6), (w * 0.6, h * 0.3),
               (w * 0.86, h * 0.64), (w, h * 0.52), (w, h)], fill=(70, 130, 110))
    d.polygon([(w * 0.6, h * 0.3), (w * 0.53, h * 0.4), (w * 0.58, h * 0.38), (w * 0.62, h * 0.42),
               (w * 0.66, h * 0.37)], fill=(245, 250, 255))
    d.rectangle((0, h * 0.84, w, h), fill=(110, 160, 90))
    return p




def logo(size, col=WHITE):
    src = Image.open(LOGO).convert("RGBA").resize((size, size), Image.LANCZOS)
    tinted = Image.new("RGBA", src.size, col + (255,))
    tinted.putalpha(src.getchannel("A"))
    return tinted


def brand_background():
    g = gradient(W, H, [MINT, TEAL, BLUE], horizontal=True)
    overlay(g, lambda o: o.ellipse((-300, -400, 900, 500), fill=(255, 255, 255, 28)))
    return g


def split(im, left, right, labels, y=130):
    """Two people's screens side by side."""
    d = ImageDraw.Draw(im)
    pw, ph = 590, 332
    for k, (src, lab) in enumerate(zip((left, right), labels)):
        x = 34 + k * (pw + 32)
        d.rounded_rectangle((x - 6, y - 6, x + pw + 6, y + ph + 6), 10, fill=(70, 78, 90))
        place(im, src, (x, y, x + pw, y + ph))
        col = A_COL if k == 0 else B_COL
        tw = d.textlength(lab, font=font(28))
        d.rounded_rectangle((x, y - 58, x + tw + 30, y - 14), 12, fill=col)
        d.text((x + 15, y - 36), lab, font=font(28), fill=WHITE, anchor="lm")


# --------------------------------------------------------------------- the cuts


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
    im = new((40, 44, 70))
    night_room_blur(im)
    d = ImageDraw.Draw(im)
    dark = darker(A_COL, 0.55)
    # MAO from the side, walking to the right; the head runs off the top.
    d.polygon([(560, -40), (300, 10), (90, 120), (40, 210), (170, 190), (90, 290), (260, 250),
               (380, 330), (470, 300), (560, 160)], fill=dark)
    d.ellipse((520, -150, 720, 60), fill=A_COL)
    d.polygon([(700, -10), (735, 30), (705, 45)], fill=A_COL)
    d.polygon([(500, 90), (690, 100), (740, 430), (720, H), (440, H), (450, 420)], fill=A_COL)
    d.line([(540, 150), (470, 330), (430, 470)], fill=darker(A_COL, 0.85), width=62, joint="curve")
    d.ellipse((395, 440, 465, 510), fill=darker(A_COL, 0.85))
    d.line([(660, 160), (720, 330), (790, 440)], fill=A_COL, width=62, joint="curve")
    d.ellipse((755, 410, 825, 480), fill=A_COL)
    foliage(im, [(40, 380, 260, 70), (130, 520, 300, 35), (40, 640, 280, -20), (210, 690, 240, 60),
                 (1180, 560, 280, -40), (1250, 420, 260, 80), (1100, 690, 260, 10)])
    arrow(im, (440, 640), (820, 640))
    text(im, (630, 600), "歩く", 30, fill=ORANGE, anchor="mm", stroke=4)
    arrow(im, (860, 120), (1060, 120))
    text(im, (960, 160), "カメラも並んで右へ", 26, fill=ORANGE, anchor="mm", stroke=4)
    note(im, "寄り・横から追う")
    return im


def c02():
    im = new((40, 44, 70))
    night_room_blur(im)
    d = ImageDraw.Draw(im)
    d.polygon([(0, 420), (W, 380), (W, H), (0, H)], fill=(78, 58, 50))
    d.line((0, 420, W, 380), fill=(110, 84, 70), width=5)
    tab = [(330, 470), (850, 440), (900, 610), (300, 650)]
    d.polygon(tab, fill=(12, 14, 18), outline=(130, 136, 144), width=5)
    scr = [(352, 482), (828, 455), (870, 598), (326, 634)]
    d.polygon(scr, fill=(22, 30, 40))
    def ripple(o):
        for k in range(3):
            r = 40 + k * 42
            o.ellipse((610 - r, 548 - r * 0.45, 610 + r, 548 + r * 0.45), outline=(140, 235, 245, 200 - k * 55), width=5)
    overlay(im, ripple)
    d.polygon([(760, 0), (1000, 0), (700, 470), (640, 430)], fill=darker(A_COL, 0.8))
    d.rounded_rectangle((600, 400, 720, 500), 40, fill=A_COL)
    d.line((640, 470, 612, 545), fill=A_COL, width=30)
    d.ellipse((597, 530, 627, 560), fill=A_COL)
    foliage(im, [(1170, 300, 280, -50), (1240, 470, 260, 60), (1120, 640, 300, 20)])
    arrow(im, (520, 300), (575, 470))
    text(im, (500, 270), "触れる", 30, fill=ORANGE, anchor="mm", stroke=4)
    note(im, "手元の大写し")
    return im


def c03():
    im = new(NIGHT["wall"])
    room(im, screen=guide("screen/controls.webp"), glow=True)
    person(im, 250, 1120, 760, A_COL)
    foliage(im, [(1200, 560, 280, -40), (1260, 420, 240, 70)])
    sfx(im, (980, 470), "ポン", 56)
    arrow(im, (640, 690), (640, 450))
    text(im, (700, 560), "手元から上へ", 26, fill=ORANGE, anchor="lm", stroke=4)
    note(im, "肩越しに、点いたスクリーン")
    return im


def c04():
    im = brand_background()
    place(im, logo(250), (515, 90, 765, 340))
    text(im, (640, 440), "KawaPlayer", 104, anchor="mm")
    text(im, (640, 530), "VRChat の動画プレイヤー", 38, anchor="mm")
    return im


def c05():
    im = new((228, 232, 236))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((110, 40, 1240, 640), 18, fill=WHITE, outline=(190, 196, 202), width=3)
    d.rounded_rectangle((110, 40, 1240, 96), 18, fill=(236, 239, 242))
    d.rectangle((110, 76, 1240, 96), fill=(236, 239, 242))
    for k, c in enumerate(((240, 100, 90), (240, 190, 70), (110, 200, 110))):
        d.ellipse((136 + k * 30, 58, 154 + k * 30, 76), fill=c)
    d.rounded_rectangle((250, 52, 900, 84), 16, fill=WHITE)
    d.text((270, 68), "playlist.vrc-hub.com", font=font(22), fill=(100, 108, 116), anchor="lm")
    src = guide("vhub/detail.webp")
    b = box_at(src, 650, 110, h=470)
    place(im, src, b)
    s = (b[2] - b[0]) / src.width
    btn = (b[0] + 18 * s - 8, b[1] + 432 * s - 8, b[0] + 610 * s + 8, b[1] + 503 * s + 8)
    d.rounded_rectangle(btn, 26, outline=ORANGE, width=7)
    bubble(im, 380, 380, "コピー！", 40, tail=(btn[0] + 6, btn[1] + 24))
    heading(im, "01", "VHub のプレイリスト", "URL ひとつで、まるごと読み込む", "VHub playlists, from one URL")
    return im


def c06():
    im = new(INK)
    place(im, guide("screen/controls.webp"), (0, 0, W, H))
    overlay(im, lambda o: o.rectangle((0, 0, W, H), fill=(0, 0, 0, 90)))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((260, 270, 1180, 360), 16, fill=(40, 44, 50), outline=(110, 190, 230), width=4)
    url = "https://playlist.vrc-hub.com/r/default/…"
    d.text((294, 315), url, font=font(34), fill=WHITE, anchor="lm")
    cx = 304 + d.textlength(url, font=font(34))
    d.line((cx, 290, cx, 340), fill=WHITE, width=4)
    text(im, (260, 236), "URL を貼り付けて Enter", 30, fill=(200, 232, 245))
    person(im, 40, 1500, 1250, A_COL)
    note(im, "UI の大写し（斜めから）")
    return im


def c07():
    im = new(INK)
    place(im, guide("screen/playlist-tracks.webp"), (0, 0, W, H))
    d = ImageDraw.Draw(im)
    inset = (300, 40, 780, 310)
    d.rounded_rectangle((inset[0] - 6, inset[1] - 6, inset[2] + 6, inset[3] + 6), 10, fill=ORANGE)
    place(im, guide("screen/playlist-loaded.webp"), inset)
    text(im, (540, 336), "はじめに「追加しました」→ 一覧へ", 26, fill=ORANGE, anchor="mm", stroke=4)
    arrow(im, (1262, 170), (1262, 560), width=6)
    telop(im, "曲の一覧が、そのまま並ぶ", en="The whole list, ready to play")
    note(im, "一覧に沿って下へ")
    return im


def c08():
    im = new(NIGHT["wall"])
    room(im, screen=sample_video(), glow=True)
    sofa(im)
    head = person(im, 1060, 700, 420, A_COL, "MAO", arm="up")
    bubble(im, 1080, 96, "きたー！", 40, tail=(head[0] + 62, head[1] - 96))
    return im


def c09():
    im = new(NIGHT["wall"])
    room(im, screen=sample_video(), glow=True, door=True)
    join_ring(im, 1130, 470, 90, 210)
    hb = person(im, 1130, 690, 400, B_COL, "RIRIKA", twin=True)
    ha = person(im, 760, 690, 400, A_COL, "MAO", arm="wave")
    bubble(im, 860, 130, "もう始まってる？", 34, tail=(hb[0] - 50, hb[1] - 100))
    heading(im, "02", "みんなで同じ映像", "あとから来ても、同じ場面から", "Everyone on the same moment")
    return im


def c10():
    im = new((22, 26, 32))
    split(im, sample_video(time="01:23"), sample_video(time="01:23"), ("MAO の画面", "RIRIKA の画面（入ったばかり）"))
    d = ImageDraw.Draw(im)
    for x in (34, 34 + 622):
        d.rounded_rectangle((x + 4, 418, x + 96, 460), 10, outline=ORANGE, width=5)
    text(im, (640, 510), "どちらも 01:23", 34, fill=ORANGE, anchor="mm", stroke=4)
    telop(im, "入ったばかりでも、同じところから", en="Join late, watch from the same point")
    return im


def c11():
    im = new(NIGHT["wall"])
    room(im, screen=sample_video(), glow=True)
    ha = person(im, 540, 800, 470, A_COL, "MAO")
    hb = person(im, 750, 800, 470, B_COL, "RIRIKA", twin=True)
    sofa(im)
    bubble(im, 840, 180, "ここ好き！", 36, tail=(hb[0] + 50, hb[1] - 100))
    bubble(im, 250, 180, "わかる〜", 36, tail=(ha[0] - 50, ha[1] - 100))
    note(im, "2 人の後ろから")
    return im


def c12():
    im = new(NIGHT["wall"])
    room(im, screen=sample_video(), glow=True)
    im = im.filter(ImageFilter.GaussianBlur(10))
    box = (230, 60, 1050, 579)
    tablet_home(im, box)
    hands(im, box)
    heading(im, "03", "タブレット", "手元で、プレイヤーを操作", "A tablet in your hands")
    note(im, "MAO の目線")
    return im


def c13():
    im = new(NIGHT["wall"])
    room(im, screen=sample_video(), glow=True)
    im = im.filter(ImageFilter.GaussianBlur(10))
    src = guide("tablet/rotate.webp", (797, 20, 1227, 700))
    box = box_at(src, 0, 40, h=620)
    box = (640 - (box[2] - box[0]) / 2, 40, 640 + (box[2] - box[0]) / 2, 660)
    place(im, src, box, radius=40)
    hands(im, box)
    d = ImageDraw.Draw(im)
    d.arc((150, 150, 390, 390), 200, 340, fill=ORANGE, width=8)
    arrow(im, (360, 195), (388, 250))
    text(im, (270, 430), "縦に持つと", 30, fill=ORANGE, anchor="mm", stroke=4)
    text(im, (270, 472), "画面も縦に", 30, fill=ORANGE, anchor="mm", stroke=4)
    note(im, "MAO の目線")
    return im


def c14():
    im = new(NIGHT["wall"])
    room(im, screen=sample_photo(), glow=True)
    hb = person(im, 1080, 690, 400, B_COL, "RIRIKA", twin=True, arm="wave")
    bubble(im, 850, 90, "おおー！", 38, tail=(hb[0] - 70, hb[1] - 96))
    d = ImageDraw.Draw(im)
    panel = (24, 76, 470, 382)
    d.rounded_rectangle(panel, 14, fill=(16, 18, 22), outline=A_COL, width=4)
    scr = tablet_frame(im, (44, 120, 450, 370))
    place(im, sample_photo(), scr)
    d.rounded_rectangle((scr[0] + 8, scr[1] + 8, scr[0] + 44, scr[1] + 44), 10, fill=(0, 0, 0, 160))
    d.text((scr[0] + 26, scr[1] + 26), "<", font=font(26), fill=WHITE, anchor="mm")
    sb = (scr[2] - 160, scr[1] + 8, scr[2] - 8, scr[1] + 46)
    d.rounded_rectangle(sb, 12, fill=(30, 60, 80), outline=(110, 200, 240), width=3)
    d.text(((sb[0] + sb[2]) / 2, (sb[1] + sb[3]) / 2), "KawaPlayer へ", font=font(20), fill=WHITE, anchor="mm")
    d.rounded_rectangle((sb[0] - 6, sb[1] - 6, sb[2] + 6, sb[3] + 6), 14, outline=ORANGE, width=5)
    text(im, (44, 86), "MAO のタブレット", 22, fill=A_COL)
    arrow(im, (470, 240), (560, 260))
    telop(im, "手元の画像を、スクリーンへ", en="Show a picture on the big screen")
    return im


def c15():
    im = new(NIGHT["wall"])
    room(im, screen=sample_video(), glow=True)
    im = im.filter(ImageFilter.GaussianBlur(10))
    box = (230, 50, 1050, 569)
    place(im, guide("tablet/visitors.webp", (36, 22, 1244, 786)), box, radius=56)
    hands(im, box)
    telop(im, "誰が来たかも、ひと目で", en="See who came by")
    note(im, "MAO の目線")
    return im


def c16():
    im = new(INK)
    place(im, guide("screen/settings-playback.webp"), (0, 0, W, H))
    overlay(im, lambda o: o.rectangle((0, 0, W, H), fill=(0, 0, 0, 150)))
    src = guide("screen/autoplay-owner-set.webp")
    b = (60, 150, 1220, 150 + 1160 * src.height / src.width)
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((b[0] - 8, b[1] - 8, b[2] + 8, b[3] + 8), 12, fill=(70, 80, 92))
    place(im, src, b)
    s = (b[2] - b[0]) / src.width
    d.rounded_rectangle((b[0] + 20 * s, b[1] + 92 * s, b[0] + 520 * s, b[1] + 120 * s), 8, outline=ORANGE, width=5)
    bubble(im, 700, 380, "インスタンスを作った人が決める", 30, tail=(b[0] + 400 * s, b[1] + 124 * s))
    heading(im, "04", "自動再生", "入ったときに流す動画を、ワールドの中で決める", "Autoplay, set inside the world")
    return im


def c17():
    im = new(DAY["wall"])
    room(im, mode=DAY, screen=sample_video(time="00:02"), glow=True, door=True)
    sofa(im, DAY)
    join_ring(im, 1130, 470, 90, 210)
    ha = person(im, 1130, 690, 400, A_COL, "MAO")
    bubble(im, 820, 120, "いつものやつ！", 36, tail=(ha[0] - 40, ha[1] - 100))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((140, 30, 420, 110), 16, fill=(10, 14, 20))
    d.text((280, 70), "次の日", font=font(46), fill=WHITE, anchor="mm")
    sfx(im, (640, 470), "♪", 64, col=WHITE)
    return im










def c18():
    im = new(NIGHT["wall"])
    room(im, screen=sample_video(), glow=True)
    person(im, 540, 800, 470, A_COL, "MAO")
    person(im, 750, 800, 470, B_COL, "RIRIKA", twin=True)
    sofa(im)
    zoom(im, (200, 60, 1080, 560), inward=False)
    telop(im, "同じ映像を、同じ瞬間に。（仮）", 50, y=H - 90)
    note(im, "ゆっくり引く")
    return im


def c19():
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


def c20():
    im = new((0, 0, 0))
    d = ImageDraw.Draw(im)
    rows = (("アバター", "MAO・RIRIKA（作者の指定の表記）"), ("BGM", "（曲名・作者）"))
    for k, (role, who) in enumerate(rows):
        y = 320 + k * 70
        d.text((600, y), role, font=font(30), fill=(150, 156, 162), anchor="rm")
        d.text((640, y), who, font=font(30), fill=WHITE, anchor="lm")
    note(im, "暗転してから")
    return im


CUTS = {name: fn for name, fn in globals().items() if name[:1] == "c" and name[1:].isdigit()}


def main(names):
    for name in names or sorted(CUTS):
        im = CUTS[name]()
        tag(im, name)
        im.convert("RGB").resize(OUT, Image.LANCZOS).save(os.path.join(HERE, name + ".webp"), "WEBP",
                                                         quality=82, method=6)
        print(name, os.path.getsize(os.path.join(HERE, name + ".webp")) // 1024, "KB")


if __name__ == "__main__":
    main(sys.argv[1:])
