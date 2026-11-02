"""Draws the bentwood tablet stand's atlases (issue #349), one 512 JPG per colour.

Run with Python 3 and Pillow, from the repository root:

    python -P "Assets/Models/TabletStand/Source~/make_textures.py" <folder with the veneer photos> Assets/Models/TabletStand

The veneer photos are free-to-use 1K diffuse JPGs, named as below. Each
atlas is the veneer above V 0.125 (rows 0 to 447, the grain running along
U, which is the length of the bent sheet) and the plywood's cut edge below
it (rows 448 to 511: thin face veneers of the stand's colour around birch
plies with glue lines), as build_bentwood.py maps them.

The photos are raw wood; each colour moves the photo's mean to the colour of
a finished piece and scales its contrast, keeping the grain.
"""
import os
import sys

from PIL import Image, ImageFilter

SRC, OUT = sys.argv[1], sys.argv[2]

# name: (photo, turn the grain to run along U, finished mean RGB, contrast)
COLOURS = {
    "Walnut": ("natural_walnut_veneer_diff_1k.jpg", False, (118, 78, 52), 1.15),
    "WhiteOak": ("white_oak_veneer_diff_1k.jpg", True, (206, 178, 140), 0.85),
    "SmokedWalnut": ("smoked_walnut_veneer_diff_1k.jpg", False, (78, 52, 38), 0.9),
    "Black": ("flamed_black_veneer_diff_1k.jpg", False, (38, 35, 34), 0.8),
}
SIZE = 512
VENEER_ROWS = 448
BIRCH = (214, 184, 140)
BIRCH_DARK = (196, 162, 118)
GLUE = (122, 96, 66)
PLIES = 7


def finish(photo, mean, contrast):
    bands = []
    for band, target in zip(photo.split(), mean):
        hist = band.histogram()
        count = sum(hist)
        src_mean = sum(i * h for i, h in enumerate(hist)) / count
        bands.append(band.point(lambda v, m=src_mean, t=target: max(0, min(255, round((v - m) * contrast + t)))))
    return Image.merge("RGB", bands)


def edge(face):
    """The cut edge: a face veneer of the stand's colour at the top and bottom,
    plies of birch between them, a darker glue line between plies."""
    strip = Image.new("RGB", (SIZE, SIZE - VENEER_ROWS), BIRCH)
    px = strip.load()
    h = strip.height
    face_rows = 4
    ply = (h - 2 * face_rows) / PLIES
    for y in range(h):
        if y < face_rows or y >= h - face_rows:
            colour = face
        else:
            k = (y - face_rows) / ply
            colour = GLUE if k - int(k) < 1 / ply else (BIRCH if int(k) % 2 == 0 else BIRCH_DARK)
        for x in range(SIZE):
            px[x, y] = colour
    return strip.filter(ImageFilter.GaussianBlur(0.6))


for name, (photo_name, turn, mean, contrast) in COLOURS.items():
    photo = Image.open(os.path.join(SRC, photo_name)).convert("RGB")
    if turn:
        photo = photo.transpose(Image.Transpose.ROTATE_90)
    # The veneer part keeps the photo's aspect: 1024 x 896 of it, made 512 x 448.
    veneer = finish(photo.crop((0, 0, 1024, 896)), mean, contrast).resize((SIZE, VENEER_ROWS), Image.LANCZOS)
    atlas = Image.new("RGB", (SIZE, SIZE))
    atlas.paste(veneer, (0, 0))
    atlas.paste(edge(mean), (0, VENEER_ROWS))
    path = os.path.join(OUT, f"KawaTabletStand_Bentwood_{name}.jpg")
    atlas.save(path, "JPEG", quality=88, optimize=True)
    print(name, os.path.getsize(path) // 1024, "KB")


# The cushion stand's fabric: no photo, a soft pile drawn from seeded noise,
# fine grain over a gentle mottle, tileable (the noise is blurred as a 3 x 3
# tile and the middle kept). One 256 JPG per colour: name -> mean RGB.
FABRICS = {
    "Red": (168, 30, 34),
    "Grey": (122, 120, 118),
    "Navy": (38, 52, 90),
    "Beige": (196, 178, 150),
}
FABRIC_SIZE = 256


def tileable_noise(seed, blur):
    import random
    rng = random.Random(seed)
    tile = Image.new("L", (FABRIC_SIZE, FABRIC_SIZE))
    tile.putdata([rng.randint(0, 255) for _ in range(FABRIC_SIZE * FABRIC_SIZE)])
    big = Image.new("L", (FABRIC_SIZE * 3, FABRIC_SIZE * 3))
    for i in range(3):
        for j in range(3):
            big.paste(tile, (i * FABRIC_SIZE, j * FABRIC_SIZE))
    big = big.filter(ImageFilter.GaussianBlur(blur))
    return big.crop((FABRIC_SIZE, FABRIC_SIZE, 2 * FABRIC_SIZE, 2 * FABRIC_SIZE))


def normalised(noise):
    """The noise as (value - mean) / standard deviation, per pixel."""
    from PIL import ImageStat
    stat = ImageStat.Stat(noise)
    mean, std = stat.mean[0], stat.stddev[0] or 1.0
    px = noise.load()
    return [[(px[x, y] - mean) / std for x in range(FABRIC_SIZE)] for y in range(FABRIC_SIZE)]


FINE_SPREAD = 0.06    # the pile's grain, as a share of the colour (one deviation)
MOTTLE_SPREAD = 0.05  # the gentle mottle
fine = normalised(tileable_noise(349, 0.7))
mottle = normalised(tileable_noise(1349, 9))
for name, mean in FABRICS.items():
    img = Image.new("RGB", (FABRIC_SIZE, FABRIC_SIZE))
    px = img.load()
    for y in range(FABRIC_SIZE):
        for x in range(FABRIC_SIZE):
            f = 1 + fine[y][x] * FINE_SPREAD + mottle[y][x] * MOTTLE_SPREAD
            px[x, y] = tuple(max(0, min(255, round(c * f))) for c in mean)
    path = os.path.join(OUT, f"KawaTabletStand_Cushion_{name}.jpg")
    img.save(path, "JPEG", quality=90, optimize=True)
    print("Cushion", name, os.path.getsize(path) // 1024, "KB")
