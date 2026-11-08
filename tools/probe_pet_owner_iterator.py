#!/usr/bin/env python3
"""Run both complete current owner iteration bodies over explicit pet stores."""
import argparse,json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import *
from probe_generic_pet_parameters import Initializer,BASE,STACK,OBJECTS,STOP
from probe_warlock_runtime import ROOT,STAGE,digest

OUT=ROOT/'.local/class-skills/pet-owner-iterator-source'
OWNER=OBJECTS+0x1000;DATA=OBJECTS+0x2000;STORE=OBJECTS+0x3000;BUCKETS=OBJECTS+0x4000;NODES=OBJECTS+0x8000
REGISTRY=OBJECTS+0x10000;UNITS=OBJECTS+0x18000;HEAP=OBJECTS+0x30000;MANAGER=OBJECTS+0x40000;VTABLE=MANAGER+0x100
TABLES=OBJECTS+0x50000;GLOBAL=BASE+0x1e03ac0;CALLBACK=STOP+0x100;FREE=STOP+0x180
def node(i):return NODES+i*64 if i else 0
def unit(i):return UNITS+i*0x200 if i else 0
def handle(address):return (address-UNITS)//0x200 if address else 0
def signed(v):return (v+2**31)%2**32-2**31

class Iterator(Initializer):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.artifacts=[]
  for name,rva,size in [('callees-pet-owner-iterator-20261109.json','0x35e6d0',386),('callees-pet-owner-bucket-20261109.json','0x35e510',446)]:
   path=STAGE/name;c=json.loads(path.read_text());row=c['records'][0]
   assert c['directCallTargetsVerified'] and row['rva']==rva and row['bytes']==size and row['allUnwindFragmentsCaptured']
   for n,h in c['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
   self.artifacts.append(path)
   for f in row['fragments']:
    p=next(ROOT/n for n,h in c['artifacts'].items() if n.endswith('.bin') and h==f['sha256']);raw=p.read_bytes();a=BASE+int(f['rva'],16)
    self.map(a,len(raw));self.uc.mem_write(a,raw);self.ranges.append((a,a+len(raw)));self.artifacts.append(p)
  self.map(OBJECTS,0x60000);self.map(STACK,0x40000);self.map(STOP,0x1000);self.map(GLOBAL,64);self.map(BASE+0x197a178,8)
  for a in [BASE+0x370610,BASE+0x93740,BASE+0xbaa540,BASE+0x155eeb0,BASE+0x7b6af0]:self.map(a,1);self.uc.mem_write(a,b'\xc3')
  self.uc.hook_add(UC_HOOK_CODE,self.hook)
 def wi(self,a,v):self.uc.mem_write(a,struct.pack('<I',v&0xffffffff))
 def ret(self,value=0):
  sp=self.uc.reg_read(UC_X86_REG_RSP);self.uc.reg_write(UC_X86_REG_RAX,value&0xffffffffffffffff);self.uc.reg_write(UC_X86_REG_RSP,sp+8);self.uc.reg_write(UC_X86_REG_RIP,self.q(sp))
 def hook(self,u,a,size,user):
  if any(x<=a<y for x,y in self.ranges):return
  c,d,t,z=[u.reg_read(r) for r in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]]
  if a==BASE+0x370610:
   assert c==1 and d==OBJECTS;sought=signed(t);self.trace.append([0,sought])
   if self.p['resolveMutation'] is not None and sought==self.p['resolveMutation'][0]:self.putq(node(self.p['resolveMutation'][1])+0x18,node(self.p['resolveMutation'][2]))
   self.ret(REGISTRY);return
  if a==CALLBACK:
   assert c==(OBJECTS if self.p['game'] else 0) and d==(OWNER if self.p['owner'] else 0) and z==0x1234
   self.visited.append(handle(t));self.trace.append([1,handle(t)])
   if len(self.visited)==1:
    if self.p['selectorAfter'] is not None:self.uc.mem_write(OBJECTS+0x106,bytes([self.p['selectorAfter']]))
    for key,value in self.p['headsAfter'].items():self.putq(BUCKETS+int(key)*32,node(value))
    for key,value in self.p['countsAfter'].items():self.wi(TABLES+int(key)*0x2000+0x1300,value)
   self.ret(1);return # Callback return is deliberately ignored by source.
  if a==BASE+0x93740:
   length=self.q(c+8);old=self.q(c);assert d>length and d<=256
   self.uc.mem_write(HEAP,bytes(self.uc.mem_read(old,length*8)));self.putq(c,HEAP);self.putq(c+16,d);self.growth+=1;self.ret();return
  if a==BASE+0xbaa540:self.ret(MANAGER);return
  if a==FREE:assert c==MANAGER;self.frees+=1;self.ret();return
  if a==BASE+0x155eeb0:self.ret(u.reg_read(UC_X86_REG_RAX));return
  if a==BASE+0x7b6af0:self.unsupported=True;self.boundary='sourceAssertion';u.emu_stop();return
  raise AssertionError(('Undeclared instruction',hex(a)))
 def run(self,p):
  self.p=p;self.trace=[];self.visited=[];self.unsupported=False;self.boundary=None;self.growth=0;self.frees=0;u=self.uc
  u.mem_write(OBJECTS,b'\0'*0x60000);u.mem_write(STACK,b'\0'*0x40000);u.mem_write(OBJECTS+0x106,bytes([p['selector']]))
  self.wi(OWNER,p['kind']);self.putq(OWNER+0x10,DATA);self.putq(DATA+0x98,STORE if p['store'] else 0);self.putq(STORE,BUCKETS)
  for i in [1,2,3]:self.putq(GLOBAL+16*i,TABLES+i*0x2000);self.wi(TABLES+i*0x2000+0x1300,p['counts'].get(str(i),p['count']))
  for key,first in p['heads'].items():self.putq(BUCKETS+32*int(key),node(first))
  for key,(flags,ident,nxt) in p['nodes'].items():self.wi(node(int(key)),flags);self.wi(node(int(key))+4,ident);self.putq(node(int(key))+0x18,node(nxt))
  self.putq(REGISTRY,unit(p['units'][0][0]) if p['units'] else 0)
  for i,(h,ident) in enumerate(p['units']):self.wi(unit(h)+8,ident);self.putq(unit(h)+0x158,unit(p['units'][i+1][0]) if i+1<len(p['units']) else 0)
  self.putq(MANAGER,VTABLE);self.putq(VTABLE+0x20,FREE)
  sp=STACK+0x20008;self.putq(sp,STOP);self.putq(sp+0x28,BUCKETS+32)
  for reg,v in [(UC_X86_REG_RSP,sp),(UC_X86_REG_RCX,OBJECTS if p['game'] else 0),(UC_X86_REG_RDX,OWNER if p['owner'] else 0),(UC_X86_REG_R8,CALLBACK),(UC_X86_REG_R9,0x1234)]:u.reg_write(reg,v)
  try:u.emu_start(BASE+(0x35e510 if p['bucketOnly'] else 0x35e6d0),STOP,count=100000)
  except unicorn.UcError as error:
   if not p['game'] and not p['bucketOnly'] and u.reg_read(UC_X86_REG_RIP)==BASE+0x35e715:
    assert error.errno==unicorn.UC_ERR_READ_UNMAPPED
    self.unsupported=True;self.boundary='nullGameDereference'
   else:raise AssertionError(('Unexpected emulator failure',p,hex(u.reg_read(UC_X86_REG_RIP)))) from error
  if not self.unsupported:assert u.reg_read(UC_X86_REG_RIP)==STOP
  return dict(visited=self.visited,trace=self.trace,unsupported=self.unsupported,boundary=self.boundary,growth=self.growth,frees=self.frees)

def sample(**values):
 p=dict(game=True,owner=True,kind=0,store=True,selector=1,count=4,counts={},selectorAfter=None,countsAfter={},headsAfter={},resolveMutation=None,bucketOnly=False,
        heads={'0':4,'1':1,'2':3,'3':0},nodes={'1':[0,101,2],'2':[0,102,0],'3':[0,103,0],'4':[0,104,0]},units=[[1,101],[2,102],[3,103],[4,104]])
 p.update(values);return p
def build(check=False):
 em=Iterator();cases=[]
 def add(**changes):p=sample(**changes);cases.append(dict(input=p,output=em.run(p)))
 add()
 for changes in [dict(owner=False),dict(kind=1),dict(kind=5),dict(store=False),dict(store=False,selector=0),dict(game=False,store=False),dict(game=False),dict(selector=0),dict(selector=255),dict(selector=2),dict(selector=3),dict(count=0),dict(count=1),dict(count=2),dict(count=3),dict(heads={'1':0,'2':0,'3':0}),dict(units=[]),dict(units=[[2,102],[3,103]]),dict(nodes={'1':[1,101,2],'2':[2,102,0],'3':[255,103,0],'4':[0,104,0]}),dict(nodes={'1':[0,101,2],'2':[0,101,0],'3':[0,103,0],'4':[0,104,0]}),dict(resolveMutation=[101,1,0]),dict(headsAfter={'1':0,'2':0}),dict(selectorAfter=2,counts={'2':2}),dict(selectorAfter=0),dict(countsAfter={'1':2})]:add(**changes)
 for selector in [1,2,3]:add(selector=selector,count=21,heads={'20':1})
 add(count=22,heads={'21':1})
 for inactive in [False,True]:add(bucketOnly=True,game=False,nodes={'1':[int(inactive),101,0]})
 nodes={str(i):[0,100+i,i+1 if i<65 else 0]for i in range(1,66)};add(count=2,heads={'1':1},nodes=nodes,units=[[i,100+i]for i in range(1,66)])
 OUT.mkdir(exist_ok=True);path=OUT/'cases.json';text=json.dumps(cases,indent=2)+'\n'
 if check:assert path.read_text()==text
 else:path.write_text(text)
 libs=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libs)==1
 artifacts=[path,Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_parameters.py',*em.artifacts,*libs]
 result=dict(referenceVersion='3.3.93847',sourceBytes=832,sourceContexts=len(cases),assertionContexts=sum(c['output']['boundary']=='sourceAssertion' for c in cases),nullGameDereferenceContexts=sum(c['output']['boundary']=='nullGameDereference' for c in cases),growthContexts=sum(c['output']['growth']>0 for c in cases),processMemoryRead=False,targetProcessCodeExecuted=False,sourceCodeExecutedInOfflineEmulator=True,unicornVersion=unicorn.__version__,artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in sorted(set(artifacts))},scope='Complete outer and bucket bodies execute against supplied player-store descriptors/linked nodes,real inline registry traversal,and explicit bucket/growth/callback/cookie services. Snapshot mutation/selector reload and65-entry growth are included. Native insertion/removal/order producers and raw allocator fidelity remain separate.')
 path=ROOT/'tools/reference/pet-owner-iterator-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
 if check:assert path.read_text()==text
 else:path.write_text(text)
 print(f"Owner iterator:{len(cases)}whole-current contexts/832bytes;per-bucket snapshots,flagged entries and65-entry growth.")
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
