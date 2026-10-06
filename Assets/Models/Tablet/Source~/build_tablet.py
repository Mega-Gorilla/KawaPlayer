"""Builds the KawaPlayer tablet mesh in Blender (issue #149).

Run from the repository root:

    blender -b --factory-startup --python "Assets/Models/Tablet/Source~/build_tablet.py" -- Assets/Models/Tablet

Writes KawaTablet.fbx and KawaTablet_Palette.png to the given folder and
prints the triangle count. Add --blend to also save KawaTablet.blend, and
--render to write preview renders (Cycles, CPU) next to them. This folder
ends in "~", so Unity does not import the script.

The tablet is modelled in the XZ plane, front facing -Y: X is width, Z is
height, Y is thickness. Before export it is turned half a turn about Z, so
in Unity it arrives unrotated with the screen facing -Z, the side a World
Space Canvas is read from. One mesh, one material; colours come from a
4-cell palette texture, every face sampling the centre of its cell.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.abspath(argv[0]) if argv else os.getcwd()
RENDER = "--render" in argv
SAVE_BLEND = "--blend" in argv
os.makedirs(OUT, exist_ok=True)

# Dimensions in metres.
WIDTH = 0.230
HEIGHT = 0.163
THICKNESS = 0.0075
CORNER_RADIUS = 0.012
CORNER_SEGMENTS = 8
EDGE_BEVEL = 0.0015
EDGE_BEVEL_SEGMENTS = 2
SIDE_BEZEL = 0.010           # left, right and top, measured from the outer edge
BOTTOM_BEZEL = 0.022         # leaves room for the home button
BUTTON_RADIUS = 0.0055
BUTTON_HEIGHT = 0.0006
BUTTON_SEGMENTS = 16

# Palette cells: body, bezel, screen, button.
PALETTE = [
    (0.78, 0.79, 0.81),  # body (silver)
    (0.06, 0.06, 0.07),  # bezel (black)
    (0.02, 0.02, 0.025), # screen (glass)
    (0.17, 0.17, 0.19),  # home button
]
BODY, BEZEL, SCREEN, BUTTON = range(4)


def rounded_rect(w, h, r, seg):
    pts = []
    corners = [(w / 2 - r, h / 2 - r, 0), (-w / 2 + r, h / 2 - r, 90),
               (-w / 2 + r, -h / 2 + r, 180), (w / 2 - r, -h / 2 + r, 270)]
    for cx, cz, a0 in corners:
        for i in range(seg + 1):
            a = math.radians(a0 + 90 * i / seg)
            pts.append((cx + r * math.cos(a), cz + r * math.sin(a)))
    return pts


bpy.ops.wm.read_factory_settings(use_empty=True)
bm = bmesh.new()

outline = rounded_rect(WIDTH, HEIGHT, CORNER_RADIUS, CORNER_SEGMENTS)
front = [bm.verts.new((x, -THICKNESS / 2, z)) for x, z in outline]
back = [bm.verts.new((x, THICKNESS / 2, z)) for x, z in outline]
front_face = bm.faces.new(front)
back_face = bm.faces.new(back)
n = len(outline)
for i in range(n):
    j = (i + 1) % n
    bm.faces.new((front[i], front[j], back[j], back[i]))
# The body is closed, so let Blender make every face point outwards.
bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
bm.normal_update()
assert front_face.normal.y < -0.99 and back_face.normal.y > 0.99

# Round the front and back perimeters.
rim = [e for e in bm.edges if (front_face in e.link_faces) or (back_face in e.link_faces)]
bmesh.ops.bevel(bm, geom=rim, offset=EDGE_BEVEL, segments=EDGE_BEVEL_SEGMENTS,
                profile=0.5, affect="EDGES", clamp_overlap=True)

bm.normal_update()

# The flat front is the face whose normal points -Y and sits on the front plane.
flat_front = [f for f in bm.faces if f.normal.y < -0.99 and abs(f.calc_center_median().y + THICKNESS / 2) < 1e-5]
assert len(flat_front) == 1, len(flat_front)
screen = flat_front[0]
inset = bmesh.ops.inset_region(bm, faces=[screen], thickness=SIDE_BEZEL - EDGE_BEVEL, depth=0.0)
bezel_faces = set(inset["faces"])

# Make the bottom bezel deeper by lifting the screen's lower edge. Every
# screen vertex below the centre belongs to the two lower corner arcs, so
# lifting them all keeps the screen a rounded rectangle.
for v in screen.verts:
    if v.co.z < 0:
        v.co.z += BOTTOM_BEZEL - SIDE_BEZEL

# Home button: a separate island resting on the bottom bezel. Its base sits a
# hair inside the body so no gap shows, and has no cap since nobody sees it.
button_z = -HEIGHT / 2 + BOTTOM_BEZEL / 2
ring_lo, ring_hi = [], []
for i in range(BUTTON_SEGMENTS):
    a = 2 * math.pi * i / BUTTON_SEGMENTS
    x, z = BUTTON_RADIUS * math.cos(a), button_z + BUTTON_RADIUS * math.sin(a)
    ring_lo.append(bm.verts.new((x, -THICKNESS / 2 + 0.00005, z)))
    ring_hi.append(bm.verts.new((x, -THICKNESS / 2 - BUTTON_HEIGHT, z)))
button_faces = [bm.faces.new(ring_hi)]
for i in range(BUTTON_SEGMENTS):
    j = (i + 1) % BUTTON_SEGMENTS
    button_faces.append(bm.faces.new((ring_lo[i], ring_lo[j], ring_hi[j], ring_hi[i])))
bm.normal_update()
# The button is open at the base, so orient it by hand: cap towards -Y, sides outwards.
for f in button_faces:
    c = f.calc_center_median()
    outward = Vector((0, -1, 0)) if f is button_faces[0] else Vector((c.x, 0, c.z - button_z))
    if f.normal.dot(outward) < 0:
        f.normal_flip()
bm.normal_update()
assert button_faces[0].normal.y < -0.99

# UVs: every face samples the centre of its palette cell.
uv = bm.loops.layers.uv.new("UVMap")
for f in bm.faces:
    if f is screen:
        cell = SCREEN
    elif f in bezel_faces:
        cell = BEZEL
    elif f in button_faces:
        cell = BUTTON
    else:
        cell = BODY
    for loop in f.loops:
        loop[uv].uv = ((cell + 0.5) / len(PALETTE), 0.5)

# Face -Z in Unity (see the module docstring).
bmesh.ops.rotate(bm, verts=bm.verts[:], cent=(0, 0, 0), matrix=Matrix.Rotation(math.pi, 3, "Z"))

mesh = bpy.data.meshes.new("KawaTablet")
bm.to_mesh(mesh)
bm.free()
obj = bpy.data.objects.new("KawaTablet", mesh)
bpy.context.scene.collection.objects.link(obj)
bpy.context.view_layer.objects.active = obj
obj.select_set(True)
bpy.ops.object.shade_smooth_by_angle(angle=math.radians(50))

# Palette texture and the single material.
size_x, size_y = 16 * len(PALETTE), 16
img = bpy.data.images.new("KawaTablet_Palette", size_x, size_y, alpha=False)
pixels = []
for _y in range(size_y):
    for x in range(size_x):
        r, g, b = PALETTE[x // 16]
        pixels += [r, g, b, 1.0]
img.pixels = pixels
img.filepath_raw = os.path.join(OUT, "KawaTablet_Palette.png")
img.file_format = "PNG"
img.save()

mat = bpy.data.materials.new("KawaTablet")
mat.use_nodes = True
nt = mat.node_tree
bsdf = nt.nodes["Principled BSDF"]
tex = nt.nodes.new("ShaderNodeTexImage")
tex.image = img
tex.interpolation = "Closest"
nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
bsdf.inputs["Roughness"].default_value = 0.6
bsdf.inputs["Metallic"].default_value = 0.0
mesh.materials.append(mat)

# Report.
tris = sum(len(p.vertices) - 2 for p in mesh.polygons)
dims = obj.dimensions
print(f"REPORT tris={tris} verts={len(mesh.vertices)} faces={len(mesh.polygons)} materials={len(mesh.materials)}"
      f" size_m=({dims.x:.4f}, {dims.y:.4f}, {dims.z:.4f})")
sw = WIDTH - 2 * SIDE_BEZEL
sh = HEIGHT - SIDE_BEZEL - BOTTOM_BEZEL
print(f"REPORT screen_m=({sw:.4f} x {sh:.4f}) aspect={sw / sh:.3f}")

if SAVE_BLEND:
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "KawaTablet.blend"))
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "KawaTablet.fbx"), use_selection=True,
                         apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
                         axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                         mesh_smooth_type="FACE", use_mesh_modifiers=True, path_mode="STRIP")

if RENDER:
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 64
    scene.render.resolution_x, scene.render.resolution_y = 1400, 1000
    scene.render.film_transparent = False
    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.2, 0.21, 0.23, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.5
    scene.world = world
    scene.view_settings.view_transform = "Standard"
    for loc, energy, size in (((0.25, 0.35, 0.3), 6, 0.4), ((-0.35, 0.2, 0.1), 2.5, 0.3), ((0.0, -0.4, 0.3), 3, 0.4)):
        ld = bpy.data.lights.new("L", "AREA")
        ld.energy, ld.size = energy, size
        lo = bpy.data.objects.new("L", ld)
        lo.location = loc
        scene.collection.objects.link(lo)
        lo.rotation_euler = (Vector((0, 0, 0)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    cd = bpy.data.cameras.new("C")
    cd.lens = 85
    cam = bpy.data.objects.new("C", cd)
    scene.collection.objects.link(cam)
    scene.camera = cam
    views = {
        "front": (0.0, 0.75, 0.0),
        "angle": (-0.42, 0.55, 0.22),
        "back": (0.45, -0.55, 0.18),
        "edge": (-0.62, 0.22, -0.05),
    }
    for name, loc in views.items():
        cam.location = loc
        cam.rotation_euler = (Vector((0, 0, 0)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(OUT, f"render_{name}.png")
        bpy.ops.render.render(write_still=True)
    # Close-up of the home button.
    cam.location = (-0.05, 0.16, button_z + 0.03)
    cam.rotation_euler = (Vector((0, THICKNESS / 2, button_z)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = os.path.join(OUT, "render_button.png")
    bpy.ops.render.render(write_still=True)
print("REPORT done")
