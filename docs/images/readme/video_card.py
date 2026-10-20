"""Draws the README's way in to the intro video (video-card.webp): the logo, the catchphrase and a play button
on KawaPlayer's colours, with a seek bar marked at the video's three chapters. The README links it to YouTube.
The video's own frames are not used: they show the avatars, whose terms do not allow them on GitHub.
After changing it, run:
  python video_card.py
Needs Pillow. The font is the package's own Zen Maru Gothic (Assets/Fonts). The card is 1280 x 720 WebP, drawn at
twice that size and reduced, so its edges are smooth.
"""
import math
import os

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
FONT = os.path.join(ROOT, "Assets", "Fonts", "ZenMaruGothic-Regular.ttf")
LOGO = os.path.join(ROOT, "Assets", "Images", "kawaplayer-logo.png")

S = 2  # drawn at twice the size
W, H = 1280 * S, 720 * S
WHITE = (255, 255, 255)
INK = (10, 52, 70)
MINT = (107, 235, 170)
TEAL = (63, 196, 198)
BLUE = (34, 166, 203)
LENGTH = 84.5  # the video, in seconds
CHAPTERS = (12.0, 30.5, 52.0)


def font(size):
    return ImageFont.truetype(FONT, size * S)


def gradient(stops):
    """The logo's three colours, left to right."""
    im = Image.new("RGBA", (W, H))
    px = im.load()
    for x in range(W):
        u = x / (W - 1) * (len(stops) - 1)
        k = min(int(u), len(stops) - 2)
        f = u - k
        c = tuple(round(stops[k][i] + (stops[k + 1][i] - stops[k][i]) * f) for i in range(3))
        for y in range(H):
            px[x, y] = c + (255,)
    return im


def overlay(im, draw):
    layer = Image.new("RGBA", im.size, (0, 0, 0, 0))
    draw(ImageDraw.Draw(layer))
    im.alpha_composite(layer)


def text(d, xy, s, size, fill=WHITE, anchor="la", bold=0):
    d.text((xy[0] * S, xy[1] * S), s, font=font(size), fill=fill, anchor=anchor, stroke_width=bold * S, stroke_fill=fill)


def diamond(d, cx, cy, r, fill):
    d.polygon([(cx * S, (cy - r) * S), ((cx + r) * S, cy * S), (cx * S, (cy + r) * S), ((cx - r) * S, cy * S)], fill=fill)


def main():
    im = gradient([MINT, TEAL, BLUE])
    # a soft light at the top left, and the river's waves along the bottom
    overlay(im, lambda o: o.ellipse((-300 * S, -420 * S, 900 * S, 480 * S), fill=(255, 255, 255, 30)))

    def waves(o):
        for k, (y, a) in enumerate(((560, 34), (590, 24))):
            pts = []
            for x in range(-20, 1300, 8):
                pts.append((x * S, (y + 10 * math.sin(x / 70 + k * 1.7)) * S))
            o.line(pts, fill=(255, 255, 255, a), width=4 * S)
    overlay(im, waves)

    # the logo, white
    src = Image.open(LOGO).convert("RGBA").resize((380 * S, 380 * S), Image.LANCZOS)
    white = Image.new("RGBA", src.size, WHITE + (255,))
    white.putalpha(src.getchannel("A"))
    im.alpha_composite(white, (90 * S, 120 * S))

    d = ImageDraw.Draw(im)
    text(d, (520, 150), "KawaPlayer", 92, bold=1)
    text(d, (526, 262), "VRChat の動画プレイヤー", 38)
    text(d, (526, 340), "URL ひとつで、みんなの上映会。", 46, bold=1)

    # the play button
    bx, by, bw, bh = 526, 430, 420, 84
    overlay(im, lambda o: o.rounded_rectangle(((bx + 4) * S, (by + 6) * S, (bx + bw + 4) * S, (by + bh + 6) * S), bh // 2 * S, fill=(0, 40, 60, 60)))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((bx * S, by * S, (bx + bw) * S, (by + bh) * S), bh // 2 * S, fill=WHITE)
    cx, cy, r = bx + 46, by + bh / 2, 26
    d.ellipse(((cx - r) * S, (cy - r) * S, (cx + r) * S, (cy + r) * S), fill=TEAL)
    d.polygon([((cx - 8) * S, (cy - 13) * S), ((cx + 14) * S, cy * S), ((cx - 8) * S, (cy + 13) * S)], fill=WHITE)
    text(d, (bx + 92, cy), "紹介動画を観る（1:24）", 32, fill=INK, anchor="lm", bold=1)

    # a seek bar with the chapters, as on the video
    x0, x1, y = 60, 1220, 650
    d.line((x0 * S, y * S, x1 * S, y * S), fill=(255, 255, 255, 255), width=4 * S)
    for t in CHAPTERS:
        diamond(d, x0 + (x1 - x0) * t / LENGTH, y, 9, WHITE)
    d.ellipse(((x0 - 10) * S, (y - 10) * S, (x0 + 10) * S, (y + 10) * S), fill=WHITE)
    text(d, (x0, y + 22), "0:00", 24)
    text(d, (x1, y + 22), "1:24", 24, anchor="ra")
    for t, s in zip(CHAPTERS, ("VHub のプレイリスト", "タブレット", "自動再生")):
        text(d, (x0 + (x1 - x0) * t / LENGTH, y - 22), s, 22, anchor="md")

    out = os.path.join(HERE, "video-card.webp")
    im.convert("RGB").resize((W // S, H // S), Image.LANCZOS).save(out, "WEBP", quality=86, method=6)
    print(out, os.path.getsize(out) // 1024, "KB")


if __name__ == "__main__":
    main()
