"""Draws the marks on the guide's pictures.

Each picture in vcc/, unity/, screen/ and tablet/ is the one of the same name in
original/, which has no marks, with what MARKS lists drawn on it: an outline around
what to press or fill in, a numbered badge, and a caption with an arrow to the
outline. Coordinates are pixels of the original. After changing MARKS or replacing
an original, run:

  python annotate.py <font> [name ...]

<font> is Zen Maru Gothic Regular (https://fonts.google.com/specimen/Zen+Maru+Gothic).
Needs Pillow. Without names, every picture is made again.

The pictures are WebP: those of VCC and Unity at their captured size, lossless or
quality 90, whichever is smaller; those of the world and of VRChat's menus at most 1280
wide (as the originals are), at quality 80.
"""
import io
import math
import os
import sys
from collections import Counter

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ACCENT = (255, 102, 0)
WHITE = (255, 255, 255)
SS = 2  # Marks are drawn at twice the size and reduced, so their edges are smooth.

# name: pad (margin added around the original, for captions), scale (size of the
# marks), marks. A mark: box, n (badge), badge (where, if not the box's top-left),
# cap (caption) and at (its centre).
MARKS = {
    "vcc/listing-page": dict(scale=0.9, marks=[
        dict(box=(526, 168, 640, 204), cap="ここを押す", at=(290, 186)),
    ]),
    "vcc/add-repository": dict(scale=0.72, pad=dict(bottom=90), marks=[
        dict(box=(236, 108, 302, 136), n=1),
        dict(box=(914, 172, 1046, 198), n=2),
        dict(box=(166, 250, 818, 286), n=3, cap="URL を貼る", at=(420, 350)),
        dict(box=(858, 250, 954, 284), n=4, cap="押す", at=(906, 350)),
    ]),
    "vcc/repository-list": dict(scale=0.6, pad=dict(top=8, bottom=70, left=8, right=8), marks=[
        dict(box=(16, 16, 196, 43), cap="これがあれば追加できている", at=(330, 92)),
    ]),
    "vcc/add-package": dict(scale=0.8, pad=dict(bottom=64), marks=[
        dict(box=(1203, 13, 1239, 49), cap="＋ を押す", at=(1060, 100)),
    ]),
    "unity/gameobject-menu": dict(scale=0.7, pad=dict(top=24, left=24), marks=[
        dict(box=(2, 1, 118, 27), n=1),
        dict(box=(8, 242, 400, 276), n=2),
        dict(box=(410, 244, 632, 276), n=3, badge=(636, 240)),
    ]),
    "unity/hierarchy-menu": dict(scale=0.7, marks=[
        dict(box=(19, 190, 328, 223), n=1),
        dict(box=(340, 192, 564, 222), n=2, badge=(568, 188)),
    ]),
    "unity/inspector": dict(scale=0.72, marks=[
        dict(box=(285, 360, 581, 385), n=1),
        dict(box=(24, 428, 832, 548), n=2),
        dict(box=(24, 563, 832, 945), n=3),
        dict(box=(24, 961, 832, 1051), n=4),
        dict(box=(24, 1067, 832, 1207), n=5),
        dict(box=(24, 1223, 832, 1380), n=6),
    ]),
    "unity/playlist-editor": dict(scale=0.8, pad=dict(top=8), marks=[
        dict(box=(855, 37, 1008, 62), n=1, cap="VHub から取り込む", at=(1010, 190)),
        dict(box=(469, 177, 553, 204), n=2, cap="手で作る", at=(300, 330)),
    ]),
    "unity/playlist-import": dict(scale=0.8, pad=dict(top=8), marks=[
        dict(box=(197, 76, 1178, 101), n=2, cap="VHub の共有 URL を貼る", at=(960, 320)),
        dict(box=(1186, 76, 1339, 101), n=3),
        dict(box=(1302, 37, 1346, 62), n=4),
    ]),
    "unity/module-manager": dict(scale=0.72, marks=[
        dict(box=(28, 423, 834, 537), n=1),
        dict(box=(746, 615, 828, 1163), n=2),
    ]),
    "unity/build": dict(scale=0.72, pad=dict(left=16), marks=[
        dict(box=(9, 257, 404, 288), n=1),
        dict(box=(10, 304, 631, 402), n=2),
    ]),
    "unity/tablet-inspector": dict(scale=0.72, pad=dict(right=270), marks=[
        dict(box=(274, 430, 664, 456), n=1, cap="KawaPlayer を入れる", at=(826, 443)),
        dict(box=(28, 700, 664, 756), n=2, cap="元の場所に戻す", at=(826, 728)),
    ]),
    "vrchat/quick-menu": dict(scale=0.8, pad=dict(bottom=64), marks=[
        dict(box=(736, 522, 796, 582), n=1, cap="歯車を押す", at=(600, 640)),
    ]),
    "vrchat/quick-menu-settings": dict(scale=0.8, marks=[
        dict(box=(772, 18, 822, 66), n=2, badge=(748, 42), cap="全画面にする", at=(560, 140)),
    ]),
    "vrchat/untrusted-urls": dict(scale=0.8, marks=[
        dict(box=(12, 338, 240, 393), n=3, badge=(244, 334)),
        dict(box=(986, 334, 1068, 378), n=4, cap="オンにする", at=(800, 357)),
    ]),
    "screen/controls": dict(scale=0.667, marks=[
        dict(box=(27, 148, 69, 575), cap="左の列", at=(173, 360)),
        dict(box=(27, 613, 1253, 699), cap="下", at=(640, 580)),
        dict(box=(892, 107, 1252, 143), cap="右", at=(1072, 73)),
    ]),
    "screen/playlist-loaded": dict(scale=0.667, marks=[
        dict(box=(29, 148, 163, 191), n=1, cap="ここに URL を入れる", at=(280, 107)),
    ]),
    "screen/playlist": dict(scale=0.667, marks=[
        dict(box=(892, 155, 1141, 193), n=1, cap="曲を押す", at=(753, 200)),
        dict(box=(1197, 109, 1260, 137), n=2, cap="削除・更新", at=(1133, 67)),
    ]),
    "screen/settings": dict(scale=0.667, marks=[
        dict(box=(164, 117, 236, 356), n=1, cap="切り替える", at=(200, 427)),
        dict(box=(160, 72, 299, 97), n=2, cap="戻る", at=(373, 40)),
    ]),
    "screen/error": dict(scale=0.667, marks=[
        dict(box=(552, 504, 728, 544), cap="押す", at=(840, 524)),
    ]),
    "screen/error-details": dict(scale=0.667, marks=[
        dict(box=(377, 497, 903, 532), n=1, cap="Discord の URL をコピー", at=(1067, 453)),
        dict(box=(377, 545, 903, 584), n=2, cap="閉じる", at=(1067, 564)),
    ]),
    "tablet/home": dict(scale=0.72, marks=[
        dict(box=(138, 442, 1142, 645), n=1, cap="アプリを開く", at=(640, 400)),
        dict(box=(618, 720, 662, 766), n=2, cap="ホームボタン", at=(880, 744)),
    ]),
    "tablet/kawaplayer": dict(scale=0.72, marks=[
        dict(box=(98, 277, 134, 314), n=1, cap="URL を入れる", at=(304, 240)),
        dict(box=(611, 714, 669, 773), n=2, cap="ホームへ戻る", at=(880, 744)),
    ]),
    "tablet/image": dict(scale=0.72, marks=[
        dict(box=(571, 74, 1176, 126), cap="画像の URL を入れる", at=(872, 184)),
    ]),
    "tablet/visitors": dict(scale=0.72, marks=[
        dict(box=(782, 72, 1176, 128), cap="一覧とログを切り替え", at=(880, 184)),
    ]),
    "tablet/settings": dict(scale=0.72, marks=[
        dict(box=(102, 147, 386, 509), cap="分類を切り替え", at=(240, 576)),
    ]),
}


def arrow(d, a, b, width):
    d.line([a, b], fill=ACCENT, width=width)
    ang = math.atan2(b[1] - a[1], b[0] - a[0])
    head = width * 4.2
    p1 = (b[0] - head * math.cos(ang - 0.45), b[1] - head * math.sin(ang - 0.45))
    p2 = (b[0] - head * math.cos(ang + 0.45), b[1] - head * math.sin(ang + 0.45))
    d.polygon([b, p1, p2], fill=ACCENT)


def nearest(box, p):
    return (min(max(p[0], box[0]), box[2]), min(max(p[1], box[1]), box[3]))


def draw_marks(size, marks, scale, font_path):
    """Draws the marks on a transparent layer SS times the picture's size."""
    layer = Image.new("RGBA", (size[0] * SS, size[1] * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    k = scale * SS
    w = max(3, round(4 * k))
    f_badge = ImageFont.truetype(font_path, round(26 * k))
    f_cap = ImageFont.truetype(font_path, round(28 * k))
    for m in marks:
        pad = 6 * k
        b = tuple(v * SS for v in m["box"])
        b = (b[0] - pad, b[1] - pad, b[2] + pad, b[3] + pad)
        d.rounded_rectangle(b, radius=12 * k, outline=ACCENT, width=w)
        if m.get("cap"):
            cx, cy = (v * SS for v in m["at"])
            pw, ph = d.textlength(m["cap"], font=f_cap) + 30 * k, 46 * k
            pill = (cx - pw / 2, cy - ph / 2, cx + pw / 2, cy + ph / 2)
            d.rounded_rectangle(pill, radius=ph / 2, fill=ACCENT)
            d.text((cx, cy), m["cap"], font=f_cap, fill=WHITE, anchor="mm")
            tip = nearest(b, (cx, cy))
            start = nearest(pill, tip)
            # Stop a little short of the outline; draw no arrow when the caption is beside it.
            ang = math.atan2(tip[1] - start[1], tip[0] - start[0])
            tip = (tip[0] - w * 2 * math.cos(ang), tip[1] - w * 2 * math.sin(ang))
            if math.dist(start, tip) > w * 6:
                arrow(d, start, tip, w)
        if m.get("n"):
            r = 19 * k
            cx, cy = (v * SS for v in m["badge"]) if "badge" in m else (b[0], b[1])
            d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=ACCENT, outline=WHITE, width=max(2, round(2 * k)))
            d.text((cx, cy + k), str(m["n"]), font=f_badge, fill=WHITE, anchor="mm")
    # Reduced with the alpha premultiplied, so the edges do not darken.
    return layer.convert("RGBa").resize(size, Image.LANCZOS).convert("RGBA")


def add_margin(im, top=0, bottom=0, left=0, right=0):
    """Adds room for captions, in the picture's most common colour."""
    color = Counter(im.getdata()).most_common(1)[0][0]
    out = Image.new("RGB", (im.width + left + right, im.height + top + bottom), color)
    out.paste(im, (left, top))
    return out


def shift(m, dx, dy):
    m = dict(m, box=(m["box"][0] + dx, m["box"][1] + dy, m["box"][2] + dx, m["box"][3] + dy))
    for key in ("at", "badge"):
        if key in m:
            m[key] = (m[key][0] + dx, m[key][1] + dy)
    return m


def make(name, spec, font_path):
    im = Image.open(os.path.join(HERE, "original", name + ".webp")).convert("RGB")
    marks = spec["marks"]
    if "pad" in spec:
        im = add_margin(im, **spec["pad"])
        marks = [shift(m, spec["pad"].get("left", 0), spec["pad"].get("top", 0)) for m in marks]
    im = Image.alpha_composite(im.convert("RGBA"), draw_marks(im.size, marks, spec["scale"], font_path)).convert("RGB")
    if name.split("/")[0] in ("vcc", "unity"):
        # Screenshots of VCC and Unity are small text on flat colours, which lossless keeps
        # sharp at about the size of quality 90; a capture with noise takes quality 90.
        options = (dict(lossless=True, quality=100), dict(quality=90))
    else:
        options = (dict(quality=80),)
    data = min((encode(im, **o) for o in options), key=len)
    path = os.path.join(HERE, name + ".webp")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(data)
    print(f"{name:26} {im.width}x{im.height} {len(data) // 1024} KB")


def encode(im, **options):
    buf = io.BytesIO()
    im.save(buf, "WEBP", method=6, **options)
    return buf.getvalue()


if __name__ == "__main__":
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    names = sys.argv[2:] or list(MARKS)
    for name in names:
        make(name, MARKS[name], sys.argv[1])
