"""Builds the normal map that raises the logo on the tablet's back (issue #189).

Run from the repository root (needs numpy, scipy and Pillow):

    python "Assets/Models/Tablet/Source~/logo_normal.py"

Reads the logo's shape (the alpha of Assets/Images/kawaplayer-logo.png) and
writes Assets/Models/Tablet/KawaTablet_LogoNormal.png. Run it again when the
logo changes.

The logo stays on its flat quad; the map only bends the light, as if the
logo stood proud of the body with a chamfer all round: flat on top, sloping
down to its edge. The chamfer lies inside the logo, since the quad shows
nothing outside it. Its width is in pixels of the 1024 logo (16 px is about
0.8 mm on the 5 cm logo), its slope in degrees. Green points up the
texture, as Unity expects.
"""
import os
import sys

import numpy as np
from PIL import Image
from scipy import ndimage

LOGO = "Assets/Images/kawaplayer-logo.png"
OUT = "Assets/Models/Tablet/KawaTablet_LogoNormal.png"
SIZE = 256
CHAMFER_PX = 16
CHAMFER_DEG = 40

alpha = np.asarray(Image.open(LOGO).getchannel("A"), dtype=np.float32) / 255
if alpha.shape[0] % SIZE or alpha.shape[1] % SIZE:
    sys.exit(f"{LOGO} is {alpha.shape[1]} x {alpha.shape[0]}; expected a multiple of {SIZE}")

# Height: 0 at the logo's edge, rising across the chamfer to 1 on the flat top.
inside = alpha >= 0.5
height = np.clip(ndimage.distance_transform_edt(inside) / CHAMFER_PX, 0, 1)
height = ndimage.gaussian_filter(height, 0.8)  # soften the folds a little

scale = alpha.shape[0] // SIZE
height = height.reshape(SIZE, scale, SIZE, scale).mean(axis=(1, 3))

# Slope per pixel of the map, scaled so the chamfer tilts by CHAMFER_DEG.
gy, gx = np.gradient(height)
k = CHAMFER_PX * np.tan(np.radians(CHAMFER_DEG)) / scale
normal = np.stack([-gx * k, gy * k, np.ones_like(height)], axis=-1)  # image rows run down, texture v up
normal /= np.linalg.norm(normal, axis=-1, keepdims=True)

rgb = np.clip(np.round((normal * 0.5 + 0.5) * 255), 0, 255).astype(np.uint8)
Image.fromarray(rgb).save(OUT, optimize=True)
print(f"{OUT}: {SIZE} x {SIZE}, {os.path.getsize(OUT):,} bytes")
