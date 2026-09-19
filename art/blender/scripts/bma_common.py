"""Shared helpers for the Be My Arms Blender -> Unity production pipeline.

Coordinate convention (documented in docs/M6_ART_PIPELINE.md):
  * Blender is Z-up; the character faces -Y; 1 Blender unit = 1 metre.
  * FBX export uses axis_forward='-Z', axis_up='Y', scale 1.0, so Unity receives
    Y-up assets facing +Z with no extra correction.

The RIG table below is the single source of truth for the standardized anchor
positions and is mirrored by Assets/Scripts/M6/Data/M6RigLayout.cs. Skin meshes are
authored in this space so any skin mounts on the shared body without pair-specific work.
"""

import os
import math
import bpy
import bmesh
from mathutils import Vector, Matrix

MASTER_VERSION = 1

# Unity-space (Y up, +Z forward), metres.
RIG = {
    "Hips": (0.00, 0.95, 0.00),
    "Chest": (0.00, 1.35, 0.00),
    "Neck": (0.00, 1.55, 0.00),
    "Head": (0.00, 1.68, 0.00),
    "ShoulderAnchor": (0.00, 1.48, 0.06),
    "WeaponAnchor": (0.00, 1.35, 0.35),
    "UtilityAnchor": (0.00, 1.15, 0.22),
    "P2CameraAnchor": (0.00, 1.50, 0.16),
    "P1CameraAnchor": (0.00, 1.68, 0.06),
}
P2_MOUNT_UNITY = RIG["ShoulderAnchor"]
CHARACTER_HEIGHT = 1.85


def u2b(p):
    """Unity (x, y, z) -> Blender (x, -z, y)."""
    x, y, z = p
    return Vector((x, -z, y))


def rel_to_p2_mount(unity_point):
    """A Unity-space point expressed relative to the P2 mount, in Blender space."""
    ox, oy, oz = P2_MOUNT_UNITY
    return u2b((unity_point[0] - ox, unity_point[1] - oy, unity_point[2] - oz))


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1.0
    scene.unit_settings.length_unit = 'METERS'


def material(name, color, metallic=0.0, roughness=0.6):
    existing = bpy.data.materials.get(name)
    if existing:
        return existing
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    if bsdf:
        bsdf.inputs['Base Color'].default_value = (color[0], color[1], color[2], 1.0)
        bsdf.inputs['Metallic'].default_value = metallic
        bsdf.inputs['Roughness'].default_value = roughness
    return mat


def _bevel(bm, offset, segments):
    if offset <= 0.0:
        return
    try:
        bmesh.ops.bevel(
            bm,
            geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
            offset=offset,
            offset_type='OFFSET',
            segments=segments,
            profile=0.5,
            affect='EDGES',
            clamp_overlap=True,
        )
    except TypeError:
        # Older/newer signature fallback.
        bmesh.ops.bevel(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces), offset=offset, segments=segments)


def _finish(name, bm, mat, smooth=False):
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    ob = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(ob)
    if mat:
        mesh.materials.append(mat)
    if smooth:
        for poly in mesh.polygons:
            poly.use_smooth = True
    return ob


def box(name, center, size, mat=None, bevel=0.0, seg=2, taper=None, shear_y=0.0):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    for v in bm.verts:
        if taper is not None and v.co.z > 0:
            v.co.x *= taper
            v.co.y *= taper
        if shear_y and v.co.z > 0:
            v.co.y += shear_y
    _bevel(bm, bevel, seg)
    bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
    return _finish(name, bm, mat)


def box_dir(name, center, size, direction, mat=None, bevel=0.0, seg=2):
    """A box whose local +Z is aligned to `direction` (for hands/fists that follow a forearm)."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    _bevel(bm, bevel, seg)
    d = Vector(direction)
    if d.length > 1e-6:
        quat = Vector((0.0, 0.0, 1.0)).rotation_difference(d.normalized())
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=quat.to_matrix())
    bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
    return _finish(name, bm, mat)


def cylinder(name, center, radius, depth, axis='Z', mat=None, seg=16, bevel=0.0, bseg=2, radius2=None):
    bm = bmesh.new()
    r2 = radius if radius2 is None else radius2
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=radius, radius2=r2, depth=depth)
    _bevel(bm, bevel, bseg)
    if axis == 'X':
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, 'Y'))
    elif axis == 'Y':
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, 'X'))
    bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
    return _finish(name, bm, mat)


def sphere(name, center, radius, mat=None, scale=(1, 1, 1), subd=1):
    bm = bmesh.new()
    try:
        bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=12, radius=radius)
    except TypeError:
        bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=12, diameter=radius * 2.0)
    bmesh.ops.scale(bm, vec=Vector(scale), verts=bm.verts)
    if subd > 0:
        try:
            bmesh.ops.subdivide_edges(bm, edges=list(bm.edges), cuts=subd, use_grid_fill=True)
        except Exception:
            pass
    bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
    return _finish(name, bm, mat, smooth=True)


def limb(name, a, b, radius_start, radius_end=None, mat=None, seg=12, bevel=0.0, bseg=1):
    """A tapered capsule/cylinder running from point a to point b (for anatomical arms/legs)."""
    a = Vector(a)
    b = Vector(b)
    delta = b - a
    depth = delta.length
    if depth < 1e-6:
        depth = 1e-6
    r2 = radius_start if radius_end is None else radius_end
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=radius_start, radius2=r2, depth=depth)
    _bevel(bm, bevel, bseg)
    if delta.length > 1e-6:
        quat = Vector((0.0, 0.0, 1.0)).rotation_difference(delta.normalized())
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=quat.to_matrix())
    mid = (a + b) * 0.5
    bmesh.ops.translate(bm, vec=mid, verts=bm.verts)
    return _finish(name, bm, mat, smooth=True)


def joint_sleeve(name, center, direction, length, radius, mat=None, seg=14, bevel=0.0):
    """An articulated armored joint: a short sleeve along `direction`, never a bare ball."""
    d = Vector(direction)
    if d.length > 1e-6:
        d = d.normalized()
    half = d * (length * 0.5)
    return limb(name, Vector(center) - half, Vector(center) + half, radius, radius * 0.92, mat, seg=seg, bevel=bevel)


def uv_unwrap(ob):
    try:
        bpy.context.view_layer.objects.active = ob
        for other in bpy.context.selected_objects:
            other.select_set(False)
        ob.select_set(True)
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.02)
        bpy.ops.object.mode_set(mode='OBJECT')
    except Exception as exc:  # UVs are not required for solid-colour placeholder materials
        print("[bma] uv_unwrap skipped for %s: %s" % (ob.name, exc))
        try:
            bpy.ops.object.mode_set(mode='OBJECT')
        except Exception:
            pass


def unwrap_all():
    for ob in list(bpy.context.scene.objects):
        if ob.type == 'MESH':
            uv_unwrap(ob)


def joint(name, location, parent=None, size=0.08):
    """An empty joint used as a rotation pivot for the procedural animation layer."""
    ob = bpy.data.objects.new(name, None)
    ob.empty_display_type = 'PLAIN_AXES'
    ob.empty_display_size = size
    bpy.context.collection.objects.link(ob)
    ob.location = Vector(location)
    if parent is not None:
        bpy.context.view_layer.update()
        ob.parent = parent
        ob.matrix_parent_inverse = parent.matrix_world.inverted()
    return ob


def attach(ob, parent):
    """Parent a mesh to a joint while keeping its world transform, so it rotates about the joint."""
    bpy.context.view_layer.update()
    ob.parent = parent
    ob.matrix_parent_inverse = parent.matrix_world.inverted()
    return ob


def export_fbx(path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=False,
        axis_forward='-Z',
        axis_up='Y',
        global_scale=1.0,
        apply_scale_options='FBX_SCALE_NONE',
        object_types={'MESH', 'EMPTY'},
        use_mesh_modifiers=True,
        mesh_smooth_type='FACE',
        use_tspace=True,
        path_mode='COPY',
    )
    print("[bma] EXPORTED " + path)


def save_blend(path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=path)
    print("[bma] SAVED_BLEND " + path)


def report_bounds():
    """Prints the combined world-space bounds so the pipeline can verify scale."""
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    for ob in bpy.context.scene.objects:
        if ob.type != 'MESH':
            continue
        for corner in ob.bound_box:
            w = ob.matrix_world @ Vector(corner)
            lo.x, lo.y, lo.z = min(lo.x, w.x), min(lo.y, w.y), min(lo.z, w.z)
            hi.x, hi.y, hi.z = max(hi.x, w.x), max(hi.y, w.y), max(hi.z, w.z)
    print("[bma] BOUNDS_BLENDER min=(%.3f,%.3f,%.3f) max=(%.3f,%.3f,%.3f) height=%.3f" %
          (lo.x, lo.y, lo.z, hi.x, hi.y, hi.z, hi.z - lo.z))
