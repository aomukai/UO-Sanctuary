#!/usr/bin/env python3
"""Replay current AI selector and its exact0/0/1/1 switch predicate composition."""
import argparse,json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import *
from probe_generic_pet_parameters import Initializer,BASE,STACK,OBJECTS,STOP
from probe_warlock_runtime import ROOT,STAGE,digest
OUT=ROOT/'.local/class-skills/pet-ai-selection-source';UNIT=OBJECTS+0x1000;DATA=OBJECTS+0x2000;A=OBJECTS+0x3000;B=OBJECTS+0x4000;OTHER=OBJECTS+0x5000;TABLE=OBJECTS+0x6000;STATS=OBJECTS+0x7000
class Selection(Initializer):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.artifacts=[]
  for name,rva in [('callees-pet-ai-entry-core-20261109.json','0x439fe0'),('callees-pet-ai-switch-20261109.json','0x27d980')]:
   path=STAGE/name;capture=json.loads(path.read_text());self.artifacts.append(path)
   for n,h in capture['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
   row=next(r for r in capture['records']if r['rva']==rva);assert row['allUnwindFragmentsCaptured']
   for f in row['fragments']:
    p=next(ROOT/n for n,h in capture['artifacts'].items()if n.endswith('.bin')and h==f['sha256']);raw=p.read_bytes();addr=BASE+int(f['rva'],16);self.map(addr,len(raw));self.uc.mem_write(addr,raw);self.ranges.append((addr,addr+len(raw)));self.artifacts.append(p)
  self.map(OBJECTS,0x10000);self.map(STACK,0x40000);self.map(STOP,0x1000);self.map(BASE+0x1e03ac0,64)
  for rva in [0x6bc90,0x6bd00,0x7b6af0]:self.map(BASE+rva,1);self.uc.mem_write(BASE+rva,b'\xc3')
  self.uc.hook_add(UC_HOOK_CODE,self.hook)
 def wi(self,a,v):self.uc.mem_write(a,struct.pack('<I',v&0xffffffff))
 def wb(self,a,v):self.uc.mem_write(a,bytes([v]))
 def ret(self,value=0):
  sp=self.uc.reg_read(UC_X86_REG_RSP);self.uc.reg_write(UC_X86_REG_RAX,value);self.uc.reg_write(UC_X86_REG_RSP,sp+8);self.uc.reg_write(UC_X86_REG_RIP,self.q(sp))
 def hook(self,u,a,size,user):
  if any(lo<=a<hi for lo,hi in self.ranges):return
  p=self.p;c=u.reg_read(UC_X86_REG_RCX);d=u.reg_read(UC_X86_REG_RDX)
  if a==BASE+0x7b6af0:self.failed=True;u.emu_stop();return
  if a in [BASE+0x6bc90,BASE+0x6bd00]:
   which='A'if a==BASE+0x6bc90 else'B';self.trace.append(f'{which}:{c}:{d}')
   if which=='A':
    if p['mutateAi']is not None:u.mem_write(A+82,struct.pack('<h',p['mutateAi']))
    if p['replacePrimary']:self.putq(DATA,OTHER)
    for name,offset in [('lateFlags',0x124),('lateKind',0),('lateMode',12)]:
     if p[name]is not None:self.wi(UNIT+offset,p[name])
   self.ret((A if which=='A'else B)if p['row'+which]else 0);return
  raise AssertionError(hex(a))
 def run(self,p):
  self.p=p;self.trace=[];self.failed=False;u=self.uc;u.mem_write(OBJECTS,b'\0'*0x10000);u.mem_write(STACK,b'\0'*0x40000)
  self.wi(UNIT,p['unitKind']);self.wi(UNIT+4,742);self.wi(UNIT+12,p['mode']);self.wb(UNIT+0x1bd,p['selector']);self.wi(UNIT+0x124,p['flags124']);self.wi(UNIT+0x128,p['flags128']);self.putq(UNIT+0x10,DATA if p['data']else 0);self.putq(DATA,A if p['primary']else 0);self.wb(DATA+0x1a,p['flags1a'])
  u.mem_write(A+82,struct.pack('<h',p['ai']));u.mem_write(OTHER+82,struct.pack('<h',7));self.wb(A+60,p['flags60']);self.wb(A+62,p['flags62']);self.wb(B+240,4 if p['walk']else 0)
  self.putq(UNIT+0x88,STATS if p['hasRoot']else 0);self.wi(STATS+28,p['root1c']);self.wi(STATS+0xb34,p['rootb34']);self.wi(STATS+0xb40,p['rootb40'])
  for s in [1,2,3]:self.putq(BASE+0x1e03ac0+16*s,TABLE);self.wi(TABLE+0x2d0,p['count'])
  sp=STACK+0x20008;self.putq(sp,STOP);self.putq(sp+0x28,p['options'][3]);values=[UNIT if p['actor']else 0,p['state'],0,0]if p['kind']=='selector'else[UNIT if p['actor']else 0,*p['options'][:3]]
  for reg,v in zip([UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9],values):u.reg_write(reg,v&0xffffffffffffffff)
  u.reg_write(UC_X86_REG_RSP,sp)
  try:u.emu_start(BASE+(0x439fe0 if p['kind']=='selector'else 0x27d980),STOP,count=10000)
  except unicorn.UcError as error:
   if u.reg_read(UC_X86_REG_RIP)==BASE+0x43a05c and u.reg_read(UC_X86_REG_RDI)==0:self.failed=True
   else:raise
  if not self.failed:assert u.reg_read(UC_X86_REG_RIP)==STOP
  raw=u.reg_read(UC_X86_REG_RAX)
  return dict(value=None if self.failed else raw if p['kind']=='selector'else bool(raw&255),unsupported=self.failed,trace=self.trace.copy())

def sample(**changes):
 p=dict(kind='selector',actor=True,unitKind=1,data=True,primary=True,ai=148,state=0,options=[0,0,1,1],selector=1,flags124=4,flags128=0,mode=1,count=232,hasRoot=True,root1c=-1,rootb34=0,rootb40=0,rowA=True,rowB=True,flags60=0,flags62=1,walk=True,flags1a=0,mutateAi=None,replacePrimary=False,lateFlags=None,lateKind=None,lateMode=None);p.update(changes);return p
def build(check=False):
 em=Selection();cases=[]
 def add(**changes):p=sample(**changes);cases.append(dict(input=p,output=em.run(p)))
 for ai in [-32768,-1,0,1,148,154,155,32767]:add(ai=ai)
 for state in [-2147483648,-1,1,7,9,10,11,12,13,17,2147483647]:
  add(state=state);add(state=state,flags124=0)
 for changes in [dict(actor=False),dict(data=False),dict(primary=False),dict(unitKind=0),dict(actor=False,state=7),dict(data=False,state=10),dict(state=10,mutateAi=7,flags124=0),dict(state=10,replacePrimary=True,flags124=0),dict(state=10,lateFlags=0),dict(state=10,lateKind=0,lateMode=17,lateFlags=0)]:add(**changes)
 for changes in [dict(),dict(actor=False),dict(unitKind=0),dict(selector=0),dict(selector=255),dict(rowA=False),dict(rowB=False),dict(walk=False),dict(flags60=64),dict(flags62=0),dict(flags124=0),dict(flags124=0x10000),dict(flags124=0,mode=0),dict(flags124=0,mode=12),dict(rootb34=0x400000),dict(rootb34=0x400000,count=54),dict(rootb34=0x400000,root1c=0),dict(rootb34=0x400000,hasRoot=False),dict(options=[1,0,1,1],rootb40=0x8000),dict(options=[1,0,1,1],rootb40=0x8000,count=143),dict(options=[0,1,1,1],flags1a=2),dict(options=[0,1,1,1],flags1a=8),dict(options=[0,1,1,1],flags1a=1),dict(options=[0,1,0,0],flags60=64,flags62=0),dict(lateKind=3,lateFlags=0),dict(lateKind=3,lateFlags=0,flags128=0x4000000)]:add(kind='switch',**changes)
 OUT.mkdir(exist_ok=True);path=OUT/'cases.json';text=json.dumps(cases,indent=2)+'\n'
 if check:assert path.read_text()==text
 else:path.write_text(text)
 libs=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libs)==1
 result=dict(referenceVersion='3.3.93847',selectorBytes=187,switchBytes=499,sourceContexts=len(cases),processMemoryRead=False,targetProcessCodeExecuted=False,sourceCodeExecutedInOfflineEmulator=True,scope='Complete selector and switch predicate execute together for states10..12;other states preserve raw special-address arithmetic. Metadata leaves and current status snapshots are explicit;native-world status projection and unqualified special descriptors are separate.',artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in sorted(set([path,Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_parameters.py',*em.artifacts,*libs]))})
 path=ROOT/'tools/reference/pet-ai-selection-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
 if check:assert path.read_text()==text
 else:path.write_text(text)
 print(f"AI selection:{len(cases)}current contexts/686bytes;composed state10..12predicate and metadata guards.")
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
