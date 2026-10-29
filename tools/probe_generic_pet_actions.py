#!/usr/bin/env python3
"""Run captured current action/disposition/weighted-choice helpers offline."""
import argparse,itertools,json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE,UC_HOOK_MEM_READ
from unicorn.x86_const import *
from probe_generic_pet_decisions import Emulator,BASE,STACK,MEM,STOP,GAME,PET,CFG,PATH
from probe_warlock_runtime import ROOT,STAGE,Image,digest
OUT=ROOT/'.local/class-skills/generic-pet-actions-source-probe.json'
SLOTS=MEM+0x10000;VEC=MEM+0x20000;TABLE=MEM+0x21000;SKILL=MEM+0x23000;MON=MEM+0x24000;TARGETPATH=MEM+0x25000;GLOBAL=BASE+0x1e03ac0
ACT=BASE+0x4b54d0;ALT=BASE+0x4b56e0;DISP=BASE+0x4b2350
FNS={k:BASE+v for k,v in dict(clear=0x3acf10,distance=0x40bfd0,collision4=0x27c710,collision0=0x27cee0,reset=0x4b1220,move=0x425bb0,finish=0x4b12d0,monster=0x6bd00,approach=0x4b1600,base=0x4253d0,initialize=0x3de170,mode=0x3dda90,skill=0x6bf40,accepted=0x372ec0,fatal=0x7b6af0).items()}
OPAQUE=[0x1122334455667788,0x2233445566778899,0x33445566778899aa,0x445566778899aabb]
def pointer(n):return MEM+0x28000+n*0x40 if n else 0
def target(p):
 if not p:return 0
 assert MEM+0x28000<=p<MEM+0x40000 and (p-MEM-0x28000)%0x40==0
 return (p-MEM-0x28000)//0x40
def signed(n):return (n+2**31)%2**32-2**31
def signed64(n):return (n+2**63)%2**64-2**63
def packxy(x,y):return signed64((x&0xffffffff)|((y&0xffffffff)<<32))
def encode(r):return struct.pack('<QQ5i4B',pointer(r[0]),r[1]&0xffffffffffffffff,*r[2:7],*r[7:])
def decode(b):
 r=list(struct.unpack('<QQ5i4B',b));r[0]=target(r[0]);r[1]=signed64(r[1]);return r
class Actions(Emulator):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.image=Image();self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.artifacts=[]
  for proof_name,rv in [('callees-live-after-combat-actions-20261029.json',[0x4b54d0,0x4b56e0]),('callees-live-action-disposition-20261029.json',[0x4b2350])]:
   pp=STAGE/proof_name;proof=json.loads(pp.read_text());self.artifacts.append(pp)
   for n,h in proof['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
   for rva in rv:
    record=next(r for r in proof['records'] if int(r['rva'],16)==rva);assert record['allUnwindFragmentsCaptured']
    for f in record['fragments']:
     assert not f['invalidInstructionMarkers'];a=int(f['rva'],16);n=int(f['endRva'],16)-a
     candidates=[ROOT/p for p in proof['artifacts'] if p.endswith('.bin') and digest((ROOT/p).read_bytes())==f['sha256']];assert len(candidates)==1
     p=candidates[0];data=p.read_bytes();assert len(data)==n;self.map(BASE+a,n);self.uc.mem_write(BASE+a,data);self.ranges.append((BASE+a,BASE+a+n));self.artifacts.append(p)
  self.map(STACK,0x40000);self.map(MEM,0x40000);self.map(STOP,4096);self.map(GLOBAL,64)
  for addr in FNS.values():self.map(addr,1);self.uc.mem_write(addr,b'\xc3')
  self.uc.hook_add(UC_HOOK_CODE,self.hook);self.uc.hook_add(UC_HOOK_MEM_READ,self.read_hook,begin=TABLE+0x11f0,end=TABLE+0x11f3)
 def read_hook(self,uc,access,address,size,value,user):
  assert address==TABLE+0x11f0 and size==4;self.trace.append([16,self.p['selector'],self.p['base']])
 def rows(self):return [decode(bytes(self.uc.mem_read(SLOTS+40*i,40))) for i in range(self.count)]
 def index(self,p):
  if not p:return -1
  assert SLOTS<=p<SLOTS+self.count*40 and (p-SLOTS)%40==0;return (p-SLOTS)//40
 def ret(self,value):
  rsp=self.uc.reg_read(UC_X86_REG_RSP);ret=self.q(rsp);self.uc.reg_write(UC_X86_REG_RAX,value&0xffffffffffffffff);self.uc.reg_write(UC_X86_REG_RSP,rsp+8);self.uc.reg_write(UC_X86_REG_RIP,ret)
 def request(self,a):return [target(self.q(a+16)),self.i(a+24),self.i(a+28),[self.q(a),self.q(a+8),self.q(a+32),self.q(a+40)]]
 def hook(self,uc,a,size,user):
  reg=lambda n:uc.reg_read(n);rsp=reg(UC_X86_REG_RSP);arg=lambda i:self.q(rsp+0x28+(i-5)*8);p=self.p
  if a==ACT:
   self.active=self.index(arg(5))
   if self.kind in ['weighted','composed']:
    self.trace.append([20,self.index(reg(UC_X86_REG_R9)),self.active,self.rows()[self.active]])
    if self.kind=='weighted':self.ret(int(self.active in p['success']));return
  if any(lo<=a<hi for lo,hi in self.ranges):return
  name=next((k for k,v in FNS.items() if v==a),None);assert name,('Undeclared helper',hex(a))
  c,d,v,z=[reg(n) for n in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]];rax=0
  success=int(self.active in p['success']) if self.kind=='composed' else p['execute']
  if name=='clear':assert c==PET and d==0 and signed(z)==-1;self.trace.append([0,0,signed(v),-1])
  elif name=='distance':assert c==PET;self.trace.append([1,signed(d),signed(v),p['distance']]);rax=p['distance']
  elif name=='collision4':assert c==PET and v==4;self.trace.append([2,target(d),4,p['collision4']]);rax=p['collision4']
  elif name=='collision0':assert c==PET and v==z==0;self.trace.append([3,target(d),0,0,p['collision0']]);rax=p['collision0']
  elif name=='reset':assert c==PET and d==v==z==0;self.trace.append([4])
  elif name=='move':
   assert c==GAME and d==PET and arg(6)&0xffffffff==0 and arg(7)&65535==4
   self.trace.append([5,signed(v),signed(z),arg(5)&255,0,4,p['move']]);rax=p['move']
  elif name=='finish':assert c==GAME and d==PET;self.trace.append([6])
  elif name=='monster':
   assert c==p['selector'] and signed(d)==742;self.trace.append([7,int(c),742,p['monster']]);rax=MON if p['monster']>=0 else 0
  elif name=='approach':assert c==GAME and d==PET;self.trace.append([8,target(v),int(z),p['approach']]);rax=p['approach']
  elif name=='base':
   assert c==GAME and d==PET and arg(8)&255==1;self.trace.append([11,int(v)&255,signed(z),target(arg(5)),signed(arg(6)),signed(arg(7)),1,success]);rax=success
  elif name=='initialize':
   assert c==PET;self.trace.append([12,signed(d)]);uc.mem_write(v,struct.pack('<QQQiiQQ',OPAQUE[0],OPAQUE[1],pointer(77),-111,222,OPAQUE[2],OPAQUE[3]))
  elif name=='mode':assert c==GAME and v==1;self.trace.append([13,*self.request(d),success]);rax=success
  elif name=='skill':
   assert c==p['selector'];self.trace.append([14,int(c),signed(d),p['skill']]);rax=SKILL if p['skill']>=0 else 0
  elif name=='accepted':assert c==GAME and d==PET and z&255==1;self.trace.append([15,target(v),1,p['accepted']]);rax=p['accepted']
  elif name=='fatal':self.trace.append([99]);self.fatal=True;uc.emu_stop();return
  else:raise AssertionError(name)
  self.ret(rax)
 def execute(self,kind,p,rows,old=-1,index=0):
  self.kind=kind;self.p=p;self.count=len(rows);self.active=index;self.fatal=False;self.trace=[]
  self.uc.mem_write(MEM,b'\0'*0x40000);self.uc.mem_write(STACK,b'\0'*0x40000)
  self.coords(PET,1,20,30,PATH);self.wi(PET+4,742);self.wq(PET+0x28,p['seed']);self.uc.mem_write(GAME+0x106,bytes([p['selector']]))
  for n in [1,2,3]:self.wq(GLOBAL+n*16,TABLE)
  self.wi(TABLE+0x11f0,p['base']);self.uc.mem_write(SKILL+0x25,bytes([max(0,p['skill'])]));self.uc.mem_write(MON+0xf1,bytes([max(0,p['monster'])]))
  for i,row in enumerate(rows):self.uc.mem_write(SLOTS+40*i,encode(row))
  t=p['shape'];unit=pointer(70);self.wi(unit,t[0]);self.wq(unit+0x38,TARGETPATH if t[1] else 0)
  if t[1]:
   if t[0] in [2,4,5]:self.wi(TARGETPATH+0x10,t[2]);self.wi(TARGETPATH+0x14,t[3])
   else:self.uc.mem_write(TARGETPATH+2,struct.pack('<H',t[2]&65535));self.uc.mem_write(TARGETPATH+6,struct.pack('<H',t[3]&65535))
  self.uc.mem_write(CFG+0x14,struct.pack('<H',p['flags']));self.wi(CFG+0x3c,p['minimum']);self.wi(CFG+0x40,p['maximum']);self.wq(CFG+0x50,pointer(7));self.uc.mem_write(CFG+0x58,struct.pack('<3i',8,9,10))
  self.wq(VEC,SLOTS);self.wq(VEC+8,len(rows));self.wq(VEC+16,0x8000000000000010)
  rsp=STACK+0x20008;self.wq(rsp,STOP);self.wq(rsp+0x28,VEC if kind in ['weighted','composed'] else SLOTS+index*40)
  entry=DISP if kind=='disposition' else ALT if kind in ['weighted','composed'] else ACT
  r9=SLOTS+old*40 if old>=0 else 0
  for n,x in [(UC_X86_REG_RSP,rsp),(UC_X86_REG_RCX,GAME),(UC_X86_REG_RDX,PET),(UC_X86_REG_R8,CFG),(UC_X86_REG_R9,SLOTS+index*40 if kind=='disposition' else r9)]:self.uc.reg_write(n,x)
  self.uc.emu_start(entry,STOP,count=50000);assert self.fatal or self.uc.reg_read(UC_X86_REG_RIP)==STOP
  raw=self.uc.reg_read(UC_X86_REG_RAX);self.upper_boolean=not self.fatal and kind!='disposition' and (raw&0xffffffff)!=(raw&255)
  ret=-1 if self.fatal else raw&(0xffffffff if kind=='disposition' else 255)
  return [ret,[target(self.q(CFG+0x50)),self.i(CFG+0x58),self.i(CFG+0x5c),self.i(CFG+0x60)],self.q(PET+0x28),self.rows(),self.trace]
def sample(**values):
 p=dict(selector=2,flags=0,minimum=25,maximum=625,distance=25,collision4=0,collision0=1,move=0,monster=0,approach=0,execute=1,skill=8,accepted=1,base=99,seed=0x29900001234,shape=[1,1,45,55],success=[]);p.update(values);return p
def candidate(rating=10,skill=41,base=0,kind=0,xy=(0,0),counter=24,target=70,index=0):return [target,packxy(*xy),rating,skill,index,16,counter,kind,base,91,92]
def build(check=False):
 em=Actions();cases=[];counts={};upper_booleans=0
 def case(kind,p,rows,old=-1,index=0):
  nonlocal upper_booleans
  result=em.execute(kind,p,rows,old,index);upper_booleans+=em.upper_boolean;cases.append([kind,p,rows,old,index,result]);counts[kind]=counts.get(kind,0)+1
 for kind,distance,limits,c4,c0,monster,approach,move in itertools.product([4,5,11,12],[0,16,17,36,37,625,626],[(25,625),(625,25),(0,0),(289,625)],[0,1],[0,1],[-1,0,128],[0,1],[0,1]):
  case('disposition',sample(distance=distance,minimum=limits[0],maximum=limits[1],collision4=c4,collision0=c0,monster=monster,approach=approach,move=move),[candidate(kind=kind)])
 for kind,target_id,shape in itertools.product([0,2,4,5,6,11,12,255],[0,70],[[1,1,45,55],[1,0,0,0],[2,1,-10,-20],[4,1,70000,-70000],[0,1,65535,1]]):
  if kind in [5,11] and not target_id:continue
  case('disposition',sample(shape=shape,distance=15),[candidate(kind=kind,target=target_id)])
 for kind,xy,base,skill,success,old,route,flags in itertools.product([0,2,4,5,6,11,12],[(0,0),(1,0),(0,1),(-1,-2),(3,-2)],[0,1],[-1,0,8],[0,1],[-1,0,1],[(25,0,0),(0,1,0),(0,0,0),(700,0,1)], [0,2]):
  distance,move,approach=route;rows=[candidate(kind=kind,xy=xy,base=base),candidate(index=1)];case('act',sample(distance=distance,move=move,approach=approach,skill=skill,execute=success,flags=flags),rows,old)
 for selector,counter,old,base,accepted in itertools.product([0,1,2,3,4,255],[-2147483648,-1,0,24,25,2147483647],[-1,0,1],[0,1],[0,1]):
  case('act',sample(selector=selector,distance=0,move=1,accepted=accepted),[candidate(kind=5,counter=counter,base=base),candidate(index=1)],old)
 vectors=[[],[1],[1,2,3],[-2147483648,2,3],[0,-5,-1],[2147483647,2,3],[-2147483648,2147483647,1],[2147483646,1,1],[2147483646,1],[0,0,0],list(range(1,17))]
 seeds=[0,1,666<<32,1<<32,2<<32,3<<32,0xffffffffffffffff,0x123456789abcdef0]
 for values,seed,success,old,composition in itertools.product(vectors,seeds,[[],[0],[1],[2],[0,2],list(range(16))],[-1,0],['weighted','composed']):
  rows=[candidate(rating=v,skill=0 if i==0 else 41+i,base=int(i%3==1),kind=5 if i%4==3 else 0,index=i) for i,v in enumerate(values)];case(composition,sample(seed=seed,success=success),rows,old if rows else -1)
 out=json.dumps(cases,separators=(',',':'))+'\n'
 if check:assert OUT.read_text()==out
 else:OUT.write_text(out)
 libraries=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libraries)==1
 artifacts=[OUT,Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_decisions.py',ROOT/'tools/probe_warlock_runtime.py',*em.artifacts]
 result=dict(referenceVersion='3.3.93847',component='Current GenericPet action/disposition/weighted-choice helpers',processMemoryRead=False,targetProcessCodeExecuted=False,sourceCodeExecutedInOfflineEmulator=True,completeAiVerified=False,handlerEnabled=False,verified=dict(contexts=len(cases),byKind=counts,machineCodeBytes=sum(b-a for a,b in em.ranges),fatalBoundaries=sum(row[-1][0]==-1 for row in cases),booleanReturnUpperWordControls=upper_booleans),emulatorLibrary=dict(path=str(libraries[0].relative_to(ROOT)),sha256=digest(libraries[0].read_bytes())),artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in sorted(set(artifacts))},services=['Range/collision,target metadata,mode/base execution,skill/monster rows,approach and skill selection are declared services. Their arguments,order and effects are compared;real world behavior is not inferred. The declared mode initializer supplies target/coordinates plus four opaque words,which the wrapper must preserve except the selected fields.','Boolean action/chooser results are consumed from AL by their captured callers;the disposition uses EAX. Unspecified upper bits are not a return value. Initial fixture register-width diagnostics remain preserved.','The disposition body executes as captured inside action;weighted composition executes all three captured helpers together. A separate weighted matrix uses declared action acceptance to isolate cumulative weights,edge sentinels,fallback retries and exact inline pet RNG. Null target on types5/11is an invalid source descriptor and excluded,rather than inventing a null guard. Dynamic allocation/reentrant vector resizing are not certified.'],remaining=['Native policies and real AI integration remain required. Current gathering/rating/ordering,mode/skill effects,world metadata/geometry,full actor lifecycle and complete dispatch linkage are separate gaps. No learned demon or class completion is enabled.'])
 receipt=ROOT/'tools/reference/generic-pet-actions-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert receipt.read_text()==encoded
 else:receipt.write_text(encoded)
 print('Current GenericPet actions:',json.dumps(result['verified']))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
