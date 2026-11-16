"""Build the cassette-deck models (cassette, radio, turntable, table) and export them as FBX for Unity.

Run headless with Blender 5.1 from the repo root:
  blender -b --factory-startup -P Tools/blender/cassette_deck.py -- [--render DIR] [--blend FILE] [--no-check]

Every run rebuilds all four models from scratch and writes Cassette.fbx, Radio.fbx, Turntable.fbx and
Table.fbx to Assets/_Project/Resources/CassetteDeck (--out DIR writes elsewhere). Afterwards it re-imports
each FBX into an empty scene and prints every object's parent, pivot, bounds, triangle count, material slots
and UV range, checks the triangle budgets and that every mesh shell is closed with outward normals.
--render DIR also writes Eevee preview PNGs: deck-<asset>.png for each model and deck-scene.png for the whole
set on the table (--only cassette scene ... renders just those). --blend FILE saves the built scene as a
.blend; keep it out of Assets/ so Unity doesn't import it.

Conventions: metres, Blender Z up, the front (the side the player sees) faces -Y while modelling. Unity's
FBX importer mirrors X, so with the exporter's -Z forward / Y up axes Blender -Y would land on Unity +Z.
The spec wants the front on Unity -Z, so the export turns each model 180 degrees about the vertical (data
only; every node keeps an identity rotation). In Unity the front then faces -Z, the player's left is -X and
up is +Y. Re-imported into Blender the fronts face +Y. --no-front-flip exports without that turn.
"""
import argparse
import math
import pathlib
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

REPO = pathlib.Path(__file__).resolve().parents[2]
DEFAULT_OUT = REPO / "Assets" / "_Project" / "Resources" / "CassetteDeck"
ASSETS = ("Cassette", "Radio", "Turntable", "Table")
BUDGET = {"Cassette": 1500, "Radio": 8000, "Turntable": 2000, "Table": 2000}

# ---------------------------------------------------------------------------------------------------------
# Spec numbers shared by several parts (metres, Blender axes, front = -Y).

CAS_W, CAS_H = 0.100, 0.064             # cassette width and height; it is 0.012 thick (+-0.006 in y)
HUB_X, WIN_Z = 0.021, -0.003          # cassette hub centres (x = +-HUB_X) and window centre height

RADIO_W, RADIO_D = 0.42, 0.12
BODY_Z0, BODY_Z1 = 0.008, 0.205        # radio body bottom (on its feet) and top surface
SPK_X, SPK_Z = 0.130, 0.105            # speaker centres
CAV_Z0, CAV_Z1, CAV_W = 0.040, 0.122, 0.114   # cassette well behind the door
DOOR_Z0, DOOR_Z1, DOOR_W = 0.0415, 0.1205, 0.111
DOOR_FRONT, DOOR_BACK = -0.0620, -0.0555
SLOT = (0.0, -0.047, CAV_Z0 + 0.001 + CAS_H / 2)   # inserted cassette centre
PLATE_TOP = BODY_Z1 + 0.0025           # top of the teal button deck; buttons start here
BTN_Y, BTN_TRAVEL = -0.020, 0.010
LIGHT_Z = 0.143

TT_R, TT_TOP, TT_GROOVE_Z = 0.26, 0.032, 0.020   # radius, collar top, slot floor (cassette bottoms)
TT_GROOVE = (0.2215, 0.2415)                       # slot radii; a flat 0.1 m cassette at r 0.23 needs ~0.241
TT_RIM_R = 0.23

TABLE_W, TABLE_D, TABLE_H, TABLE_T = 0.90, 0.55, 0.68, 0.028

PALETTE = {
    # slot: (sRGB hex, roughness, alpha)
    "Shell": ("#E8704F", 0.42, 1.0),
    "Label": ("#F7F2E8", 0.75, 1.0),
    "Window": ("#3A4448", 0.05, 0.30),
    "Reel": ("#EFE7D6", 0.45, 1.0),
    "Tape": ("#4B2E22", 0.55, 1.0),
    "RadioBody": ("#F3E7CF", 0.45, 1.0),
    "RadioTrim": ("#23978A", 0.40, 1.0),
    "Speaker": ("#33373B", 0.70, 1.0),
    "Glass": ("#3A4448", 0.05, 0.30),
    "Button": ("#F07A3A", 0.35, 1.0),
    "Light": ("#FFC85C", 0.30, 1.0),
    "Turntable": ("#6A4A36", 0.62, 1.0),
    "TurntableBase": ("#23978A", 0.45, 1.0),
    "TableTop": ("#C29F78", 0.55, 1.0),
    "TableLegs": ("#23978A", 0.45, 1.0),
}
MATS = {}


# ---------------------------------------------------------------------------------------------------------
# Small helpers

def srgb(hexstr, alpha=1.0):
    h = hexstr.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(((x + 0.055) / 1.055) ** 2.4 if x > 0.04045 else x / 12.92 for x in c) + (alpha,)


def make_material(name, hexcol, rough=0.5, alpha=1.0, emit=0.0):
    mat = bpy.data.materials.new(name)
    if not mat.node_tree:
        mat.use_nodes = True
    bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    col = srgb(hexcol)
    bsdf.inputs["Base Color"].default_value = col
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Alpha"].default_value = alpha
    if emit:
        bsdf.inputs["Emission Color"].default_value = col
        bsdf.inputs["Emission Strength"].default_value = emit
    mat.diffuse_color = col[:3] + (alpha,)
    mat.roughness = rough
    if alpha < 1.0:
        mat.surface_render_method = "BLENDED"
    return mat


def make_materials():
    MATS.clear()
    for name, (hexcol, rough, alpha) in PALETTE.items():
        MATS[name] = make_material(name, hexcol, rough, alpha)


def rrect(w, h, r, seg, cx=0.0, cy=0.0):
    """Rounded rectangle outline, counter-clockwise, as (u, v) points."""
    hw, hh = w / 2, h / 2
    r = min(r, hw * 0.999, hh * 0.999)
    pts = []
    for ox, oy, a0 in ((hw - r, hh - r, 0), (-hw + r, hh - r, 90), (-hw + r, -hh + r, 180), (hw - r, -hh + r, 270)):
        for i in range(seg + 1):
            a = math.radians(a0 + 90 * i / seg)
            pts.append((cx + ox + r * math.cos(a), cy + oy + r * math.sin(a)))
    return pts


def circle(n, r, cx=0.0, cy=0.0, a0=0.0):
    return [(cx + r * math.cos(a0 + 2 * math.pi * i / n), cy + r * math.sin(a0 + 2 * math.pi * i / n)) for i in range(n)]


def to3(u, v, w, plane):
    """Map outline coordinates (u, v) and extrusion depth w onto Blender axes."""
    if plane == "XZ":   # front-facing outline, extruded along Y (depth)
        return Vector((u, w, v))
    if plane == "XY":   # top-facing outline, extruded along Z (height)
        return Vector((u, v, w))
    if plane == "YZ":   # side-facing outline, extruded along X
        return Vector((w, u, v))
    raise ValueError(plane)


def prism(outline, w0, w1, plane="XZ", bm=None):
    """Closed prism from an outline extruded between depths w0 and w1."""
    bm = bm or bmesh.new()
    a = [bm.verts.new(to3(u, v, w0, plane)) for u, v in outline]
    b = [bm.verts.new(to3(u, v, w1, plane)) for u, v in outline]
    n = len(outline)
    new = [bm.faces.new(a[::-1]), bm.faces.new(b)]
    for i in range(n):
        j = (i + 1) % n
        new.append(bm.faces.new((a[i], a[j], b[j], b[i])))
    bmesh.ops.recalc_face_normals(bm, faces=new)
    return bm


def ring_prism(outer, inner, w0, w1, plane="XZ", bm=None):
    """Closed annulus prism; outer and inner outlines need the same vertex count."""
    bm = bm or bmesh.new()
    oa = [bm.verts.new(to3(u, v, w0, plane)) for u, v in outer]
    ob = [bm.verts.new(to3(u, v, w1, plane)) for u, v in outer]
    ia = [bm.verts.new(to3(u, v, w0, plane)) for u, v in inner]
    ib = [bm.verts.new(to3(u, v, w1, plane)) for u, v in inner]
    new = []
    n = len(outer)
    for i in range(n):
        j = (i + 1) % n
        new.append(bm.faces.new((oa[i], oa[j], ia[j], ia[i])))
        new.append(bm.faces.new((ob[i], ib[i], ib[j], ob[j])))
        new.append(bm.faces.new((oa[i], ob[i], ob[j], oa[j])))
        new.append(bm.faces.new((ia[i], ia[j], ib[j], ib[i])))
    bmesh.ops.recalc_face_normals(bm, faces=new)
    return bm


def lathe(profile, segments, axis="Z", center=(0.0, 0.0, 0.0), closed=False, bm=None, matrix=None):
    """Spin a (radius, height) profile around an axis. Radius-0 points become poles."""
    bm = bm or bmesh.new()
    cx, cy, cz = center

    def point(r, h, t):
        x, y = r * math.cos(t), r * math.sin(t)
        if axis == "Z":
            p = Vector((cx + x, cy + y, cz + h))
        else:  # spin around Y: the profile height runs along Y (depth)
            p = Vector((cx + x, cy + h, cz + y))
        return matrix @ p if matrix else p

    angles = [2 * math.pi * j / segments for j in range(segments)]
    rings = []
    for r, h in profile:
        if r < 1e-9:
            rings.append([bm.verts.new(point(0, h, 0))])
        else:
            rings.append([bm.verts.new(point(r, h, t)) for t in angles])
    pairs = list(zip(rings, rings[1:]))
    if closed:
        pairs.append((rings[-1], rings[0]))
    new = []
    for A, B in pairs:
        if len(A) == 1 and len(B) == 1:
            continue
        for j in range(segments):
            k = (j + 1) % segments
            if len(A) == 1:
                new.append(bm.faces.new((A[0], B[k], B[j])))
            elif len(B) == 1:
                new.append(bm.faces.new((A[j], A[k], B[0])))
            else:
                new.append(bm.faces.new((A[j], A[k], B[k], B[j])))
    bmesh.ops.recalc_face_normals(bm, faces=new)
    return bm


def fillet(a, p, b, radius, seg):
    """Arc points rounding the corner at p between neighbours a and b (2D Vectors)."""
    d1, d2 = (a - p).normalized(), (b - p).normalized()
    half = d1.angle(d2) / 2
    t = radius / math.tan(half)
    c = p + (d1 + d2).normalized() * (radius / math.sin(half))
    v1, v2 = p + d1 * t - c, p + d2 * t - c
    ang = v1.angle(v2)
    sign = 1 if (v1.x * v2.y - v1.y * v2.x) > 0 else -1
    a0 = math.atan2(v1.y, v1.x)
    return [c + Vector((math.cos(a0 + sign * ang * s / seg), math.sin(a0 + sign * ang * s / seg))) * radius
            for s in range(seg + 1)]


def fillet_path(points, radius, seg):
    """Polyline with every inner corner replaced by an arc of the given radius."""
    pts = [Vector(p) for p in points]
    out = [pts[0]]
    for i in range(1, len(pts) - 1):
        out += fillet(pts[i - 1], pts[i], pts[i + 1], radius, seg)
    out.append(pts[-1])
    return out


def rounded_poly(points, radius, seg):
    """Closed polygon outline with rounded corners, as (u, v) points."""
    pts = [Vector(p) for p in points]
    out = []
    for i in range(len(pts)):
        out += fillet(pts[i - 1], pts[i], pts[(i + 1) % len(pts)], radius, seg)
    return [(q.x, q.y) for q in out]


def tube_xz(path, y, radius, sides, bm=None):
    """Closed round tube along a path in the XZ plane at depth y."""
    bm = bm or bmesh.new()
    rings = []
    for i, p in enumerate(path):
        prev_p = path[max(i - 1, 0)]
        next_p = path[min(i + 1, len(path) - 1)]
        t = (next_p - prev_p).normalized()
        n = Vector((-t.y, t.x))  # in-plane normal (x, z)
        ring = []
        for s in range(sides):
            a = 2 * math.pi * s / sides
            off = n * (radius * math.cos(a))
            ring.append(bm.verts.new((p.x + off.x, y + radius * math.sin(a), p.y + off.y)))
        rings.append(ring)
    new = [bm.faces.new(rings[0][::-1]), bm.faces.new(rings[-1])]
    for A, B in zip(rings, rings[1:]):
        for s in range(sides):
            k = (s + 1) % sides
            new.append(bm.faces.new((A[s], A[k], B[k], B[s])))
    bmesh.ops.recalc_face_normals(bm, faces=new)
    return bm


def new_collection(name):
    coll = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(coll)
    return coll


def add_empty(name, coll, parent=None, location=(0, 0, 0), size=0.02):
    ob = bpy.data.objects.new(name, None)
    ob.empty_display_type = "ARROWS"
    ob.empty_display_size = size
    coll.objects.link(ob)
    ob.parent = parent
    ob.location = location
    return ob


def mesh_obj(name, bm, slots, coll, parent=None):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for s in slots:
        me.materials.append(MATS[s])
    ob = bpy.data.objects.new(name, me)
    coll.objects.link(ob)
    ob.parent = parent
    return ob


def apply_modifiers(ob):
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    old = ob.data
    ob.modifiers.clear()
    ob.data = me
    bpy.data.meshes.remove(old)
    me.name = ob.name


def bevel(ob, width, segments, angle=50.0):
    m = ob.modifiers.new("Bevel", "BEVEL")
    m.width = width
    m.segments = segments
    m.limit_method = "ANGLE"
    m.angle_limit = math.radians(angle)
    m.use_clamp_overlap = True
    m.profile = 0.5
    apply_modifiers(ob)


def cut(ob, cutter_bm, slot):
    """Boolean difference; the new cut faces get the given material slot."""
    cutter = mesh_obj("_cutter", cutter_bm, [slot], ob.users_collection[0])
    m = ob.modifiers.new("Cut", "BOOLEAN")
    m.operation = "DIFFERENCE"
    m.solver = "EXACT"
    m.object = cutter
    m.material_mode = "TRANSFER"
    apply_modifiers(ob)
    bpy.data.objects.remove(cutter)
    # TRANSFER may append a duplicate slot; fold faces back onto the first slot with that material.
    mats = list(ob.data.materials)
    first = {}
    for i, mt in enumerate(mats):
        first.setdefault(mt.name, i)
    remap = [first[mt.name] for mt in mats]
    for p in ob.data.polygons:
        p.material_index = remap[p.material_index]
    while len(ob.data.materials) > len(first):
        ob.data.materials.pop()


def merge_into(target, sources):
    """Join source meshes into target (all built in the same space), keeping material slots by name."""
    names = [m.name for m in target.data.materials]
    bm = bmesh.new()
    bm.from_mesh(target.data)
    for src in sources:
        for m in src.data.materials:
            if m.name not in names:
                target.data.materials.append(m)
                names.append(m.name)
        remap = [names.index(m.name) for m in src.data.materials]
        for p in src.data.polygons:
            p.material_index = remap[p.material_index]
        bm.from_mesh(src.data)
        me = src.data
        bpy.data.objects.remove(src)
        bpy.data.meshes.remove(me)
    bm.to_mesh(target.data)
    bm.free()


def set_pivot(ob, pivot):
    """Move the object origin to pivot (given in the parent's space) without moving the geometry."""
    pivot = Vector(pivot)
    ob.data.transform(Matrix.Translation(ob.location - pivot))
    ob.location = pivot


def select_only(obs):
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for o in obs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = obs[0]


def finalize(ob, sharp_angle=40.0, uv=True):
    """Triangulate n-gons, outward normals, smooth + sharp-by-angle, face-weighted normals, UVs."""
    me = ob.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    ngons = [f for f in bm.faces if len(f.verts) > 4]
    if ngons:
        bmesh.ops.triangulate(bm, faces=ngons, quad_method="BEAUTY", ngon_method="BEAUTY")
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
    me.set_sharp_from_angle(angle=math.radians(sharp_angle))
    me.update()
    if uv:
        select_only([ob])
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.01)
        bpy.ops.object.mode_set(mode="OBJECT")
    m = ob.modifiers.new("WeightedNormals", "WEIGHTED_NORMAL")
    m.mode = "FACE_AREA"
    m.weight = 50
    m.keep_sharp = True
    apply_modifiers(ob)


def planar_uv(ob):
    """Label UV: a clean 0-1 rectangle, u along the width (X), v along the height (Z)."""
    me = ob.data
    xs = [v.co.x for v in me.vertices]
    zs = [v.co.z for v in me.vertices]
    x0, x1, z0, z1 = min(xs), max(xs), min(zs), max(zs)
    while me.uv_layers:
        me.uv_layers.remove(me.uv_layers[0])
    layer = me.uv_layers.new(name="UVMap")
    for loop in me.loops:
        co = me.vertices[loop.vertex_index].co
        layer.data[loop.index].uv = ((co.x - x0) / (x1 - x0), (co.z - z0) / (z1 - z0))


def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons) if ob.type == "MESH" else 0


# ---------------------------------------------------------------------------------------------------------
# Cassette

def build_cassette():
    coll = new_collection("Cassette")
    root = add_empty("Cassette", coll)
    t = 0.0055  # shell half thickness; the trapezoid plates reach +-0.006

    shell = mesh_obj("Shell", prism(rrect(CAS_W, CAS_H, 0.004, 4), -t, t), ["Shell"], coll, root)
    bevel(shell, 0.0012, 2, angle=60)
    cut(shell, prism(rrect(0.062, 0.019, 0.005, 3, cy=WIN_Z), -0.02, 0.0025), "Shell")      # tape window
    for sx in (-1, 1):                                                                        # hub holes
        cut(shell, prism(circle(12, 0.004, sx * HUB_X, WIN_Z), 0.0015, 0.02), "Shell")
    trap = [(-0.034, -0.0305), (0.034, -0.0305), (0.027, -0.019), (-0.027, -0.019)]
    plates = []
    for y0, y1 in ((-0.0060, -0.0053), (0.0053, 0.0060)):
        plate = mesh_obj("_plate", prism(trap, y0, y1), ["Shell"], coll, root)
        bevel(plate, 0.0003, 1, angle=60)
        plates.append(plate)
    merge_into(shell, plates)

    label = mesh_obj("Label", prism(rrect(0.088, 0.0195, 0.002, 3, cy=0.01925), -0.0059, -0.0053),
                     ["Label"], coll, root)
    window = mesh_obj("Window", prism(rrect(0.0632, 0.0202, 0.0056, 3, cy=WIN_Z), -0.0053, -0.0047),
                      ["Window"], coll, root)

    reels = []
    for name, sx in (("ReelLeft", -1), ("ReelRight", 1)):
        cx = sx * HUB_X
        angs, radii = [], []
        for k in range(6):  # six drive teeth on the inside of the hub
            base = 60 * k
            angs += [base - 8, base + 8, base + 30]
            radii += [0.0024, 0.0024, 0.0035]
        outer = [(cx + 0.0055 * math.cos(math.radians(a)), WIN_Z + 0.0055 * math.sin(math.radians(a))) for a in angs]
        inner = [(cx + r * math.cos(math.radians(a)), WIN_Z + r * math.sin(math.radians(a))) for a, r in zip(angs, radii)]
        reels.append(mesh_obj(name, ring_prism(outer, inner, -0.0042, 0.0025), ["Reel"], coll, root))

    bm = bmesh.new()
    for sx, ro in ((-1, 0.0088), (1, 0.0072)):
        ring_prism(circle(18, ro, sx * HUB_X, WIN_Z), circle(18, 0.0045, sx * HUB_X, WIN_Z), -0.0032, 0.0025, bm=bm)
    tape = mesh_obj("Tape", bm, ["Tape"], coll, root)

    for ob in (shell, window, tape, *reels):
        finalize(ob)
    finalize(label, uv=False)
    planar_uv(label)
    for ob, sx in zip(reels, (-1, 1)):
        set_pivot(ob, (sx * HUB_X, 0.0, WIN_Z))
    return root


# ---------------------------------------------------------------------------------------------------------
# Radio

def build_radio():
    coll = new_collection("Radio")
    root = add_empty("Radio", coll)
    half_d = RADIO_D / 2

    body = mesh_obj("Body", prism(rrect(RADIO_W, BODY_Z1 - BODY_Z0, 0.03, 5, cy=(BODY_Z0 + BODY_Z1) / 2), -half_d, half_d),
                    ["RadioBody", "RadioTrim"], coll, root)
    bevel(body, 0.012, 3, angle=50)
    for sx in (-1, 1):
        cut(body, prism(circle(32, 0.050, sx * SPK_X, SPK_Z), -0.08, -0.035), "RadioTrim")
    cut(body, prism(rrect(CAV_W, CAV_Z1 - CAV_Z0, 0.008, 3, cy=(CAV_Z0 + CAV_Z1) / 2), -0.08, -0.028), "RadioTrim")

    extras = []
    ring_profile = [(0.0485, -0.0560), (0.0490, -0.0625), (0.0510, -0.0655), (0.0545, -0.0667),
                    (0.0575, -0.0648), (0.0587, -0.0612), (0.0587, -0.0590)]
    for sx in (-1, 1):  # teal bezel rings round the speakers
        extras.append(mesh_obj("_ring", lathe(ring_profile, 32, axis="Y", center=(sx * SPK_X, 0, SPK_Z), closed=True),
                               ["RadioTrim"], coll, root))
    foot = [(0, 0.0), (0.0115, 0.0), (0.0135, 0.0018), (0.0135, 0.011), (0, 0.011)]
    for fx in (-1, 1):
        for fy in (-1, 1):
            extras.append(mesh_obj("_foot", lathe(foot, 12, center=(fx * 0.165, fy * 0.034, 0)), ["RadioTrim"], coll, root))
    handle_y = 0.024
    path = fillet_path([(-0.155, BODY_Z1 - 0.006), (-0.155, 0.234), (0.155, 0.234), (0.155, BODY_Z1 - 0.006)], 0.018, 6)
    extras.append(mesh_obj("_handle", tube_xz(path, handle_y, 0.0085, 12), ["RadioTrim"], coll, root))
    for sx in (-1, 1):
        mount = mesh_obj("_mount", prism(rrect(0.032, 0.028, 0.008, 3, cx=sx * 0.155, cy=handle_y), BODY_Z1 - 0.0015,
                                          BODY_Z1 + 0.007, plane="XY"), ["RadioTrim"], coll, root)
        bevel(mount, 0.0025, 2)
        extras.append(mount)
    deck = mesh_obj("_deck", prism(rrect(0.142, 0.044, 0.009, 3, cy=BTN_Y), BODY_Z1 - 0.0015, PLATE_TOP, plane="XY"),
                    ["RadioTrim"], coll, root)
    bevel(deck, 0.0015, 2)
    extras.append(deck)
    bezel = mesh_obj("_bezel", prism(rrect(0.070, 0.013, 0.0065, 3, cy=LIGHT_Z), -0.0612, -0.0585), ["RadioTrim"], coll, root)
    bevel(bezel, 0.001, 2)
    extras.append(bezel)
    merge_into(body, extras)

    speakers = []
    cone = [(0.0, -0.0472), (0.008, -0.0463), (0.0135, -0.0445), (0.0162, -0.0422), (0.030, -0.0478),
            (0.0440, -0.0532), (0.0462, -0.0563), (0.0482, -0.0553), (0.0494, -0.0525), (0.0494, -0.0400), (0.0, -0.0400)]
    for name, sx in (("SpeakerLeft", -1), ("SpeakerRight", 1)):
        spk = mesh_obj(name, lathe(cone, 32, axis="Y", center=(sx * SPK_X, 0, SPK_Z)), ["Speaker"], coll, root)
        speakers.append(spk)

    door = mesh_obj("Door", prism(rrect(DOOR_W, DOOR_Z1 - DOOR_Z0, 0.006, 3, cy=(DOOR_Z0 + DOOR_Z1) / 2),
                                  DOOR_FRONT, DOOR_BACK), ["RadioTrim", "Glass"], coll, root)
    bevel(door, 0.0012, 2, angle=60)
    glass_z = (0.0585, 0.1085)
    cut(door, prism(rrect(0.086, glass_z[1] - glass_z[0], 0.004, 3, cy=sum(glass_z) / 2), -0.08, -0.05), "RadioTrim")
    lip = mesh_obj("_lip", prism(rrect(0.040, 0.0035, 0.0015, 2, cy=0.1165), -0.0652, DOOR_FRONT + 0.0005),
                   ["RadioTrim"], coll, root)
    bevel(lip, 0.0007, 1)
    glass = mesh_obj("_glass", prism(rrect(0.0872, glass_z[1] - glass_z[0] + 0.0012, 0.0046, 3, cy=sum(glass_z) / 2),
                                     -0.0600, -0.0585), ["Glass"], coll, root)
    merge_into(door, [lip, glass])

    # Chunky keys shaped like their symbols, so they read without text from any angle: a stop square, a play
    # triangle pointing to the player's right, and an eject triangle pointing away from the player over a bar.
    buttons = []
    keys = {
        "StopButton": (-0.042, [rrect(0.030, 0.030, 0.005, 3)]),
        "PlayButton": (0.0, [rounded_poly([(-0.015, -0.017), (0.019, 0.0), (-0.015, 0.017)], 0.0045, 3)]),
        "EjectButton": (0.044, [rounded_poly([(-0.016, -0.002), (0.016, -0.002), (0.0, 0.015)], 0.0035, 3),
                                rrect(0.032, 0.0075, 0.0025, 2, cy=-0.0095)]),
    }
    for name, (cx, outlines) in keys.items():
        top = PLATE_TOP + BTN_TRAVEL
        parts = []
        for outline in outlines:
            pts = [(cx + u, BTN_Y + v) for u, v in outline]
            part = mesh_obj(name if not parts else "_part", prism(pts, PLATE_TOP - 0.004, top, plane="XY"),
                            ["Button"], coll, root)
            bevel(part, 0.0026, 3)
            parts.append(part)
        merge_into(parts[0], parts[1:])
        buttons.append((parts[0], cx))

    light = mesh_obj("Light", prism(rrect(0.058, 0.0062, 0.0031, 3, cy=LIGHT_Z), -0.0630, -0.0600), ["Light"], coll, root)
    bevel(light, 0.0009, 2)

    for ob in (body, door, light, *speakers, *(b for b, _ in buttons)):
        finalize(ob)
    for spk, sx in zip(speakers, (-1, 1)):
        set_pivot(spk, (sx * SPK_X, -0.0563, SPK_Z))
    set_pivot(door, (0.0, DOOR_FRONT, DOOR_Z0))
    set_pivot(light, (0.0, -0.0615, LIGHT_Z))
    for btn, cx in buttons:
        set_pivot(btn, (cx, BTN_Y, PLATE_TOP))
    add_empty("Slot", coll, root, SLOT, size=0.03)
    return root


# ---------------------------------------------------------------------------------------------------------
# Turntable

def build_turntable():
    coll = new_collection("Turntable")
    root = add_empty("Turntable", coll)
    base_profile = [(0, 0.0), (0.244, 0.0), (0.2495, 0.004), (0.2495, 0.0075), (0.245, 0.0105), (0.122, 0.0105),
                    (0.120, 0.0115), (0, 0.0115)]
    base = mesh_obj("Base", lathe(base_profile, 24), ["TurntableBase"], coll, root)  # mostly hidden
    g0, g1 = TT_GROOVE
    floor, collar = 0.024, TT_TOP
    platter_profile = [
        (0, 0.0390), (0.021, 0.0382), (0.0285, 0.0345), (0.030, floor),            # centre knob to turn it by
        (0.150, floor), (0.1525, floor - 0.0015),                                    # a record-like step
        (0.203, floor - 0.0015), (0.2085, 0.0300), (0.2145, collar), (g0, collar),  # collar with the cassette slot
        (g0, TT_GROOVE_Z), (g1, TT_GROOVE_Z), (g1, collar), (0.2505, collar),
        (0.2577, 0.0300), (TT_R, 0.0240), (TT_R, 0.0115), (0, 0.0115)]             # rounded outer rim, flat bottom
    # segments divisible by 4 keep the bounds exactly +-TT_R; the profile is lean so the rim can be rounder
    platter = mesh_obj("Platter", lathe(platter_profile, 52), ["Turntable"], coll, root)
    finalize(base)
    finalize(platter)
    add_empty("RimHeight", coll, root, (0.0, -TT_RIM_R, TT_GROOVE_Z), size=0.03)
    return root


# ---------------------------------------------------------------------------------------------------------
# Table

def build_table():
    coll = new_collection("Table")
    root = add_empty("Table", coll)
    top = mesh_obj("Top", prism(rrect(TABLE_W, TABLE_D, 0.05, 6), TABLE_H - TABLE_T, TABLE_H, plane="XY"),
                   ["TableTop"], coll, root)
    bevel(top, 0.006, 3, angle=50)

    bm = bmesh.new()
    under = TABLE_H - TABLE_T
    leg_profile = [(0, 0.0), (0.0105, 0.0), (0.0125, 0.003), (0.0195, 0.62), (0.0195, 0.665), (0, 0.665)]
    for sx in (-1, 1):
        for sy in (-1, 1):
            top_pt = Vector((sx * 0.37, sy * 0.205, under))
            foot_pt = Vector((sx * 0.393, sy * 0.228, 0.0))
            d = top_pt - foot_pt
            rot = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_matrix().to_4x4()
            # the leg is a little longer than the gap so its top end hides inside the apron
            lathe(leg_profile, 12, bm=bm, matrix=Matrix.Translation(foot_pt) @ rot @ Matrix.Scale(d.length / 0.64, 4, (0, 0, 1)))
    apron_z = (under - 0.05 + under) / 2
    for sy in (-1, 1):
        prism(rrect(0.74, 0.05, 0.004, 1, cy=apron_z), sy * 0.205 - 0.009, sy * 0.205 + 0.009, plane="XZ", bm=bm)
    for sx in (-1, 1):
        prism(rrect(0.392, 0.05, 0.004, 1, cy=apron_z), sx * 0.37 - 0.009, sx * 0.37 + 0.009, plane="YZ", bm=bm)
    for v in bm.verts:  # the splayed feet tilt a hair below the floor; flatten them onto it
        v.co.z = max(v.co.z, 0.0)
    legs = mesh_obj("Legs", bm, ["TableLegs"], coll, root)
    finalize(top)
    finalize(legs)
    add_empty("TopSurface", coll, root, (0.0, 0.0, TABLE_H), size=0.05)
    return root


# ---------------------------------------------------------------------------------------------------------
# Export

def flip_front(root):
    """Turn a model 180 degrees about its vertical axis, in the data, keeping identity node rotations."""
    rot = Matrix.Rotation(math.pi, 4, "Z")
    for ch in root.children:
        ch.location = (rot @ ch.location.to_4d()).to_3d()
        if ch.type == "MESH":
            ch.data.transform(rot)
            ch.data.update()


def export_fbx(root, path, flip=True):
    obs = [root, *root.children]
    if flip:
        flip_front(root)
    select_only(obs)
    bpy.ops.export_scene.fbx(
        filepath=str(path), use_selection=True, axis_forward="-Z", axis_up="Y", apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True, object_types={"EMPTY", "MESH"},
        use_mesh_modifiers=True, mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False,
        use_custom_props=False, path_mode="AUTO", embed_textures=False)
    if flip:
        flip_front(root)  # back to the modelling orientation for the previews


# ---------------------------------------------------------------------------------------------------------
# Preview renders

BACKDROP = "#ABA7A1"   # neutral warm grey behind the previews
SHELL_COLOURS = ["#E8704F", "#F2C14E", "#5BA3D9", "#9C7BD8", "#6CC59A", "#F28CB1", "#3F5F8C", "#E2483D", "#F59A4A"]


def look_at(ob, target):
    d = Vector(target) - ob.location
    ob.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()


def world_bbox(obs):
    pts = []
    for o in obs:
        if o.type == "MESH":
            pts += [o.matrix_world @ Vector(c) for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def preview_scene(name, res=(1600, 1200)):
    sc = bpy.data.scenes.new(name)
    sc.render.engine = "BLENDER_EEVEE"
    sc.eevee.taa_render_samples = 96
    sc.eevee.use_raytracing = True
    sc.eevee.use_fast_gi = True
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.render.image_settings.file_format = "PNG"
    try:  # keeps base colours close to their hex values, like an untonemapped Quest frame
        sc.view_settings.view_transform = "Khronos PBR Neutral"
    except TypeError:
        sc.view_settings.view_transform = "AgX"
    world = bpy.data.worlds.new(name)
    if not world.node_tree:
        world.use_nodes = True
    nt = world.node_tree
    out = next(n for n in nt.nodes if n.type == "OUTPUT_WORLD")
    light_bg = next(n for n in nt.nodes if n.type == "BACKGROUND")     # soft ambient fill
    light_bg.inputs["Color"].default_value = srgb("#C9C6C1")
    light_bg.inputs["Strength"].default_value = 0.35
    seen_bg = nt.nodes.new("ShaderNodeBackground")                       # what the camera sees behind
    seen_bg.inputs["Color"].default_value = srgb(BACKDROP)
    seen_bg.inputs["Strength"].default_value = 1.0
    mix = nt.nodes.new("ShaderNodeMixShader")
    path = nt.nodes.new("ShaderNodeLightPath")
    nt.links.new(path.outputs["Is Camera Ray"], mix.inputs[0])
    nt.links.new(light_bg.outputs[0], mix.inputs[1])
    nt.links.new(seen_bg.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs["Surface"])
    sc.world = world
    return sc


def add_floor(sc, z, size=30.0):
    mat = bpy.data.materials.get("_Floor")
    if not mat:  # a faint glow keeps the far floor from going dark behind the lit area
        mat = make_material("_Floor", BACKDROP, 0.9, emit=0.18)
    bm = prism(rrect(size, size, 0.1, 1), z - 0.01, z, plane="XY")
    me = bpy.data.meshes.new("_floor")
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat)
    ob = bpy.data.objects.new("_floor", me)
    sc.collection.objects.link(ob)


def add_area(sc, name, loc, target, irradiance, size, color=(1, 1, 1)):
    ld = bpy.data.lights.new(name, "AREA")
    dist = (Vector(loc) - Vector(target)).length
    ld.energy = irradiance * math.pi * dist * dist
    ld.size = size
    ld.color = color
    if hasattr(ld, "use_shadow_jitter"):
        ld.use_shadow_jitter = True
    ob = bpy.data.objects.new(name, ld)
    sc.collection.objects.link(ob)
    ob.location = loc
    look_at(ob, target)


def light_rig(sc, target, cam_dir, scale):
    """Soft three-point light around the camera direction (unit vector from target to camera)."""
    t = Vector(target)
    side = (-cam_dir).cross(Vector((0, 0, 1))).normalized()   # points to the camera's right
    up = Vector((0, 0, 1))
    add_area(sc, "Key", t + (cam_dir * 2.2 - side * 2.0 + up * 2.4) * scale, t, 3.6, 2.0 * scale, (1.0, 0.96, 0.9))
    add_area(sc, "Fill", t + (cam_dir * 2.5 + side * 2.6 + up * 0.6) * scale, t, 1.4, 3.0 * scale, (0.92, 0.96, 1.0))
    add_area(sc, "Rim", t + (-cam_dir * 2.6 + side * 0.8 + up * 2.2) * scale, t, 3.0, 1.5 * scale, (1.0, 0.98, 0.95))


def fit_distance(points, target, cam_dir, lens, aspect, margin=0.88):
    """Camera distance along cam_dir from target at which all points fit in the frame."""
    fwd = -cam_dir
    right = fwd.cross(Vector((0, 0, 1))).normalized()
    up = right.cross(fwd)
    tan_h = 18.0 / lens * margin
    tan_v = tan_h / aspect
    d = 0.0
    for p in points:
        v = Vector(p) - target
        along = v.dot(cam_dir)
        d = max(d, along + abs(v.dot(right)) / tan_h, along + abs(v.dot(up)) / tan_v)
    return d


def bbox_corners(lo, hi):
    return [Vector((x, y, z)) for x in (lo.x, hi.x) for y in (lo.y, hi.y) for z in (lo.z, hi.z)]


def add_camera(sc, loc, target, lens):
    cd = bpy.data.cameras.new("Camera")
    cd.lens = lens
    cd.clip_start = 0.005
    ob = bpy.data.objects.new("Camera", cd)
    sc.collection.objects.link(ob)
    ob.location = loc
    look_at(ob, target)
    sc.camera = ob


def render(sc, path):
    sc.render.filepath = str(path)
    bpy.ops.render.render(write_still=True, scene=sc.name)
    print(f"Rendered {path}")


PREVIEW_VIEW = {"Cassette": (32, 18), "Radio": (35, 22), "Turntable": (30, 38), "Table": (35, 24)}  # azimuth, elevation


def render_asset(root, out_png):
    res = (1600, 1200)
    sc = preview_scene(f"Preview_{root.name}", res)
    sc.collection.children.link(root.users_collection[0])
    lo, hi = world_bbox(root.children)
    c = (lo + hi) / 2
    az, el = (math.radians(a) for a in PREVIEW_VIEW[root.name])
    cam_dir = Vector((math.sin(az) * math.cos(el), -math.cos(az) * math.cos(el), math.sin(el)))
    lens = 60
    dist = fit_distance(bbox_corners(lo, hi), c, cam_dir, lens, res[0] / res[1], margin=0.80)
    add_camera(sc, c + cam_dir * dist, c, lens)
    light_rig(sc, c, cam_dir, dist / 2.5)
    add_floor(sc, lo.z)
    render(sc, out_png)


def copy_asset(root, coll, parent=None, matrix=Matrix()):
    new_root = root.copy()
    coll.objects.link(new_root)
    new_root.parent = parent
    new_root.matrix_parent_inverse = Matrix()
    new_root.matrix_basis = matrix
    parts = {}
    for ch in root.children:
        c = ch.copy()
        coll.objects.link(c)
        c.parent = new_root
        c.matrix_parent_inverse = Matrix()
        parts[ch.name] = c
    return new_root, parts


def tint_shell(parts, hexcol):
    mat = make_material(f"_Shell{hexcol}", hexcol, PALETTE["Shell"][1])
    shell = parts["Shell"]
    shell.material_slots[0].link = "OBJECT"
    shell.material_slots[0].material = mat


def render_scene(roots, out_png):
    sc = preview_scene("Preview_Scene", res=(1920, 1200))
    coll = bpy.data.collections.new("SceneCopies")
    sc.collection.children.link(coll)
    copy_asset(roots["Table"], coll)
    top = Matrix.Translation((0, 0, TABLE_H))
    tt_root, tt = copy_asset(roots["Turntable"], coll, matrix=top @ Matrix.Translation((-0.19, -0.02, 0)))
    tt["Platter"].rotation_euler.z = math.radians(-8)    # just turning a cassette to the front
    n = 8
    for i in range(n):
        theta = math.radians(-90 + 360 * i / n)
        pos = Matrix.Translation((TT_RIM_R * math.cos(theta), TT_RIM_R * math.sin(theta), TT_GROOVE_Z + CAS_H / 2))
        _, parts = copy_asset(roots["Cassette"], coll, parent=tt["Platter"],
                              matrix=pos @ Matrix.Rotation(theta + math.pi / 2, 4, "Z"))
        tint_shell(parts, SHELL_COLOURS[i])
    radio_root, radio = copy_asset(roots["Radio"], coll,
                                   matrix=top @ Matrix.Translation((0.235, 0.19, 0)) @ Matrix.Rotation(math.radians(-6), 4, "Z"))
    door = radio["Door"]
    door.rotation_euler.x = math.radians(30)          # top edge tilted out toward the player
    lift = 0.040                                      # cassette raised along the open door, half inside
    local = Vector(SLOT) - door.location + Vector((0, 0, lift))
    _, parts = copy_asset(roots["Cassette"], coll, parent=door, matrix=Matrix.Translation(local))
    tint_shell(parts, SHELL_COLOURS[8])

    # a seated player's three-quarter view, framed on the table top and everything on it
    target = Vector((0.0, 0.0, TABLE_H + 0.07))
    cam_dir = Vector((0.27, -1.0, 0.50)).normalized()
    lens = 40
    pts = bbox_corners(Vector((-TABLE_W / 2, -TABLE_D / 2, TABLE_H - 0.05)), Vector((TABLE_W / 2, TABLE_D / 2, TABLE_H + 0.25)))
    cam_loc = target + cam_dir * fit_distance(pts, target, cam_dir, lens, 1920 / 1200, margin=0.92)
    add_camera(sc, cam_loc, target, lens)
    light_rig(sc, target, cam_dir, 0.75)
    add_floor(sc, 0.0)
    render(sc, out_png)


# ---------------------------------------------------------------------------------------------------------
# Re-import check

def fbx_nodes(path):
    """Raw transforms and axis settings straight from the FBX file (what Unity reads)."""
    from io_scene_fbx import parse_fbx
    root, _ = parse_fbx.parse(str(path))

    def find(elem, key):
        return next((e for e in elem.elems if e.id == key), None)

    def props70(elem):
        out = {}
        p70 = find(elem, b"Properties70")
        for p in (p70.elems if p70 else []):
            out[p.props[0].decode()] = p.props[4:]
        return out

    gs = props70(find(root, b"GlobalSettings"))
    axes = {k: gs[k][0] for k in ("UpAxis", "UpAxisSign", "FrontAxis", "FrontAxisSign", "CoordAxis",
                                   "CoordAxisSign", "UnitScaleFactor") if k in gs}
    nodes = {}
    for m in find(root, b"Objects").elems:
        if m.id != b"Model":
            continue
        name = m.props[1].split(b"\x00")[0].decode()
        p = props70(m)
        nodes[name] = {k: tuple(round(float(x), 5) for x in p.get(k, ())) for k in
                       ("Lcl Translation", "Lcl Rotation", "Lcl Scaling", "PreRotation") if k in p}
    return axes, nodes


def shell_report(me):
    """Count connected shells, and those that are inside out (negative volume) or not closed."""
    bm = bmesh.new()
    bm.from_mesh(me)
    seen, count, inward, open_ = set(), 0, 0, 0
    for f in bm.faces:
        if f in seen:
            continue
        seen.add(f)
        stack, island = [f], []
        while stack:
            g = stack.pop()
            island.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h not in seen:
                        seen.add(h)
                        stack.append(h)
        count += 1
        vol = 0.0
        for g in island:
            vs = [v.co for v in g.verts]
            for i in range(1, len(vs) - 1):
                vol += vs[0].dot(vs[i].cross(vs[i + 1]))
        if any(len(e.link_faces) != 2 for g in island for e in g.edges):
            open_ += 1
        elif vol < 0:
            inward += 1
    bm.free()
    return count, inward, open_


def check(out_dir):
    ok = True
    print("\n=== Re-import check (Blender Z-up; Unity = (-x, z, -y) of these) ===")
    for name in ASSETS:
        path = out_dir / f"{name}.fbx"
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=str(path), bake_space_transform=True)
        obs = sorted(bpy.context.scene.objects, key=lambda o: (o.parent is not None, o.name))
        axes, raw = fbx_nodes(path)
        total = sum(tris(o) for o in obs)
        lo, hi = world_bbox(obs)
        print(f"\n{name}.fbx  axes {axes}")
        print(f"  total {total} tris (budget {BUDGET[name]}), size x {hi.x - lo.x:.4f} y {hi.y - lo.y:.4f} z {hi.z - lo.z:.4f}, "
              f"bounds {tuple(round(v, 4) for v in lo)} .. {tuple(round(v, 4) for v in hi)}")
        if total > BUDGET[name]:
            ok = False
            print("  OVER BUDGET")
        for o in obs:
            p = o.matrix_world.translation
            rot = tuple(round(math.degrees(a), 2) for a in o.matrix_world.to_euler())
            scl = tuple(round(s, 4) for s in o.matrix_world.to_scale())
            line = (f"  {o.name:<12} parent={o.parent.name if o.parent else '-':<10} pivot=({p.x:+.4f}, {p.y:+.4f}, {p.z:+.4f})"
                    f" unity=({-p.x:+.4f}, {p.z:+.4f}, {-p.y:+.4f}) rot={rot} scale={scl}")
            if o.type == "MESH":
                blo, bhi = world_bbox([o])
                mats = [m.name for m in o.data.materials]
                line += (f"\n               tris={tris(o)} slots={mats} bounds=({blo.x:+.4f}, {blo.y:+.4f}, {blo.z:+.4f})"
                         f"..({bhi.x:+.4f}, {bhi.y:+.4f}, {bhi.z:+.4f})")
                if o.data.uv_layers:
                    uv = o.data.uv_layers[0].data
                    us = [d.uv.x for d in uv]
                    vs = [d.uv.y for d in uv]
                    line += f" uv=[{min(us):.3f}..{max(us):.3f}]x[{min(vs):.3f}..{max(vs):.3f}]"
                else:
                    line += " NO UV"
                    ok = False
                count, inward, open_ = shell_report(o.data)
                line += f" shells={count}"
                if inward or open_:
                    line += f" INWARD={inward} OPEN={open_}"
                    ok = False
            r = raw.get(o.name, {})
            line += f"\n               fbx {r}"
            print(line)
    print("\nCHECK", "OK" if ok else "FAILED")
    return ok


# ---------------------------------------------------------------------------------------------------------

def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser(prog="cassette_deck.py", description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--out", type=pathlib.Path, default=DEFAULT_OUT, help="FBX output folder")
    ap.add_argument("--render", type=pathlib.Path, help="folder for deck-*.png previews")
    ap.add_argument("--blend", type=pathlib.Path, help="also save the built scene as this .blend")
    ap.add_argument("--no-check", action="store_true", help="skip the re-import check")
    ap.add_argument("--no-front-flip", action="store_true", help="export with the front on Blender -Y as modelled")
    ap.add_argument("--only", nargs="*", help="render only these previews (asset names or 'scene')")
    args = ap.parse_args(argv)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    make_materials()
    roots = {"Cassette": build_cassette(), "Radio": build_radio(), "Turntable": build_turntable(), "Table": build_table()}

    args.out.mkdir(parents=True, exist_ok=True)
    for name, root in roots.items():
        path = args.out / f"{name}.fbx"
        export_fbx(root, path, flip=not args.no_front_flip)
        print(f"Exported {path} ({sum(tris(o) for o in root.children)} tris)")

    if args.blend:
        bpy.ops.wm.save_as_mainfile(filepath=str(args.blend.resolve()))
    if args.render:
        args.render.mkdir(parents=True, exist_ok=True)
        want = set(n.lower() for n in (args.only or [*ASSETS, "scene"]))
        for name, root in roots.items():
            if name.lower() in want:
                render_asset(root, args.render.resolve() / f"deck-{name.lower()}.png")
        if "scene" in want:
            render_scene(roots, args.render.resolve() / "deck-scene.png")
    if not args.no_check:
        check(args.out)


if __name__ == "__main__":
    main()
