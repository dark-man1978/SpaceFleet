#!/usr/bin/env python3
"""Rebuild the original MIT-licensed Flotte-A models. Requires numpy and scipy.
All geometry is authored here; generated concept images are not used as textures.
Run from any directory: python3 Tools/build_fleet_assets.py
"""
import json, math, struct, random
from pathlib import Path
from collections import defaultdict
import numpy as np
from scipy.spatial import ConvexHull

OUT=Path(__file__).resolve().parents[1]/'Assets/Models'
PALETTE={
 'armor':('909794',.12,.68), 'armor_light':('a7aaa4',.08,.68),
 'slate':('59646c',.18,.65), 'edge':('35434d',.20,.68),
 'dark':('101a23',.45,.57), 'rubber':('10151a',.05,.87),
 'metal':('869199',.85,.37), 'yellow':('bca057',.28,.67),
 'glass':('142d3d',.63,.20), 'screen':('06141e',.10,.31),
 'white_glow':('bdced7',.0,.5), 'blue_glow':('72accc',.0,.5),
 'amber_glow':('c6a163',.0,.5), 'paint_chip':('677078',.70,.70)}

class Model:
 def __init__(self,name):self.name=name;self.groups=defaultdict(list);self.rng=random.Random(315)
 def tri(self,a,b,c,mat):
  a,b,c=np.asarray(a,dtype=float),np.asarray(b,dtype=float),np.asarray(c,dtype=float)
  n=np.cross(b-a,c-a);l=np.linalg.norm(n)
  if l<1e-10:return
  n/=l
  shade=.97+self.rng.uniform(-.027,.027) if mat not in ['glass','screen'] and not mat.endswith('glow') else 1
  for p in (a,b,c):self.groups[mat].append((p,n,(shade,shade,shade,1)))
 def quad(self,a,b,c,d,mat):self.tri(a,b,c,mat);self.tri(a,c,d,mat)
 def hull(self,points,mat):
  p=np.asarray(points,dtype=float);h=ConvexHull(p)
  for face,eq in zip(h.simplices,h.equations):
   a,b,c=p[face]
   if np.dot(np.cross(b-a,c-a),eq[:3])<0:b,c=c,b
   self.tri(a,b,c,mat)
 def box(self,center,size,mat='armor',bevel=.08,rotation=None):
  c=np.asarray(center,float);s=np.asarray(size,float)/2;b=min(bevel,float(min(s))*.65)
  if b<=0:pts=[(x,y,z) for x in [-s[0],s[0]] for y in [-s[1],s[1]] for z in [-s[2],s[2]]]
  else:
   pts=[]
   for axis in range(3):
    for i in [-1,1]:
     for j in [-1,1]:
      for k in [-1,1]:
       v=np.array([i,j,k],float)*(s-b);v[axis]=[i,j,k][axis]*s[axis];pts.append(v)
  pts=np.asarray(pts)
  if rotation is not None:pts=pts@np.asarray(rotation).T
  self.hull(pts+c,mat)
 def pipe(self,a,b,r,mat='metal',segments=12,r2=None):
  a,b=np.asarray(a,float),np.asarray(b,float);axis=b-a;axis/=np.linalg.norm(axis)
  helper=np.array([0,1,0]) if abs(axis[1])<.9 else np.array([1,0,0])
  u=np.cross(axis,helper);u/=np.linalg.norm(u);v=np.cross(axis,u);r2=r if r2 is None else r2
  rings=[]
  for cen,rad in [(a,r),(b,r2)]:rings.append([cen+(u*math.cos(t*2*math.pi/segments)+v*math.sin(t*2*math.pi/segments))*rad for t in range(segments)])
  for i in range(segments):
   j=(i+1)%segments;self.quad(rings[0][i],rings[0][j],rings[1][j],rings[1][i],mat)
   self.tri(a,rings[0][j],rings[0][i],mat);self.tri(b,rings[1][i],rings[1][j],mat)
 def beam(self,a,b,width,mat='edge'):
  a,b=np.asarray(a,float),np.asarray(b,float);axis=b-a;length=np.linalg.norm(axis);axis/=length
  helper=np.array([0,1,0]) if abs(axis[1])<.9 else np.array([1,0,0]);u=np.cross(helper,axis);u/=np.linalg.norm(u);v=np.cross(axis,u)
  self.box((a+b)/2,[width,width,length],mat,width*.2,np.array([u,v,axis]).T)
 def plate(self,points,y,thick,mat):
  self.hull([(x,yy,z) for x,z in points for yy in [y-thick/2,y+thick/2]],mat)
 def loft(self,rings,mat):
  # Rings ordered counterclockwise as seen from the nose, with corresponding vertices.
  for a,b in zip(rings,rings[1:]):
   for i in range(len(a)):j=(i+1)%len(a);self.quad(a[i],b[i],b[j],a[j],mat)
  self.hull(rings[0]+rings[-1],mat) if len(rings)==2 else None
  self.hull(rings[:1][0]+[np.mean(rings[0],axis=0)+[0,0,.01]],mat)
  self.hull(rings[-1]+[np.mean(rings[-1],axis=0)-[0,0,.01]],mat)
 def write(self,file):
  materials=[];primitives=[];views=[];accessors=[];binary=bytearray()
  def add(array,kind):
   a=np.asarray(array,dtype='<f4');raw=a.tobytes();off=len(binary);binary.extend(raw)
   while len(binary)%4:binary.append(0)
   views.append({'buffer':0,'byteOffset':off,'byteLength':len(raw),'target':34962})
   ac={'bufferView':len(views)-1,'componentType':5126,'count':len(a),'type':kind}
   if kind=='VEC3':ac.update(min=a.min(axis=0).tolist(),max=a.max(axis=0).tolist())
   accessors.append(ac);return len(accessors)-1
  for mat,items in self.groups.items():
   color,metal,rough=PALETTE[mat];srgb=[int(color[i:i+2],16)/255 for i in (0,2,4)]
   rgb=[v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in srgb]
   m={'name':mat,'pbrMetallicRoughness':{'baseColorFactor':rgb+[1],'metallicFactor':metal,'roughnessFactor':rough}}
   if mat.endswith('glow'):m['emissiveFactor']=rgb
   materials.append(m)
   p,n,c=zip(*items)
   primitives.append({'attributes':{'POSITION':add(p,'VEC3'),'NORMAL':add(n,'VEC3'),'COLOR_0':add(c,'VEC4')},'material':len(materials)-1,'mode':4})
  gltf={'asset':{'version':'2.0','generator':'Space Fleet original Flotte A mesh builder (MIT)'},'scene':0,'scenes':[{'nodes':[0]}],'nodes':[{'name':self.name,'mesh':0}],'meshes':[{'name':self.name,'primitives':primitives}],'materials':materials,'buffers':[{'byteLength':len(binary)}],'bufferViews':views,'accessors':accessors,
   'extras':{'license':'MIT','copyright':'2026 dark-man1978','triangles':sum(len(g)//3 for g in self.groups.values()),'forward':'-Z','units':'meters'}}
  data=json.dumps(gltf,separators=(',',':')).encode();data+=b' '*((-len(data))%4)
  result=struct.pack('<4sII',b'glTF',2,12+8+len(data)+8+len(binary))+struct.pack('<I4s',len(data),b'JSON')+data+struct.pack('<I4s',len(binary),b'BIN\0')+binary
  OUT.mkdir(parents=True,exist_ok=True);(OUT/file).write_bytes(result)
  print(file, len(result),'bytes',gltf['extras']['triangles'],'triangles',len(materials),'material surfaces')

def fighter():
 m=Model('SF12_Kestrel_Flotte_A')
 rings=[]
 for z,w,h,cy in [(-10,.42,.38,-.35),(-8,1.08,.65,-.12),(-4,1.45,.85,0),(0,1.85,1,0),(5.7,1.95,1.0,0),(8.5,1.65,.82,-.1)]:
  rings.append([(x*w,cy+y*h,z) for x,y in [(-.65,1),(.65,1),(1,.5),(1,-.5),(.65,-1),(-.65,-1),(-1,-.5),(-1,.5)]])
 m.loft(rings,'armor_light')
 m.box([0,-.75,-.4],[1.45,.55,16.3],'slate',.18)
 m.box([0,-.27,-9.8],[.6,.5,.8],'edge',.13)
 m.pipe([0,-.25,-10.15],[0,-.25,-10.55],.15,'metal',12)
 for side in [-1,1]:
  wing=[(side*1.35,-2.7),(side*2.4,-2.0),(side*8.4,3.8),(side*8.65,5.5),(side*6.1,7.5),(side*1.4,6.4)]
  m.plate(wing,-.1,.45,'slate')
  # Individual armor panels separated by dark joints.
  m.plate([(side*1.65,-2.1),(side*2.3,-1.65),(side*5.1,1.1),(side*5.1,6.4),(side*1.7,5.85)],.17,.13,'armor_light')
  m.plate([(side*5.22,1.25),(side*8.1,4.0),(side*8.25,5.25),(side*5.24,7.0)],.16,.13,'armor')
  m.plate([(side*2.0,.3),(side*2.55,.75),(side*7.4,5.35),(side*6.8,5.7)],.255,.025,'yellow')
  m.box([side*7.6,.13,4.7],[.7,.38,1.5],'edge',.10)
  m.pipe([side*5.6,-.4,1.2],[side*5.6,-.4,-2.1],.19,'metal',12)
  m.pipe([side*5.6,-.4,-2.1],[side*5.6,-.4,-2.6],.24,'edge',12)
  m.box([side*5.6,-.4,3.4],[.72,.85,4.0],'slate',.17)
  # Paired armored engine nacelles, intake lips, segmented exhaust rings.
  m.box([side*2.7,-.1,4.3],[2.25,2.1,8.1],'slate',.32)
  m.box([side*2.7,1.03,3.8],[1.45,.15,6.1],'armor',.055)
  m.box([side*2.7,.1,.16],[1.56,1.18,.15],'dark',.05)
  for v in range(5):m.box([side*2.7-.60+v*.30,.1,.04],[.07,1.05,.12],'metal',.015)
  m.pipe([side*2.7,-.1,7.7],[side*2.7,-.1,8.7],1.03,'metal',20,r2=.92)
  m.pipe([side*2.7,-.1,8.71],[side*2.7,-.1,8.83],.77,'dark',20)
  m.pipe([side*2.7,-.1,8.84],[side*2.7,-.1,8.86],.59,'blue_glow',20)
  m.pipe([side*2.7,-.1,8.87],[side*2.7,-.1,8.90],.24,'white_glow',12)
  for i in range(10):
   t=i*math.tau/10;x=side*2.7+math.cos(t)*.91;y=-.1+math.sin(t)*.91
   m.beam([x,y,7.75],[x,y,8.65],.09,'edge')
  # Twin canted vertical stabilizers.
  m.hull([(side*x,y,z) for x,y,z in [(2.2,1.1,4.4),(2.4,1.1,8.1),(3.1,3.9,7.9),(3,3.7,6.8),(2.39,1.1,4.4),(2.59,1.1,8.1),(3.29,3.9,7.9),(3.19,3.7,6.8)]],'armor')
  m.beam([side*2.48,1.3,6.5],[side*3.1,3.45,7.6],.12,'yellow')
  # Service covers, fasteners and sparse geometric paint wear.
  for z in [-4,-1,2,5]:
   m.box([side*1.35,.68,z],[.11,.10,1.5],'edge',.025)
  for x,z in [(2.0,-1.1),(2.0,4.8),(4.4,1.5),(4.4,5.4),(6.0,3.0),(7.3,4.8)]:
   m.pipe([side*x,.25,z],[side*x,.29,z],.055,'metal',8)
  for i in range(45):
   x=m.rng.uniform(2,4.6);z=m.rng.uniform(1.5,5.4);length=m.rng.uniform(.025,.16)
   m.tri([side*x,.249,z],[side*(x+length),.249,z+.025],[side*x,.249,z+.045],'paint_chip')
  m.box([side*8.1,.28,4.7],[.13,.10,.35],'amber_glow',.015)
 # Faceted opaque exterior glazing and slim structural frames.
 canopy=[(-.85,1.0,-6.5),(.85,1.0,-6.5),(-1.15,1.0,-.1),(1.15,1.0,-.1),(-.55,2.0,-4.8),(.55,2.0,-4.8),(-.7,2.1,-1.3),(.7,2.1,-1.3)]
 m.hull(canopy,'glass')
 for a,b in [(0,1),(0,4),(1,5),(4,5),(4,6),(5,7),(6,7),(2,6),(3,7),(2,3),(0,2),(1,3)]:m.beam(canopy[a],canopy[b],.09,'slate')
 m.box([0,1.18,1.9],[1.15,.33,2.8],'armor',.12)
 for i in range(5):m.box([0,1.39,1.0+i*.38],[.85,.06,.13],'edge',.02)
 return m

def cockpit():
 m=Model('Kestrel_Flotte_A_Cockpit')
 tangent=math.tan(math.radians(40))
 def screen(x,y,z=1.0):return np.array([(x/1280-.5)*2*tangent*1.6*z,(.5-y/800)*2*tangent*z,-z])
 def panel(x,y,w,h,mat='edge',z=1.04,depth=.05,bevel=.015):
  p=screen(x+w/2,y+h/2,z);m.box(p,[w/1280*2*tangent*1.6*z,h/800*2*tangent*z,depth],mat,bevel)
 # Upper glare shield and angled side consoles around an open forward window.
 panel(-100,596,1480,270,'dark',1.12,.18,.04)
 for a,b in [((87,590),(7,8)),((1193,590),(1273,8))]:
  va,vb=screen(*a,.99),screen(*b,1.02);m.beam(va,vb,.045,'edge')
  offset=np.array([-.013 if a[0]<640 else .013,0,.025]);m.beam(va+offset,vb+offset,.012,'metal')
  for f in [.16,.55,.89]:
   p=va*(1-f)+vb*f;m.box(p+[0,0,.028],[.05,.026,.012],'yellow',.003)
 for side in [-1,1]:
  points=[screen(640+side*575,545,1),screen(640+side*630,575,1),screen(640+side*400,613,1)]
  m.beam(points[0],points[1],.065,'slate');m.beam(points[0],points[2],.045,'edge')
  for f in [.18,.45,.74]:
   p=points[0]*(1-f)+points[2]*f;m.pipe(p+[0,0,.027],p+[0,0,.037],.009,'metal',8)
 # Screen bezels match the controller HUD at 1280x800 / vertical FOV80.
 for x,y,w,h in [(28,600,290,178),(333,600,240,178),(588,600,217,178),(820,600,432,178)]:
  panel(x-9,y-7,w+18,h+16,'slate',1.00,.045,.014)
  panel(x-4,y-3,w+8,h+8,'rubber',.979,.025,.008)
  panel(x,y,w,h,'screen',.963,.02,.004)
  for xx in [x-4,x+w+4]:
   for yy in [y-3,y+h+3]:
    p=screen(xx,yy,.945);m.pipe(p+[0,0,-.002],p+[0,0,.006],.005,'metal',8)
 # Recessed mechanical controls on the glare shield: toggle banks, knobs, guarded buttons.
 for x in [45,77,109,141,173,1105,1137,1169,1201,1233]:
  panel(x,565,22,20,'dark',.98,.025,.004)
  p=screen(x+11,575,.951);m.pipe(p+[0,-.009,0],p+[0,.009,.012],.004,'metal',8)
  panel(x+5,560,12,3,'amber_glow' if x%3==0 else 'white_glow',.947,.006,.001)
 for x in [248,290,955,997]:
  p=screen(x,578,.96);m.pipe(p+[0,0,-.016],p+[0,0,.013],.021,'dark',16);m.pipe(p+[0,0,.014],p+[0,0,.019],.015,'metal',16)
  m.beam(p+[0,.003,.021],p+[0,.014,.021],.003,'white_glow')
 panel(560,566,160,29,'edge',.98,.04,.008)
 for i in range(4):panel(573+i*35,574,20,9,'amber_glow' if i==0 else 'white_glow',.951,.005,.001)
 # Narrow service rails and yellow safety notches at the bottom edge.
 panel(0,787,1280,26,'edge',.96,.06,.01)
 for x in [20,45,70,1190,1215,1240]:panel(x,788,11,5,'yellow',.921,.008,.001)
 return m

if __name__=='__main__':
 fighter().write('kestrel_fleet_a.glb')
 cockpit().write('cockpit_fleet_a.glb')
