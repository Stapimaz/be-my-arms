"""
Build the Be My Arms P1/P2 presentation bodies from the Quaternius Universal Animation Library
(CC0). The source library ships one skinned "Mannequin" on a 53-bone humanoid rig plus 46 clips.

The locked Be My Arms anatomy is:
  P1 = head + main torso + pelvis + legs (no visible arms)
  P2 = upper chest / clavicle / shoulders + two complete arms (no humanoid head)
  P2 viewmodel = shoulders + arms only (for first person)

We derive all three by deleting vertices whose dominant skin weight belongs to the bones that
must not render, then capping the resulting boundary loops. The skeleton, skin weights, materials
and animation clips are otherwise preserved, so all three share one compatible rig.

Usage (Blender 4.5 headless):
  blender -b --factory-startup --python tools/pipeline/build-quaternius-bodies.py -- \
      <source.fbx> <out-dir> [--merge-shoulders-for-p1]
"""

import bpy
import bmesh
import os
import sys

# Clips kept in the derived exports (name fragments). Everything else is discarded so the
# exported files stay small; the animation set covers the vertical slice's needs.
KEEP_CLIPS = (
    "A_TPose",
    "Idle_Loop",
    "Walk_Loop",
    "Jog_Fwd_Loop",
    "Sprint_Loop",
    "Jump_Start",
    "Jump_Loop",
    "Jump_Land",
    "Death01",
    "Hit_Chest",
    "Hit_Head",
    "Pistol_Aim_Neutral",
    "Pistol_Shoot",
    "Pistol_Reload",
    "Pistol_Idle_Loop",
)

ARM_BONES = ("DEF-upper_arm.", "DEF-forearm.", "DEF-hand.", "DEF-f_", "DEF-thumb.")
P2_REMOVE = (
    "DEF-head",
    "DEF-neck",
    "DEF-hips",
    "DEF-spine.001",
    "DEF-spine.002",
    "DEF-thigh.",
    "DEF-shin.",
    "DEF-foot.",
    "DEF-toe.",
)
ARMS_REMOVE = (
    "DEF-head",
    "DEF-neck",
    "DEF-hips",
    "DEF-spine.",
    "DEF-thigh.",
    "DEF-shin.",
    "DEF-foot.",
    "DEF-toe.",
)


def log(msg):
    print("[quaternius] " + str(msg))
    sys.stdout.flush()


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def import_fbx(path):
    bpy.ops.import_scene.fbx(filepath=path)


def find_armature_and_mesh():
    arm = next((o for o in bpy.data.objects if o.type == "ARMATURE"), None)
    mesh = next((o for o in bpy.data.objects if o.type == "MESH"), None)
    if arm is None or mesh is None:
        raise RuntimeError("expected an armature and a mesh in the imported FBX")
    return arm, mesh


def dominant_group(mesh_obj, v):
    groups = mesh_obj.vertex_groups
    best = None
    best_w = 0.0
    for g in v.groups:
        if g.weight > best_w:
            best_w = g.weight
            best = groups[g.group].name
    return best


def remove_vertices(mesh_obj, name_fragments):
    bm = bmesh.new()
    bm.from_mesh(mesh_obj.data)
    layer = bm.verts.layers.deform.active
    group_index = {vg.index: vg.name for vg in mesh_obj.vertex_groups}
    do_remove = []
    if layer is not None:
        for v in bm.verts:
            weights = v[layer]
            best_w = -1.0
            best = None
            for gi, w in weights.items():
                if w > best_w:
                    best_w = w
                    best = group_index.get(gi)
            if best and any(frag in best for frag in name_fragments):
                do_remove.append(v)
    if do_remove:
        bmesh.ops.delete(bm, geom=do_remove, context="VERTS")
        boundary = [e for e in bm.edges if e.is_boundary]
        if boundary:
            bmesh.ops.holes_fill(bm, edges=boundary)
            bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 3])
    bm.to_mesh(mesh_obj.data)
    bm.free()
    mesh_obj.data.update()


def trim_actions():
    kept = 0
    removed = 0
    for action in list(bpy.data.actions):
        name = action.name.split("|")[-1]
        if name in KEEP_CLIPS:
            action.name = name
            action.use_fake_user = True
            kept += 1
        else:
            bpy.data.actions.remove(action)
            removed += 1
    log(f"actions kept={kept} removed={removed}")


def export_fbx(path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    for obj in bpy.data.objects:
        obj.select_set(True)
    if bpy.context.view_layer.objects.active is None and bpy.data.objects:
        bpy.context.view_layer.objects.active = bpy.data.objects[0]
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=False,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        global_scale=1.0,
        axis_forward="-Z",
        axis_up="Y",
        bake_anim=True,
        bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False,
        add_leaf_bones=False,
        armature_nodetype="NULL",
        mesh_smooth_type="FACE",
        use_mesh_modifiers=True,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
    )
    log(f"wrote {path} ({os.path.getsize(path)} bytes)")


def build_variant(src, out_dir, filename, remove_fragments):
    reset()
    import_fbx(src)
    arm, mesh = find_armature_and_mesh()
    arm.name = "Rig"
    mesh.name = "Body"
    remove_vertices(mesh, remove_fragments)
    trim_actions()
    export_fbx(os.path.join(out_dir, filename))


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    if len(argv) < 2:
        raise SystemExit("usage: build-quaternius-bodies.py <source.fbx> <out-dir>")
    src, out_dir = argv[0], argv[1]
    log(f"source={src}")
    log(f"out={out_dir}")

    build_variant(src, out_dir, "Q_P1_Body.fbx", ARM_BONES)
    build_variant(src, out_dir, "Q_P2_Body.fbx", P2_REMOVE)
    build_variant(src, out_dir, "Q_P2_Arms.fbx", ARMS_REMOVE)
    log("done")


if __name__ == "__main__":
    main()
