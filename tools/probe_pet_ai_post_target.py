#!/usr/bin/env python3
"""Execute complete post-target stage with declared helper/metadata leaves."""
import argparse,json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import *
from probe_generic_pet_parameters import Initializer,BASE,STACK,OBJECTS,STOP
from probe_warlock_runtime import ROOT,STAGE,digest
OUT=ROOT/'.local/class-skills/pet-ai-post-target-source'
ACTOR=OBJECTS+0x1000;TICK=OBJECTS+0x2000;TARGET=OBJECTS+0x3000;NEXT=OBJECTS+0x4000;A=OBJECTS+0x5000;B=OBJECTS+0x6000;ROOM=OBJECTS+0x7000;MID=OBJECTS+0x8000;LEVEL=OBJECTS+0x9000
FUNCTIONS={k:BASE+v for k,v in dict(first=0x43a8e0,second=0x43aa60,primary=0x6bc90,secondary=0x6bd00,room=0x278450,level=0x2993a0,special=0x40c450,search=0x40d9f0,wander=0x425e80,cookie=0x155eeb0).items()}
class Post(Initializer):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.artifacts=[]
  path=STAGE/'callees-pet-ai-entry-core-20261109.json';capture=json.loads(path.read_text());self.artifacts.append(path)
  for n,h in capture['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
  row=next(r for r in capture['records']if r['rva']=='0x43af30');assert row['allUnwindFragmentsCaptured']and row['bytes']==390
  for f in row['fragments']:
   p=next(ROOT/n for n,h in capture['artifacts'].items()if n.endswith('.bin')and h==f['sha256']);raw=p.read_bytes();a=BASE+int(f['rva'],16);self.map(a,len(raw));self.uc.mem_write(a,raw);self.ranges.append((a,a+len(raw)));self.artifacts.append(p)
  self.map(OBJECTS,0x10000);self.map(STACK,0x40000);self.map(STOP,0x1000);self.map(BASE+0x197a178,8)
  for a in FUNCTIONS.values():self.map(a,1);self.uc.mem_write(a,b'\xc3')
  self.uc.hook_add(UC_HOOK_CODE,self.hook)
 def wi(self,a,v):self.uc.mem_write(a,struct.pack('<I',v&0xffffffff))
 def wb(self,a,v):self.uc.mem_write(a,bytes([v]))
 def si(self,a):return struct.unpack('<i',self.uc.mem_read(a,4))[0]
 def ret(self,value=0):
  sp=self.uc.reg_read(UC_X86_REG_RSP);self.uc.reg_write(UC_X86_REG_RAX,value&0xffffffffffffffff);self.uc.reg_write(UC_X86_REG_RSP,sp+8);self.uc.reg_write(UC_X86_REG_RIP,self.q(sp))
 def target(self,a):return 7 if a==TARGET else 8 if a==NEXT else 0
 def hook(self,u,a,size,user):
  if any(lo<=a<hi for lo,hi in self.ranges):return
  c,d,v,z=[u.reg_read(r)for r in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]];sp=u.reg_read(UC_X86_REG_RSP);p=self.p
  name=next((n for n,x in FUNCTIONS.items()if x==a),None);signed=lambda x:(x+2**31)%2**32-2**31;actor=ACTOR if p['actor']else 0
  if name=='cookie':self.ret(u.reg_read(UC_X86_REG_RAX));return
  if name in ['first','second']:
   assert c==OBJECTS and d==actor and v==TICK;self.trace.append(name)
   if name=='first'and p['clearFirst']:self.putq(TICK+0x10,0)
   if name=='second'and p['changeSecond']:self.putq(TICK+0x10,NEXT);self.wi(TICK+0x20,80)
   self.ret(int(p[name]));return
  if name in ['primary','secondary']:
   self.trace.append(f'{name}:{c}:{signed(d)}')
   if name=='primary'and p['changeLookup']:self.wb(OBJECTS+0x106,2);self.wi(ACTOR+4,743)
   self.ret((A if name=='primary'else B)if p[name]else 0);return
  if name=='room':assert c==actor;self.trace.append('room');self.ret(ROOM if p['room']else 0);return
  if name=='level':self.trace.append(f'level:{c}:{signed(d)}');self.ret(LEVEL if p['level']else 0);return
  if name=='special':assert c==actor;self.trace.append(f'special:{self.target(d)}');self.ret(p['special']);return
  if name=='search':
   assert c==OBJECTS and d==actor and z==0 and self.si(sp+0x28)==9 and self.q(v)==0 and self.si(v+0x18)==0x7fffffff
   self.trace.append(f'search:9:{self.target(self.q(v+0x28))}:2147483647')
   self.putq(v,NEXT if p['found']else 0);self.wi(v+0x18,p['foundDistance']);self.ret();return
  if name=='wander':assert c==OBJECTS and d==actor and v&255==4;self.trace.append('wander:4');self.ret();return
  raise AssertionError(('Undeclared instruction',hex(a)))
 def run(self,p):
  self.p=p;self.trace=[];u=self.uc;u.mem_write(OBJECTS,b'\0'*0x10000);u.mem_write(STACK,b'\0'*0x40000)
  self.wb(OBJECTS+0x106,1);self.wi(ACTOR+4,742);self.putq(TICK+0x10,TARGET if p['target']else 0);self.wi(TICK+0x20,p['distance']);self.wi(TICK+0x24,37)
  self.wb(A+0x3c,p['primaryFlags']);self.wb(B+0xf0,p['secondaryFlags']);self.putq(ROOM+0x18,MID);self.putq(MID+0x90,LEVEL);self.wi(LEVEL+0x1f8,40);self.wb(LEVEL+0x3f,p['walk'])
  sp=STACK+0x20008;self.putq(sp,STOP)
  for r,value in [(UC_X86_REG_RSP,sp),(UC_X86_REG_RCX,OBJECTS),(UC_X86_REG_RDX,ACTOR if p['actor']else 0),(UC_X86_REG_R8,TICK)]:u.reg_write(r,value)
  failed=False
  try:u.emu_start(BASE+0x43af30,STOP,count=10000)
  except unicorn.UcError as error:
   if u.reg_read(UC_X86_REG_RIP)==BASE+0x43b016:failed=True
   else:raise
  if not failed:assert u.reg_read(UC_X86_REG_RIP)==STOP
  return dict(result=None if failed else bool(u.reg_read(UC_X86_REG_RAX)&0xffffffff),unsupported=failed,target=self.target(self.q(TICK+0x10)),distance=self.si(TICK+0x20),combat=self.si(TICK+0x24),trace=self.trace.copy())
def sample(**changes):
 p=dict(actor=True,target=True,first=False,second=False,clearFirst=False,changeSecond=False,primary=True,secondary=True,changeLookup=False,primaryFlags=2,secondaryFlags=4,room=True,level=True,walk=10,distance=20,special=0,found=True,foundDistance=6);p.update(changes);return p
def build(check=False):
 em=Post();cases=[]
 def add(**changes):p=sample(**changes);cases.append(dict(input=p,output=em.run(p)))
 for changes in [dict(),dict(target=False),dict(first=True),dict(second=True),dict(primary=False),dict(primaryFlags=0),dict(secondary=False),dict(secondaryFlags=0),dict(room=False),dict(level=False),dict(walk=0),dict(distance=0),dict(distance=10),dict(distance=11),dict(distance=-1),dict(distance=-2147483648),dict(distance=2147483647),dict(walk=255,distance=255),dict(walk=255,distance=256),dict(special=1),dict(special=-1),dict(found=False),dict(foundDistance=-1),dict(foundDistance=2147483647),dict(actor=False),dict(changeLookup=True),dict(clearFirst=True),dict(changeSecond=True),dict(clearFirst=True,second=True),dict(changeLookup=True,found=False)]:add(**changes)
 OUT.mkdir(exist_ok=True);path=OUT/'cases.json';text=json.dumps(cases,indent=2)+'\n'
 if check:assert path.read_text()==text
 else:path.write_text(text)
 libs=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libs)==1
 result=dict(referenceVersion='3.3.93847',sourceBytes=390,sourceContexts=len(cases),sourceCodeExecutedInOfflineEmulator=True,processMemoryRead=False,targetProcessCodeExecuted=False,scope='Complete post-target orchestration against explicit first/second helper callbacks,metadata,room graph,special predicate,descriptor9search and wander leaves. Only caller-initialized search fields are qualified;unwritten bytes and helper internals are not inferred.',artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in sorted(set([path,Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_parameters.py',*em.artifacts,*libs]))})
 path=ROOT/'tools/reference/pet-ai-post-target-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
 if check:assert path.read_text()==text
 else:path.write_text(text)
 print(f'AI post-target:{len(cases)}current contexts/390bytes;early exits,lookup order,unsigned range and retarget/wander.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
