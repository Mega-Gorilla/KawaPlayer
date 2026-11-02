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

- The floor stand is a kiosk: a 40 x 30 cm base plate, a rounded cover
  where the column stands on it, a column with a groove down its front, a
  hinge, and a box with a rim round the tablet, leaning back 30 degrees with
  the screen at about 1.15 m.
- The magnet stand is aluminium: one sheet bent into a foot that runs
  forwards, widening, and a neck leaning back 15 degrees, narrowing, which
  ends in a hinge with a knuckle at each end, on a round swivel behind a
  24 x 14 cm plate, with two pads and a round magnet on its face, which the
  tablet sticks to. The plate is smaller than the tablet, so the tablet
  hides it. It is centred on its footprint.

Faces meeting at more than 40 degrees are split, the rest shade smooth, so
the bends and the rounded corners look round and the edges stay sharp.

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

    def ring(self, outer, inner, z0, z1, matrix, cell):
        """The area between two outlines in local XY with as many points each, extruded from z0 to z1."""
        n = len(outer)
        vo = [self.bm.verts.new(matrix @ Vector((x, y, z))) for z in (z0, z1) for x, y in outer]
        vi = [self.bm.verts.new(matrix @ Vector((x, y, z))) for z in (z0, z1) for x, y in inner]
        verts = vo + vi
        o, i_ = 0, 2 * n
        faces = []
        for i in range(n):
            j = (i + 1) % n
            faces += [[o + n + i, o + n + j, i_ + n + j, i_ + n + i], [o + i, i_ + i, i_ + j, o + j],
                      [o + i, o + j, o + n + j, o + n + i], [i_ + j, i_ + i, i_ + n + i, i_ + n + j]]
        self._faces(verts, faces, cell)

    def sheet(self, stations, thickness, cell):
        """A bent sheet across X: per station, a point of its top surface in (y, z), the normal into the sheet
        there and its half width."""
        rows = []
        for p, n, hw in stations:
            q = p + thickness * n
            rows.append([self.bm.verts.new((x, y, z)) for x, (y, z) in ((-hw, p), (hw, p), (-hw, q), (hw, q))])
        TL, TR, BL, BR = range(4)
        verts = [v for row in rows for v in row]
        faces = []
        for r in range(len(rows) - 1):
            a, b = 4 * r, 4 * (r + 1)
            faces += [[a + TL, a + TR, b + TR, b + TL], [a + BR, a + BL, b + BL, b + BR],
                      [a + BL, a + TL, b + TL, b + BL], [a + TR, a + BR, b + BR, b + TR]]
        last = 4 * (len(rows) - 1)
        faces += [[TR, TL, BL, BR], [last + TL, last + TR, last + BR, last + BL]]
        self._faces(verts, faces, cell)

    def centre_depth(self):
        """Moves the mesh to be centred front to back; returns how far."""
        ys = [v.co.y for v in self.bm.verts]
        shift = -(min(ys) + max(ys)) / 2
        bmesh.ops.translate(self.bm, vec=(0, shift, 0), verts=self.bm.verts)
        return shift

    def finish(self, name):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        self.bm.normal_update()
        for f in self.bm.faces:
            f.smooth = True
        sharp = [e for e in self.bm.edges if len(e.link_faces) == 2 and e.calc_face_angle(0) > math.radians(40)]
        bmesh.ops.split_edges(self.bm, edges=sharp)
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


def report(matrix, local, shift=0.0):
    """A point in a part's local frame, with the part's lean, in Unity: (x, height, depth)."""
    p = matrix @ Vector(local)
    return f"(0, {p.z:.5f}, {p.y + shift:.5f})"


def v2(y, z):
    return Vector((y, z))


# Floor stand.
fb = Builder()
fb.prism(rounded_rect(0.40, 0.30, 0.02, 4), 0.0, 0.010, Matrix.Identity(4), WHITE)
COLUMN_Y = 0.04
# The rounded cover where the column stands on the base.
fb.prism(rounded_rect(0.11, 0.075, 0.03, 4), 0.010, 0.030, Matrix.Translation((0, COLUMN_Y, 0)), WHITE)
# The column: two profiles with a groove between them down its front.
POLE_TOP = 1.05
COLUMN_W, COLUMN_D, GROOVE_W, GROOVE_D = 0.056, 0.042, 0.008, 0.006
pole_h = POLE_TOP - 0.03
side_w = (COLUMN_W - GROOVE_W) / 2
for x in (-(GROOVE_W + side_w) / 2, (GROOVE_W + side_w) / 2):
    fb.box((side_w, COLUMN_D, pole_h), Matrix.Translation((x, COLUMN_Y, 0.03 + pole_h / 2)), WHITE)
fb.box((GROOVE_W, COLUMN_D - GROOVE_D, pole_h), Matrix.Translation((0, COLUMN_Y + GROOVE_D / 2, 0.03 + pole_h / 2)), LIGHT)
# The hinge at the column's top, behind the box.
fb.box((0.05, 0.04, 0.05), Matrix.Translation((0, COLUMN_Y + 0.01, POLE_TOP + 0.015)), LIGHT)
FLOOR_TILT = 30.0
holder = lean(FLOOR_TILT, Vector((0, 0.02, POLE_TOP + 0.09)))
holder_face = plate_frame(holder)
RIM = 0.024
OUTER_W, OUTER_H = TABLET_WIDTH + 2 * RIM, TABLET_HEIGHT + 2 * RIM
outline = rounded_rect(OUTER_W, OUTER_H, 0.016, 4)
# The box behind the tablet's back (local y 0 .. 0.026), and the rim round the
# tablet, standing 4.5 mm proud of its face (local y -0.012 .. 0), 1 mm clear of it.
fb.prism(outline, -0.026, 0.0, holder_face, WHITE)
fb.ring(outline, rounded_rect(TABLET_WIDTH + 0.002, TABLET_HEIGHT + 0.002, 0.004, 4), 0.0, 0.012, holder_face, WHITE)
floor = fb.finish("KawaTabletStand_Floor")
print(f"REPORT floor dock_unity_position={report(holder, (0, -TABLET_THICKNESS / 2, 0))} dock_unity_euler=({FLOOR_TILT:.1f}, 0, 0)")
print(f"REPORT floor button_unity_position={report(holder, (0, -0.0006, 0))} button_unity_euler=({FLOOR_TILT:.1f}, 0, 0)")

# Magnet stand: one sheet, the foot running forwards from the bend, widening,
# and the neck leaning back, narrowing, with the plate on the neck's front.
mb = Builder()
SHEET_T = 0.006
FOOT = 0.120                 # the foot's top surface, from its front edge to the bend
FOOT_FRONT_W, NECK_BOTTOM_W, NECK_TOP_W = 0.140, 0.105, 0.080
BEND_R = 0.012               # of the top surface, which is inside the bend
BEND_SEGMENTS = 6
MAGNET_TILT = 15.0
NECK_ANGLE = 90.0 - MAGNET_TILT  # parallel to the plate
PLATE_T = 0.008
PLATE_Z = 0.172              # the plate's middle, where the neck ends behind it
# Behind the plate: a round swivel, and the hinge the neck ends in (its axis
# across X, at the plate's middle), with a knuckle at each end.
SWIVEL_R, SWIVEL_T = 0.038, 0.006
HINGE_R, HINGE_L, KNUCKLE_L = 0.010, 0.088, 0.004
HINGE_BACK = PLATE_T / 2 + SWIVEL_T + HINGE_R - 0.002  # the hinge's axis, behind the plate's middle
stations = [(v2(-FOOT, SHEET_T), v2(0, -1), FOOT_FRONT_W / 2), (v2(0, SHEET_T), v2(0, -1), NECK_BOTTOM_W / 2)]
bend_center = v2(0, SHEET_T + BEND_R)
for k in range(1, BEND_SEGMENTS + 1):
    phi = math.radians(NECK_ANGLE * k / BEND_SEGMENTS)
    stations.append((bend_center + BEND_R * v2(math.sin(phi), -math.cos(phi)), v2(math.sin(phi), -math.cos(phi)), NECK_BOTTOM_W / 2))
neck_dir = v2(math.cos(math.radians(NECK_ANGLE)), math.sin(math.radians(NECK_ANGLE)))
neck_in = stations[-1][1]
neck_top = stations[-1][0] + ((PLATE_Z - stations[-1][0].y) / neck_dir.y) * neck_dir
stations.append((neck_top, neck_in, NECK_TOP_W / 2))
mb.sheet(stations, SHEET_T, GREY)
# The neck's middle ends on the hinge's axis; the plate is in front of it.
plate_middle = neck_top + (SHEET_T / 2 - HINGE_BACK) * neck_in
plate = lean(MAGNET_TILT, Vector((0, plate_middle.x, plate_middle.y)))
face = plate_frame(plate)
# The plate (local y -0.004 .. 0.004), and on its face the pads and the magnet (1 mm).
mb.prism(rounded_rect(0.24, 0.14, 0.015, 4), -PLATE_T / 2, PLATE_T / 2, face, GREY)
for x in (-0.095, 0.095):
    mb.prism(rounded_rect(0.035, 0.12, 0.012, 3), PLATE_T / 2, PLATE_T / 2 + 0.001, face @ Matrix.Translation((x, 0, 0)), PAD)
mb.prism(circle(0.02, 16), PLATE_T / 2, PLATE_T / 2 + 0.001, face, DISC)
# Behind it, the swivel and the hinge (a cylinder along X).
mb.prism(circle(SWIVEL_R, 12), -PLATE_T / 2 - SWIVEL_T, -PLATE_T / 2, face, GREY)
hinge = face @ Matrix.Translation((0, 0, -HINGE_BACK)) @ Matrix.Rotation(math.radians(90), 4, "Y")
mb.prism(circle(HINGE_R, 10), -HINGE_L / 2, HINGE_L / 2, hinge, GREY)
for z0 in (-HINGE_L / 2 - KNUCKLE_L, HINGE_L / 2):
    mb.prism(circle(HINGE_R + 0.0015, 10), z0, z0 + KNUCKLE_L, hinge, DISC)
magnet_shift = mb.centre_depth()
magnet = mb.finish("KawaTabletStand_Magnet")
rest = -(PLATE_T / 2 + 0.001)
print(f"REPORT magnet dock_unity_position={report(plate, (0, rest - TABLET_THICKNESS / 2, 0), magnet_shift)} dock_unity_euler=({MAGNET_TILT:.1f}, 0, 0)")
print(f"REPORT magnet button_unity_position={report(plate, (0, rest - 0.0006, 0), magnet_shift)} button_unity_euler=({MAGNET_TILT:.1f}, 0, 0)")

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
