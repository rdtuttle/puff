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
PINK=(.98,.32,.51,1);LIGHTPINK=(1,.57,.67,1);DARKPINK=(.45,.07,.19,1)
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
def finish(name,height,xoffset):
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
  if part=='Body':pivot=(0,0,.65)
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
uv('Round pink body',(0,0,.76),(.59,.44,.61),PINK)
for sign in [-1,1]:
 uv('Foot',(sign*.30,-.09,.14),(.23,.30,.13),PINK,'LeftLeg' if sign<0 else 'RightLeg')
 uv('Tiny arm',(sign*.56,-.03,.57),(.20,.11,.10),PINK,'LeftArm' if sign<0 else 'RightArm')
 triangle('Pointed ear',[(sign*.30,0,1.19),(sign*.66,.015,1.50),(sign*.58,-.08,.98)],PINK)
 triangle('Ear inset',[(sign*.39,-.021,1.19),(sign*.60,-.015,1.40),(sign*.55,-.10,1.10)],DARKPINK)
 uv('Eye white',(sign*.235,-.387,.88),(.181,.070,.221),WHITE)
 uv('Teal iris',(sign*.235,-.445,.88),(.139,.035,.173),TEAL)
 uv('Pupil',(sign*.235,-.475,.90),(.086,.012,.119),BLACK)
 uv('Eye sparkle',(sign*.235-.032,-.49,.965),(.035,.012,.044),WHITE)
 uv('Cheek',(sign*.38,-.366,.65),(.086,.023,.041),(.94,.18,.31,1))
uv('Mouth',(0,-.433,.60),(.097,.014,.080),DARKPINK,'Mouth')
uv('Tongue',(0,-.45,.569),(.057,.009,.029),(.98,.22,.29,1),'Mouth')
curve('Forehead curl',[(.12,-.32,1.25),(.07,-.40,1.40),(-.12,-.41,1.42),(-.20,-.43,1.30),(-.10,-.49,1.20),(.055,-.50,1.23),(.08,-.50,1.30)],.075,LIGHTPINK)
# Plush tufts along the silhouette and embroidered lip give the pink creature a tactile finish.
curve('Upper lip',[(-.085,-.45,.645),(0,-.452,.627),(.085,-.45,.645)],.008,DARKPINK)
# Soft belly gives pokes an obvious target; glossy layered eyes and fabric details read at desk scale.
uv('Velvety belly patch',(0,-.338,.69),(.31,.098,.25),(.99,.59,.66,1))
for sign in [-1,1]:
 for i in range(3):
  uv('Tiny cheek freckle',(sign*(.34+i*.038),-.40+i*.012,.63+i*.037),(.009,.007,.009),(.76,.20,.35,1),segments=8,rings=6)
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
print('PET_MODEL_COMPLETE',json.dumps(stats))
