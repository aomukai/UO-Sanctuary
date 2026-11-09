#!/usr/bin/env python3
"""Current linked-unit traversal/partition/predicate and fixed death functor."""
import argparse,json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import *
from probe_generic_pet_parameters import Initializer,BASE,STACK,OBJECTS,STOP
from probe_warlock_runtime import ROOT,STAGE,digest
OUT=ROOT/'.local/class-skills/pet-linked-units-source'
ACTOR=OBJECTS+0x1000;VECTOR=OBJECTS+0x2000;FDATA=OBJECTS+0x3000;HEAD=OBJECTS+0x4000;BUFFER=OBJECTS+0x5000;TEMP=OBJECTS+0x6000;ALLOC=OBJECTS+0x7000;AVT=OBJECTS+0x7100;FVT=OBJECTS+0x7200
F={k:BASE+v for k,v in dict(bucket=0x370610,owner=0x370680,death=0x392be0,destruct=0x5f320,cleanup=0x5f1b0,getAlloc=0xbaa540,memset=0x1562240,memmove=0x15624e0,fatal=0x7b6af0).items()};F.update(allocate=STOP+0x1010,free=STOP+0x1020)
def unit(i):return ACTOR if i==100 else OBJECTS+0x10000+i*0x1000
class Linked(Initializer):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.artifacts=[]
  for name,wanted in [('callees-pet-owner-loss-links-20261109.json',{'0x333e10','0x333c20'}),('callees-pet-linked-validity-20261109.json',{'0x333b60'}),('pet-death-functor-20261109.json',{'0x334050'})]:
   path=STAGE/name;capture=json.loads(path.read_text());self.artifacts.append(path)
   for n,h in capture['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
   rows=[r for r in capture['records']if r['rva']in wanted];assert len(rows)==len(wanted)
   for row in rows:
    assert row['allUnwindFragmentsCaptured']
    for f in row['fragments']:
     p=next(ROOT/n for n,h in capture['artifacts'].items()if n.endswith('.bin')and h==f['sha256']);raw=p.read_bytes();a=BASE+int(f['rva'],16);self.map(a,len(raw));self.uc.mem_write(a,raw);self.ranges.append((a,a+len(raw)));self.artifacts.append(p)
  self.map(OBJECTS,0x400000);self.map(STACK,0x40000);self.map(STOP,0x2000)
  for a in F.values():self.map(a,1);self.uc.mem_write(a,b'\xc3')
  self.uc.reg_write(UC_X86_REG_CR0,(self.uc.reg_read(UC_X86_REG_CR0)&~4)|2);self.uc.reg_write(UC_X86_REG_CR4,self.uc.reg_read(UC_X86_REG_CR4)|0x200);self.uc.hook_add(UC_HOOK_CODE,self.hook)
 def wi(self,a,v):self.uc.mem_write(a,struct.pack('<I',v&0xffffffff))
 def si(self,a):return struct.unpack('<i',self.uc.mem_read(a,4))[0]
 def ret(self,value=0):
  sp=self.uc.reg_read(UC_X86_REG_RSP);self.uc.reg_write(UC_X86_REG_RAX,value&0xffffffffffffffff);self.uc.reg_write(UC_X86_REG_RSP,sp+8);self.uc.reg_write(UC_X86_REG_RIP,self.q(sp))
 def hook(self,u,a,size,user):
  if any(lo<=a<hi for lo,hi in self.ranges):return
  c,d,v,z=[u.reg_read(r)for r in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]];name=next((n for n,x in F.items()if x==a),None);p=self.p
  if name=='bucket':assert c==1 and d==OBJECTS;self.trace.append(f'resolve:1:{v}');self.ret(HEAD);return
  if name=='owner':
   assert c==OBJECTS;self.trace.append(f'resolve:{d}:{v}');self.ret(unit(v)if (d==1 and v in [100,11,12,13])or(d==0 and v==300)else 0);return
  if name=='death':
   assert c==OBJECTS and d in [unit(11),unit(12),unit(13)]and v==0 and z==1
   target=next(i for i in [11,12,13]if unit(i)==d);self.trace.append('death:'+str(target));self.calls+=1
   if p['kill']:self.wi(d+12,0)
   if self.calls==1:
    if p['editIndex']is not None:self.wi(BUFFER+4*p['editIndex'],p['editValue'])
    if p['editCount']is not None:self.putq(VECTOR+0x90,p['editCount'])
   self.ret();return
  if name=='destruct':self.trace.append('release');self.ret();return
  if name=='cleanup':self.ret();return
  if name=='getAlloc':self.ret(ALLOC);return
  if name=='allocate':assert c==ALLOC and v==16;self.ret(TEMP);return
  if name=='free':assert c==ALLOC and d==TEMP;self.ret();return
  if name=='memset':assert d==0;u.mem_write(c,b'\0'*v);self.ret(c);return
  if name=='memmove':
   if v:u.mem_write(c,bytes(u.mem_read(d,v)))
   self.ret(c);return
  if name=='fatal':self.unsupported=True;u.emu_stop();return
  raise AssertionError(('Unknown instruction',hex(a)))
 def call(self,rva,c,d,v=0):
  sp=STACK+0x20008;self.putq(sp,STOP)
  for reg,value in [(UC_X86_REG_RSP,sp),(UC_X86_REG_RCX,c),(UC_X86_REG_RDX,d),(UC_X86_REG_R8,v)]:self.uc.reg_write(reg,value)
  self.uc.emu_start(BASE+rva,STOP,count=10000)
  if not self.unsupported:assert self.uc.reg_read(UC_X86_REG_RIP)==STOP
  return self.uc.reg_read(UC_X86_REG_RAX)
 def run(self,p):
  self.p=p;u=self.uc;u.mem_write(OBJECTS,b'\0'*0x400000);u.mem_write(STACK,b'\0'*0x40000);self.trace=[];self.calls=0;self.unsupported=False
  self.wi(ACTOR,p['kind']);self.wi(ACTOR+8,100);self.putq(ACTOR+0x10,OBJECTS+0x8000 if p['data']else 0);self.putq(OBJECTS+0x8000+0x38,VECTOR if p['ai']else 0)
  self.putq(VECTOR+0x88,BUFFER);self.putq(VECTOR+0x90,len(p['ids']));self.putq(FDATA,FVT);self.putq(FDATA+8,OBJECTS);self.putq(FVT,F['destruct']);self.putq(FVT+8,BASE+0x334050)
  self.putq(ALLOC,AVT);self.putq(AVT+8,F['allocate']);self.putq(AVT+32,F['free'])
  for i,guid in enumerate(p['ids']):self.wi(BUFFER+i*4,guid)
  self.putq(HEAD,unit(11))
  for i,t in enumerate(p['targets']):
   guid=11+i;address=unit(guid);data=address+0x400;ai=address+0x600
   self.wi(address,t['kind']);self.wi(address+8,guid);self.wi(address+12,t['mode']);self.wi(address+0x124,t['flags124']);self.wi(address+0x128,t['flags128']);self.putq(address+0x158,unit(guid+1)if i<2 else 0)
   self.putq(address+0x10,data if t['data']else 0);self.putq(data+0x38,ai if t['ai']else 0);self.putq(ai+0x60,OBJECTS if t['ownerGame']else 0);self.wi(ai+0x68,t['owner']);self.wi(ai+0x6c,t['ownerKind'])
  actor=ACTOR if p['actor']else 0;game=OBJECTS if p['game']else 0;result=None
  if p['action']=='valid':result=bool(self.call(0x333b60,actor,unit(p['candidate'])if p['candidate']else 0)&255)
  else:
   if p['action']in ['notify','both']:self.call(0x333e10,game,actor,FDATA)
   if not self.unsupported and p['action']in ['clear','both']:result=self.call(0x333c20,game,actor)&0xffffffff
  return dict(result=result,unsupported=self.unsupported,trace=self.trace.copy(),ids=[self.si(BUFFER+i*4)for i in range(len(p['ids']))],count=self.q(VECTOR+0x90),modes=[self.si(unit(i)+12)for i in [11,12,13]])
def sample(**changes):
 targets=[dict(kind=1,mode=1,flags124=0,flags128=0,data=True,ai=True,ownerGame=True,ownerKind=1,owner=owner)for owner in [100,300,100]]
 p=dict(actor=True,game=True,kind=1,data=True,ai=True,ids=[11,12,13,11,99],targets=targets,action='both',candidate=11,kill=False,editIndex=None,editValue=13,editCount=None)
 p.update(changes);return p
def build(check=False):
 em=Linked();cases=[]
 def add(**changes):p=sample(**changes);cases.append(dict(input=p,output=em.run(p)))
 for action in ['notify','clear','both']:
  for changes in [{},dict(actor=False),dict(kind=0),dict(kind=3),dict(data=False),dict(ai=False),dict(game=False),dict(ids=[])]:add(action=action,**changes)
 add(kill=True);add(editIndex=1);add(editCount=1);add(ids=[99,12]);add(ids=[11,11,13]);add(ids=[12,13,11])
 for changes in [dict(kind=0,mode=1),dict(kind=0,mode=17),dict(kind=1,mode=0),dict(kind=1,mode=12),dict(flags124=0x10000),dict(kind=2,flags128=0x4000000),dict(kind=2),dict(data=False),dict(ai=False),dict(ownerGame=False),dict(ownerKind=0),dict(owner=99)]:
  for actor in [True,False]:
   p=sample(action='valid',actor=actor);p['targets'][0].update(changes);cases.append(dict(input=p,output=em.run(p)))
 add(action='valid',candidate=0)
 OUT.mkdir(exist_ok=True);path=OUT/'cases.json';encoded=json.dumps(cases,indent=2)+'\n'
 if check:assert path.read_text()==encoded
 else:path.write_text(encoded)
 libs=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libs)==1
 result=dict(referenceVersion='3.3.93847',sourceContexts=len(cases),machineCodeBytes=sum(b-a for a,b in em.ranges),processMemoryRead=False,targetProcessCodeExecuted=False,sourceCodeExecutedInOfflineEmulator=True,
  scope='Complete notification/cleanup/validity plus fixed owner-loss Invoke over stable caller-provided vector storage. Typed registry bucket/owner lookup and recursive death effects declared;actual Invoke runs. Logical fixed-functor release and allocator/memcpy services declared;arbitrary storage relocation/destructor/allocator ABI not certified.',
  artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in sorted(set([path,Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_parameters.py',*em.artifacts,*libs]))})
 path=ROOT/'tools/reference/pet-linked-units-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert path.read_text()==encoded
 else:path.write_text(encoded)
 print(f"Linked-unit lifecycle:{len(cases)}current contexts/{result['machineCodeBytes']}bytes,including fixed recursive-death Invoke.")
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
