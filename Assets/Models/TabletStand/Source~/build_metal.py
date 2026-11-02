"""Builds the two metal tablet stands in Blender (issue #349): the floor stand
and the magnet stand.

Run from the repository root:

    blender -b --factory-startup --python "Assets/Models/TabletStand/Source~/build_metal.py" -- Assets/Models/TabletStand

Writes KawaTabletStand_Floor.fbx, KawaTabletStand_Magnet.fbx and
KawaTabletStand_Palette.png to the given folder, and prints each one's
triangle count, where the tablet rests on it and where its return button
goes. This folder ends in "~", so Unity does not import the script.

Modelled like the tablet and the bentwood stand: X is width, Z is height,
the front faces -Y, and each is turned half a turn about Z before export,
so in Unity the front faces -Z and depth is +Z. Each is one mesh with one
material; its colours come from cells of a small palette, every face
sampling the middle of its cell, as the tablet does.

- The floor stand is a kiosk: a 40 x 30 cm base plate, two columns and a
  hinge, and a holder that frames the tablet, leaning back 30 degrees with
  the screen at about 1.15 m.
- The magnet stand is aluminium: a base plate, a tapered neck leaning back,
  and a 24 x 14 cm plate leaning back 15 degrees, with two pads and a round
  magnet, which the tablet sticks to. The plate is smaller than the tablet,
  so the tablet hides it.

The return button goes where the tablet rests (the holder's back, the
magnet), so it shows when the tablet is away.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.abspath(argv[0]) if argv else os.getcwd()
os.makedirs(OUT, exist_ok=True)

TABLET_WIDTH = 0.300
TABLET_HEIGHT = 0.1895
TABLET_THICKNESS = 0.0075

PALETTE = [
    (0.90, 0.90, 0.89),  # white powder coat
    (0.78, 0.78, 0.78),  # light grey parts
    (0.12, 0.12, 0.13),  # dark rubber
    (0.33, 0.34, 0.36),  # space grey aluminium
    (0.60, 0.60, 0.61),  # pads
    (0.47, 0.47, 0.48),  # the magnet
    (0.5, 0.5, 0.5),
    (0.5, 0.5, 0.5),
]
WHITE, LIGHT, DARK, GREY, PAD, DISC = range(6)


def cell_uv(cell):
    return ((cell + 0.5) / len(PALETTE), 0.5)


class Builder:
    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")

    def _faces(self, verts, faces, cell):
        made = []
        for f in faces:
            face = self.bm.faces.new([verts[i] for i in f])
            for loop in face.loops:
                loop[self.uv].uv = cell_uv(cell)
            made.append(face)
        return made

    def prism(self, outline, z0, z1, matrix, cell):
        """An outline in local XY (counter-clockwise), extruded from z0 to z1."""
        n = len(outline)
        verts = [self.bm.verts.new(matrix @ Vector((x, y, z))) for z in (z0, z1) for x, y in outline]
        faces = [list(reversed(range(n))), list(range(n, 2 * n))]
        faces += [[i, (i + 1) % n, n + (i + 1) % n, n + i] for i in range(n)]
        self._faces(verts, faces, cell)

    def box(self, size, matrix, cell):
        sx, sy, sz = (s / 2 for s in size)
        self.prism([(-sx, -sy), (sx, -sy), (sx, sy), (-sx, sy)], -sz, sz, matrix, cell)

    def hexahedron(self, bottom, top, cell):
        """Eight corners: bottom and top quads, counter-clockwise from above."""
        verts = [self.bm.verts.new(Vector(p)) for p in bottom + top]
        faces = [[3, 2, 1, 0], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]]
        self._faces(verts, faces, cell)

    def finish(self, name):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        mesh = bpy.data.meshes.new(name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        mat = bpy.data.materials.new(name)
        mesh.materials.append(mat)
        tris = sum(len(p.vertices) - 2 for p in mesh.polygons)
        d = obj.dimensions
        print(f"REPORT {name} tris={tris} verts={len(mesh.vertices)} size_m=({d.x:.4f}, {d.y:.4f}, {d.z:.4f})")
        return obj


def rounded_rect(w, h, r, seg):
    pts = []
    for cx, cy, a0 in ((w / 2 - r, h / 2 - r, 0), (-w / 2 + r, h / 2 - r, 90), (-w / 2 + r, -h / 2 + r, 180), (w / 2 - r, -h / 2 + r, 270)):
        for k in range(seg + 1):
            a = math.radians(a0 + 90 * k / seg)
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def circle(r, seg):
    return [(r * math.cos(2 * math.pi * k / seg), r * math.sin(2 * math.pi * k / seg)) for k in range(seg)]


def lean(degrees, origin):
    """Leaning back by degrees (the top towards +Y), at origin, local Z up and -Y front."""
    return Matrix.Translation(origin) @ Matrix.Rotation(math.radians(-degrees), 4, "X")


def plate_frame(matrix):
    """A plate's local frame (X width, Y depth, Z up the plate) laid on its face: local
    prisms are extruded along Z, so turn Z to face -Y."""
    return matrix @ Matrix.Rotation(math.radians(90), 4, "X")


def report(name, matrix, local):
    """A point in a part's local frame, with the part's lean, in Unity: (x, height, depth)."""
    p = matrix @ Vector(local)
    return f"(0, {p.z:.5f}, {p.y:.5f})"


# Floor stand.
fb = Builder()
fb.prism(rounded_rect(0.40, 0.30, 0.02, 4), 0.0, 0.010, Matrix.Identity(4), WHITE)
fb.box((0.08, 0.07, 0.02), Matrix.Translation((0, 0.04, 0.020)), LIGHT)
POLE_TOP = 1.05
for x in (-0.016, 0.016):
    fb.box((0.022, 0.045, POLE_TOP - 0.03), Matrix.Translation((x, 0.04, 0.03 + (POLE_TOP - 0.03) / 2)), WHITE)
fb.box((0.06, 0.05, 0.06), Matrix.Translation((0, 0.04, POLE_TOP + 0.02)), LIGHT)
FLOOR_TILT = 30.0
holder = lean(FLOOR_TILT, Vector((0, 0.02, POLE_TOP + 0.09)))
RIM = 0.018
OUTER_W, OUTER_H = TABLET_WIDTH + 2 * RIM, TABLET_HEIGHT + 2 * RIM
# Back plate behind the tablet's back (local y 0 .. 0.012), and the rim around
# the tablet, standing 4.5 mm proud of its face (local y -0.012 .. 0).
fb.box((OUTER_W, 0.012, OUTER_H), holder @ Matrix.Translation((0, 0.006, 0)), WHITE)
for sx, sz, x, z in ((OUTER_W, RIM, 0, TABLET_HEIGHT / 2 + RIM / 2), (OUTER_W, RIM, 0, -TABLET_HEIGHT / 2 - RIM / 2),
                     (RIM, TABLET_HEIGHT, TABLET_WIDTH / 2 + RIM / 2, 0), (RIM, TABLET_HEIGHT, -TABLET_WIDTH / 2 - RIM / 2, 0)):
    fb.box((sx, 0.012, sz), holder @ Matrix.Translation((x, -0.006, z)), WHITE)
floor = fb.finish("KawaTabletStand_Floor")
print(f"REPORT floor dock_unity_position={report('floor', holder, (0, -TABLET_THICKNESS / 2, 0))} dock_unity_euler=({FLOOR_TILT:.1f}, 0, 0)")
print(f"REPORT floor button_unity_position={report('floor', holder, (0, -0.0006, 0))} button_unity_euler=({FLOOR_TILT:.1f}, 0, 0)")

# Magnet stand.
mb = Builder()
mb.prism(rounded_rect(0.17, 0.15, 0.012, 4), 0.0, 0.006, Matrix.Translation((0, -0.01, 0)), GREY)
NECK_T = 0.006
neck_b = (-0.005, 0.006)     # (y, z) of the neck's front face at the bottom
neck_t = (0.045, 0.150)      # and at the top
mb.hexahedron(
    [(-0.05, neck_b[0], neck_b[1]), (0.05, neck_b[0], neck_b[1]), (0.05, neck_b[0] + NECK_T, neck_b[1]), (-0.05, neck_b[0] + NECK_T, neck_b[1])],
    [(-0.035, neck_t[0], neck_t[1]), (0.035, neck_t[0], neck_t[1]), (0.035, neck_t[0] + NECK_T, neck_t[1]), (-0.035, neck_t[0] + NECK_T, neck_t[1])],
    GREY)
MAGNET_TILT = 15.0
PLATE_T = 0.008
plate = lean(MAGNET_TILT, Vector((0, neck_t[0] - PLATE_T / 2, 0.172)))
face = plate_frame(plate)
# The plate (local y -0.004 .. 0.004), and on its face the pads and the magnet (1 mm).
mb.prism(rounded_rect(0.24, 0.14, 0.015, 4), -PLATE_T / 2, PLATE_T / 2, face, GREY)
for x in (-0.095, 0.095):
    mb.prism(rounded_rect(0.035, 0.12, 0.012, 3), PLATE_T / 2, PLATE_T / 2 + 0.001, face @ Matrix.Translation((x, 0, 0)), PAD)
mb.prism(circle(0.02, 16), PLATE_T / 2, PLATE_T / 2 + 0.001, face, DISC)
magnet = mb.finish("KawaTabletStand_Magnet")
rest = -(PLATE_T / 2 + 0.001)
print(f"REPORT magnet dock_unity_position={report('magnet', plate, (0, rest - TABLET_THICKNESS / 2, 0))} dock_unity_euler=({MAGNET_TILT:.1f}, 0, 0)")
print(f"REPORT magnet button_unity_position={report('magnet', plate, (0, rest - 0.0006, 0))} button_unity_euler=({MAGNET_TILT:.1f}, 0, 0)")

# The palette.
img = bpy.data.images.new("KawaTabletStand_Palette", width=8 * len(PALETTE), height=8)
pixels = []
for y in range(8):
    for c in PALETTE:
        pixels += [*c, 1.0] * 8
img.pixels = pixels
img.filepath_raw = os.path.join(OUT, "KawaTabletStand_Palette.png")
img.file_format = "PNG"
img.save()

for obj, name in ((floor, "KawaTabletStand_Floor"), (magnet, "KawaTabletStand_Magnet")):
    obj.matrix_world = Matrix.Rotation(math.pi, 4, "Z") @ obj.matrix_world
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + ".fbx"), use_selection=True,
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             mesh_smooth_type="FACE", use_mesh_modifiers=True, path_mode="STRIP")
print("REPORT done")
