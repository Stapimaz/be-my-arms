"""Adapt the CC0 Quaternius base character; retain its actual topology, weights and rig.
No primitive-based character geometry or generated locomotion. Unity performs Humanoid retargeting.
Usage: blender -b --python ... -- <base.fbx> <hair.fbx> <output-directory>
"""
import bpy, bmesh, os, sys
from mathutils import Matrix, Vector

base, hair, out = sys.argv[sys.argv.index('--') + 1:]
os.makedirs(out, exist_ok=True)

def material(name, color):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1)
    return m

def export(name):
    bpy.ops.export_scene.fbx(filepath=os.path.join(out,name+'.fbx'), axis_forward='-Z',axis_up='Y',
        add_leaf_bones=False,bake_anim=False,apply_scale_options='FBX_SCALE_ALL',mesh_smooth_type='OFF')
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out,name+'.blend'))

def dominant(obj,v):
    return obj.vertex_groups[max(v.groups,key=lambda g:g.weight).group].name if v.groups else ''

def trim(obj, keep):
    bm=bmesh.new(); bm.from_mesh(obj.data); bm.verts.ensure_lookup_table()
    remove=[bm.verts[v.index] for v in obj.data.vertices if not keep(v)]
    bmesh.ops.delete(bm,geom=remove,context='VERTS'); bm.to_mesh(obj.data); bm.free()

for fps in (False,True):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=base)
    arm=next(o for o in bpy.data.objects if o.type=='ARMATURE'); arm.name='Rig'
    body=max((o for o in bpy.data.objects if o.type=='MESH'),key=lambda o:len(o.data.vertices))
    p1=material('Fighter_P1Suit',(.19,.28,.39))
    p2=material('Fighter_P2Suit',(.62,.17,.10))
    body.data.materials.clear()
    body.data.materials.append(p2 if fps else p1)
    arms=('clavicle','upperarm','lowerarm','hand','index','middle','ring','pinky','thumb')
    for p in body.data.polygons:
        p.material_index=0
        p.use_smooth=True
    if fps:
        for o in list(bpy.data.objects):
            if o.type=='MESH' and o!=body: bpy.data.objects.remove(o,do_unlink=True)
        trim(body,lambda v: dominant(body,v).startswith(arms))
        left=body.copy();left.data=body.data.copy();bpy.context.collection.objects.link(left)
        for obj,sign in ((body,1),(left,-1)):
            bm=bmesh.new();bm.from_mesh(obj.data)
            bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.00001,
                plane_co=Vector((sign*.30,0,0)),plane_no=Vector((sign,0,0)),clear_inner=True)
            boundary=[e for e in bm.edges if e.is_boundary and all(abs(v.co.x-sign*.30)<.0001 for v in e.verts)]
            bmesh.ops.holes_fill(bm,edges=boundary)
            bmesh.ops.triangulate(bm,faces=[f for f in bm.faces if len(f.verts)>3])
            bm.to_mesh(obj.data);bm.free();obj.name='SmoothForearmsHands_'+('R' if sign>0 else 'L')
    else:
        # Partition polygons, not independent cut/capped bodies: the two surfaces share exactly
        # the source seam, bindpose and weighted skeleton, even when the torso bends or crouches.
        other=body.copy(); other.data=body.data.copy(); bpy.context.collection.objects.link(other)
        body.name='P1_Surface'; other.name='P2_Surface'
        other.data.materials.clear();other.data.materials.append(p2)
        for obj,want in ((body,False),(other,True)):
            bm=bmesh.new(); bm.from_mesh(obj.data); bm.faces.ensure_lookup_table()
            remove=[]
            for poly in obj.data.polygons:
                is_arm=any(dominant(obj,obj.data.vertices[i]).startswith(arms) for i in poly.vertices)
                if is_arm!=want: remove.append(bm.faces[poly.index])
            bmesh.ops.delete(bm,geom=remove,context='FACES')
            bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS')
            bm.to_mesh(obj.data); bm.free()
        for obj in bpy.data.objects:
            if obj.type=='MESH' and obj not in (body,other):
                obj.data.materials.clear();obj.data.materials.append(p1)
                for poly in obj.data.polygons: poly.material_index=0;poly.use_smooth=True
        # The exposed ready-made head keeps its face readable. The separately exported hairstyle
        # did not match this base's bind orientation and is omitted from the active fighter.
    arm.animation_data_clear()
    export('Fighter_Arms' if fps else 'Fighter_Shared')
