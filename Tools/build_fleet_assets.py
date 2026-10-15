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

def engine(m,x,y,z,r=1,enemy=False):
 """Recessed exhaust with a dark collar and a smaller emissive core."""
 m.pipe([x,y,z-.7*r],[x,y,z],r,'metal',16,r2=r*.92)
 m.pipe([x,y,z+.01],[x,y,z+.06*r],r*.76,'dark',16)
 m.pipe([x,y,z+.07*r],[x,y,z+.09*r],r*.54,'red_glow' if enemy else 'blue_glow',16)

def canopy(m,width,height,z0,z1):
 pts=[(-width*.8,.7,z0),(width*.8,.7,z0),(-width,.7,z1),(width,.7,z1),
      (-width*.5,height,z0+1),(width*.5,height,z0+1),(-width*.65,height,z1-1),(width*.65,height,z1-1)]
 m.hull(pts,'glass')
 for a,b in [(0,1),(0,4),(1,5),(4,5),(4,6),(5,7),(6,7),(2,6),(3,7),(2,3),(0,2),(1,3)]:m.beam(pts[a],pts[b],.11,'edge')

def vanguard():
 m=Model('SF24_Vanguard_Flotte_A')
 m.hull([(-.5,-.5,-11),(.5,-.5,-11),(-.4,.3,-11),(.4,.3,-11),
         (-2.1,-1.1,-1),(2.1,-1.1,-1),(-1.7,1,-1),(1.7,1,-1),
         (-2,-1,9),(2,-1,9),(-1.7,1.1,9),(1.7,1.1,9)],'armor_light')
 canopy(m,1.15,2.2,-7,-.4)
 m.box([0,-1.05,-1],[2,.5,16],'slate',.16)
 for side in [-1,1]:
  # Broad diamond wing planform, distinct from the narrow Kestrel.
  m.plate([(side*1.4,-5),(side*3.6,-3.4),(side*10.4,3.2),(side*6.5,8),(side*1.5,7)],0,.7,'slate')
  m.plate([(side*2,-3.9),(side*3.3,-2.8),(side*9.5,3.2),(side*6.1,7.2),(side*2,6.5)],.4,.12,'armor')
  m.plate([(side*6.9,1.2),(side*7.45,1.7),(side*7.4,5.6),(side*6.9,6.2)],.49,.025,'yellow')
  m.box([side*3.5,0,4.4],[2.8,2.5,9.3],'edge',.4)
  m.box([side*3.5,1.28,3.7],[2.1,.18,6.9],'armor_light',.06)
  m.box([side*3.5,.1,-.35],[1.8,1.4,.12],'dark',.03)
  for i in range(5):m.box([side*3.5-.7+i*.35,.1,-.43],[.09,1.1,.15],'metal',.02)
  engine(m,side*3.5,0,9.5,1.2)
  m.box([side*6.3,-.6,1.1],[1,.9,4.4],'slate',.18)
  m.pipe([side*6.3,-.6,-1],[side*6.3,-.6,-5],.23,'metal',12)
  m.box([side*2.8,-1.5,2.5],[1.2,1.1,5],'edge',.18)
  for z in [1,3.3]:m.box([side*2.8,-2.07,z],[.8,.07,1.6],'dark',.02)
  for z in [1,2,3,4]:m.box([side*2,.95,z],[.5,.1,.25],'dark',.025)
  m.box([side*9.2,.5,3.3],[.17,.1,.5],'amber_glow',.02)
 m.hull([(-.18,1,3),(.18,1,3),(-.16,1,8.5),(.16,1,8.5),(-.1,4,7),(.1,4,7),(-.1,3.7,5.8),(.1,3.7,5.8)],'slate')
 m.beam([0,1.3,3.7],[0,3.6,6.3],.15,'yellow')
 return m

def atlas():
 m=Model('SB8_Atlas_Flotte_A')
 # Blunt armored center hull and a broad twin-pod bomber silhouette.
 m.hull([(-1.8,-.9,-12),(1.8,-.9,-12),(-1.3,.8,-12),(1.3,.8,-12),
         (-2.8,-1.5,-3),(2.8,-1.5,-3),(-2.4,1.3,-3),(2.4,1.3,-3),
         (-2.5,-1.5,10),(2.5,-1.5,10),(-2.2,1.3,10),(2.2,1.3,10)],'armor_light')
 canopy(m,1.55,2.7,-9,-2.5)
 m.box([0,-1.6,0],[3.4,.7,18],'edge',.2)
 for side in [-1,1]:
  m.plate([(side*2,-4),(side*12,-1),(side*12,6),(side*3,9)],-.1,1.1,'slate')
  m.plate([(side*3,-3),(side*10.8,-.5),(side*10.8,5.5),(side*3,7.8)],.52,.18,'armor')
  m.box([side*8.1,.3,2.1],[4.1,3.7,16.8],'slate',.6)
  for z in [-3.6,.4,4.4]:m.box([side*8.1,2.2,z],[3.3,.2,3.6],'armor_light',.07)
  m.box([side*8.1,.4,-6.4],[2.8,2,.2],'dark',.06)
  for i in range(5):m.box([side*8.1-1.1+i*.55,.4,-6.55],[.1,1.7,.18],'metal',.02)
  engine(m,side*8.1,.2,11.1,1.7)
  # Visible paired torpedo bays, with recessed armored doors.
  m.box([side*4.4,-1.7,1.2],[2.5,2.5,12.8],'edge',.3)
  m.box([side*4.4,-3,1.2],[2.1,.12,11.3],'dark',.04)
  for z in [-2.5,1.2,4.9]:
   m.box([side*4.4,-3.1,z],[1.8,.12,3.3],'armor',.04)
   m.box([side*4.4,-3.18,z],[.1,.03,2.9],'yellow',.01)
  m.pipe([side*2.3,-.6,-8],[side*2.3,-.6,-13],.27,'metal',12)
  m.hull([(side*x,y,z) for x,y,z in [(8,2,4),(8.3,2,10),(9.4,5,9),(9,4.5,6),(8.2,2,4),(8.5,2,10),(9.6,5,9),(9.2,4.5,6)]],'armor')
  m.box([side*8.1,2.36,-3],[2,.07,.7],'yellow',.02)
  for z in [1,2,3,4]:m.box([side*1.8,1.42,z],[.65,.12,.35],'dark',.03)
 m.box([0,1.5,3],[2,.4,5],'slate',.12)
 return m

def raider():
 m=Model('Raider_Flotte_A')
 m.hull([(0,0,-10),(-1.6,-1,0),(1.6,-1,0),(-1.4,1,0),(1.4,1,0),(-1.5,-.8,8),(1.5,-.8,8),(-1.3,.8,8),(1.3,.8,8)],'enemy_armor')
 canopy(m,.9,1.65,-5,0)
 for side in [-1,1]:
  # Forward swept hooked wings and downward fins identify the hostile faction.
  m.plate([(side*1.1,1),(side*4.5,-2),(side*8.5,-7),(side*9.6,-5.4),(side*7.5,4),(side*1.3,6)],-.2,.65,'enemy_dark')
  m.plate([(side*2.1,1),(side*5,-1.6),(side*8.5,-5.8),(side*7,3),(side*2.1,5)],.17,.12,'enemy_armor')
  m.beam([side*7.8,.28,-4.7],[side*6.8,.28,1.6],.15,'enemy_mark')
  m.box([side*2.3,0,4.3],[2.2,2.4,7],'enemy_dark',.35)
  engine(m,side*2.3,0,8.2,1.05,True)
  m.pipe([side*7.8,-.4,-3],[side*7.8,-.4,-7.6],.21,'metal',12)
  m.hull([(side*x,y,z) for x,y,z in [(3,0,4),(3,0,7),(4,-3.2,6),(4.2,-3,5),(3.2,0,4),(3.2,0,7),(4.2,-3.2,6),(4.4,-3,5)]],'enemy_armor')
  for z in [1,2,3]:m.box([side*1.4,1,z],[.45,.12,.3],'enemy_mark',.02)
 return m

def carrier(enemy=False):
 m=Model('RNS_Tyrant_Flotte_A' if enemy else 'SFS_Resolute_Flotte_A')
 armor='enemy_armor' if enemy else 'armor';light='enemy_armor' if enemy else 'armor_light';dark='enemy_dark' if enemy else 'edge';mark='enemy_mark' if enemy else 'yellow';glow='red_glow' if enemy else 'blue_glow'
 # Full-length open launch deck stays below the existing launch and landing path.
 m.hull([(-38,-48,-288),(38,-48,-288),(-53,17,-288),(53,17,-288),
         (-73,-55,170),(73,-55,170),(-74,19,170),(74,19,170),
         (-55,-36,284),(55,-36,284),(-56,18,284),(56,18,284)],armor)
 m.box([0,23,-5],[102,6,490],'dark',1.5)
 for z in range(-220,231,30):
  m.box([0,26.15,z],[1.4,.12,12],'white_glow',.02)
  for side in [-1,1]:m.box([side*34,26.2,z],[9,.13,1.4],mark,.04)
 for side in [-1,1]:
  # Segmented armored outriggers, broken by recessed service seams.
  m.box([side*82,-1,27],[39,65,458],dark,6)
  for z in range(-170,221,65):
   m.box([side*82,31,z],[33,4,58],light,1)
   m.box([side*102,3,z],[3,43,57],armor,1)
   m.box([side*104,3,z],[1.2,21,35],'dark',.5)
   for dz in [-10,0,10]:m.box([side*105,3,z+dz],[1,16,2],'metal',.2)
  m.box([side*48,27,0],[1.4,1,436],glow,.15)
  for z in range(-195,231,35):m.box([side*54,28,z],[3,2,6],'white_glow',.3)
  for z in [-140,15,160]:
   m.box([side*116,15,z],[27,10,32],dark,2)
   m.pipe([side*116,20,z],[side*116,28,z],7,armor,12)
   m.box([side*116,30,z],[15,6,16],armor,1)
   for x in [-4,4]:m.pipe([side*116+x,30,z-5],[side*116+x,30,z-22],1.2,'metal',8)
  for y in [-26,1]:engine(m,side*36,y,286,12,enemy)
  for z in [-225,240]:m.box([side*81,34,z],[24,1,6],mark,.3)
 # Starboard island, armored command bridge, sensor mast and antenna bars.
 m.box([75,53,98],[42,45,103],dark,4)
 m.hull([(51,69,37),(99,69,37),(55,103,65),(95,103,65),(51,69,145),(99,69,145),(55,103,133),(95,103,133)],light)
 m.box([75,88,48],[35,5,2],glow,.3)
 for x in [61,68,75,82,89]:m.box([x,88,46.8],[.9,5.2,.8],dark,.1)
 m.box([75,104,100],[24,3,45],armor,.5)
 m.pipe([78,105,108],[78,145,108],1.5,'metal',8)
 m.beam([58,132,108],[98,132,108],2,dark)
 for x in [58,98]:m.box([x,135,108],[2,8,5],glow,.4)
 m.box([0,3,286],[43,25,2],'dark',.5)
 for side in [-1,1]:m.box([side*23,3,287],[1,22,1],mark,.1)
 # Hostile generator pedestals exactly match Game.Activate's subsystem mounts.
 if enemy:
  m.hull([(-38,-48,-280),(38,-48,-280),(-53,17,-280),(53,17,-280),
          (-12,-25,-330),(12,-25,-330),(-12,8,-330),(12,8,-330)],armor)
  for side in [-1,1]:
   m.plate([(side*98,40),(side*151,110),(side*133,213),(side*98,235)],-8,16,dark)
   m.plate([(side*102,53),(side*141,113),(side*126,201),(side*102,221)],1,2,armor)
   m.beam([side*107,3,80],[side*137,3,117],2,mark)
   m.beam([side*91,23,-110],[side*125,54,-110],10,dark)
   m.pipe([side*125,48,-110],[side*125,60,-110],15,armor,16)
   m.pipe([side*125,60,-110],[side*125,61,-110],12,glow,16)
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
 PALETTE.update({'enemy_armor':('65515b',.25,.65),'enemy_dark':('27252e',.25,.67),
                 'enemy_mark':('bd6a53',.2,.65),'red_glow':('ed7259',0,.5)})
 fighter().write('kestrel_fleet_a.glb')
 cockpit().write('cockpit_fleet_a.glb')
 vanguard().write('vanguard_fleet_a.glb')
 atlas().write('atlas_fleet_a.glb')
 raider().write('raider_fleet_a.glb')
 carrier().write('resolute_fleet_a.glb')
 carrier(True).write('tyrant_fleet_a.glb')
