import bpy, sys, json
from mathutils import Vector
for path in sys.argv[sys.argv.index('--') + 1:]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    arms = [o for o in bpy.data.objects if o.type == 'ARMATURE']
    print('SOURCE', path)
    for arm in arms:
        print('RIG', arm.name, 'scale', tuple(arm.scale))
        print('BONES', [b.name for b in arm.data.bones])
        for b in arm.data.bones:
            if any(s in b.name for s in ('hips', 'spine', 'thigh', 'shin', 'foot', 'head', 'upper_arm', 'forearm', 'hand', 'middle', 'thumb')):
                print('BONE', b.name, tuple(round(x, 4) for x in arm.matrix_world @ b.head_local), tuple(round(x, 4) for x in arm.matrix_world @ b.tail_local))
    for obj in bpy.data.objects:
        if obj.type == 'MESH':
            pts = [obj.matrix_world @ Vector(v) for v in obj.bound_box]
            print('MESH', obj.name, len(obj.data.vertices), [round(min(p[i] for p in pts),4) for i in range(3)], [round(max(p[i] for p in pts),4) for i in range(3)])
    for action in bpy.data.actions:
        print('ACTION', action.name, tuple(action.frame_range))
