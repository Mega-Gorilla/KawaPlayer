"""Builds the bentwood tablet stand mesh in Blender (issue #349).

Run from the repository root:

    blender -b --factory-startup --python "Assets/Models/TabletStand/Source~/build_bentwood.py" -- Assets/Models/TabletStand

Writes KawaTabletStand_Bentwood.fbx to the given folder and prints the triangle count
and where the tablet rests on it. Add --render to also write preview renders
(Cycles, CPU), with a flat tablet stand-in, to the folder given after it
(--render <folder>): keep them outside Assets, or Unity imports them into
the package. This folder ends in "~", so Unity does not import the script.

The stand is one sheet of bent plywood, 8 mm thick: a base whose front edge
curls up into a lip, bending up at the back into a support steeper than the
tablet, so the tablet stands on the base behind the lip and leans on the
support's top edge. It is modelled like the tablet: X is width, Z is
height, the front faces -Y, and it is turned half a turn about Z before
export, so in Unity the front faces -Z and depth is +Z.

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
RENDER_DIR = os.path.abspath(argv[argv.index("--render") + 1]) if "--render" in argv else None
os.makedirs(OUT, exist_ok=True)

# Metres. The profile is in (y, z): y is depth, the front at -y; z is height.
WIDTH = 0.160
THICKNESS = 0.008
# The tablet it holds (Assets/Models/Tablet/Source~/build_tablet.py).
TABLET_HEIGHT = 0.1895
TABLET_THICKNESS = 0.0075
TABLET_ANGLE = 70.0          # the tablet's lean, from the table
SUPPORT_ANGLE = 78.0         # the support is steeper, so it stays behind the tablet
TABLET_BACK_Y = -0.050       # the tablet's lower back edge, standing on the base
REST = 0.110                 # how far up the tablet's back the support's top edge touches it
LIP_START_Y = -0.060         # where the base's top surface starts to curl up into the lip
LIP_RADIUS = 0.010
LIP_ANGLE = 60.0
LIP_SEGMENTS = 5
BEND_RADIUS = 0.016          # of the top surface, where the base bends up into the support
BEND_SEGMENTS = 10
CORNER_TOP = 0.022           # rounded corners of the sheet's two ends, seen unrolled
CORNER_LIP = 0.010
CORNER_SEGMENTS = 4
GAP = 0.0005                 # between the tablet and the stand

# Atlas: the veneer above V 0.125, the plywood stripes below it. The veneer
# part is a patch of VENEER_U x VENEER_V metres.
VENEER_V0 = 0.125
VENEER_U = 0.26
VENEER_V = 0.2275
STRIPES = (0.012, 0.113)


def v2(y, z):
    return Vector((y, z))


def direction(deg):
    a = math.radians(deg)
    return v2(math.cos(a), math.sin(a))


base_top = THICKNESS
d_tablet = direction(TABLET_ANGLE)
n_tablet = v2(d_tablet.y, -d_tablet.x)            # out of the tablet's back
p_back = v2(TABLET_BACK_Y, base_top + GAP)        # the tablet's lower back edge
rest = p_back + REST * d_tablet - GAP * n_tablet  # the support's top front edge
e = direction(SUPPORT_ANGLE)
corner = rest - ((rest.y - base_top) / e.y) * e   # where the top surface's base and support lines meet
tangent = BEND_RADIUS * math.tan(math.radians(SUPPORT_ANGLE) / 2)
bend_start = corner - v2(tangent, 0)
bend_center = bend_start + v2(0, BEND_RADIUS)

# The top (front) surface, from the lip's end to the support's top edge,
# as (point, direction of travel).
path = []
lip_center = v2(LIP_START_Y, base_top + LIP_RADIUS)
for k in range(LIP_SEGMENTS + 1):
    phi = math.radians(LIP_ANGLE) * (1 - k / LIP_SEGMENTS)
    path.append((lip_center + LIP_RADIUS * v2(-math.sin(phi), -math.cos(phi)), v2(math.cos(phi), -math.sin(phi))))
path.append((bend_start, v2(1, 0)))
for k in range(1, BEND_SEGMENTS + 1):
    theta = math.radians(-90 + SUPPORT_ANGLE * k / BEND_SEGMENTS)
    t = v2(-math.sin(theta), math.cos(theta))
    path.append((bend_center + BEND_RADIUS * v2(math.cos(theta), math.sin(theta)), t))
path.append((rest, e))

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
print(f"REPORT unrolled_length={total:.4f} bend_start_y={bend_start.x:.4f} clearance_behind_tablet={bend_start.x - TABLET_BACK_Y:.4f}")

# Where the tablet rests, in Unity: its middle, and its lean about X. Depth
# (Blender y) becomes Unity z, height (Blender z) Unity y.
middle = p_back + (TABLET_HEIGHT / 2) * d_tablet - (TABLET_THICKNESS / 2) * n_tablet
tilt = 90.0 - TABLET_ANGLE
print(f"REPORT dock_unity_position=(0, {middle.y:.5f}, {middle.x:.5f}) dock_unity_euler=({tilt:.1f}, 0, 0)")
print(f"REPORT rest_point_unity=(0, {rest.y:.5f}, {rest.x:.5f}) lip_top_unity_y={path[0][0].y:.5f}")

if RENDER_DIR:
    os.makedirs(RENDER_DIR, exist_ok=True)
    scene = bpy.context.scene
    tab = bpy.data.objects.new("Tablet", bpy.data.meshes.new("Tablet"))
    tb = bmesh.new()
    bmesh.ops.create_cube(tb, size=1.0)
    tb.to_mesh(tab.data)
    tb.free()
    tab.scale = (0.30, TABLET_THICKNESS, TABLET_HEIGHT)
    tab.location = (0, middle.x, middle.y)
    tab.rotation_euler = (math.radians(-tilt), 0, 0)
    scene.collection.objects.link(tab)
    floor = bpy.data.objects.new("Floor", bpy.data.meshes.new("Floor"))
    fb = bmesh.new()
    bmesh.ops.create_grid(fb, x_segments=1, y_segments=1, size=0.4)
    fb.to_mesh(floor.data)
    fb.free()
    scene.collection.objects.link(floor)
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 32
    scene.render.resolution_x, scene.render.resolution_y = 900, 700
    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.75, 0.76, 0.78, 1)
    scene.world = world
    cd = bpy.data.cameras.new("C")
    cd.lens = 60
    cam = bpy.data.objects.new("C", cd)
    scene.collection.objects.link(cam)
    scene.camera = cam
    for name, loc in {"side": (0.55, 0.0, 0.08), "angle": (0.35, -0.42, 0.25), "front": (0.0, -0.6, 0.12)}.items():
        cam.location = loc
        cam.rotation_euler = (Vector((0, -0.01, 0.07)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(RENDER_DIR, f"render_{name}.png")
        bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(tab)
    bpy.data.objects.remove(floor)

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
