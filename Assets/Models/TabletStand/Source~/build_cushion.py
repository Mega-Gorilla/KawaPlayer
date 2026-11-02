"""Builds the cushion tablet stand in Blender (issue #349): a soft wedge with
a bolster across its front, like a plush tablet pillow.

Run from the repository root:

    blender -b --factory-startup --python "Assets/Models/TabletStand/Source~/build_cushion.py" -- Assets/Models/TabletStand

Writes KawaTabletStand_Cushion.fbx to the given folder and prints the
triangle count, where the tablet rests on it and where its return button
goes. This folder ends in "~", so Unity does not import the script.

Modelled like the other stands: X is width, Z is height, the front faces -Y,
and it is turned half a turn about Z before export, so in Unity the front
faces -Z and depth is +Z. The tablet stands in the groove between the
bolster and the wedge and leans on the wedge's front slope. Every edge is
rounded and the shading smooth, so it reads as stuffed; the fabric is a
tileable texture (make_textures.py), mapped round the wedge and the bolster.
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

TABLET_THICKNESS = 0.0075
TABLET_HEIGHT = 0.1895
WIDTH = 0.300
# The wedge's section in (y, z): front foot, top, back foot.
FRONT, TOP, BACK = Vector((-0.02, 0.0)), Vector((0.06, 0.15)), Vector((0.14, 0.0))
WEDGE_ROUND = 0.022
WEDGE_ROUND_SEG = 4
END_INSET = 0.014            # the wedge's and the bolster's ends round off over this much
BOLSTER_R = 0.03
BOLSTER_Y = -0.056
BOLSTER_SEG = 14
REST_Z = 0.012               # the tablet's lower back edge, on the wedge's front slope
FABRIC = 0.30                # metres of fabric per texture repeat


def rounded_polygon(points, r, seg):
    """A convex polygon (counter-clockwise) with every corner rounded by r."""
    out = []
    n = len(points)
    for i in range(n):
        p, a, b = points[i], points[i - 1], points[(i + 1) % n]
        da, db = (a - p).normalized(), (b - p).normalized()
        half = math.acos(max(-1.0, min(1.0, da.dot(db)))) / 2
        dist = r / math.tan(half)
        c = p + (da + db).normalized() * (r / math.sin(half))
        t0, t1 = p + da * dist, p + db * dist
        a0 = math.atan2(t0.y - c.y, t0.x - c.x)
        a1 = math.atan2(t1.y - c.y, t1.x - c.x)
        while a1 < a0:
            a1 += 2 * math.pi
        if a1 - a0 > math.pi:
            a1 -= 2 * math.pi
        for k in range(seg + 1):
            a = a0 + (a1 - a0) * k / seg
            out.append(Vector((c.x + r * math.cos(a), c.y + r * math.sin(a))))
    return out


bm = bmesh.new()
uv = bm.loops.layers.uv.new("UVMap")


def loft(section, centre, length, inset, smooth_uv):
    """A section in (y, z) swept along X over length, its two ends rounded off:
    rings at the ends shrink towards the section's centre, then a cap."""
    rings = []
    stations = [(-length / 2, 0.55), (-length / 2 + inset * 0.35, 0.86), (-length / 2 + inset, 1.0),
                (length / 2 - inset, 1.0), (length / 2 - inset * 0.35, 0.86), (length / 2, 0.55)]
    for x, scale in stations:
        rings.append([bm.verts.new((x, centre.x + (p.x - centre.x) * scale, centre.y + (p.y - centre.y) * scale)) for p in section])
    perimeter = [0.0]
    for a, b in zip(section, section[1:] + section[:1]):
        perimeter.append(perimeter[-1] + (b - a).length)
    n = len(section)
    for r0, r1, (x0, _), (x1, _) in zip(rings, rings[1:], stations, stations[1:]):
        for i in range(n):
            j = (i + 1) % n
            face = bm.faces.new((r0[i], r0[j], r1[j], r1[i]))
            for loop, (s, x) in zip(face.loops, ((perimeter[i], x0), (perimeter[i + 1], x0), (perimeter[i + 1], x1), (perimeter[i], x1))):
                loop[uv].uv = (s / FABRIC, (x + length / 2) / FABRIC)
    for ring in (rings[0], rings[-1]):
        face = bm.faces.new(ring)
        for loop in face.loops:
            loop[uv].uv = (loop.vert.co.y / FABRIC, loop.vert.co.z / FABRIC)


wedge = rounded_polygon([FRONT, BACK, TOP], WEDGE_ROUND, WEDGE_ROUND_SEG)
loft(wedge, (FRONT + TOP + BACK) / 3, WIDTH, END_INSET, True)
bolster = [Vector((BOLSTER_Y + BOLSTER_R * math.cos(2 * math.pi * k / BOLSTER_SEG), BOLSTER_R + BOLSTER_R * math.sin(2 * math.pi * k / BOLSTER_SEG)))
           for k in range(BOLSTER_SEG)]
loft(bolster, Vector((BOLSTER_Y, BOLSTER_R)), WIDTH, END_INSET, True)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

mesh = bpy.data.meshes.new("KawaTabletStand_Cushion")
bm.to_mesh(mesh)
bm.free()
for poly in mesh.polygons:
    poly.use_smooth = True
obj = bpy.data.objects.new("KawaTabletStand_Cushion", mesh)
bpy.context.scene.collection.objects.link(obj)
mesh.materials.append(bpy.data.materials.new("KawaTabletStand_Cushion"))
tris = sum(len(p.vertices) - 2 for p in mesh.polygons)
d = obj.dimensions
print(f"REPORT KawaTabletStand_Cushion tris={tris} verts={len(mesh.vertices)} size_m=({d.x:.4f}, {d.y:.4f}, {d.z:.4f})")

# The tablet leans on the wedge's front slope (the flat part between the
# rounded corners), its lower back edge at REST_Z.
slope = (TOP - FRONT).normalized()
angle = math.degrees(math.atan2(slope.y, slope.x))
normal_out = Vector((-slope.y, slope.x))     # out of the slope, towards the front and up
back_edge = FRONT + slope * (REST_Z / slope.y) + normal_out * 0.0005
middle = back_edge + slope * (TABLET_HEIGHT / 2) + normal_out * (TABLET_THICKNESS / 2)
button = FRONT + slope * (0.085 / slope.y) + normal_out * 0.0006
print(f"REPORT cushion slope_angle={angle:.1f} dock_unity_position=(0, {middle.y:.5f}, {middle.x:.5f}) dock_unity_euler=({90 - angle:.1f}, 0, 0)")
print(f"REPORT cushion button_unity_position=(0, {button.y:.5f}, {button.x:.5f}) button_unity_euler=({90 - angle:.1f}, 0, 0)")
front_of_tablet = middle + normal_out * (TABLET_THICKNESS / 2) - slope * (TABLET_HEIGHT / 2)
print(f"REPORT cushion tablet_front_bottom=({front_of_tablet.x:.4f}, {front_of_tablet.y:.4f}) bolster_back_y={BOLSTER_Y + BOLSTER_R:.4f}")

obj.matrix_world = Matrix.Rotation(math.pi, 4, "Z") @ obj.matrix_world
bpy.ops.object.select_all(action="DESELECT")
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "KawaTabletStand_Cushion.fbx"), use_selection=True,
                         apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
                         axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                         mesh_smooth_type="FACE", use_mesh_modifiers=True, path_mode="STRIP")
print("REPORT done")
