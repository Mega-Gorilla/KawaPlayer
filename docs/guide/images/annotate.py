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

# name: src (the original to draw on, if not the one of the same name), crop (the part
# of the original to keep), pad (margin added around it, for captions), scale (size of
# the marks), marks. A mark: box, n (badge), badge (where, if not the box's top-left;
# a badge alone needs no box), cap (caption) and at (its centre). Coordinates are those
# of the original, before crop and pad.
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
    "vcc/update-package": dict(scale=0.8, pad=dict(bottom=70), marks=[
        dict(box=(691, 9, 1009, 55), cap="押すと更新", at=(560, 104)),
    ]),
    "unity/gameobject-menu": dict(scale=0.7, pad=dict(top=24, left=24), marks=[
        dict(box=(2, 1, 118, 27), n=1),
        dict(box=(8, 242, 400, 276), n=2),
        dict(box=(410, 244, 632, 276), n=3, badge=(636, 240)),
    ]),
    "unity/gameobject-menu-tablet": dict(src="unity/gameobject-menu", scale=0.7, pad=dict(top=24, left=24), marks=[
        dict(box=(2, 1, 118, 27), n=1),
        dict(box=(8, 242, 400, 276), n=2),
        dict(box=(410, 396, 632, 427), n=3, badge=(636, 392)),
    ]),
    "unity/hierarchy-empty": dict(scale=0.7, marks=[
        dict(box=(16, 196, 360, 318), cap="何もないところを押す", at=(188, 368)),
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
    # The settings page shows each part of the inspector under its heading, with the
    # number it has on the whole inspector.
    "unity/inspector-language": dict(src="unity/inspector", crop=(196, 338, 676, 400), scale=0.72, marks=[
        dict(n=1, badge=(260, 372)),
    ]),
    "unity/inspector-appearance": dict(src="unity/inspector", crop=(3, 418, 841, 556), scale=0.72, pad=dict(left=28), marks=[
        dict(n=2, badge=(-11, 441)),
    ]),
    "unity/inspector-player": dict(src="unity/inspector", crop=(3, 553, 841, 953), scale=0.72, pad=dict(left=28), marks=[
        dict(n=3, badge=(-11, 576)),
    ]),
    "unity/inspector-playlist": dict(src="unity/inspector", crop=(3, 951, 841, 1059), scale=0.72, pad=dict(left=28), marks=[
        dict(n=4, badge=(-11, 974)),
    ]),
    "unity/inspector-modules": dict(src="unity/inspector", crop=(3, 1057, 841, 1215), scale=0.72, pad=dict(left=28), marks=[
        dict(n=5, badge=(-11, 1080)),
    ]),
    "unity/inspector-version": dict(src="unity/inspector", crop=(3, 1213, 841, 1391), scale=0.72, pad=dict(left=28), marks=[
        dict(n=6, badge=(-11, 1236)),
    ]),
    # The first look at the editor, with nothing to press yet.
    "unity/playlist-editor": dict(scale=0.8, marks=[]),
    "unity/playlist-import": dict(scale=0.8, pad=dict(top=8), marks=[
        dict(box=(855, 37, 1008, 62), n=1),
        dict(box=(197, 76, 1178, 101), n=2, cap="VHub の VRChat用URL を貼る", at=(960, 320)),
        dict(box=(1186, 76, 1339, 101), n=3),
        dict(box=(1302, 37, 1346, 62), n=4),
    ]),
    "unity/playlist-manual": dict(scale=0.8, pad=dict(top=8), marks=[
        dict(box=(470, 288, 510, 315), n=1, cap="プレイリストを足す", at=(300, 380)),
        dict(box=(1241, 320, 1283, 347), n=2, cap="曲を足す", at=(1080, 420)),
        dict(box=(592, 222, 1333, 310), n=3),
        dict(box=(1302, 37, 1346, 62), n=4),
    ]),
    # The toolbar and the panel each button opens, cut from the top of the editor.
    "unity/playlist-ytdlp": dict(crop=(0, 0, 1352, 168), scale=0.8, pad=dict(top=8), marks=[
        dict(box=(709, 37, 852, 62), n=1),
        dict(box=(12, 138, 134, 160), n=2, badge=(156, 149)),
        dict(box=(1193, 137, 1339, 160), n=3),
    ]),
    "unity/playlist-json-import": dict(crop=(0, 0, 1352, 168), scale=0.8, pad=dict(top=8), marks=[
        dict(box=(1011, 37, 1161, 62), n=1),
        dict(box=(1186, 137, 1339, 160), n=2),
    ]),
    # Export JSON's panel shows in the picture of the hand-made playlist.
    "unity/playlist-export": dict(src="unity/playlist-manual", crop=(0, 0, 1352, 168), scale=0.8, pad=dict(top=8), marks=[
        dict(box=(1165, 37, 1297, 62), n=1),
        dict(box=(1204, 137, 1339, 160), n=2),
    ]),
    # The track list of a real playlist, which the owner allowed for this picture only.
    "unity/playlist-tracks": dict(crop=(582, 62, 1352, 482), scale=0.8, pad=dict(left=16), marks=[
        dict(box=(1251, 68, 1346, 94), n=1, badge=(1234, 81)),
        dict(box=(592, 121, 612, 136), n=2),
        dict(box=(741, 115, 940, 140), n=3),
        dict(box=(741, 144, 1313, 201), n=4, badge=(726, 172)),
        dict(box=(1285, 118, 1310, 138), n=5, badge=(1266, 128)),
    ]),
    "unity/module-manager": dict(scale=0.72, marks=[
        dict(box=(28, 423, 834, 537), n=1),
        dict(box=(746, 615, 828, 1163), n=2),
    ]),
    # The quickstart only fills in the KawaPlayer field; the tablet page explains the rest.
    "unity/tablet-player-field": dict(src="unity/tablet-inspector", scale=0.72, pad=dict(right=270), marks=[
        dict(box=(274, 430, 664, 456), cap="KawaPlayer を入れる", at=(826, 443)),
    ]),
    "unity/build": dict(scale=0.72, pad=dict(left=16), marks=[
        dict(box=(9, 257, 404, 288), n=1),
        dict(box=(10, 304, 631, 402), n=2),
    ]),
    # Each module's settings, cut from its inspector below the U# header (the modules page).
    "unity/module-default-url": dict(crop=(6, 289, 840, 365), scale=0.72, marks=[]),
    "unity/module-playlist-loader": dict(crop=(6, 293, 840, 1045), scale=0.72, marks=[]),
    "unity/module-autoplay-track": dict(crop=(6, 368, 840, 623), scale=0.72, marks=[]),
    "unity/module-autoplay-playlist": dict(crop=(6, 368, 840, 632), scale=0.72, marks=[]),
    "unity/module-permission": dict(crop=(6, 398, 840, 761), scale=0.72, marks=[]),
    "unity/module-persistence": dict(crop=(6, 368, 840, 542), scale=0.72, marks=[]),
    "unity/module-slideshow": dict(crop=(6, 398, 840, 473), scale=0.72, marks=[]),
    "unity/module-timeline-sync": dict(crop=(6, 368, 840, 602), scale=0.72, marks=[]),
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
    "vrchat/site-menu": dict(scale=0.8, pad=dict(left=16), marks=[
        dict(box=(8, 169, 388, 233), n=1),
    ]),
    "vrchat/site-my-worlds": dict(scale=0.8, pad=dict(left=12), marks=[
        dict(box=(22, 178, 588, 668), n=2),
    ]),
    "vrchat/site-edit-world-info": dict(scale=0.8, marks=[]),
    "vrchat/site-allowed-domains": dict(scale=0.9, marks=[
        dict(box=(31, 149, 1115, 203), n=3, cap="playlist.vrc-hub.com を入れる", at=(745, 119)),
    ]),
    "vhub/top-search": dict(src="vhub/top", scale=0.8, marks=[
        dict(box=(309, 223, 1250, 278), n=1),
    ]),
    "vhub/top-login": dict(src="vhub/top", scale=0.8, marks=[
        dict(box=(1169, 12, 1250, 52), n=1, badge=(1158, 68)),
        dict(box=(1082, 12, 1160, 52), n=2, badge=(1072, 68)),
    ]),
    "vhub/detail": dict(scale=0.8, marks=[
        dict(box=(18, 432, 611, 504), n=1),
    ]),
    "vhub/detail-copied": dict(scale=0.8, marks=[]),
    "vhub/create-name": dict(scale=0.8, marks=[
        dict(box=(10, 36, 728, 103), n=1),
        dict(box=(14, 136, 138, 163), n=2),
    ]),
    "vhub/create-videos": dict(scale=0.8, marks=[
        dict(box=(34, 176, 599, 230), n=1),
        dict(box=(613, 176, 703, 230), n=2),
    ]),
    "vhub/create-classify": dict(scale=0.8, marks=[
        dict(box=(34, 118, 703, 179), n=1),
        dict(box=(34, 453, 216, 495), n=2),
        dict(box=(30, 552, 706, 668), n=3),
    ]),
    "vhub/create-button": dict(scale=0.8, marks=[
        dict(box=(9, 12, 729, 77), n=1),
    ]),
    "vhub/created-url": dict(scale=0.8, marks=[
        dict(box=(343, 328, 1110, 378), n=1),
        dict(box=(1122, 323, 1265, 383), n=2),
    ]),
    "vhub/edit-button": dict(scale=0.8, marks=[
        dict(box=(216, 132, 350, 204), n=1),
    ]),
    "vhub/top-library": dict(src="vhub/top", scale=0.8, marks=[
        dict(box=(258, 12, 377, 52), n=1),
    ]),
    "vhub/edit-videos": dict(scale=0.8, marks=[
        dict(box=(48, 284, 164, 454), n=1),
        dict(box=(1160, 292, 1222, 331), n=2),
    ]),
    "vhub/edit-save": dict(scale=0.8, pad=dict(top=14), marks=[
        dict(box=(1126, 8, 1272, 74), n=1),
    ]),
    "vhub/delete": dict(scale=0.8, marks=[
        dict(box=(35, 110, 307, 166), n=1),
    ]),
    "screen/controls": dict(scale=0.667, marks=[
        dict(box=(27, 148, 69, 575), cap="左の列", at=(173, 360)),
        dict(box=(27, 613, 1253, 699), cap="下", at=(640, 580)),
        dict(box=(892, 107, 1252, 143), cap="右", at=(1072, 73)),
    ]),
    "screen/controls-left": dict(src="screen/controls", scale=0.667, marks=[
        dict(box=(28, 147, 68, 189), n=1),
        dict(box=(28, 211, 68, 253), n=2),
        dict(box=(28, 275, 68, 317), n=3),
        dict(box=(28, 339, 68, 381), n=4),
        dict(box=(28, 403, 68, 445), n=5),
        dict(box=(28, 467, 68, 509), n=6),
        dict(box=(28, 531, 68, 573), n=7),
    ]),
    "screen/controls-right": dict(src="screen/controls", scale=0.667, marks=[
        dict(box=(892, 104, 960, 140), n=1),
        dict(box=(1032, 104, 1088, 140), n=2),
        dict(box=(1138, 104, 1252, 140), n=3),
    ]),
    "screen/controls-bottom": dict(src="screen/controls", scale=0.667, marks=[
        dict(box=(35, 659, 69, 693), n=1),
        dict(box=(87, 659, 121, 693), n=2),
        dict(box=(139, 659, 173, 693), n=3),
        dict(box=(191, 659, 225, 693), n=4),
        dict(box=(28, 614, 1252, 642), n=5, badge=(640, 604)),
        dict(box=(1056, 656, 1250, 696), n=6),
    ]),
    "screen/playlist-list": dict(scale=0.667, marks=[
        dict(box=(1136, 102, 1254, 142), n=1),
        dict(box=(884, 154, 1110, 196), n=2, cap="押すと曲の一覧へ", at=(1000, 252)),
    ]),
    "screen/playlist-tracks": dict(scale=0.667, marks=[
        dict(box=(884, 198, 1200, 246), n=1, cap="曲を押す", at=(760, 222)),
        dict(box=(898, 106, 934, 142), n=2),
    ]),
    "screen/track-actions": dict(scale=0.667, marks=[
        dict(box=(914, 198, 962, 246), n=1),
        dict(box=(976, 198, 1052, 246), n=2),
        dict(box=(1058, 198, 1134, 246), n=3),
        dict(box=(1152, 198, 1214, 246), n=4),
    ]),
    "screen/playlist-vhub": dict(src="screen/playlist", scale=0.667, marks=[
        dict(box=(1197, 109, 1260, 137), n=1, cap="削除・更新", at=(1133, 67)),
    ]),
    "screen/queue": dict(scale=0.667, marks=[
        dict(box=(892, 104, 960, 140), n=1),
        dict(box=(886, 152, 1140, 300), n=2),
    ]),
    "screen/queue-actions": dict(scale=0.667, marks=[
        dict(box=(916, 198, 956, 248), n=1),
        dict(box=(982, 198, 1050, 248), n=2),
        dict(box=(1062, 198, 1130, 248), n=3),
        dict(box=(1156, 198, 1198, 248), n=4),
    ]),
    "screen/history": dict(scale=0.667, marks=[
        dict(box=(1032, 104, 1088, 140), n=1),
        dict(box=(886, 152, 1140, 334), n=2),
    ]),
    "screen/settings-playback": dict(scale=0.667, marks=[
        dict(box=(250, 120, 1080, 226), n=1),
        dict(box=(250, 236, 1126, 386), n=2),
        dict(box=(250, 397, 1066, 532), n=3),
    ]),
    "screen/settings-playback2": dict(scale=0.667, marks=[
        dict(box=(250, 398, 726, 490), n=4),
        dict(box=(250, 503, 980, 616), n=5),
    ]),
    "screen/settings-video": dict(scale=0.667, marks=[
        dict(box=(250, 118, 640, 203), n=1),
        dict(box=(250, 215, 1136, 292), n=2),
        dict(box=(250, 297, 830, 415), n=3),
    ]),
    "screen/settings-other": dict(scale=0.667, marks=[
        dict(box=(250, 116, 860, 250), n=1),
    ]),
    "screen/autoplay-owner": dict(scale=0.667, marks=[
        dict(box=(24, 124, 196, 174), n=1),
        dict(box=(214, 124, 352, 174), n=2),
    ]),
    "screen/autoplay-owner-set": dict(scale=0.667, marks=[
        dict(box=(24, 94, 508, 122), n=1),
    ]),
    "screen/autoplay-other": dict(scale=0.667, marks=[]),
    "screen/url-button": dict(src="screen/controls", scale=0.667, marks=[
        dict(box=(26, 146, 70, 190), n=1, cap="ここを押す", at=(190, 168)),
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
    "screen/error-access-denied": dict(scale=0.667, marks=[
        dict(box=(369, 316, 911, 410), cap="ワールドの制作者向けの案内", at=(1090, 363)),
    ]),
    "tablet/home": dict(scale=0.72, marks=[
        dict(box=(138, 442, 1142, 645), n=1, cap="アプリを開く", at=(640, 400)),
        dict(box=(618, 720, 662, 766), n=2, cap="ホームボタン", at=(880, 744)),
    ]),
    "tablet/kawaplayer": dict(scale=0.72, marks=[
        dict(box=(98, 277, 134, 314), n=1, cap="URL を入れる", at=(304, 240)),
        dict(box=(611, 714, 669, 773), n=2, cap="ホームへ戻る", at=(880, 744)),
    ]),
    "tablet/rotate": dict(scale=0.72, marks=[]),
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
        if "box" in m:
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
    m = dict(m)
    if "box" in m:
        m["box"] = (m["box"][0] + dx, m["box"][1] + dy, m["box"][2] + dx, m["box"][3] + dy)
    for key in ("at", "badge"):
        if key in m:
            m[key] = (m[key][0] + dx, m[key][1] + dy)
    return m


def make(name, spec, font_path):
    im = Image.open(os.path.join(HERE, "original", spec.get("src", name) + ".webp")).convert("RGB")
    marks = spec["marks"]
    if "crop" in spec:
        c = spec["crop"]
        im = im.crop(c)
        marks = [shift(m, -c[0], -c[1]) for m in marks]
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
