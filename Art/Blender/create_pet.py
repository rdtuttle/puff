"""Build one soft, detailed, reference-inspired mixed-reality pet and export FBX.
Run with Blender --background --python Art/Blender/create_pet.py
Original modeling script; no downloaded meshes/textures.
"""
import bpy, math, os, json, random
random.seed(31)
from mathutils import Vector
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'../..'))
OUT=os.path.join(ROOT,'Assets/TabletopMayhem/Resources/Fighters')
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
matcache={};pieces=[];rigparts={};models=[];stats={}
PINK=(.98,.57,.72,1);LIGHTPINK=(1,.73,.82,1);DARKPINK=(.45,.07,.19,1)
BLACK=(.025,.03,.04,1);WHITE=(.94,.96,1,1);TEAL=(.03,.55,.63,1)
BROWN=(.27,.115,.045,1);TAN=(.46,.25,.12,1);GOLD=(.86,.49,.07,1)
SKIN=(.72,.39,.22,1);RED=(.65,.025,.03,1);DENIM=(.045,.085,.14,1)
def material(c):
 key=tuple(c)
 if key not in matcache:
  m=bpy.data.materials.new('Palette');m.diffuse_color=c;m.use_nodes=True
  bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=c;bs.inputs['Roughness'].default_value=.58
  matcache[key]=m
 return matcache[key]
def keep(o,name,c,part):
 o.name=name;o.data.materials.append(material(c));pieces.append(o);rigparts.setdefault(part,[]).append(o)
 return o
def uv(name,p,s,c,part='Body',segments=24,rings=16):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=rings,location=p)
 o=bpy.context.object;o.scale=s;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 for poly in o.data.polygons:poly.use_smooth=True
 return keep(o,name,c,part)
def box(name,p,s,c,part='Body',bevel=.03):
 bpy.ops.mesh.primitive_cube_add(size=1,location=p);o=bpy.context.object;o.scale=s
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 if bevel:
  mod=o.modifiers.new('Rounded silhouette','BEVEL');mod.width=bevel;mod.segments=2
  bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
 return keep(o,name,c,part)
def rod(name,a,b,r,c,part='Body',r2=None):
 a,b=Vector(a),Vector(b);d=b-a
 bpy.ops.mesh.primitive_cone_add(vertices=12,radius1=r,radius2=r if r2 is None else r2,depth=d.length,location=(a+b)/2)
 o=bpy.context.object;o.rotation_euler=d.to_track_quat('Z','Y').to_euler()
 return keep(o,name,c,part)
def curve(name,coords,r,c,part='Body'):
 cu=bpy.data.curves.new(name,'CURVE');cu.dimensions='3D';cu.bevel_depth=r;cu.bevel_resolution=2;cu.resolution_u=10;cu.use_fill_caps=True
 sp=cu.splines.new('BEZIER');sp.bezier_points.add(len(coords)-1)
 for b,p in zip(sp.bezier_points,coords):b.co=p;b.handle_left_type='AUTO';b.handle_right_type='AUTO'
 o=bpy.data.objects.new(name,cu);bpy.context.collection.objects.link(o)
 bpy.context.view_layer.objects.active=o;o.select_set(True)
 for x in bpy.context.selected_objects:
  if x!=o:x.select_set(False)
 bpy.ops.object.convert(target='MESH');return keep(bpy.context.object,name,c,part)
def triangle(name,points,c,part='Body'):
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(points,[],[(0,1,2),(2,1,0)]);mesh.update()
 o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o)
 mod=o.modifiers.new('Soft ear thickness','SOLIDIFY');mod.thickness=.025
 bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
 return keep(o,name,c,part)
def begin():
 global pieces,rigparts
 pieces=[];rigparts={}
def finish(name,height,xoffset,pivots=None):
 # Vertex colors retain the palette while every articulated part uses one draw-call material.
 vertexmat=bpy.data.materials.get('FighterVertexColor')
 if vertexmat is None:
  vertexmat=bpy.data.materials.new('FighterVertexColor');vertexmat.use_nodes=True
  nt=vertexmat.node_tree;v=nt.nodes.new('ShaderNodeVertexColor');v.layer_name='Col'
  nt.links.new(v.outputs['Color'],nt.nodes.get('Principled BSDF').inputs['Base Color'])
  nt.links.new(v.outputs['Alpha'],nt.nodes.get('Principled BSDF').inputs['Roughness'])
 for o in pieces:
  attr=o.data.color_attributes.new(name='Col',type='BYTE_COLOR',domain='CORNER')
  for poly in o.data.polygons:
   color=o.data.materials[poly.material_index].diffuse_color
   # Palette alpha carries roughness to the mobile shader; subtle vertex variation breaks flat plastic.
   rough=.28 if ('eye' in o.name.lower() or 'lens' in o.name.lower()) else .43 if ('armor' in o.name.lower() or 'gold' in o.name.lower()) else .78
   for li in poly.loop_indices:
    co=o.data.vertices[o.data.loops[li].vertex_index].co
    grain=1+random.uniform(-.025,.025)
    attr.data[li].color=tuple(min(1,max(0,c*grain)) for c in color[:3])+(rough,)
  o.data.materials.clear();o.data.materials.append(vertexmat)
 root=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(root)
 meshes=[]
 for part,objects in rigparts.items():
  bpy.ops.object.select_all(action='DESELECT')
  for o in objects:o.select_set(True)
  bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();o=bpy.context.object;o.name=part
  bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
  # Put limb pivots at shoulders / hips so Unity can animate poses.
  if pivots and part in pivots:pivot=pivots[part]
  elif part=='Body':pivot=(0,0,.65)
  elif part=='LeftArm':pivot=(-.55,-.03,.61)
  elif part=='RightArm':pivot=(.55,-.03,.61)
  elif part=='LeftLeg':pivot=(-.30,-.09,.14)
  elif part=='RightLeg':pivot=(.30,-.09,.14)
  elif part=='LeftForearm':pivot=(-.47,-.045,1.04)
  elif part=='RightForearm':pivot=(.47,-.045,1.04)
  elif part=='LeftShin':pivot=(-.20,0,.48)
  elif part=='RightShin':pivot=(.20,0,.48)
  elif part=='Mouth':pivot=(0,-.43,.62)
  else:pivot=(0,0,0)
  bpy.context.scene.cursor.location=pivot;bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
  o.parent=root;meshes.append(o)
 # Hierarchical elbows and knees preserve the bind pose and bend independently.
 bpy.context.view_layer.update()
 bypart={o.name:o for o in meshes}
 for lower,upper in [('LeftArm','Body'),('RightArm','Body'),('LeftForearm','LeftArm'),('RightForearm','RightArm'),('LeftShin','LeftLeg'),('RightShin','RightLeg')]:
  if lower in bypart and upper in bypart:
   o=bypart[lower];world=o.matrix_world.copy();o.parent=bypart[upper];o.matrix_world=world
 bpy.context.view_layer.update()
 # Scale characters to their tabletop dimensions: 17-20 cm.
 scale=height/max(v[2] for o in meshes for v in [o.matrix_world@Vector(c) for c in o.bound_box])
 root.scale=(scale,)*3
 bpy.context.view_layer.update()
 bpy.ops.object.select_all(action='DESELECT');root.select_set(True)
 for o in meshes:o.select_set(True)
 bpy.context.view_layer.objects.active=root
 bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,name+'.fbx'),use_selection=True,object_types={'MESH','EMPTY'},
  apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,
  add_leaf_bones=False,bake_anim=False,path_mode='AUTO')
 tris=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes)
 stats[name]={'triangles':tris,'articulated_meshes':len(meshes),'height_m':height}
 # Free the canonical joint names before generating the next character in this scene.
 for o in meshes:o.name=name+'_'+o.name
 root.location.x=xoffset;models.append(root)
 return root
# PUFF: round pink body, pointed ears, oversized teal eyes, forehead curl.
begin()
uv('Round pink body',(0,0,.76),(.62,.46,.62),PINK)
for sign in [-1,1]:
 uv('Foot',(sign*.30,-.09,.14),(.23,.30,.13),PINK,'LeftLeg' if sign<0 else 'RightLeg')
 uv('Tiny arm',(sign*.56,-.03,.57),(.20,.11,.10),PINK,'LeftArm' if sign<0 else 'RightArm')
 triangle('Pointed ear',[(sign*.30,0,1.19),(sign*.66,.015,1.50),(sign*.58,-.08,.98)],PINK)
 triangle('Ear inset',[(sign*.39,-.021,1.19),(sign*.60,-.015,1.40),(sign*.55,-.10,1.10)],BLACK)
 uv('Eye white',(sign*.235,-.387,.88),(.181,.070,.221),WHITE)
 uv('Teal iris',(sign*.235,-.445,.88),(.139,.035,.173),TEAL)
 uv('Pupil',(sign*.235,-.475,.90),(.086,.012,.119),BLACK)
 uv('Eye sparkle',(sign*.235-.032,-.49,.965),(.035,.012,.044),WHITE)
 uv('Cheek',(sign*.38,-.431,.65),(.073,.018,.036),(.94,.36,.47,1))
uv('Mouth',(0,-.473,.60),(.090,.013,.070),DARKPINK,'Mouth')
uv('Tongue',(0,-.489,.565),(.052,.009,.025),(.98,.35,.42,1),'Mouth')
# A ribbon behind the right ear gives Puff a recognisable silhouette without hiding her face.
bowred=(.88,.035,.09,1)
leftBow=uv('Bow left loop',(.34,.095,1.43),(.19,.055,.12),bowred);leftBow.rotation_euler[1]=-.25
rightBow=uv('Bow right loop',(.66,.095,1.43),(.19,.055,.12),bowred);rightBow.rotation_euler[1]=.25
uv('Bow centre',(.50,.032,1.43),(.070,.064,.070),RED)
curve('Bow pleat left',[(.22,.034,1.39),(.39,.029,1.42),(.47,.026,1.43)],.009,(1,.35,.38,1))
curve('Bow pleat right',[(.78,.034,1.39),(.61,.029,1.42),(.53,.026,1.43)],.009,(1,.35,.38,1))
curve('Forehead curl',[(.12,-.32,1.25),(.07,-.40,1.40),(-.12,-.41,1.42),(-.20,-.43,1.30),(-.10,-.49,1.20),(.055,-.50,1.23),(.08,-.50,1.30)],.075,LIGHTPINK)
# Plush tufts along the silhouette and embroidered lip give the pink creature a tactile finish.
curve('Upper lip',[(-.080,-.480,.645),(0,-.482,.627),(.080,-.480,.645)],.008,DARKPINK)
# Soft belly gives pokes an obvious target; glossy layered eyes and fabric details read at desk scale.
uv('Velvety belly patch',(0,-.338,.69),(.31,.098,.25),(.99,.59,.66,1))
for sign in [-1,1]:
 for i in range(3):
  uv('Tiny cheek freckle',(sign*(.34+i*.038),-.449+i*.012,.63+i*.037),(.009,.007,.009),(.76,.20,.35,1),segments=8,rings=6)
 curve('Upper lash',[(sign*.10,-.445,1.065),(sign*.23,-.458,1.105),(sign*.36,-.426,1.055)],.007,BLACK)
 uv('Iris ring',(sign*.235,-.445,.88),(.144,.012,.176),(.01,.26,.32,1),segments=24,rings=12)
 uv('Iris inner',(sign*.235,-.466,.88),(.109,.010,.139),(.08,.68,.76,1),segments=24,rings=12)
 uv('Deep pupil',(sign*.235,-.482,.90),(.073,.008,.100),BLACK,segments=24,rings=12)
 uv('Soft catchlight',(sign*.235-.045,-.495,.96),(.027,.008,.035),WHITE,segments=16,rings=10)
 # The closed-eye mask is an optional node enabled only while sleeping.
 uv('Sleep lid',(sign*.235,-.492,.88),(.184,.037,.227),PINK,'SleepFace')
 curve('Sleep smile',[(-.08,-.52,.62),(0,-.522,.59),(.08,-.52,.62)],.010,DARKPINK,'SleepFace')
 curve('Sleep lash',[(sign*.10,-.530,.91),(sign*.23,-.531,.89),(sign*.36,-.525,.91)],.012,DARKPINK,'SleepFace')
# Small tufts are limited to side and back so the face stays clean.
for i in range(44):
 a=i*2*math.pi/44
 if math.cos(a)<-.30:continue
 x=.55*math.sin(a);y=.33*math.cos(a);z=.75+.045*math.sin(i*1.7)
 uv('Plush tuft',(x,y,z),(.018,.014,.015),LIGHTPINK,segments=8,rings=6)
finish('Puff',.19,0)

# Three original tabletop challengers inspired by the supplied silhouettes.
def eyes_pair(y,z,spacing=.19,white=WHITE,iris=BLACK):
 for s in [-1,1]:
  uv('Eye white',(s*spacing,y,z),(.105,.040,.105),white)
  uv('Iris',(s*spacing,y-.038,z),(.048,.010,.064),iris)
  uv('Eye highlight',(s*spacing-.017,y-.049,z+.028),(.015,.006,.020),WHITE)

begin()
GI=(.91,.87,.72,1);GI_SHADOW=(.68,.64,.53,1);HAIR=(.11,.07,.06,1)
uv('Karate torso',(0,0,1.04),(.43,.25,.51),GI)
uv('Fighter face',(0,-.06,1.52),(.27,.25,.30),SKIN)
uv('Jaw',(0,-.16,1.37),(.22,.14,.11),SKIN)
for s in [-1,1]:
 uv('Dark eyebrow',(s*.11,-.281,1.57),(.095,.018,.028),HAIR)
 uv('Focused eye',(s*.11,-.288,1.52),(.047,.012,.029),BLACK)
 uv('Spiky hair',(s*.18,.01,1.78),(.14,.14,.18),HAIR)
 uv('Sleeve',(s*.42,0,1.27),(.17,.20,.22),GI,'LeftArm' if s<0 else 'RightArm')
 uv('Forearm',(s*.48,-.04,1.04),(.13,.13,.23),SKIN,'LeftArm' if s<0 else 'RightArm')
 uv('Red glove',(s*.49,-.06,.89),(.15,.15,.15),RED,'LeftArm' if s<0 else 'RightArm')
 uv('Trouser leg',(s*.19,0,.42),(.18,.22,.40),GI,'LeftLeg' if s<0 else 'RightLeg')
 uv('Foot',(s*.20,-.11,.08),(.16,.26,.09),SKIN,'LeftLeg' if s<0 else 'RightLeg')
 uv('Gi lapel',(s*.13,-.245,1.24),(.085,.018,.34),GI_SHADOW)
curve('Headband',[( -.25,-.05,1.69),(0,-.265,1.68),(.25,-.05,1.69)],.038,RED)
curve('Trailing headband',[(.24,.02,1.69),(.43,.17,1.68),(.58,.31,1.60)],.034,RED)
box('Dark belt',(0,-.02,.72),(.70,.31,.10),HAIR)
box('Belt tail',(.22,-.23,.55),(.08,.045,.38),HAIR)
finish('KarateComet',.23,.42,{'Body':(0,0,.95),'LeftArm':(-.39,0,1.28),'RightArm':(.39,0,1.28),'LeftLeg':(-.18,0,.73),'RightLeg':(.18,0,.73)})

begin()
FUR=(.45,.27,.17,1);ORANGE=(.95,.48,.15,1);PALE=(1,.83,.48,1);DARK=(.16,.12,.12,1)
uv('Cat body',(0,0,.64),(.62,.43,.51),FUR)
uv('Cream muzzle',(0,-.40,.65),(.35,.16,.22),PALE)
uv('Cat face',(0,-.20,.96),(.47,.34,.34),ORANGE)
for s in [-1,1]:
 triangle('Pointed cat ear',[(s*.26,-.11,1.13),(s*.54,-.04,1.52),(s*.53,-.09,1.04)],DARK)
 triangle('Ear pink',[(s*.32,-.15,1.15),(s*.51,-.09,1.40),(s*.48,-.16,1.10)],RED)
 uv('Angry eye white',(s*.20,-.508,1.03),(.125,.028,.11),WHITE)
 uv('Golden slit',(s*.20,-.534,1.03),(.031,.008,.075),GOLD)
 uv('Cat cheek',(s*.36,-.44,.79),(.10,.034,.10),PALE)
 uv('Claw paw',(s*.62,-.14,.47),(.20,.18,.15),PALE,'LeftArm' if s<0 else 'RightArm')
 uv('Paw leg',(s*.25,-.10,.24),(.21,.26,.19),FUR,'LeftLeg' if s<0 else 'RightLeg')
 for j in range(3):
  uv('Claw',(s*(.52+j*.055),-.29,.45),(.018,.085,.025),WHITE,'LeftArm' if s<0 else 'RightArm')
uv('Nose',(0,-.56,.78),(.064,.03,.04),BLACK)
curve('Fierce mouth',[(-.11,-.52,.70),(0,-.54,.65),(.11,-.52,.70)],.013,DARK)
curve('Striped tail',[(.37,.29,.47),(.71,.42,.49),(.88,.45,.85),(.68,.50,1.17)],.105,ORANGE)
for i in range(4):
 uv('Dark body stripe',(-.25+i*.16,.27,.75),(.047,.10,.15),DARK)
box('Red battle scarf',(0,-.23,1.07),(.56,.13,.10),RED)
finish('Clawstorm',.18,.82,{'Body':(0,0,.65),'LeftArm':(-.50,-.04,.65),'RightArm':(.50,-.04,.65),'LeftLeg':(-.24,0,.28),'RightLeg':(.24,0,.28)})

begin()
MEAT=(.51,.17,.10,1);CRUST=(.67,.28,.14,1);SAUCE=(.90,.34,.20,1)
uv('Meatball body',(0,0,.67),(.57,.51,.57),MEAT,segments=32,rings=24)
for i in range(55):
 a=i*2.399;u=(i+.5)/55;z=.25+u*.84;r=.55*math.sqrt(max(0,1-((z-.67)/.57)**2))
 uv('Baked crust fleck',(r*math.cos(a),-.06+r*math.sin(a),z),(.032,.015,.025),CRUST,segments=8,rings=6)
for s in [-1,1]:
 uv('Big angry eye',(s*.20,-.475,.80),(.14,.055,.17),WHITE)
 uv('Dark pupil',(s*.20,-.53,.80),(.062,.011,.105),BLACK)
 uv('Raised brow',(s*.20,-.50,.99),(.15,.031,.038),BLACK)
 uv('Round fist',(s*.58,-.06,.57),(.17,.18,.16),CRUST,'LeftArm' if s<0 else 'RightArm')
 uv('Short leg',(s*.24,-.03,.19),(.12,.14,.25),MEAT,'LeftLeg' if s<0 else 'RightLeg')
 uv('Boot',(s*.25,-.14,.09),(.17,.25,.11),CRUST,'LeftLeg' if s<0 else 'RightLeg')
 uv('Headband check', (s*.31,-.33,1.10),(.16,.018,.07),WHITE)
uv('Open mouth',(0,-.519,.54),(.16,.025,.14),BLACK)
uv('Tongue',(0,-.545,.48),(.095,.014,.046),SAUCE)
curve('Red racing headband',[(-.48,-.15,1.11),(0,-.49,1.15),(.48,-.15,1.11)],.045,RED)
for i in range(5):box('White checker',( -.34+i*.17,-.43+abs(i-2)*.045,1.16),(.082,.027,.07),WHITE)
finish('MeatballMarauder',.18,1.22,{'Body':(0,0,.67),'LeftArm':(-.50,-.03,.60),'RightArm':(.50,-.03,.60),'LeftLeg':(-.23,0,.26),'RightLeg':(.23,0,.26)})

scene=bpy.context.scene
scene.render.engine='CYCLES';scene.cycles.samples=48
scene.world.color=(.16,.16,.16)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.004));floor=bpy.context.object;floor.name='Studio floor';floor.data.materials.append(material((.025,.035,.045,1)))
for p,power,size in [((-.35,-.5,.9),70,.7),((.5,-.3,.6),50,.55),((0,.4,.8),90,.5)]:
 bpy.ops.object.light_add(type='AREA',location=p);o=bpy.context.object;o.data.energy=power*.14;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(Vector((0,0,.10))-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(.16,-.7,.27));camera=bpy.context.object
camera.rotation_euler=(Vector((0,0,.10))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=.35;scene.camera=camera
scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG';scene.render.filepath=os.path.join(ROOT,'Art/Blender/pet-preview.png')
sleepface=next((o for o in bpy.data.objects if o.name.startswith('Puff_SleepFace')),None)
if sleepface:sleepface.hide_render=True
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT,'Art/Blender/Puff.blend'))
with open(os.path.join(ROOT,'Art/Blender/pet-model-stats.json'),'w') as f:json.dump(stats,f,indent=2)
bpy.ops.render.render(write_still=True)
if sleepface:
 sleepface.hide_render=False
 scene.render.filepath=os.path.join(ROOT,'Art/Blender/pet-sleep-preview.png')
 bpy.ops.render.render(write_still=True)
 sleepface.hide_render=True
for model in models[1:]:
 target=Vector((model.location.x,0,.11))
 camera.location=(model.location.x+.16,-.72,.29)
 camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
 camera.data.ortho_scale=.37
 scene.render.filepath=os.path.join(ROOT,'Art/Blender/'+model.name.lower()+'-preview.png')
 bpy.ops.render.render(write_still=True)
print('PET_MODEL_COMPLETE',json.dumps(stats))
