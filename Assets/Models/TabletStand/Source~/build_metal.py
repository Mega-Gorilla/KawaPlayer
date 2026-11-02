"""Builds the two metal tablet stands in Blender (issue #349): the floor stand
and the magnet stand.

Run from the repository root:

    blender -b --factory-startup --python "Assets/Models/TabletStand/Source~/build_metal.py" -- Assets/Models/TabletStand

Writes KawaTabletStand_Floor.fbx, KawaTabletStand_Magnet.fbx and their
palettes (KawaTabletStand_Palette.png, _Palette_Black.png and
_Palette_Silver.png) to the given folder, and prints each one's triangle
count, where the tablet rests on it and where its return button goes. This
folder ends in "~", so Unity does not import the script.

Modelled like the tablet and the bentwood stand: X is width, Z is height,
the front faces -Y, and each is turned half a turn about Z before export,
so in Unity the front faces -Z and depth is +Z. Each is one mesh with one
material; its colours come from cells of a small palette, every face
sampling the middle of its cell, as the tablet does. Each colour's material
takes a palette with the same cells in other colours: the plain one is the
white floor stand and the space grey magnet stand, the others are black
and silver.

- The floor stand (Signage, in the inspector) is a kiosk: a 40 x 30 cm base
  plate, a rounded cover where the column stands on it, a column with a
  groove down its front, a hinge, and a box with a rim round the tablet,
  leaning back 30 degrees with the screen at about 1.15 m.
- The magnet stand (Aluminium, in the inspector) is one sheet bent into a
  foot that runs forwards, widening, and a neck that comes back over it,
  leaning forwards 20 degrees and narrowing, so the weight stands over the
  foot. At its top the neck bends back into a tab, flat against the back of
  a 24 x 14 cm plate leaning back 15 degrees, with two pads and a round
  magnet on its face, which the tablet sticks to. The plate is smaller than
  the tablet, so the tablet hides it. It is centred on its footprint.

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

# The cells: the floor stand's body and its parts (the groove, the hinge),
# the magnet stand's aluminium, its pads and its magnet; two and the last
# two are spare.
FLOOR_BODY, FLOOR_PARTS, SPARE, ALUMINIUM, PAD, DISC = range(6)
CELLS = 8
GREY_50 = (0.5, 0.5, 0.5)
PALETTES = {
    "": [(0.90, 0.90, 0.89), (0.78, 0.78, 0.78), GREY_50, (0.33, 0.34, 0.36), (0.60, 0.60, 0.61), (0.47, 0.47, 0.48), GREY_50, GREY_50],
    "_Black": [(0.11, 0.11, 0.12), (0.22, 0.22, 0.23), GREY_50, (0.10, 0.10, 0.11), (0.25, 0.25, 0.26), (0.18, 0.18, 0.19), GREY_50, GREY_50],
    "_Silver": [(0.70, 0.71, 0.73), (0.55, 0.56, 0.58), GREY_50, (0.74, 0.75, 0.77), (0.46, 0.46, 0.47), (0.62, 0.62, 0.63), GREY_50, GREY_50],
}


def cell_uv(cell):
    return ((cell + 0.5) / CELLS, 0.5)


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
fb.prism(rounded_rect(0.40, 0.30, 0.02, 4), 0.0, 0.010, Matrix.Identity(4), FLOOR_BODY)
COLUMN_Y = 0.04
# The rounded cover where the column stands on the base.
fb.prism(rounded_rect(0.11, 0.075, 0.03, 4), 0.010, 0.030, Matrix.Translation((0, COLUMN_Y, 0)), FLOOR_BODY)
# The column: two profiles with a groove between them down its front.
POLE_TOP = 1.05
COLUMN_W, COLUMN_D, GROOVE_W, GROOVE_D = 0.056, 0.042, 0.008, 0.006
pole_h = POLE_TOP - 0.03
side_w = (COLUMN_W - GROOVE_W) / 2
for x in (-(GROOVE_W + side_w) / 2, (GROOVE_W + side_w) / 2):
    fb.box((side_w, COLUMN_D, pole_h), Matrix.Translation((x, COLUMN_Y, 0.03 + pole_h / 2)), FLOOR_BODY)
fb.box((GROOVE_W, COLUMN_D - GROOVE_D, pole_h), Matrix.Translation((0, COLUMN_Y + GROOVE_D / 2, 0.03 + pole_h / 2)), FLOOR_PARTS)
# The hinge at the column's top, behind the box.
fb.box((0.05, 0.04, 0.05), Matrix.Translation((0, COLUMN_Y + 0.01, POLE_TOP + 0.015)), FLOOR_PARTS)
FLOOR_TILT = 30.0
holder = lean(FLOOR_TILT, Vector((0, 0.02, POLE_TOP + 0.09)))
holder_face = plate_frame(holder)
RIM = 0.024
OUTER_W, OUTER_H = TABLET_WIDTH + 2 * RIM, TABLET_HEIGHT + 2 * RIM
outline = rounded_rect(OUTER_W, OUTER_H, 0.016, 4)
# The box behind the tablet's back (local y 0 .. 0.026), and the rim round the
# tablet, standing 4.5 mm proud of its face (local y -0.012 .. 0), 1 mm clear of it.
fb.prism(outline, -0.026, 0.0, holder_face, FLOOR_BODY)
fb.ring(outline, rounded_rect(TABLET_WIDTH + 0.002, TABLET_HEIGHT + 0.002, 0.004, 4), 0.0, 0.012, holder_face, FLOOR_BODY)
floor = fb.finish("KawaTabletStand_Floor")
print(f"REPORT floor dock_unity_position={report(holder, (0, -TABLET_THICKNESS / 2, 0))} dock_unity_euler=({FLOOR_TILT:.1f}, 0, 0)")
print(f"REPORT floor button_unity_position={report(holder, (0, -0.0006, 0))} button_unity_euler=({FLOOR_TILT:.1f}, 0, 0)")

# Magnet stand: one sheet, the foot running forwards from the bend at the
# back, widening, and the neck coming back over it, leaning forwards and
# narrowing, so the plate and the tablet stand over the foot; at its top the
# neck bends back into a tab flat against the plate's back, which leans the
# plate back.
mb = Builder()
SHEET_T = 0.006
FOOT = 0.120                 # the foot's top surface, from its front edge to the bend
FOOT_FRONT_W, NECK_BOTTOM_W, NECK_TOP_W = 0.140, 0.105, 0.080
BEND_R = 0.012               # of the top surface, which is inside the bend
BEND_SEGMENTS = 8
MAGNET_TILT = 15.0           # the plate, leaning back
NECK_ANGLE = 110.0           # from the foot: past upright, so the neck leans forwards 20 degrees
TOP_R = 0.015                # the bend into the tab, of the top surface, which is outside it
TOP_SEGMENTS = 4
TAB = 0.050                  # the tab, its middle behind the plate's middle
PLATE_T = 0.008
PLATE_Z = 0.172              # the plate's middle
a_neck, a_tab = math.radians(NECK_ANGLE), math.radians(90.0 - MAGNET_TILT)
neck_dir, tab_dir = v2(math.cos(a_neck), math.sin(a_neck)), v2(math.cos(a_tab), math.sin(a_tab))
plate_front = v2(-math.sin(a_tab), math.cos(a_tab))  # the tab's top surface faces the plate
stations = [(v2(-FOOT, SHEET_T), v2(0, -1), FOOT_FRONT_W / 2), (v2(0, SHEET_T), v2(0, -1), NECK_BOTTOM_W / 2)]
bend_center = v2(0, SHEET_T + BEND_R)
for k in range(1, BEND_SEGMENTS + 1):
    phi = a_neck * k / BEND_SEGMENTS
    stations.append((bend_center + BEND_R * v2(math.sin(phi), -math.cos(phi)), v2(math.sin(phi), -math.cos(phi)), NECK_BOTTOM_W / 2))
# The neck ends where the bend and the half tab above it bring the plate's middle to PLATE_Z.
neck_top_z = PLATE_Z - (PLATE_T / 2) * plate_front.y - (TAB / 2) * tab_dir.y - TOP_R * (math.cos(a_tab) - math.cos(a_neck))
neck_top = stations[-1][0] + ((neck_top_z - stations[-1][0].y) / neck_dir.y) * neck_dir
stations.append((neck_top, stations[-1][1], NECK_TOP_W / 2))
top_center = neck_top + TOP_R * v2(math.sin(a_neck), -math.cos(a_neck))
for k in range(1, TOP_SEGMENTS + 1):
    phi = a_neck + (a_tab - a_neck) * k / TOP_SEGMENTS
    stations.append((top_center + TOP_R * v2(-math.sin(phi), math.cos(phi)), v2(math.sin(phi), -math.cos(phi)), NECK_TOP_W / 2))
tab_start = stations[-1][0]
stations.append((tab_start + TAB * tab_dir, stations[-1][1], NECK_TOP_W / 2))
mb.sheet(stations, SHEET_T, ALUMINIUM)
plate_middle = tab_start + (TAB / 2) * tab_dir + (PLATE_T / 2) * plate_front
plate = lean(MAGNET_TILT, Vector((0, plate_middle.x, plate_middle.y)))
face = plate_frame(plate)
# The plate (local y -0.004 .. 0.004), and on its face the pads and the magnet (1 mm).
mb.prism(rounded_rect(0.24, 0.14, 0.015, 4), -PLATE_T / 2, PLATE_T / 2, face, ALUMINIUM)
for x in (-0.095, 0.095):
    mb.prism(rounded_rect(0.035, 0.12, 0.012, 3), PLATE_T / 2, PLATE_T / 2 + 0.001, face @ Matrix.Translation((x, 0, 0)), PAD)
mb.prism(circle(0.02, 16), PLATE_T / 2, PLATE_T / 2 + 0.001, face, DISC)
magnet_shift = mb.centre_depth()
magnet = mb.finish("KawaTabletStand_Magnet")
rest = -(PLATE_T / 2 + 0.001)
print(f"REPORT magnet dock_unity_position={report(plate, (0, rest - TABLET_THICKNESS / 2, 0), magnet_shift)} dock_unity_euler=({MAGNET_TILT:.1f}, 0, 0)")
print(f"REPORT magnet button_unity_position={report(plate, (0, rest - 0.0006, 0), magnet_shift)} button_unity_euler=({MAGNET_TILT:.1f}, 0, 0)")

# The palettes, 8 x 8 pixels a cell.
for suffix, colours in PALETTES.items():
    name = "KawaTabletStand_Palette" + suffix
    img = bpy.data.images.new(name, width=8 * CELLS, height=8)
    pixels = []
    for y in range(8):
        for c in colours:
            pixels += [*c, 1.0] * 8
    img.pixels = pixels
    img.filepath_raw = os.path.join(OUT, name + ".png")
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
