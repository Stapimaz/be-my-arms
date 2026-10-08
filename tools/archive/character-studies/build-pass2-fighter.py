"""Pass 2 source authoring. Blender 4.5; CC0 Quaternius inputs, project-authored suit/gaits/rifle.
Usage: blender -b --python tools/pipeline/build-pass2-fighter.py -- <UAL1.fbx> <base.fbx> <out>
All clips are baked to the native Generic skeleton; no runtime retargeting or root motion.
"""
import bpy, bmesh, sys, os, math
from mathutils import Vector, Matrix, Quaternion
sys.path.insert(0, os.path.dirname(__file__))
import importlib.util
spec = importlib.util.spec_from_file_location('q', os.path.join(os.path.dirname(__file__), 'build-quaternius-bodies.py'))
q = importlib.util.module_from_spec(spec)
spec.loader.exec_module(q)
args=sys.argv[sys.argv.index('--') + 1:]
src, base, out = args[:3]
ual2=args[3] if len(args)>3 else None
only=args[4] if len(args)>4 else None
os.makedirs(out, exist_ok=True)

def material(name, color, metal=0):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    bs = m.node_tree.nodes.get('Principled BSDF')
    bs.inputs['Base Color'].default_value = (*color, 1)
    bs.inputs['Metallic'].default_value = metal
    bs.inputs['Roughness'].default_value = .58
    return m

def box(name, pos, size, mat, bone=None, bevel=.025):
    bpy.ops.mesh.primitive_cube_add(size=1, location=pos)
    o = bpy.context.object
    o.name = name
    o.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    mod = o.modifiers.new('Authored bevel', 'BEVEL'); mod.width=bevel; mod.segments=3
    bpy.ops.object.modifier_apply(modifier=mod.name)
    o.data.materials.append(mat)
    if bone: skin(o, bone)
    return o

def skin(obj, bone):
    group = obj.vertex_groups.new(name=bone)
    group.add(list(range(len(obj.data.vertices))), 1, 'REPLACE')
    mod = obj.modifiers.new('Fighter skin', 'ARMATURE'); mod.object = arm
    obj.parent = arm
    obj.matrix_parent_inverse = arm.matrix_world.inverted()

def sleeve(name, bone, radius, mat, portion=.7):
    b = arm.data.bones[bone]
    a, z = arm.matrix_world @ b.head_local, arm.matrix_world @ b.tail_local
    mid = a.lerp(z, .52)
    bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=radius, depth=(z-a).length*portion, location=mid)
    o=bpy.context.object; o.name=name
    o.rotation_mode='QUATERNION'; o.rotation_quaternion=Vector((0,0,1)).rotation_difference(z-a)
    o.data.materials.append(mat); skin(o,bone)
    bevel=o.modifiers.new('Edge radius','BEVEL'); bevel.width=.012; bevel.segments=2
    bpy.context.view_layer.objects.active=o; bpy.ops.object.modifier_apply(modifier=bevel.name)

def reset_pose():
    arm.animation_data.action = None
    for b in arm.pose.bones: b.matrix_basis=Matrix.Identity(4)
    bpy.context.view_layer.update()

def solve_leg(side, target):
    thigh=arm.pose.bones['DEF-thigh.'+side]; shin=arm.pose.bones['DEF-shin.'+side]
    foot=arm.pose.bones['DEF-foot.'+side]
    h=thigh.head.copy(); d=target-h; length=min(d.length, thigh.length+shin.length-.002)
    n=d.normalized(); pole=Vector((0,1,0)); pole=(pole-n*pole.dot(n)).normalized()
    a=(thigh.length**2-shin.length**2+length**2)/(2*length)
    knee=h+n*a+pole*math.sqrt(max(0,thigh.length**2-a*a))
    for b, start, end in ((thigh,h,knee),(shin,knee,target)):
        rest=b.bone.matrix_local
        rot=(rest.to_quaternion() @ Vector((0,1,0))).rotation_difference(end-start)
        b.matrix=Matrix.Translation(start) @ rot.to_matrix().to_4x4() @ rest.to_quaternion().to_matrix().to_4x4()
        bpy.context.view_layer.update()
    foot.matrix=Matrix.Translation(target) @ foot.bone.matrix_local.to_quaternion().to_matrix().to_4x4()
    bpy.context.view_layer.update()

def key_pose(frame):
    for b in arm.pose.bones:
        b.rotation_mode='QUATERNION'
        b.keyframe_insert('location',frame=frame)
        b.keyframe_insert('rotation_quaternion',frame=frame)
        b.keyframe_insert('scale',frame=frame)

def gait(name, direction, crouch=False, sprint=False):
    reset_pose(); act=bpy.data.actions.new(name); arm.animation_data.action=act
    period=24 if sprint else 32 if not crouch else 40
    stride=1.02 if sprint else .85 if not crouch else .36
    step=Vector((math.sin(direction),math.cos(direction),0))
    rest={s: arm.data.bones['DEF-foot.'+s].head_local.copy() for s in ('L','R')}
    for f in range(period+1):
        # Fully planted stance half, curved swing half; both cycles start on the left foot.
        reset_pose()
        phase=f/period
        hip=arm.pose.bones['DEF-hips']; m=hip.matrix.copy()
        m.translation.z -= (.29 if crouch else .18 if sprint else .14) + (.014 if crouch else .028)*(1-math.cos(phase*4*math.pi))
        m.translation.x += .016*math.sin(phase*2*math.pi)
        hip.matrix=m; bpy.context.view_layer.update()
        for s,shift in (('L',0),('R',.5)):
            p=(phase+shift)%1
            if p<.5: travel=(.5-2*p)*stride; lift=0
            else:
                swing=(p-.5)*2; smooth=swing*swing*(3-2*swing)
                travel=(-.5+smooth)*stride; lift=math.sin(swing*math.pi)*(.10 if sprint else .065)
            target=rest[s]+step*travel+Vector((0,0,lift))
            solve_leg(s,target)
        spine=arm.pose.bones['DEF-spine.002']
        spine.rotation_mode='QUATERNION'
        spine.rotation_quaternion=Quaternion((1,0,0),math.radians(-12 if crouch else -5 if sprint else -2))
        arm.animation_data.action=act
        key_pose(f+1)
    act.use_fake_user=True

def build(role):
    global arm
    q.reset(); q.import_fbx(src); arm,body=q.find_armature_and_mesh()
    arm.name='Rig'; body.name='Undersuit'; reset_pose()
    for a in list(bpy.data.actions):
        short=a.name.split('|')[-1]
        if short in ('Idle_Loop','Jump_Start','Jump_Loop','Jump_Land','Death01','Crouch_Idle_Loop','A_TPose'):
            a.name=short; a.use_fake_user=True
        else: bpy.data.actions.remove(a)
    cloth=material('Fighter_Undersuit',(.075,.10,.13)); plate=material('Fighter_Armor',(.24,.30,.34),.35)
    trim=material('Fighter_Team',(.12,.72,.76),.2); glove=material('Fighter_Grip',(.045,.055,.06))
    skinmat=material('Fighter_Skin',(.64,.39,.27)); light=material('Fighter_Lens',(.25,.9,.93),.3)
    body.data.materials.clear(); body.data.materials.append(cloth)
    body.data.materials.append(skinmat); body.data.materials.append(glove)
    for p in body.data.polygons:
        b=q.dominant_group(body,body.data.vertices[p.vertices[0]]) or ''
        p.material_index=1 if 'head' in b or 'neck' in b else 2 if 'hand' in b or 'f_' in b or 'thumb' in b else 0
        p.use_smooth=True
    if role=='P1':
        q.remove_vertices(body,q.ARM_BONES)
        # Replace the featureless mannequin head with the CC0 sculpted superhero head.
        q.remove_vertices(body,('DEF-head',))
        before=set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=base)
        imported=set(bpy.data.objects)-before
        basebody=next(o for o in imported if o.type=='MESH')
        basearm=next(o for o in imported if o.type=='ARMATURE')
        q.remove_vertices(basebody,tuple(g.name for g in basebody.vertex_groups if g.name!='Head'))
        head_src=basearm.matrix_world @ basearm.data.bones['Head'].head_local
        head_dst=arm.matrix_world @ arm.data.bones['DEF-head'].head_local
        for v in basebody.data.vertices:
            v.co=basebody.matrix_world @ v.co-head_src+head_dst
        basebody.parent=None; basebody.matrix_world=Matrix.Identity(4)
        for mod in list(basebody.modifiers): basebody.modifiers.remove(mod)
        basebody.vertex_groups.clear(); skin(basebody,'DEF-head'); basebody.name='P1_ExposedHead'
        basebody.data.materials.clear(); basebody.data.materials.append(skinmat)
        for p in basebody.data.polygons: p.material_index=0
        for obj in imported:
            if obj!=basebody: bpy.data.objects.remove(obj,do_unlink=True)
        # Imported base actions are not compatible and are not exported.
        for a in list(bpy.data.actions):
            if 'Armature|' in a.name: bpy.data.actions.remove(a)
        box('Chest shell',(0,.01,1.235),(.42,.25,.28),plate,'DEF-spine.002')
        box('Chest inset',(0,.153,1.26),(.29,.045,.15),trim,'DEF-spine.002',.014)
        box('Abdominal panels',(0,.105,1.055),(.28,.08,.16),plate,'DEF-spine.001',.015)
        box('Belt',(0,.0,.96),(.34,.25,.075),glove,'DEF-hips',.012)
        box('Back power core',(0,-.18,1.26),(.25,.10,.28),plate,'DEF-spine.002')
        for s,sign in (('L',-1),('R',1)):
            sleeve('Thigh shell '+s,'DEF-thigh.'+s,.098,plate,.56)
            sleeve('Shin shell '+s,'DEF-shin.'+s,.075,plate,.67)
            box('Kneepad '+s,(sign*.089,.060,.53),(.135,.095,.13),glove,'DEF-shin.'+s,.02)
            box('Boot '+s,(sign*.089,.048,.058),(.15,.27,.105),glove,'DEF-foot.'+s,.023)
            box('Boot trim '+s,(sign*.089,.152,.070),(.12,.018,.040),trim,'DEF-foot.'+s,.008)
        # Small brow/temple hardware leaves the face/head silhouette exposed.
        box('Brow visor',(0,.115,1.712),(.205,.035,.045),glove,'DEF-head',.012)
        box('Visor lens',(0,.137,1.713),(.162,.012,.026),light,'DEF-head',.006)
    else:
        q.remove_vertices(body,q.P2_REMOVE+('DEF-spine.003',) if role=='P2' else q.ARMS_REMOVE+('DEF-shoulder.','DEF-upper_arm.'))
        for s,sign in (('L',-1),('R',1)):
            sleeve('Vambrace '+s,'DEF-forearm.'+s,.066,plate,.75)
            if role=='P2':
                sleeve('Mechanical upper sleeve '+s,'DEF-upper_arm.'+s,.082,plate,.69)
                box('Shoulder housing '+s,(sign*.225,-.015,1.418),(.21,.27,.20),plate,'DEF-upper_arm.'+s,.045)
                box('Shoulder accent '+s,(sign*.225,.129,1.443),(.16,.025,.055),trim,'DEF-upper_arm.'+s,.009)
        if role=='P2':
            box('P2 clavicle harness',(0,-.025,1.425),(.39,.26,.115),glove,'DEF-spine.003',.025)
    if role!='Arms':
        if ual2:
            before=set(bpy.data.objects); before_actions=set(bpy.data.actions)
            bpy.ops.import_scene.fbx(filepath=ual2)
            names={'pelvis':'DEF-hips','spine_01':'DEF-spine.001','spine_02':'DEF-spine.002','spine_03':'DEF-spine.003','neck_01':'DEF-neck','Head':'DEF-head','root':'root'}
            for side,cap in (('l','L'),('r','R')):
                for old,new in (('clavicle','shoulder'),('upperarm','upper_arm'),('lowerarm','forearm'),('hand','hand'),('thigh','thigh'),('calf','shin'),('foot','foot'),('ball','toe')):
                    names[old+'_'+side]='DEF-'+new+'.'+cap
                for finger in ('index','middle','ring','pinky','thumb'):
                    for joint in range(1,4):
                        names[f'{finger}_{joint:02}_{side}']=f'DEF-'+('thumb' if finger=='thumb' else 'f_'+finger)+f'.{joint:02}.{cap}'
            for a in set(bpy.data.actions)-before_actions:
                short=a.name.split('|')[-1]
                if short in ('Slide_Loop','ClimbUp_1m'):
                    a.name=short; a.use_fake_user=True
                    for fc in a.fcurves:
                        for old,new in names.items(): fc.data_path=fc.data_path.replace('"'+old+'"','"'+new+'"')
                else: bpy.data.actions.remove(a)
            for o in set(bpy.data.objects)-before: bpy.data.objects.remove(o,do_unlink=True)
        for name,angle in (('F',0),('FR',45),('R',90),('BR',135),('B',180),('BL',225),('L',270),('FL',315)):
            gait('Jog_'+name+'_Loop',math.radians(angle))
            gait('Crouch_'+name+'_Loop',math.radians(angle),True)
        gait('Sprint_Loop',0,sprint=True)
    reset_pose(); q.stash_actions(arm)
    q.export_fbx(os.path.join(out,'Fighter_'+role+'.fbx'))
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out,'Fighter_'+role+'.blend'))

for role in (only,) if only else ('P1','P2','Arms'): build(role)

# Authored split-component rifle: +Y in Blender becomes +Z in Unity, origin at trigger wrist.
q.reset()
dark=material('Rifle_Receiver',(.095,.13,.16),.65); metal=material('Rifle_Steel',(.23,.28,.31),.7)
rubber=material('Rifle_Grip',(.035,.045,.055)); accent=material('Rifle_Accent',(.10,.67,.70),.4)
box('Receiver',(0,.16,.09),(.075,.30,.11),dark,None,.012)
box('Handguard',(0,.39,.095),(.065,.25,.085),metal,None,.011)
box('Stock',(0,-.14,.07),(.052,.28,.11),rubber,None,.019)
box('Stock cheek',(0,-.16,.13),(.057,.19,.035),dark,None,.009)
grip=box('PistolGrip',(0,.02,-.015),(.052,.06,.12),rubber,None,.011); grip.rotation_euler.x=math.radians(-14)
box('Magazine',(0,.16,-.05),(.058,.105,.17),dark,None,.012)
box('Magazine base',(0,.16,-.143),(.067,.118,.022),rubber,None,.007)
box('Bolt',(.043,.15,.09),(.013,.085,.045),metal,None,.003)
box('TopRail',(0,.24,.156),(.034,.45,.018),rubber,None,.002)
for i in range(12): box('Rail tooth '+str(i),(0,.025+i*.036,.171),(.046,.014,.012),metal,None,.002)
box('Sight',(0,.105,.196),(.044,.035,.067),dark,None,.006)
box('Sight aperture',(0,.104,.214),(.025,.036,.021),accent,None,.002)
box('Trigger guard',(0,.038,-.004),(.067,.116,.020),dark,None,.006)
for sign in (-1,1):
    for i in range(5): box('Vent', (sign*.034,.30+i*.039,.11),(.008,.019,.027),rubber,None,.003)
for name, y, radius, depth in (('Barrel',.574,.014,.23),('MuzzleBrake',.70,.023,.045)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=16,radius=radius,depth=depth,location=(0,y,.103),rotation=(math.pi/2,0,0))
    o=bpy.context.object; o.name=name; o.data.materials.append(metal)
bpy.ops.export_scene.fbx(filepath=os.path.join(out,'Rifle.fbx'),axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out,'Rifle.blend'))
