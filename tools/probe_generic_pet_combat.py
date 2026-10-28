#!/usr/bin/env python3
"""Execute captured current GenericPet combat orchestration offline."""
import argparse,itertools,json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import *
from probe_generic_pet_decisions import Emulator,BASE,STACK,MEM,STOP,GAME,PET,OWNER,CFG,PATH,ROOM
from probe_warlock_runtime import ROOT,STAGE,Image,digest
SLOTS=MEM+0x10000;TABLE=MEM+0x12000;GLOBAL=BASE+0x1e03ac0
FNS={k:BASE+v for k,v in dict(gather=0x4b42f0,range=0x4b30f0,search=0x4b3df0,rateold=0x4b45e0,property=0x266860,room=0x278450,blocked=0x2a5740,reset=0x4b1220,move=0x425bb0,movefinish=0x4b12d0,prepare=0x4b2810,rate=0x4b5200,order=0x4b7100,act=0x4b54d0,alternative=0x4b56e0,cookie=0x155eeb0,fatal=0x7b6af0).items()}
def pointer(value):return MEM+0x16000+value*16 if value else 0
def target(value):
 if not value:return 0
 assert value>=MEM+0x16000 and (value-MEM-0x16000)%16==0;return (value-MEM-0x16000)//16
# source row:targetQ,opaqueQ,rating,skill,opaque24,opaque28,counter,byte36,baseflag,byte38,byte39
def encode(c):return struct.pack('<QQ5i4B',pointer(c[0]),c[1],*c[2:7],*c[7:])
def decode(b):
 a=list(struct.unpack('<QQ5i4B',b));a[0]=target(a[0]);return a
def make(count):return [[20+i,0x1122330000000000+i,100-i,10+i,i,50+i,60+i,70+i,int(i==1),80+i,90+i] for i in range(count)]
def ordered(rows,mode):
 if mode==0:return rows
 if mode==1:return list(reversed(rows))
 return sorted(rows,key=lambda c:(-c[2],c[4]))
def wrap(x):return (x+2147483648)%4294967296-2147483648
class Combat(Emulator):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.image=Image();self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.artifacts=[]
  proofpath=STAGE/'generic-helpers-actions.json';proof=json.loads(proofpath.read_text());self.artifacts.append(proofpath)
  for n,h in proof['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
  row=next(r for r in proof['records'] if r['rva']=='0x4b58a0');assert row['allUnwindFragmentsCaptured']
  for i,f in enumerate(row['fragments']):
   p=STAGE/f'generic-actions-4b58a0-part{i}.bin';b=p.read_bytes();assert digest(b)==f['sha256'];a=BASE+int(f['rva'],16);self.map(a,len(b));self.uc.mem_write(a,b);self.ranges.append((a,a+len(b)));self.artifacts.append(p)
  self.map(STACK,0x40000);self.map(MEM,0x20000);self.map(STOP,4096);self.map(GLOBAL,64);self.map(BASE+0x197a178,8);self.uc.mem_write(BASE+0x197a178,b'\0'*8)
  for addr in FNS.values():self.map(addr,1);self.uc.mem_write(addr,b'\xc3')
  self.uc.reg_write(UC_X86_REG_CR0,(self.uc.reg_read(UC_X86_REG_CR0)&~4)|2);self.uc.reg_write(UC_X86_REG_CR4,self.uc.reg_read(UC_X86_REG_CR4)|0x200);self.uc.hook_add(UC_HOOK_CODE,self.hook)
 def rows(self,vec):return [decode(bytes(self.uc.mem_read(self.q(vec)+40*i,40))) for i in range(self.q(vec+8))]
 def index(self,ptr):assert SLOTS<=ptr<SLOTS+16*40 and (ptr-SLOTS)%40==0;return (ptr-SLOTS)//40
 def state(self):return [target(self.q(CFG+0x50)),self.i(CFG+0x58),self.i(CFG+0x5c),self.i(CFG+0x60)]
 def hook(self,uc,a,size,user):
  if any(lo<=a<hi for lo,hi in self.ranges):return
  name=next((k for k,v in FNS.items() if v==a),None);assert name,('Undeclared code',hex(a))
  reg=lambda r:uc.reg_read(r);c,d,v,z=[reg(r) for r in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]];rsp=reg(UC_X86_REG_RSP);arg=lambda i:self.q(rsp+0x28+(i-5)*8);rax=0;p=self.p
  if name=='gather':
   assert c==PET and d==CFG;self.vec=v;self.wq(v,SLOTS);self.wq(v+8,len(p['rows']));self.wq(v+16,0x8000000000000010)
   for i,row in enumerate(p['rows']):uc.mem_write(SLOTS+i*40,encode(row))
   self.trace.append([0])
  elif name=='range':
   assert c==PET and d==CFG;value=p['ranges'][min(self.rangeindex,1)];self.rangeindex+=1;self.trace.append([1,self.rows(v),value]);rax=value
  elif name=='search':
   assert d==GAME and v==PET and z==(OWNER if p['owner'] else 0) and arg(5)==CFG
   rows=self.rows(arg(6));radius=struct.unpack('<i',struct.pack('<I',arg(7)&0xffffffff))[0];bias=target(arg(8));slots=p['targets'][min(self.searchindex,1)];words=[101+self.searchindex,201+self.searchindex];self.searchindex+=1
   self.trace.append([2,rows,radius,bias,slots,words.copy()]);uc.mem_write(c,struct.pack('<7Q',*[pointer(x) for x in slots],*words));rax=c
  elif name in ['rateold','rate']:
   assert c==GAME and d==PET and v==(OWNER if p['owner'] else 0) and z==CFG
   sel=arg(5);rowptr=arg(6);row=decode(bytes(uc.mem_read(rowptr,40)));idx=row[4];selection=[target(self.q(sel+i*8)) for i in range(5)]+[self.q(sel+40),self.q(sel+48)] if name=='rate' else [self.q(sel),self.q(sel+8)]
   self.trace.append([3 if name=='rateold' else 4,row.copy(),selection.copy()]);row[2]=p['scores'][idx];row[6]=wrap(row[6]+10);uc.mem_write(rowptr,encode(row))
   if name=='rateold':self.wq(sel,selection[0]+1);self.wq(sel+8,selection[1]^idx)
  elif name=='property':assert c==PET;self.trace.append([5,2]);rax=2
  elif name=='room':assert c==PET;self.trace.append([6]);rax=ROOM
  elif name=='blocked':assert c==ROOM and d==20 and v==30 and z==2 and arg(5)==64;self.trace.append([7,p['blocked']]);rax=p['blocked']
  elif name=='reset':assert c==PET and d==v==z==0;self.trace.append([8])
  elif name=='move':assert c==GAME and d==PET and v==20 and z==30 and (arg(5)&255)==5 and (arg(6)&0xffffffff)==0 and (arg(7)&65535)==4;self.trace.append([9,p['move']]);rax=p['move']
  elif name=='movefinish':assert c==GAME and d==PET;self.trace.append([10])
  elif name=='prepare':assert c==GAME and d==PET and v==(OWNER if p['owner'] else 0) and z==self.vec;self.trace.append([11])
  elif name=='order':
   assert c==SLOTS and d==SLOTS+len(p['rows'])*40 and v==len(p['rows']) and z==0
   rows=ordered(self.rows(self.vec),p['order']);self.trace.append([12,[r[4] for r in rows]])
   for i,row in enumerate(rows):uc.mem_write(SLOTS+i*40,encode(row))
  elif name=='act':
   assert c==GAME and d==PET and v==CFG;row=decode(bytes(uc.mem_read(arg(5),40)));old=decode(bytes(uc.mem_read(z,40))) if z else None;success=int(row[4] in p['success']);self.trace.append([13,old,row,success]);rax=success
   if success:self.wq(CFG+0x50,pointer(row[0]));self.wi(CFG+0x58,row[3]);self.wi(CFG+0x5c,wrap(row[6]+1))
  elif name=='alternative':
   assert c==GAME and d==PET and v==CFG and arg(5)==self.vec;old=decode(bytes(uc.mem_read(z,40))) if z else None;self.trace.append([14,old,self.rows(self.vec),p['alternative']]);rax=p['alternative']
  elif name=='fatal':self.trace.append([15]);self.fatal=True;uc.emu_stop();return
  elif name=='cookie':rax=reg(UC_X86_REG_RAX)
  else:raise AssertionError(name)
  ret=self.q(rsp);uc.reg_write(UC_X86_REG_RAX,rax);uc.reg_write(UC_X86_REG_RSP,rsp+8);uc.reg_write(UC_X86_REG_RIP,ret)
 def combat(self,p):
  self.reset();self.p=p;self.rangeindex=0;self.searchindex=0;self.fatal=False
  self.coords(PET,1,20,30,PATH);self.wq(CFG+0x50,pointer(p['memory'][0]));self.uc.mem_write(CFG+0x58,struct.pack('<3i',*p['memory'][1:]));self.wi(CFG+0x20,p['choose']);self.uc.mem_write(GAME+0x106,bytes([p['selector']]))
  for i in [1,2,3]:self.wq(GLOBAL+i*16,TABLE);self.wi(TABLE+0x11f0,p['base'])
  rsp=STACK+0x20008;self.wq(rsp,STOP)
  for reg,value in [(UC_X86_REG_RSP,rsp),(UC_X86_REG_RCX,GAME),(UC_X86_REG_RDX,PET),(UC_X86_REG_R8,OWNER if p['owner'] else 0),(UC_X86_REG_R9,CFG)]:self.uc.reg_write(reg,value)
  self.uc.emu_start(BASE+0x4b58a0,STOP,count=30000)
  assert self.fatal or self.uc.reg_read(UC_X86_REG_RIP)==STOP,'No normal return'
  return [-1 if self.fatal else self.uc.reg_read(UC_X86_REG_RAX)&255,self.state(),self.rows(self.vec),self.trace]
def expected(p):
 rows=[r.copy() for r in p['rows']];memory=p['memory'].copy();trace=[[0]];oldtarget=memory[0];remembered=None;rangeindex=0;searchindex=0
 if memory[1]>-1 and memory[2]<25:
  if p['selector'] not in [1,2,3]:return [-1,memory,rows,trace+[[15]]]
  remembered=next((i for i,r in enumerate(rows) if (r[8]!=0 if memory[1]==p['base'] else r[3]==memory[1])),None)
  if remembered is not None:rows[remembered][6]=memory[2]
 memory[0]=0;memory[1]=-1;memory[2]=0
 def radius(items):
  nonlocal rangeindex
  value=p['ranges'][min(rangeindex,1)];rangeindex+=1;trace.append([1,[r.copy() for r in items],value]);return value
 def search(items,r,bias):
  nonlocal searchindex
  slots=p['targets'][min(searchindex,1)];words=[101+searchindex,201+searchindex];searchindex+=1;trace.append([2,[x.copy() for x in items],r,bias,slots,words.copy()]);return slots,words
 def rate(row,selection,old):
  trace.append([3 if old else 4,row.copy(),selection.copy()]);row[2]=p['scores'][row[4]];row[6]=wrap(row[6]+10)
  if old:selection[0]+=1;selection[1]^=row[4]
 if oldtarget:
  subset=[rows[remembered].copy()] if remembered is not None else rows
  r=radius(subset)
  if r:
   memory[3]=wrap(memory[3]+1);bias=oldtarget
   if memory[3]>5:memory[3]=0;bias=0
   slots,words=search(subset,r,bias)
   if oldtarget not in slots:oldtarget=0;remembered=None
  else:oldtarget=0;remembered=None
 if oldtarget:
  if remembered is not None:rows[remembered][0]=oldtarget;rows[remembered][2]=2147483647
  else:
   slots,words=search(rows,0,0)
   for row in rows:row[0]=oldtarget;rate(row,words,True)
 else:
  remembered=None;trace += [[5,2],[6],[7,p['blocked']]]
  if p['blocked']:
   trace += [[8],[9,p['move']]]
   if p['move']:trace.append([10]);return [1,memory,rows,trace]
  r=radius(rows);slots,words=search(rows,r,0);trace.append([11]);selection=slots+words
  for row in rows:rate(row,selection,False)
 result=0
 if p['choose']==0:
  rows=ordered(rows,p['order']);trace.append([12,[r[4] for r in rows]])
  for row in rows:
   if row[2]==-2147483648:break
   old=rows[remembered].copy() if remembered is not None else None;success=int(row[4] in p['success']);trace.append([13,old,row.copy(),success])
   if success:memory[:3]=[row[0],row[3],wrap(row[6]+1)];result=1;break
 elif p['choose']==1:trace.append([14,rows[remembered].copy() if remembered is not None else None,[r.copy() for r in rows],p['alternative']]);result=p['alternative']
 return [result,memory,rows,trace]
def sample(count=3,**kw):
 p=dict(rows=make(count),memory=[7,11,2,0],selector=1,base=99,choose=0,owner=1,ranges=[9,13],targets=[[7,0,0,0,0],[0,0,0,0,0]],scores=[40-i for i in range(count)],blocked=0,move=0,order=2,success=[0],alternative=1);p.update(kw);return p
def build(check=False):
 em=Combat();cases=[]
 def case(p):
  actual=em.combat(p);want=expected(p);assert actual==want,(p,actual,want);cases.append([p,actual])
 for count,skill,counter,cycle,selector,old in itertools.product([0,1,3,16],[-2,-1,0,10,11,99],[ -2147483648,-1,0,24,25,2147483647],[-1,0,4,5,6,2147483647],[1,2,3],[0,7]):case(sample(count,memory=[old,skill,counter,cycle],selector=selector))
 for slot,known,r0,r1,choose,order,success,owner in itertools.product(range(6),[0,1],[0,9],[0,13],[-1,0,1,2],[0,1,2],[0,1],[0,1]):
  targets=[0]*5
  if slot<5:targets[slot]=7
  case(sample(memory=[7,11 if known else 777,24,5],targets=[targets,[3,4,5,6,8]],ranges=[r0,r1],choose=choose,order=order,success=[0,2] if success else [],owner=owner,alternative=success))
 for blocked,move,choose,order,scores,success in itertools.product([0,1],[0,1],[0,1,2],[0,1,2],[[10,10,10],[-2147483648,20,30],[20,-2147483648,30],[30,20,-2147483648]],[[],[0],[1],[2]]):case(sample(memory=[0,-1,0,4],blocked=blocked,move=move,choose=choose,order=order,scores=scores,success=success))
 for selector,skill,counter in itertools.product([0,4,255],[-1,10],[24,25]):case(sample(selector=selector,memory=[7,skill,counter,3]))
 # Base-mode flag is matched independently of skill ID; first matching row wins.
 for match in [0,1,2]:
  p=sample(memory=[7,99,-3,0]);p['rows'][0][8]=int(match!=2);p['rows'][2][8]=1;case(p)
 out=ROOT/'.local/class-skills/generic-pet-combat-source-probe.json';encoded=json.dumps(cases,separators=(',',':'))+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 libraries=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libraries)==1
 artifacts=[out,Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_decisions.py',ROOT/'tools/probe_warlock_runtime.py',*em.artifacts]
 result=dict(referenceVersion='3.3.93847',component='Current GenericPet combat orchestration',currentCombatRva='0x4b58a0',machineCodeBytes=sum(b-a for a,b in em.ranges),matrixCases=len(cases),fatalSelectorCases=sum(row[1][0]==-1 for row in cases),processMemoryRead=False,targetProcessCodeExecuted=False,sourceCodeExecutedInOfflineEmulator=True,completeAiVerified=False,handlerEnabled=False,
  services=['Gather supplies0..16opaque40-byte descriptors in a declared borrowed container;dynamic allocation/failure/exception paths are not emulated. Base-mode skill identity comes from synthetic selector1..3tables. Invalid selector reaches the real current fatal-service boundary and is recorded,not returned as false.', 'Range/search/blocked-movement/rating/order/action/alternative helpers are declared synthetic services. Search supplies five targets plus two opaque words. Ordering uses independent supplied identity/reverse/rating fixtures,not a claim about the actual helper comparator. Core target validity,movement and NPC skill execution remain unverified.', 'Stack cookie checking is a declared no-op preserving the return register. The initial fixture zeroed that register;the corrected service preserves it without modifying captured instructions.', 'Remembered descriptor is a vector slot;after supplied ordering,action helpers receive the contents of that slot rather than a detached pre-order object. Raw40-byte payload/counters/flags are preserved except explicit current wrapper writes and declared service mutations.'],
  emulatorLibrary=dict(path=str(libraries[0].relative_to(ROOT)),sha256=digest(libraries[0].read_bytes())),artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in sorted(set(artifacts))})
 targetpath=ROOT/'tools/reference/generic-pet-combat-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert targetpath.read_text()==encoded
 else:targetpath.write_text(encoded)
 print(f'Current GenericPet combat:{len(cases)} orchestration contexts,{result["fatalSelectorCases"]}fatal-selector boundaries;real helper behavior remains gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
