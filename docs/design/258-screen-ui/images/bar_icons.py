"""bar_icons.py: draws the images the new bottom bar of the screen UI uses (issue #258) into Assets/Images.

White shapes on transparent, 512 x 512 like the other icons (stroke 30), drawn at 4x and reduced.
bar-circle.png is the round ground of the bar's buttons, bar-scrim.png the dark gradient behind the bar
(transparent at the top, opaque at the bottom; the Image's colour sets the darkness).
Run from the repository root: python docs/design/258-screen-ui/images/bar_icons.py
"""
from PIL import Image, ImageDraw

S = 4                      # supersampling
W = 512 * S
OUT = "Assets/Images/"


def canvas():
    im = Image.new("L", (W, W), 0)
    return im, ImageDraw.Draw(im)


def save(mask, name, size=512):
    mask = mask.resize((size, size), Image.LANCZOS)
    im = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    im.putalpha(mask)
    im.save(OUT + name)


def p(*xy):
    return [v * S for v in xy]


def round_poly(d, pts, r):
    """A filled polygon with corners rounded by r (pixels at 512)."""
    pts = [(x * S, y * S) for x, y in pts]
    d.polygon(pts, fill=255)
    d.line(pts + [pts[0]], fill=255, width=int(2 * r * S), joint="curve")
    for x, y in pts:
        d.ellipse([x - r * S, y - r * S, x + r * S, y + r * S], fill=255)


def stroke(d, a, b, w=30):
    d.line([a[0] * S, a[1] * S, b[0] * S, b[1] * S], fill=255, width=w * S)
    for x, y in (a, b):
        d.ellipse(p(x - w / 2, y - w / 2, x + w / 2, y + w / 2), fill=255)


im, d = canvas(); round_poly(d, [(176, 132), (176, 380), (388, 256)], 22); save(im, "bar-play.png")
im, d = canvas(); d.rounded_rectangle(p(146, 116, 226, 396), radius=28 * S, fill=255); d.rounded_rectangle(p(286, 116, 366, 396), radius=28 * S, fill=255); save(im, "bar-pause.png")
im, d = canvas(); d.rounded_rectangle(p(136, 136, 376, 376), radius=44 * S, fill=255); save(im, "bar-stop.png")
im, d = canvas(); d.rounded_rectangle(p(112, 128, 168, 384), radius=28 * S, fill=255); round_poly(d, [(392, 140), (392, 372), (212, 256)], 20); save(im, "bar-prev.png")
im, d = canvas(); d.rounded_rectangle(p(344, 128, 400, 384), radius=28 * S, fill=255); round_poly(d, [(120, 140), (120, 372), (300, 256)], 20); save(im, "bar-next.png")
im, d = canvas()
stroke(d, (88, 132), (300, 132)); stroke(d, (88, 222), (300, 222)); stroke(d, (88, 312), (220, 312))
stroke(d, (408, 116), (408, 340))
d.ellipse(p(318, 300, 422, 404), fill=255)
save(im, "bar-list.png")
im, d = canvas()
for x in (128, 256, 384):
    d.ellipse(p(x - 40, 216, x + 40, 296), fill=255)
save(im, "bar-more.png")
im, d = canvas(); d.ellipse(p(0, 0, 512, 512), fill=255); save(im, "bar-circle.png", 256)

# the list panel (P3): the queue tab (lines and a plus), the history tab (a clock), and the playlist's
# actions: its URL and delete (a bin); the playlist tab uses bar-list.png. The URL is shown to copy, so its
# icon is copy (two sheets) with a link on the front one (P6); a track's URL and the dialog's copy field use it too
im, d = canvas()
stroke(d, (88, 132), (340, 132)); stroke(d, (88, 232), (340, 232)); stroke(d, (88, 332), (236, 332))
stroke(d, (360, 268), (360, 396)); stroke(d, (296, 332), (424, 332))
save(im, "bar-queue.png")
im, d = canvas()
d.ellipse(p(64, 64, 448, 448), outline=255, width=30 * S)
stroke(d, (256, 150), (256, 256)); stroke(d, (256, 256), (322, 300))
save(im, "bar-history.png")
im, d = canvas()
d.rounded_rectangle(p(150, 60, 450, 360), radius=56 * S, outline=255, width=30 * S)
d.rounded_rectangle(p(52, 130, 380, 470), radius=70 * S, fill=0)
d.rounded_rectangle(p(66, 144, 366, 456), radius=56 * S, outline=255, width=30 * S)
link = Image.open(OUT + "link.png").split()[-1].resize((210 * S, 210 * S), Image.LANCZOS)
im.paste(link, (112 * S, 196 * S), link)
save(im, "bar-url.png")
im, d = canvas()
stroke(d, (104, 140), (408, 140)); stroke(d, (208, 140), (208, 100)); stroke(d, (208, 100), (304, 100)); stroke(d, (304, 100), (304, 140))
stroke(d, (148, 188), (174, 420)); stroke(d, (174, 420), (338, 420)); stroke(d, (338, 420), (364, 188))
stroke(d, (226, 244), (230, 360)); stroke(d, (286, 244), (282, 360))
save(im, "bar-trash.png")

# the gradient: 0 at the top, 0.72 at 45 % down, 1 at the bottom
h = 256
g = Image.new("RGBA", (4, h), (255, 255, 255, 0))
for y in range(h):
    t = y / (h - 1)
    a = 0.72 * t / 0.45 if t < 0.45 else 0.72 + 0.28 * (t - 0.45) / 0.55
    for x in range(4):
        g.putpixel((x, y), (255, 255, 255, round(255 * a)))
g.save(OUT + "bar-scrim.png")
print("done")
