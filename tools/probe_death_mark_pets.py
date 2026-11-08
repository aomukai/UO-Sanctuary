#!/usr/bin/env python3
"""Execute the complete current Death Mark pet callback over explicit services."""
import argparse,json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import *
from probe_generic_pet_parameters import Initializer,BASE,STACK,OBJECTS,STOP
from probe_warlock_runtime import ROOT,STAGE,digest
GAME=OBJECTS;PET=OBJECTS+0x1000;PARAM=OBJECTS+0x2000;ROOTSTAT=OBJECTS+0x3000;ROW=OBJECTS+0x4000;NODES=OBJECTS+0x5000
CAP=STAGE/'death-mark-callback-20261108-after-user-cast.json'
RVA=dict(place=0x3739a0,overlay=0x27ab60,target=0x398a80,idle=0x425550,unlink=0x24c5d0,free=0x24cd40)
class Callback(Initializer):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.code=[]
  p=json.loads(CAP.read_text());assert p['allUnwindFragmentsCaptured']and p['sourceReferencesVerified']
  for f in p['fragments']:
   binary=next(ROOT/n for n,h in p['artifacts'].items()if n.endswith('.bin')and h==f['sha256']);raw=binary.read_bytes();assert digest(raw)==f['sha256']and f['invalidInstructionMarkers']==0
   a=BASE+int(f['rva'],16);self.map(a,len(raw));self.uc.mem_write(a,raw);self.ranges.append((a,a+len(raw)));self.code.append(binary)
  for a,n in [(STACK,0x40000),(OBJECTS,0x10000),(STOP,0x1000)]:self.map(a,n)
  for a in RVA.values():self.map(BASE+a,1);self.uc.mem_write(BASE+a,b'\xc3')
  self.uc.hook_add(UC_HOOK_CODE,self.hook)
 def integer(self,p):return struct.unpack('<i',self.uc.mem_read(p,4))[0]
 def puti(self,p,v):self.uc.mem_write(p,struct.pack('<i',v))
 def node(self,n):return NODES+(n-1)*0x100 if n else 0
 def index(self,p):assert p>=NODES and(p-NODES)%0x100==0;return(p-NODES)//0x100+1
 def chain(self,p):
  values=[]
  while p:assert len(values)<32;values.append(self.index(p));p=self.q(p+0x68)
  return values
 def hook(self,u,address,size,user):
  if any(a<=address<b for a,b in self.ranges):return
  c=self.c;a,b,d,e=[u.reg_read(r)for r in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]];sp=u.reg_read(UC_X86_REG_RSP);value=0
  signed=lambda n:(n&0x7fffffff)-(n&0x80000000)
  if address==BASE+RVA['place']:
   assert(a,b,d)==(GAME,PET,0);assert self.q(sp+0x30)==self.q(sp+0x38)==0;self.trace.append([1,signed(e),signed(self.q(sp+0x28))]);value=c['place']
  elif address==BASE+RVA['overlay']:
   assert a==PET;self.trace.append([2,signed(b)])
  elif address==BASE+RVA['target']:
   assert a==PET;self.trace.append([3,signed(b),signed(d)])
  elif address==BASE+RVA['idle']:
   assert(a,b,d)==(GAME,PET,1);self.trace.append([4,1])
  elif address==BASE+RVA['unlink']:
   assert a==c['unitSelector'];idx=self.index(b);self.trace.append([5,a,idx]);self.puti(b+0x1c,c['afterFlags'])
   for head in [ROOTSTAT+0xd0,ROOTSTAT+0xd8]:
    prev=head;p=self.q(prev)
    while p:
     if p==b:self.putq(prev,self.q(p+0x68));break
     prev=p+0x68;p=self.q(prev)
   self.putq(b+0x68,0)
   if c['changeRoot']==1:self.putq(PET+0x88,0)
   elif c['changeRoot']==2:self.puti(ROOTSTAT+0x1c,0)
  elif address==BASE+RVA['free']:
   assert a==c['gameSelector'];self.trace.append([6,a,self.index(b)])
  else:raise AssertionError(('Undeclared instruction',hex(address)))
  u.reg_write(UC_X86_REG_RAX,value&0xffffffffffffffff);u.reg_write(UC_X86_REG_RIP,self.q(sp));u.reg_write(UC_X86_REG_RSP,sp+8)
 def run(self,c):
  self.c=c;self.trace=[];u=self.uc;u.mem_write(OBJECTS,b'\0'*0x10000);u.mem_write(STACK,b'\0'*0x40000)
  u.mem_write(GAME+0x106,bytes([c['gameSelector']]));u.mem_write(PET+0x1bd,bytes([c['unitSelector']]));self.putq(PET+0x88,ROOTSTAT if c['root']else 0);self.puti(ROOTSTAT+0x1c,c['rootFlags'])
  self.putq(PARAM+0x18,ROW);u.mem_write(PARAM,struct.pack('<5iB',c['targetType'],c['targetId'],c['x'],c['y'],c['remaining'],int(c['clear'])));u.mem_write(ROW+0x13c,struct.pack('<h',c['overlay']))
  self.nodes=c['first']+c['second'];index=0
  for chain,offset in [(c['first'],0xd0),(c['second'],0xd8)]:
   self.putq(ROOTSTAT+offset,self.node(index+1)if chain else 0)
   for j,(state,flags)in enumerate(chain):
    index+=1;p=self.node(index);self.puti(p+0x20,state);self.puti(p+0x1c,flags);self.putq(p+0x68,self.node(index+1)if j+1<len(chain)else 0)
  rsp=STACK+0x20008;self.putq(rsp,STOP);u.reg_write(UC_X86_REG_RSP,rsp)
  for reg,v in [(UC_X86_REG_RCX,GAME),(UC_X86_REG_RDX,OBJECTS+0xf000),(UC_X86_REG_R8,PET if c['pet']else 0),(UC_X86_REG_R9,PARAM if c['directive']else 0)]:u.reg_write(reg,v)
  u.emu_start(BASE+0x44aa70,STOP,count=10000);assert u.reg_read(UC_X86_REG_RIP)==STOP
  return dict(remaining=self.integer(PARAM+0x10),root=bool(self.q(PET+0x88)),rootFlags=self.integer(ROOTSTAT+0x1c),first=self.chain(self.q(ROOTSTAT+0xd0)),second=self.chain(self.q(ROOTSTAT+0xd8)),flags=[self.integer(self.node(n)+0x1c)for n in range(1,len(self.nodes)+1)],trace=self.trace.copy())
COMMAND_CAP=STAGE/'callees-death-mark-target-command-20261109.json'
class Command(Callback):
 def __init__(self):
  super().__init__();p=json.loads(COMMAND_CAP.read_text());r=next(r for r in p['records']if r['rva']=='0x398a80');assert r['allUnwindFragmentsCaptured']and p['directCallTargetsVerified']
  for f in r['fragments']:
   binary=next(ROOT/n for n,h in p['artifacts'].items()if n.endswith('.bin')and h==f['sha256']);raw=binary.read_bytes();assert digest(raw)==f['sha256']and f['invalidInstructionMarkers']==0
   a=BASE+int(f['rva'],16);self.map(a,len(raw));self.uc.mem_write(a,raw);self.ranges.append((a,a+len(raw)));self.code.append(binary)
  self.map(BASE+0x6bc90,1);self.uc.mem_write(BASE+0x6bc90,b'\xc3')
 def hook(self,u,address,size,user):
  if any(a<=address<b for a,b in self.ranges):return
  assert address==BASE+0x6bc90;c=self.c;sp=u.reg_read(UC_X86_REG_RSP)
  a,b=[u.reg_read(r)for r in [UC_X86_REG_RCX,UC_X86_REG_RDX]];assert(a,b)==(c['selector'],c['classId']);self.trace.append([1,a,b])
  self.puti(PET,c['afterKind']);self.putq(PET+0x10,ROOTSTAT if c['afterData']else 0)
  u.reg_write(UC_X86_REG_RAX,ROW if c['row']else 0);u.reg_write(UC_X86_REG_RIP,self.q(sp));u.reg_write(UC_X86_REG_RSP,sp+8)
 def run(self,c):
  self.c=c;self.trace=[];u=self.uc;u.mem_write(OBJECTS,b'\0'*0x10000);u.mem_write(STACK,b'\0'*0x40000)
  self.puti(PET,c['kind']);self.puti(PET+4,c['classId']);self.putq(PET+0x10,ROOTSTAT if c['data']else 0)
  u.mem_write(PET+0x1bd,bytes([c['selector']]));u.mem_write(ROW+0x3e,bytes([c['flags']]))
  self.puti(ROOTSTAT+0x48,c['initialTarget']);self.puti(ROOTSTAT+0x4c,c['initialType']);sp=STACK+0x20008;self.putq(sp,STOP);u.reg_write(UC_X86_REG_RSP,sp)
  for reg,value in [(UC_X86_REG_RCX,PET if c['present']else 0),(UC_X86_REG_RDX,c['type']&0xffffffff),(UC_X86_REG_R8,c['target']&0xffffffff)]:u.reg_write(reg,value)
  u.emu_start(BASE+0x398a80,STOP,count=1000);assert u.reg_read(UC_X86_REG_RIP)==STOP
  return dict(result=u.reg_read(UC_X86_REG_RAX)&0xffffffff,kind=self.integer(PET),data=bool(self.q(PET+0x10)),target=self.integer(ROOTSTAT+0x48),type=self.integer(ROOTSTAT+0x4c),trace=self.trace.copy())

def build(check=False):
 em=Callback();cases=[];base=dict(pet=True,directive=True,remaining=3,place=1,clear=True,overlay=17,unitSelector=2,gameSelector=1,targetType=2,targetId=77,x=100,y=200,root=True,rootFlags=-1,afterFlags=0,changeRoot=0,first=[[21,-1],[1,-1],[11,-1],[2,-1]],second=[[21,1],[1,1],[11,1]])
 def add(**kw):c=base|kw;cases.append(dict(input=c,output=em.run(c)))
 add()
 for key,values in dict(pet=[False],directive=[False],remaining=[-2147483648,-1,0,1,2147483647],place=[0,-1,2],clear=[False],overlay=[-32768,-1,0,32767],targetType=[0,1,-1,2147483647],targetId=[-2147483648,-1,2147483647],root=[False],rootFlags=[0,1,2147483647,-2147483648],afterFlags=[-1,1,-2147483648],unitSelector=[0,255],gameSelector=[0,255]).items():
  for v in values:add(**{key:v})
 patterns=[([],[]),([],[ [21,-1],[1,-1],[11,-1]]),([[21,-1],[1,0],[11,1]],[]),([[2,0],[1,-1],[99,0],[21,-1],[11,-1]],[[11,0],[21,0]]),([[21,-1],[21,0],[1,-1],[11,-1]],[[21,0],[1,0],[11,0]])]
 for first,second in patterns:
  for after in [-2147483648,-1,0,1]:
   for change in [0,1,2]:add(first=first,second=second,afterFlags=after,changeRoot=change)
 for x,y in [(-2147483648,2147483647),(0,0)]:add(x=x,y=y)
 command=Command();commandCases=[];cb=dict(present=True,kind=1,classId=742,selector=2,data=True,row=True,flags=1,afterKind=1,afterData=True,type=2,target=77,initialTarget=-1,initialType=0)
 def cmd(**kw):c=cb|kw;commandCases.append(dict(input=c,output=command.run(c)))
 for present in [False,True]:
  for kind in [0,1,2,5]:
   for type in [-2147483648,-1,0,1,2,4,5,2147483647]:cmd(present=present,kind=kind,type=type)
 for flags in [0,1,2,3,128,255]:
  for row in [False,True]:
   for data in [False,True]:
    for afterKind in [0,1]:
     for afterData in [False,True]:cmd(flags=flags,row=row,data=data,afterKind=afterKind,afterData=afterData)
 for target in [-2147483648,-1,2147483647]:cmd(target=target)
 for selector in [0,255]:cmd(selector=selector)
 stage=ROOT/'.local/class-skills/death-mark-pets-source';stage.mkdir(exist_ok=True);p=stage/'cases.json';encoded=json.dumps(dict(cases=cases,commandCases=commandCases),separators=(',',':'))+'\n'
 if check:assert p.read_text()==encoded
 else:p.write_text(encoded)
 states=ROOT/'tools/reference/d2r-named-equipment/json/states.json';data=json.loads(states.read_text());assert {data[k]['*ID']for k in ['stunned','freeze','cold']}=={21,1,11}
 proof=dict(referenceVersion='3.3.93847',entryRva='0x44aa70',currentMachineCodeExecutedOffline=True,targetProcessCodeExecuted=False,processMemoryRead=False,cases=len(cases),commandCases=len(commandCases),commandBytes=126,sourceBytes=sum(b-a for a,b in em.ranges),unicornVersion=unicorn.__version__,emulatorLibrarySha256=digest(next(Path(unicorn.__file__).parent.rglob('libunicorn.so*')).read_bytes()),
  scope='Complete current callback over explicit services;quota changes only after successful placement;overlay,target,idle and21/1/11 first-list-before-second removal/free order. Root is reloaded after each removal;post-unlink signed flags control free. Valid acyclic source lists are supplied. State2/other states and duplicate lists remain untouched when not selected.',
  externalServices=['Placement3739a0 supplies a result and records all source room/skip/last zero arguments;the native binding separately reuses GenericPetMonsterPlacement.','Overlay27ab60,target command398a80,idle425550,unlink24c5d0 and free24cd40 record exact arguments. Unlink rewires selected nodes and may null/disable the root;their helper internals are not emulated.'],
  limits=['This callback comparison does not qualify world demon/room/CC adapters or learned DeathMark. Protected target-command body and complete stat-service binding remain separate.'],artifacts={str(f.relative_to(ROOT)):digest(f.read_bytes())for f in [p,Path(__file__).resolve(),CAP,COMMAND_CAP,states,*dict.fromkeys(em.code+command.code)]})
 out=ROOT/'tools/reference/death-mark-pets-current-evidence.json';s=json.dumps(proof,indent=2)+'\n'
 if check:assert out.read_text()==s
 else:out.write_text(s)
 print(f'Pet target command: {len(commandCases)} current-code cases,126bytes;kind/capability/data guards and raw command fields.')
 print(f'Death Mark pet callback: {len(cases)} current-code cases,546bytes;placement/quota,command/idle,ordered mutable control-list removal.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
