"""Builds the cushion tablet stand in Blender (issue #349): one soft piece,
like a plush tablet pillow: a wedge at the back, a bolster across the front,
and the groove between them, all on one base.

Run from the repository root:

    blender -b --factory-startup --python "Assets/Models/TabletStand/Source~/build_cushion.py" -- Assets/Models/TabletStand

Writes KawaTabletStand_Cushion.fbx to the given folder and prints the
triangle count, where the tablet rests on it and where its return button
goes. This folder ends in "~", so Unity does not import the script.

Modelled like the other stands: X is width, Z is height, the front faces -Y,
and it is turned half a turn about Z before export, so in Unity the front
faces -Z and depth is +Z. One section, the base's flat bottom, the bolster,
the groove's floor a little above the base and the wedge, is swept across
the width, the edges of its flat ends rounded. The tablet stands in the
groove and leans on the wedge's front slope. Where the tablet's home button
is, in the middle, the bolster is pressed down to the groove's floor, so the
button shows and can be pressed. The corners are rounded and the shading
smooth, so it reads as stuffed; the fabric is a tileable texture
(make_textures.py), mapped round the section.
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
# The wedge's triangle in (y, z): the foot of its front slope, its top, its back foot.
FRONT, TOP, BACK = Vector((-0.02, 0.0)), Vector((0.06, 0.15)), Vector((0.14, 0.0))
WEDGE_ROUND = 0.022
WEDGE_ROUND_SEG = 4
BASE_T = 0.012               # the groove's floor, above the base's bottom
GROOVE_ROUND = 0.006         # where the floor meets the slope and the bolster
GROOVE_ROUND_SEG = 3
BOLSTER_R = 0.03
BOLSTER_Y = -0.056
BOLSTER_SEG = 14             # segments a full turn of the bolster would have
# The ends' edges round off: rings drawn in by these, this far from the end
# (less than half the base under the groove, which they draw in from both sides).
END_ROUND = ((0.0, 0.005), (0.0015, 0.002), (0.005, 0.0))
# The home button's notch: flat over the middle, sloping to nothing beyond;
# there the bolster is pressed down to this share of its height over the floor.
NOTCH_FLAT, NOTCH_OUT, NOTCH_LEFT = 0.022, 0.038, 0.05
REST_Z = 0.012               # the tablet's lower back edge, on the wedge's front slope
FABRIC = 0.30                # metres of fabric per texture repeat


def corner_arc(p, a, b, r, seg):
    """The corner p between the lines to a and to b, rounded by r: seg + 1 points from the a side to the b side."""
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
    return [Vector((c.x + r * math.cos(a), c.y + r * math.sin(a))) for a in (a0 + (a1 - a0) * k / seg for k in range(seg + 1))]


# The section, counter-clockwise: the back corner, the top, down the slope to
# the groove's floor, along it to the bolster, over the bolster to the front
# and down to its foot; the base's bottom closes it back to the back corner.
bolster_c = Vector((BOLSTER_Y, BOLSTER_R))
bolster_foot = Vector((BOLSTER_Y, 0.0))
slope_foot = FRONT + (TOP - FRONT) * (BASE_T / TOP.y)          # where the slope meets the floor
# The floor meets the bolster's back, rounded by GROOVE_ROUND: the round's
# centre stands that high over the floor, touching the bolster from outside.
fc = Vector((0.0, BASE_T + GROOVE_ROUND))
fc.x = BOLSTER_Y + math.sqrt((BOLSTER_R + GROOVE_ROUND) ** 2 - (fc.y - BOLSTER_R) ** 2)
floor_end = Vector((fc.x, BASE_T))
on_bolster = bolster_c + (fc - bolster_c) * (BOLSTER_R / (BOLSTER_R + GROOVE_ROUND))
a0 = -math.pi / 2
a1 = math.atan2(on_bolster.y - fc.y, on_bolster.x - fc.x)
while a1 > a0:
    a1 -= 2 * math.pi
floor_round = [fc + GROOVE_ROUND * Vector((math.cos(a), math.sin(a)))
               for a in (a0 + (a1 - a0) * k / GROOVE_ROUND_SEG for k in range(GROOVE_ROUND_SEG + 1))]
t_start = math.atan2(on_bolster.y - BOLSTER_R, on_bolster.x - BOLSTER_Y)
t_end = 1.5 * math.pi                                             # its foot
steps = max(2, round((t_end - t_start) / (2 * math.pi) * BOLSTER_SEG))
bolster = [bolster_c + BOLSTER_R * Vector((math.cos(t), math.sin(t)))
           for t in (t_start + (t_end - t_start) * k / steps for k in range(1, steps + 1))]
section = (corner_arc(BACK, bolster_foot, TOP, WEDGE_ROUND, WEDGE_ROUND_SEG)
           + corner_arc(TOP, BACK, slope_foot, WEDGE_ROUND, WEDGE_ROUND_SEG)
           + corner_arc(slope_foot, TOP, floor_end, GROOVE_ROUND, GROOVE_ROUND_SEG)
           + floor_round + bolster)
in_front = [p.x < floor_end.x + 0.001 for p in section]          # the bolster's points, pressed down in the notch
assert min(p.y for p in section) >= -1e-9 and floor_end.x < slope_foot.x, "the groove must stay above the base"


def pressed(notch):
    """The section with the bolster pressed down by notch (0 none, 1 fully)."""
    out = []
    for p, front in zip(section, in_front):
        if front and p.y > BASE_T:
            p = Vector((p.x, BASE_T + (p.y - BASE_T) * (1 - notch * (1 - NOTCH_LEFT))))
        out.append(p)
    return out


def drawn_in(points, d):
    """The section with every point moved d into it (along the mitre of its two sides' inward normals)."""
    out = []
    n = len(points)
    for i in range(n):
        a, p, b = points[i - 1], points[i], points[(i + 1) % n]
        n1 = Vector((-(p - a).y, (p - a).x)).normalized()
        n2 = Vector((-(b - p).y, (b - p).x)).normalized()
        out.append(p + d * (n1 + n2) / (1 + n1.dot(n2)))
    return out


assert sum(a.x * b.y - b.x * a.y for a, b in zip(section, section[1:] + section[:1])) > 0, "the section must run counter-clockwise"
bm = bmesh.new()
uv = bm.loops.layers.uv.new("UVMap")
# Across the width: (x, how far the ring is drawn in, for the rounded ends;
# how far the bolster is pressed down).
stations = ([(-WIDTH / 2 + dx, d, 0.0) for dx, d in END_ROUND]
            + [(-NOTCH_OUT, 0.0, 0.0), (-NOTCH_FLAT, 0.0, 1.0), (NOTCH_FLAT, 0.0, 1.0), (NOTCH_OUT, 0.0, 0.0)]
            + [(WIDTH / 2 - dx, d, 0.0) for dx, d in reversed(END_ROUND)])
rings = []
for x, d, notch in stations:
    rings.append([bm.verts.new((x, p.x, p.y)) for p in drawn_in(pressed(notch), d)])
perimeter = [0.0]
for a, b in zip(section, section[1:] + section[:1]):
    perimeter.append(perimeter[-1] + (b - a).length)
n = len(section)
for r0, r1, (x0, _, _), (x1, _, _) in zip(rings, rings[1:], stations, stations[1:]):
    for i in range(n):
        j = (i + 1) % n
        face = bm.faces.new((r0[i], r0[j], r1[j], r1[i]))
        for loop, (s, x) in zip(face.loops, ((perimeter[i], x0), (perimeter[i + 1], x0), (perimeter[i + 1], x1), (perimeter[i], x1))):
            loop[uv].uv = (s / FABRIC, (x + WIDTH / 2) / FABRIC)
caps = [bm.faces.new(rings[0]), bm.faces.new(rings[-1])]
for face in caps:
    for loop in face.loops:
        loop[uv].uv = (loop.vert.co.y / FABRIC, loop.vert.co.z / FABRIC)
# The section is not convex (the groove), so the ends are cut into triangles
# here; and they shade flat, apart from the rounded edges round them.
caps = set(bmesh.ops.triangulate(bm, faces=caps, quad_method="BEAUTY", ngon_method="EAR_CLIP")["faces"])
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bmesh.ops.split_edges(bm, edges=[e for e in bm.edges if len(e.link_faces) == 2 and (e.link_faces[0] in caps) != (e.link_faces[1] in caps)])

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
print(f"REPORT cushion bolster_top={2 * BOLSTER_R:.4f} pressed_to={max(p.y for p, f in zip(pressed(1.0), in_front) if f):.4f} "
      f"notch_flat=+-{NOTCH_FLAT:.3f} groove_floor={BASE_T:.4f}")

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
