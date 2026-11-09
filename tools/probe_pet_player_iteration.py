#!/usr/bin/env python3
"""Replay current living-player iteration and exact typed registry bucket layout."""
import argparse,json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import *
from probe_generic_pet_parameters import Initializer,BASE,STACK,OBJECTS,STOP
from probe_warlock_runtime import ROOT,STAGE,digest
OUT=ROOT/'.local/class-skills/pet-player-iteration-source';TABLE=OBJECTS+0x8000;CALLBACK=STOP+0x1010;CONTEXT=OBJECTS+0x9000
def ptr(i):return OBJECTS+0x10000+i*0x2000 if i else 0
class Players(Initializer):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.artifacts=[]
  for name,wanted in [('pet-detach-broadcast-20261109.json',{'0x3722a0'}),('services-pet-registry-bucket-leaf-20261109.json',{'0x370610'})]:
   path=STAGE/name;cap=json.loads(path.read_text());self.artifacts.append(path)
   for n,h in cap['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
   for row in [r for r in cap['records']if r['rva']in wanted]:
    assert row.get('allUnwindFragmentsCaptured')or row.get('boundedLeafReference')
    for f in row['fragments']:
     assert f['captured']and f['invalidInstructionMarkers']==0
     path=next(ROOT/n for n,h in cap['artifacts'].items()if n.endswith('.bin')and h==f['sha256']);raw=path.read_bytes();a=BASE+int(f['rva'],16);self.map(a,len(raw));self.uc.mem_write(a,raw);self.ranges.append((a,a+len(raw)));self.artifacts.append(path)
  self.map(OBJECTS,0x40000);self.map(STACK,0x40000);self.map(STOP,0x2000);self.map(BASE+0x1e03ac0,64)
  for a in [CALLBACK,BASE+0x7b6af0]:self.map(a,1);self.uc.mem_write(a,b'\xc3')
  self.uc.hook_add(UC_HOOK_CODE,self.hook)
 def wi(self,a,v):self.uc.mem_write(a,struct.pack('<I',v&0xffffffff))
 def ret(self):
  sp=self.uc.reg_read(UC_X86_REG_RSP);self.uc.reg_write(UC_X86_REG_RSP,sp+8);self.uc.reg_write(UC_X86_REG_RIP,self.q(sp))
 def hook(self,u,a,size,user):
  if any(lo<=a<hi for lo,hi in self.ranges):return
  if a==BASE+0x7b6af0:self.unsupported=True;u.emu_stop();return
  assert a==CALLBACK and u.reg_read(UC_X86_REG_RCX)==OBJECTS and u.reg_read(UC_X86_REG_R8)==CONTEXT
  id=(u.reg_read(UC_X86_REG_RDX)-OBJECTS-0x10000)//0x2000;self.visits.append(id)
  if len(self.visits)==1:
   if self.p['nextChange']is not None:
    first,second=self.p['nextChange'];self.putq(ptr(first)+0x158,ptr(second))
   if self.p['lateBucket']is not None:self.putq(OBJECTS+0x2230+127*8,ptr(self.p['lateBucket']))
  self.ret()
 def run(self,p):
  self.p=p;u=self.uc;u.mem_write(OBJECTS,bytes(0x40000));u.mem_write(STACK,bytes(0x40000));self.visits=[];self.unsupported=False
  if p['action']=='players':
   for bucket,id in p['heads'].items():self.putq(OBJECTS+0x2230+int(bucket)*8,ptr(id))
   for name,row in p['units'].items():
    id=int(name);a=ptr(id);root=a+0x500;self.wi(a,row['kind']);u.mem_write(a+0x1bd,bytes([row['selector']]));self.putq(a+0x158,ptr(row['next']));self.putq(a+0x88,root if row['root']else 0);self.wi(root+0x1c,row['root1c']);self.wi(root+0xb30,row['word0']);self.wi(root+0xb44,row['word5'])
   for s in [1,2,3]:self.putq(BASE+0x1e03ac0+s*16,TABLE)
   self.wi(TABLE+0x2d0,p['count']);args=[OBJECTS if p['game']else 0,CALLBACK if p['callback']else 0,CONTEXT];entry=0x3722a0
  else:args=[p['kind']&0xffffffff,OBJECTS if p['game']else 0,p['id']&0xffffffff];entry=0x370610
  sp=STACK+0x20008;self.putq(sp,STOP)
  for reg,value in zip([UC_X86_REG_RSP,UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8],[sp,*args]):u.reg_write(reg,value)
  u.emu_start(BASE+entry,STOP,count=15000)
  if not self.unsupported:assert u.reg_read(UC_X86_REG_RIP)==STOP
  result=None
  if p['action']=='bucket':
   raw=u.reg_read(UC_X86_REG_RAX);result=raw-(OBJECTS if p['game']else 0)if raw else None
  return dict(unsupported=self.unsupported,visits=self.visits.copy(),offset=result)
def unit(**changes):
 p=dict(kind=0,selector=1,next=0,root=True,root1c=-1,word0=0,word5=0);p.update(changes);return p
def sample(**changes):
 p=dict(action='players',game=True,callback=True,count=232,heads={'0':1,'127':4},units={'1':unit(next=2),'2':unit(next=3,word0=128),'3':unit(word5=0x4000000),'4':unit()},nextChange=None,lateBucket=None);p.update(changes);return p
def build(check=False):
 em=Players();cases=[]
 def add(p):cases.append(dict(input=p,output=em.run(p)))
 for kind in [-1,0,1,2,3,4,5,6]:
  for id in [-1,0,127,128,129,2147483647]:add(dict(action='bucket',kind=kind,id=id,game=True))
 add(dict(action='bucket',kind=0,id=0,game=False))
 for changes in [{},dict(game=False),dict(callback=False),dict(heads={}),dict(count=7),dict(count=8),dict(count=186),dict(count=187),dict(nextChange=[1,3]),dict(lateBucket=1),dict(heads={'0':1,'1':1})]:add(sample(**changes))
 for changes in [dict(kind=0,selector=0),dict(kind=0,selector=4),dict(kind=1,selector=3),dict(kind=3),dict(kind=2,selector=0),dict(root=False),dict(root1c=0),dict(word0=128),dict(word5=0x4000000)]:
  p=sample(heads={'0':1});p['units']['1'].update(changes);add(p)
 OUT.mkdir(exist_ok=True);path=OUT/'cases.json';encoded=json.dumps(cases,indent=2)+'\n'
 if check:assert path.read_text()==encoded
 else:path.write_text(encoded)
 libs=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libs)==1
 result=dict(referenceVersion='3.3.93847',sourceContexts=len(cases),machineCodeBytes=sum(b-a for a,b in em.ranges),playerIteratorBytes=469,bucketLeafBytes=112,processMemoryRead=False,targetProcessCodeExecuted=False,sourceCodeExecutedInOfflineEmulator=True,
  scope='Complete current player enumeration plus bounded112byte registry leaf with all direct branches/RET inside sample. Supplied finite bucket/list/state graph and callback. Live next after callbacks,late bucket and duplicate membership compared. Registry birth/order and protected pet-notification payload are not certified.',
  artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in sorted(set([path,Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_parameters.py',*em.artifacts,*libs]))})
 path=ROOT/'tools/reference/pet-player-iteration-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert path.read_text()==encoded
 else:path.write_text(encoded)
 print(f"Player iteration/registry layout:{len(cases)}current contexts/{result['machineCodeBytes']}bytes;protected callback remains separate.")
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
