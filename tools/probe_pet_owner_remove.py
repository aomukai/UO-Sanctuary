#!/usr/bin/env python3
"""Current node unlink/free/counter callback composed with owner detachment."""
import argparse,json,struct
from pathlib import Path
import unicorn
from unicorn.x86_const import *
from probe_pet_owner_detach import Detach,sample as detach_sample,PET,OWNER,DATA,STORE,SLOTS,NODES,F as BASE_F
from probe_generic_pet_parameters import BASE,OBJECTS,STACK,STOP
from probe_warlock_runtime import ROOT,STAGE,digest
OUT=ROOT/'.local/class-skills/pet-owner-remove-source';HEAD=OBJECTS+0xd000;SHARED=OBJECTS+0xe000;FREEVT=OBJECTS+0xf000
EXTRA={k:BASE+v for k,v in dict(bucket=0x370610,special=0x35b450,prepareFree=0x7c63e0).items()};EXTRA['free']=STOP+0x1020
class Remove(Detach):
 def __init__(self):
  super().__init__();self.recording=False
  path=STAGE/'callees-pet-detach-effects-20261109.json';cap=json.loads(path.read_text());self.artifacts.append(path)
  for n,h in cap['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
  row=next(r for r in cap['records']if r['rva']=='0x35c720');assert row['bytes']==578 and row['allUnwindFragmentsCaptured']
  for f in row['fragments']:
   path=next(ROOT/n for n,h in cap['artifacts'].items()if n.endswith('.bin')and h==f['sha256']);raw=path.read_bytes();a=BASE+int(f['rva'],16);self.map(a,len(raw));self.uc.mem_write(a,raw);self.ranges.append((a,a+len(raw)));self.artifacts.append(path)
  for a in EXTRA.values():self.map(a,1);self.uc.mem_write(a,b'\xc3')
  self.map(BASE+0x1cf9f70,8)
 def hook(self,u,a,size,user):
  if not getattr(self,'recording',False):return super().hook(u,a,size,user)
  if any(lo<=a<hi for lo,hi in self.ranges):return
  c,d,v,z=[u.reg_read(r)for r in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]];name=next((n for n,x in EXTRA.items()if x==a),None)
  if name=='bucket':assert c==1 and d==OBJECTS;self.trace.append('resolve:'+str((v+2**31)%2**32-2**31));self.ret(HEAD if self.p['lookup']else 0);return
  if name=='special':self.trace.append('special:'+str((d+2**31)%2**32-2**31));self.ret();return
  if name=='prepareFree':self.ret();return
  if name=='free':
   assert c==BASE+0x1cf9f70;index=(d-NODES)//32+1;assert 1<=index<=len(self.p['ids']);self.freed.append(index);self.trace.append('free:'+str(index));self.ret();return
  return super().hook(u,a,size,user)
 def run_case(self,p):
  self.recording=False;super().run(dict(p,action='packed',game=True));self.p=p;self.trace=[];self.unsupported=False;self.freed=[];self.applyFlags=None
  root=SLOTS+p['type']*32;self.wi(root+8,p['rootCount']);self.wi(SHARED+8,p['sharedCount']);self.putq(root+16,root if p['alias']else SHARED if p['shared']else 0)
  self.putq(HEAD,PET);self.putq(PET+0x158,0);self.wi(PET+0x124,p['unitFlags']);self.putq(BASE+0x1cf9f70,FREEVT);self.putq(FREEVT+32,EXTRA['free'])
  self.recording=True;sp=STACK+0x20008;self.putq(sp,STOP)
  args=[OBJECTS if p['game']else 0,OWNER if p['owner']else 0,p['id'],p['special']]if p['action']=='remove'else[OBJECTS if p['game']else 0,OWNER if p['owner']else 0,PET if p['pet']else 0,0]
  for reg,value in zip([UC_X86_REG_RSP,UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9],[sp,*args]):self.uc.reg_write(reg,value&0xffffffffffffffff)
  try:self.uc.emu_start(BASE+(0x35c720 if p['action']=='remove'else 0x35c970),STOP,count=20000)
  except unicorn.UcError:assert self.unsupported
  if not self.unsupported:assert self.uc.reg_read(UC_X86_REG_RIP)==STOP
  self.recording=False
  def chain(first):
   result=[]
   while first:
    result.append((first-NODES)//32+1);first=self.q(first+24);assert len(result)<32
   return result
  return dict(unsupported=self.unsupported,trace=self.trace.copy(),rootCount=self.si(root+8),sharedCount=self.si(root+8)if p['alias']else self.si(SHARED+8),head=chain(self.q(root)),
    flags=[struct.unpack('<I',self.uc.mem_read(NODES+i*32,4))[0]for i in range(len(p['ids']))],next=[(self.q(NODES+i*32+24)-NODES)//32+1 if self.q(NODES+i*32+24)else 0 for i in range(len(p['ids']))],
    unitFlags=struct.unpack('<I',self.uc.mem_read(PET+0x124,4))[0],freed=self.freed.copy(),applyFlags=self.applyFlags)
def sample(**changes):
 p=detach_sample(action='remove');p.update(special=0,lookup=True,rootCount=3,shared=False,sharedCount=9,alias=False,unitFlags=0xffffffff);p.update(changes);return p
def build(check=False):
 em=Remove();cases=[]
 def add(**changes):p=sample(**changes);cases.append(dict(input=p,output=em.run_case(p)))
 for action in ['remove','detach']:
  for changes in [{},dict(owner=False),dict(kind=1),dict(data=False),dict(store=False),dict(game=False),dict(ownerSelector=0),dict(gameSelector=0),dict(count=0),dict(id=18),dict(id=19),dict(id=99),dict(lookup=False),dict(shared=True),dict(alias=True,rootCount=2),dict(alias=True,rootCount=1),dict(rootCount=0),dict(shared=True,sharedCount=0),dict(ids=[17,17,19]),dict(type=7)]:add(action=action,**changes)
 for special in [1,-1]:
  for changes in [{},dict(id=99),dict(lookup=False),dict(id=99,ids=[99,18,19],type=0),dict(id=17,ids=[99,18,19])]:add(special=special,**changes)
 add(unitFlags=0x7fffffff);add(shared=True,rootCount=1,sharedCount=-2147483648)
 OUT.mkdir(exist_ok=True);path=OUT/'cases.json';encoded=json.dumps(cases,indent=2)+'\n'
 if check:assert path.read_text()==encoded
 else:path.write_text(encoded)
 libs=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libs)==1
 result=dict(referenceVersion='3.3.93847',sourceContexts=len(cases),machineCodeBytes=sum(b-a for a,b in em.ranges),newRoutineBytes=578,processMemoryRead=False,targetProcessCodeExecuted=False,sourceCodeExecutedInOfflineEmulator=True,
  scope='Complete node-removal callback composed with current detach/store/packed helpers. Typed registry bucket,status/client/broadcast,special35b450 and logical allocator release are explicit services. Counter alias/underflow and normal-detach absence of hireable status are compared;no native birth/persistence/effects claimed.',
  artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in sorted(set([path,Path(__file__).resolve(),ROOT/'tools/probe_pet_owner_detach.py',ROOT/'tools/probe_generic_pet_parameters.py',*em.artifacts,*libs]))})
 path=ROOT/'tools/reference/pet-owner-remove-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert path.read_text()==encoded
 else:path.write_text(encoded)
 print(f"Player pet node removal:{len(cases)}current contexts/{result['machineCodeBytes']}composed bytes,578new bytes.")
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
