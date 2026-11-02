"""Builds the bentwood tablet stand mesh in Blender (issue #349).

Run from the repository root:

    blender -b --factory-startup --python "Assets/Models/TabletStand/Source~/build_bentwood.py" -- Assets/Models/TabletStand

Writes KawaTabletStand_Bentwood.fbx to the given folder and prints the triangle count,
where the tablet rests on it and where its return button goes. This folder
ends in "~", so Unity does not import the script.

The stand is one sheet of bent plywood, 8 mm thick, an S seen from the side:
a long base whose front edge turns up a little, a wide bend at the back that
carries the sheet up and forwards, and a second bend that turns it back into
a panel leaning back, a little steeper than the tablet, so the tablet stands
on the base and leans on the panel's top edge. The return button sits on the
panel, in the gap behind the tablet. It is modelled like the tablet: X is
width, Z is height, the front faces -Y, and it is turned half a turn about Z
before export, so in Unity the front faces -Z and depth is +Z. The stand is
centred on its footprint.

One mesh, one material: the faces sample the veneer part of a 512 atlas
(V 0.125 to 1, the grain along U), the cut edges its plywood stripes (V 0
to 0.125); make_textures.py draws the atlases.
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

# Metres. The profile is in (y, z): y is depth, the front at -y; z is height.
WIDTH = 0.100
THICKNESS = 0.008
# The tablet it holds (Assets/Models/Tablet/Source~/build_tablet.py).
TABLET_HEIGHT = 0.1895
TABLET_THICKNESS = 0.0075
TABLET_ANGLE = 70.0          # the tablet's lean, from the table
# The top surface's path, from where the base bends up at the back.
REAR_RADIUS = 0.032          # the wide bend at the back...
REAR_ANGLE = 124.0           # ...until the sheet heads up and forwards
REAR_SEGMENTS = 9
MIDDLE = 0.016               # straight, leaning forwards
UPPER_RADIUS = 0.022         # the bend back...
PANEL_ANGLE = 76.0           # ...into the panel, steeper than the tablet, which leans on its top edge only
UPPER_SEGMENTS = 5
PANEL = 0.050                # the panel, flat
FOOT_MARGIN = 0.050          # the base runs this far in front of the tablet's foot...
LIP_RADIUS = 0.030           # ...and then turns up
LIP_ANGLE = 22.0
LIP_SEGMENTS = 3
CORNER_TOP = 0.020           # rounded corners of the sheet's two ends, seen unrolled
CORNER_LIP = 0.010
CORNER_SEGMENTS = 4
GAP = 0.0005                 # between the tablet and the stand
BUTTON_DOWN = 0.031          # the return button's middle, this far down the panel from its top edge
BUTTON_OUT = 0.001           # and this far in front of it (the button is 35 mm across)

# Atlas: the veneer above V 0.125, the plywood stripes below it. The veneer
# part is a patch of VENEER_U x VENEER_V metres; the sheet is shorter than
# VENEER_U unrolled, so the grain does not repeat on it.
VENEER_V0 = 0.125
VENEER_U = 0.30
VENEER_V = 0.2275
STRIPES = (0.012, 0.113)


def v2(y, z):
    return Vector((y, z))


def direction(deg):
    a = math.radians(deg)
    return v2(math.cos(a), math.sin(a))


base_top = THICKNESS

# The top (front) surface as (point, direction of travel), from where the
# base bends up at the back (y 0) to the panel's top edge: the rear bend
# turns left (its centre above the sheet), the upper bend right (its centre
# behind the sheet).
back = [(v2(0, base_top), v2(1, 0))]
rear_center = v2(0, base_top + REAR_RADIUS)
for k in range(1, REAR_SEGMENTS + 1):
    phi = math.radians(REAR_ANGLE * k / REAR_SEGMENTS)
    back.append((rear_center + REAR_RADIUS * v2(math.sin(phi), -math.cos(phi)), v2(math.cos(phi), math.sin(phi))))
upper_start = back[-1][0] + MIDDLE * direction(REAR_ANGLE)
back.append((upper_start, direction(REAR_ANGLE)))
a1, a2 = math.radians(REAR_ANGLE), math.radians(PANEL_ANGLE)
upper_center = upper_start + UPPER_RADIUS * v2(math.sin(a1), -math.cos(a1))
for k in range(1, UPPER_SEGMENTS + 1):
    phi = a1 + (a2 - a1) * k / UPPER_SEGMENTS
    back.append((upper_center + UPPER_RADIUS * v2(-math.sin(phi), math.cos(phi)), v2(math.cos(phi), math.sin(phi))))
e = direction(PANEL_ANGLE)
rest = back[-1][0] + PANEL * e                     # the panel's top front edge
back.append((rest, e))

# The tablet leans on that edge, with its foot on the base.
d_tablet = direction(TABLET_ANGLE)
n_tablet = v2(d_tablet.y, -d_tablet.x)             # out of the tablet's back
on_back = rest - GAP * n_tablet                    # a point of the tablet's back
p_back = on_back + ((base_top + GAP - on_back.y) / d_tablet.y) * d_tablet  # its lower back edge
REST = (rest - p_back).length                      # how far up the tablet's back the panel's top edge touches it

# The base runs on in front of the tablet's foot, then turns up into the lip.
lip_start_y = (p_back - TABLET_THICKNESS * n_tablet).x - FOOT_MARGIN
path = []
lip_center = v2(lip_start_y, base_top + LIP_RADIUS)
for k in range(LIP_SEGMENTS + 1):
    phi = math.radians(LIP_ANGLE) * (1 - k / LIP_SEGMENTS)
    path.append((lip_center + LIP_RADIUS * v2(-math.sin(phi), -math.cos(phi)), v2(math.cos(phi), -math.sin(phi))))
path += back

# Length along the top surface, unrolled.
lengths = [0.0]
for (a, _), (b, _) in zip(path, path[1:]):
    lengths.append(lengths[-1] + (b - a).length)
total = lengths[-1]


def at(u):
    """The point and the into-the-sheet normal of the top surface at length u."""
    for i in range(len(path) - 1):
        if lengths[i + 1] >= u or i == len(path) - 2:
            span = lengths[i + 1] - lengths[i]
            f = 0.0 if span == 0 else (u - lengths[i]) / span
            p = path[i][0].lerp(path[i + 1][0], f)
            t = path[i][1].lerp(path[i + 1][1], f).normalized()
            return p, v2(t.y, -t.x)


def half_width(u):
    for d, r in ((u, CORNER_LIP), (total - u, CORNER_TOP)):
        if d < r:
            return WIDTH / 2 - r + math.sqrt(max(0.0, r * r - (r - d) ** 2))
    return WIDTH / 2


stations = set(lengths)
for k in range(CORNER_SEGMENTS + 1):
    a = math.radians(90 * k / CORNER_SEGMENTS)
    stations.add(CORNER_LIP * (1 - math.cos(a)))
    stations.add(total - CORNER_TOP * (1 - math.cos(a)))
stations = sorted(s for s in stations if 0 <= s <= total)

# One closed sheet: per station, top left/right and bottom left/right.
bm = bmesh.new()
uv = bm.loops.layers.uv.new("UVMap")
rows = []
for s in stations:
    p, n = at(s)
    q = p + THICKNESS * n
    hw = half_width(s)
    rows.append((s, hw, [bm.verts.new((x, y, z)) for x, (y, z) in ((-hw, p), (hw, p), (-hw, q), (hw, q))]))

TL, TR, BL, BR = range(4)
faces = []  # (face, kind)
for (s0, _, a), (s1, _, b) in zip(rows, rows[1:]):
    faces.append((bm.faces.new((a[TL], a[TR], b[TR], b[TL])), "top"))
    faces.append((bm.faces.new((a[BR], a[BL], b[BL], b[BR])), "bottom"))
    faces.append((bm.faces.new((a[BL], a[TL], b[TL], b[BL])), "side"))
    faces.append((bm.faces.new((a[TR], a[BR], b[BR], b[TR])), "side"))
faces.append((bm.faces.new((rows[0][2][TR], rows[0][2][TL], rows[0][2][BL], rows[0][2][BR])), "end"))
faces.append((bm.faces.new((rows[-1][2][TL], rows[-1][2][TR], rows[-1][2][BR], rows[-1][2][BL])), "end"))
bm.normal_update()
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

# UVs, before the edges are split.
station_of = {}
for s, hw, vs in rows:
    for i, v in enumerate(vs):
        station_of[v] = (s, i)
for face, kind in faces:
    for loop in face.loops:
        s, i = station_of[loop.vert]
        x = loop.vert.co.x
        if kind in ("top", "bottom"):
            u_ = s / VENEER_U
            v_ = VENEER_V0 + (1 - VENEER_V0) * (x + VENEER_V / 2) / VENEER_V
        else:
            depth = 0.0 if i in (TL, TR) else 1.0
            u_ = s / VENEER_U if kind == "side" else (x + WIDTH / 2) / WIDTH * 0.5
            v_ = STRIPES[0] + depth * (STRIPES[1] - STRIPES[0])
        loop[uv].uv = (u_, v_)

# Hard edges between the faces and the cut edges: split them, so each part
# shades smooth on its own.
sharp = [ed for ed in bm.edges if len({k for f, k in faces if f in ed.link_faces}) > 1]
bmesh.ops.split_edges(bm, edges=sharp)

# Centre the stand on its footprint, front to back.
ys = [v.co.y for v in bm.verts]
SHIFT = -(min(ys) + max(ys)) / 2
bmesh.ops.translate(bm, vec=(0, SHIFT, 0), verts=bm.verts)

mesh = bpy.data.meshes.new("KawaTabletStand_Bentwood")
bm.to_mesh(mesh)
bm.free()
for poly in mesh.polygons:
    poly.use_smooth = True
obj = bpy.data.objects.new("KawaTabletStand_Bentwood", mesh)
bpy.context.scene.collection.objects.link(obj)
mat = bpy.data.materials.new("KawaTabletStand_Bentwood")
mesh.materials.append(mat)

tris = sum(len(p.vertices) - 2 for p in mesh.polygons)
dims = obj.dimensions
print(f"REPORT tris={tris} verts={len(mesh.vertices)} faces={len(mesh.polygons)} size_m=({dims.x:.4f}, {dims.y:.4f}, {dims.z:.4f})")
print(f"REPORT unrolled_length={total:.4f} rest_up_tablet_back={REST:.4f} tablet_foot_unity_z={p_back.x + SHIFT:.4f}")

# Where the tablet rests, in Unity: its middle, and its lean about X; and
# where the return button goes, facing out of the panel. Depth (Blender y)
# becomes Unity z, height (Blender z) Unity y.
middle = p_back + (TABLET_HEIGHT / 2) * d_tablet - (TABLET_THICKNESS / 2) * n_tablet
tilt = 90.0 - TABLET_ANGLE
button = rest - BUTTON_DOWN * e + BUTTON_OUT * v2(-e.y, e.x)
print(f"REPORT dock_unity_position=(0, {middle.y:.5f}, {middle.x + SHIFT:.5f}) dock_unity_euler=({tilt:.1f}, 0, 0)")
print(f"REPORT button_unity_position=(0, {button.y:.5f}, {button.x + SHIFT:.5f}) button_unity_euler=({90.0 - PANEL_ANGLE:.1f}, 0, 0)")
print(f"REPORT rest_point_unity=(0, {rest.y:.5f}, {rest.x + SHIFT:.5f}) lip_top_unity_y={path[0][0].y:.5f}")

# Half a turn about Z, so the front faces -Z in Unity, as the tablet does.
obj.matrix_world = Matrix.Rotation(math.pi, 4, "Z") @ obj.matrix_world
bpy.ops.object.select_all(action="DESELECT")
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "KawaTabletStand_Bentwood.fbx"), use_selection=True,
                         apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
                         axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                         mesh_smooth_type="FACE", use_mesh_modifiers=True, path_mode="STRIP")
print("REPORT done")
