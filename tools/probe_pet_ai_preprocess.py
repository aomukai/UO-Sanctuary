#!/usr/bin/env python3
"""Replay complete current AI preprocessing and saved-target handler together."""
import argparse,json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import *
from probe_generic_pet_parameters import Initializer,BASE,STACK,OBJECTS,STOP
from probe_warlock_runtime import ROOT,STAGE,digest
OUT=ROOT/'.local/class-skills/pet-ai-preprocess-source'
ACTOR=OBJECTS+0x1000;DATA=OBJECTS+0x2000;PATH=OBJECTS+0x3000;MOVE=OBJECTS+0x4000;CTRL=OBJECTS+0x5000;TICK=OBJECTS+0x6000;TARGET=OBJECTS+0x7000;TPATH=OBJECTS+0x8000;ROW=OBJECTS+0x9000;DOOR=OBJECTS+0xa000;TABLE=OBJECTS+0xb000;STATS=OBJECTS+0xc000
FUNCTIONS={k:BASE+v for k,v in dict(walk=0x6bd00,resolve=0x370680,distance=0x40c060,toward=0x425bb0,targetMove=0x4263a0,collision=0x2a5bf0,search=0x40d9f0,doorMetadata=0x295890,canInteract=0x27de50,interact=0x3e5570,idle=0x425550,cookie=0x155eeb0,assertion=0x7b6af0).items()}
class Preprocess(Initializer):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.artifacts=[]
  for name,rva in [('callees-pet-ai-entry-core-20261109.json','0x43a6c0'),('callees-pet-ai-preprocess-leaf-20261109.json','0x43a4f0')]:
   path=STAGE/name;capture=json.loads(path.read_text());self.artifacts.append(path)
   for n,h in capture['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
   row=next(r for r in capture['records']if r['rva']==rva);assert row['allUnwindFragmentsCaptured']
   for f in row['fragments']:
    p=next(ROOT/n for n,h in capture['artifacts'].items()if n.endswith('.bin')and h==f['sha256']);raw=p.read_bytes();a=BASE+int(f['rva'],16);self.map(a,len(raw));self.uc.mem_write(a,raw);self.ranges.append((a,a+len(raw)));self.artifacts.append(p)
  self.map(OBJECTS,0x10000);self.map(STACK,0x40000);self.map(STOP,0x1000);self.map(BASE+0x1e03ac0,64);self.map(BASE+0x197a178,8)
  for a in FUNCTIONS.values():self.map(a,1);self.uc.mem_write(a,b'\xc3')
  self.uc.hook_add(UC_HOOK_CODE,self.hook)
 def wi(self,a,v):self.uc.mem_write(a,struct.pack('<I',v&0xffffffff))
 def wb(self,a,v):self.uc.mem_write(a,bytes([v]))
 def si(self,a):return struct.unpack('<i',self.uc.mem_read(a,4))[0]
 def sw(self,a,v):self.uc.mem_write(a,struct.pack('<H',v&65535))
 def ret(self,value=0):
  sp=self.uc.reg_read(UC_X86_REG_RSP);self.uc.reg_write(UC_X86_REG_RAX,value&0xffffffffffffffff);self.uc.reg_write(UC_X86_REG_RSP,sp+8);self.uc.reg_write(UC_X86_REG_RIP,self.q(sp))
 def hook(self,u,a,size,user):
  if any(lo<=a<hi for lo,hi in self.ranges):return
  c,d,v,z=[u.reg_read(r)for r in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]];sp=u.reg_read(UC_X86_REG_RSP);p=self.p
  name=next((n for n,x in FUNCTIONS.items()if x==a),None);signed=lambda x:(x+2**31)%2**32-2**31
  actor=ACTOR if p['actor']else 0
  if name=='cookie':self.ret(u.reg_read(UC_X86_REG_RAX));return # Compiler cookie epilogue preserves the result register.
  if name=='assertion':self.failed=True;u.emu_stop();return
  if name=='walk':
   self.trace.append(f'walk:{c}:{signed(d)}')
   if p['lateId']is not None:self.wi(CTRL+0x3c,p['lateId'])
   if p['lateKind']is not None:self.wi(CTRL+0x40,p['lateKind'])
   self.ret(ROW if p['walkRow']else 0);return
  if name=='resolve':
   assert c==OBJECTS;self.trace.append(f'resolve:{signed(d)}:{signed(v)}');self.ret(TARGET if p['target']else 0);return
  if name=='distance':assert c==actor and d==TARGET;self.trace.append('distance');self.ret(p['distance']);return
  if name=='toward':
   assert c==OBJECTS and d==actor and u.mem_read(sp+0x28,1)[0]==19 and self.si(sp+0x30)==0 and self.si(sp+0x38)&65535==4
   self.trace.append(f'toward:{signed(v)}:{signed(z)}:19:0:4');self.ret(p['toward']);return
  if name=='targetMove':
   assert c==OBJECTS and d==actor and v==TARGET and z&255==19 and self.si(sp+0x28)&65535==0
   self.trace.append('targetMove:19:0');self.ret();return
  if name=='collision':
   assert c==33 and self.si(sp+0x28)==0x7fff
   self.trace.append(f'collision:{d}:{v}:{signed(z)}:32767');self.ret(p['collisionResult']);return
  if name=='search':
   assert c==OBJECTS and d==actor and z==0 and self.si(sp+0x28)==7 and self.si(v+0x18)==9 and self.q(v)==0
   self.trace.append('doorSearch:7:9');self.putq(v,DOOR if p['door']else 0);self.ret();return
  if name=='doorMetadata':self.trace.append(f'doorMetadata:{c}:{signed(d)}');self.ret(ROW if p['doorRow']else 0);return
  if name=='canInteract':assert c==actor and d==DOOR;self.trace.append('canInteract');self.ret(int(p['interact']));return
  if name=='interact':assert c==OBJECTS and d==actor and v==DOOR;self.trace.append('interact');self.ret();return
  if name=='idle':assert c==OBJECTS and d==actor;self.trace.append(f'idle:{v}');self.ret();return
  raise AssertionError(('Undeclared instruction',hex(a)))
 def run(self,p):
  self.p=p;self.trace=[];self.failed=False;u=self.uc;u.mem_write(OBJECTS,b'\0'*0x10000);u.mem_write(STACK,b'\0'*0x40000)
  self.wb(OBJECTS+0x106,1);self.wi(ACTOR,p['kind']);self.wi(ACTOR+4,742);self.wb(ACTOR+0x1bd,p['selector']);self.putq(ACTOR+0x10,DATA if p['data']else 0);self.putq(DATA+0x40,MOVE if p['movement']else 0);self.wi(MOVE+0x20,9);self.wb(MOVE+0x28,7)
  self.putq(ACTOR+0x38,PATH if p['path']else 0);self.putq(PATH+0x20,33);self.sw(PATH+2,p['pathX']);self.sw(PATH+6,p['pathY']);self.wi(PATH+0x58,14);self.wi(PATH+0xa0,p['pathActive']);self.sw(PATH+0x68,p['collision'])
  self.putq(ACTOR+0x88,STATS if p['hasRoot']else 0);self.wi(STATS+0x1c,p['root1c']);self.wi(STATS+0xb30,p['rootb30'])
  for s in [1,2,3]:self.putq(BASE+0x1e03ac0+16*s,TABLE);self.wi(TABLE+0x2d0,p['count'])
  self.putq(TICK,CTRL if p['control']else 0);self.putq(TICK+0x28,ROW if p['primary']else 0);self.wi(CTRL+0x3c,p['id']);self.wi(CTRL+0x40,p['targetKind']);self.wb(ROW+0x3c,p['flags']);self.wb(ROW+0xf0,4 if p['walk']else 0);self.wb(ROW+0x12d,int(p['usable']))
  self.wi(TARGET,p['candidateKind']);self.wi(TARGET+0xc,p['mode']);self.wi(TARGET+0x124,p['flags124']);self.wi(TARGET+0x128,p['flags128']);self.putq(TARGET+0x38,TPATH if p['targetPath']else 0)
  self.sw(TPATH+2,p['x']);self.sw(TPATH+6,p['y']);self.wi(TPATH+0x10,p['x']);self.wi(TPATH+0x14,p['y']);self.wi(DOOR+4,5)
  sp=STACK+0x20008;self.putq(sp,STOP)
  for r,value in [(UC_X86_REG_RSP,sp),(UC_X86_REG_RCX,OBJECTS),(UC_X86_REG_RDX,ACTOR if p['actor']else 0),(UC_X86_REG_R8,TICK)]:u.reg_write(r,value)
  try:u.emu_start(BASE+(0x43a4f0 if p['direct']else 0x43a6c0),STOP,count=10000)
  except unicorn.UcError as error:
   pc=u.reg_read(UC_X86_REG_RIP)
   # Explicit source dereferences,which the managed binding refuses.
   if pc in [BASE+x for x in [0x43a768,0x43a788,0x43a807,0x43a50b,0x43a5e7,0x43a695,0x43a61a]]:self.failed=True
   else:raise AssertionError((hex(pc),p))from error
  if not self.failed:assert u.reg_read(UC_X86_REG_RIP)==STOP
  return dict(result=None if self.failed else bool(u.reg_read(UC_X86_REG_RAX)&0xffffffff),unsupported=self.failed,id=self.si(CTRL+0x3c),targetKind=self.si(CTRL+0x40),word20=self.si(MOVE+0x20),byte28=u.mem_read(MOVE+0x28,1)[0],collision=struct.unpack('<H',u.mem_read(PATH+0x68,2))[0],trace=self.trace.copy())
def sample(**changes):
 p=dict(direct=False,actor=True,kind=1,data=True,movement=True,path=True,selector=1,count=232,hasRoot=True,root1c=-1,rootb30=0,control=True,primary=True,flags=0,pathX=65537,pathY=-1,pathActive=0,collision=0,collisionResult=0,door=False,doorRow=True,usable=True,interact=True,id=-1,targetKind=0,walkRow=True,walk=True,target=True,candidateKind=0,mode=1,flags124=0,flags128=0,distance=1,x=70000,y=-2,targetPath=True,toward=1,lateId=None,lateKind=None);p.update(changes);return p
def build(check=False):
 em=Preprocess();cases=[]
 def add(**changes):p=sample(**changes);cases.append(dict(input=p,output=em.run(p)))
 add()
 for changes in [dict(rootb30=0x200000),dict(rootb30=0x200000,count=21),dict(rootb30=0x200000,root1c=0),dict(rootb30=0x200000,hasRoot=False),dict(rootb30=0x200000,kind=0),dict(rootb30=0x200000,kind=3),dict(rootb30=0x200000,kind=2),dict(selector=0),dict(primary=False),dict(control=False)]:add(**changes)
 for changes in [dict(),dict(collisionResult=0x800),dict(collisionResult=0x800,door=True),dict(collisionResult=0x800,door=True,interact=False),dict(collisionResult=0x800,door=True,usable=False),dict(collisionResult=0x800,door=True,doorRow=False),dict(pathActive=1,collision=0x800,door=True),dict(pathActive=1,collision=0,collisionResult=0x800,door=True),dict(path=False),dict(kind=5),dict(actor=False),dict(collisionResult=0x800,door=True,usable=False,id=42,distance=21)]:add(flags=8,**changes)
 for changes in [dict(),dict(walkRow=False),dict(walk=False),dict(target=False),dict(flags124=0x10000),dict(candidateKind=0,mode=0),dict(candidateKind=0,mode=17),dict(candidateKind=1,mode=0),dict(candidateKind=1,mode=12),dict(candidateKind=1,mode=1),dict(candidateKind=2),dict(candidateKind=3,flags128=0x4000000),dict(distance=2),dict(distance=20),dict(distance=21),dict(distance=-1),dict(targetKind=1),dict(toward=0),dict(candidateKind=4),dict(targetPath=False),dict(candidateKind=2,targetPath=False),dict(movement=False),dict(data=False),dict(kind=0),dict(lateId=77,lateKind=1),dict(actor=False)]:add(id=42,**changes)
 add(direct=True,id=42,distance=21);add(direct=True,id=42,distance=21,movement=False)
 OUT.mkdir(exist_ok=True);path=OUT/'cases.json';text=json.dumps(cases,indent=2)+'\n'
 if check:assert path.read_text()==text
 else:path.write_text(text)
 libs=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libs)==1
 result=dict(referenceVersion='3.3.93847',preprocessBytes=544,savedTargetBytes=464,sourceContexts=len(cases),sourceCodeExecutedInOfflineEmulator=True,processMemoryRead=False,targetProcessCodeExecuted=False,scope='Complete preprocessing and saved-target handling execute together. Status/path/object/registry and movement calls are explicit leaves;source writes,lookup order,coordinate decoding and action arguments are compared. Missing required dereferences are refused;birth/persistence/world projections are separate.',artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in sorted(set([path,Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_parameters.py',*em.artifacts,*libs]))})
 path=ROOT/'tools/reference/pet-ai-preprocess-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
 if check:assert path.read_text()==text
 else:path.write_text(text)
 print(f'AI preprocessing:{len(cases)}current contexts/1008bytes;stun,door and saved-target paths.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
