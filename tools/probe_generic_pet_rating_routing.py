#!/usr/bin/env python3
"""Execute the captured current target-category rating wrapper offline."""
import argparse,itertools,json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import *
from probe_generic_pet_decisions import Emulator,BASE,STACK,MEM,STOP,GAME,PET,OWNER,CFG
from probe_warlock_runtime import ROOT,STAGE,Image,digest
OUT=ROOT/'.local/class-skills/generic-pet-rating-routing-source-probe.json'
ROW=MEM+0x10000;SELECTION=MEM+0x11000;SKILL=MEM+0x12000;ENTRY=BASE+0x4b5200
FNS={k:BASE+v for k,v in dict(skill=0x6bf40,rate=0x4b45e0).items()}
def signed(n):return (n+2**31)%2**32-2**31
def pointer(n):return MEM+0x18000+n*32 if n else 0
def target(p):
 if not p:return 0
 assert MEM+0x18000<p<MEM+0x1f000 and (p-MEM-0x18000)%32==0;return (p-MEM-0x18000)//32
def encode(r):return struct.pack('<QQ5i4B',pointer(r[0]),r[1]&0xffffffffffffffff,*r[2:7],*r[7:])
def decode(b):
 r=list(struct.unpack('<QQ5i4B',b));r[0]=target(r[0]);r[1]=(r[1]+2**63)%2**64-2**63;return r
def response(row,p,index):
 # Declared full per-target scorer. Distinct mutations prove copy/discard and
 # retry semantics;this is not a proposed gameplay scoring formula.
 r=list(row);r[0]=0 if p['retarget']==1 else 9 if p['retarget']==2 else r[0];r[1]=-9223372036854775808+index;r[2]=p['ratings'][index];r[4]=signed(r[4]+index+31);r[5]=-index-91;r[6]=signed(r[6]+1);r[9]=index+21;r[10]=index+71
 return r
class Routing(Emulator):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.image=Image();self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.artifacts=[]
  pp=STAGE/'callees-live-after-combat-actions-20261029.json';proof=json.loads(pp.read_text());self.artifacts.append(pp)
  for n,h in proof['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
  record=next(r for r in proof['records'] if r['rva']=='0x4b5200');assert record['allUnwindFragmentsCaptured']
  for f in record['fragments']:
   a=int(f['rva'],16);n=int(f['endRva'],16)-a;paths=[ROOT/p for p,h in proof['artifacts'].items() if p.endswith('.bin') and h==f['sha256']];assert len(paths)==1
   p=paths[0];data=p.read_bytes();assert len(data)==n and digest(data)==f['sha256'];self.map(BASE+a,n);self.uc.mem_write(BASE+a,data);self.ranges.append((BASE+a,BASE+a+n));self.artifacts.append(p)
  table=bytes(self.uc.mem_read(BASE+0x4b549c,52));assert struct.unpack('<13I',table)==(0x4b52a9,0x4b52a9,0x4b52bf,0x4b52f3,0x4b52f8,0x4b52cf,0x4b52fe,0x4b52cf,0x4b52cf,0x4b52cf,0x4b52bf,0x4b52f3,0x4b5402)
  self.map(STACK,0x40000);self.map(MEM,0x20000);self.map(STOP,4096)
  for a in FNS.values():self.map(a,1);self.uc.mem_write(a,b'\xc3')
  self.uc.reg_write(UC_X86_REG_CR0,(self.uc.reg_read(UC_X86_REG_CR0)&~4)|2);self.uc.reg_write(UC_X86_REG_CR4,self.uc.reg_read(UC_X86_REG_CR4)|0x200);self.uc.hook_add(UC_HOOK_CODE,self.hook)
 def ret(self,value=0):
  rsp=self.uc.reg_read(UC_X86_REG_RSP);ret=self.q(rsp);self.uc.reg_write(UC_X86_REG_RAX,value&0xffffffffffffffff);self.uc.reg_write(UC_X86_REG_RSP,rsp+8);self.uc.reg_write(UC_X86_REG_RIP,ret)
 def hook(self,uc,a,size,user):
  if any(lo<=a<hi for lo,hi in self.ranges):return
  c,d,v,z=[uc.reg_read(n) for n in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]];p=self.p
  if a==FNS['skill']:
   assert c&255==p['selector'];self.trace.append([0,p['selector'],signed(d),p['flags']]);
   if p['flags'] is not None:uc.mem_write(SKILL+39,bytes([p['flags']]))
   self.ret(SKILL if p['flags'] is not None else 0)
  elif a==FNS['rate']:
   assert (c,d,v,z)==(GAME,PET,OWNER,CFG);rsp=uc.reg_read(UC_X86_REG_RSP);context=self.q(rsp+0x28);candidate=self.q(rsp+0x30);assert context==SELECTION+40
   row=decode(bytes(uc.mem_read(candidate,40)));index=len(self.scores);assert index<len(p['ratings']);self.scores.append(row);self.trace.append([1,row,self.i(context),self.i(context+4),self.i(context+8),self.i(context+12)])
   uc.mem_write(candidate,encode(response(row,p,index)));self.ret()
  else:raise AssertionError(('Undeclared helper',hex(a)))
 def execute(self,p,row):
  self.p=p;self.trace=[];self.scores=[];self.uc.mem_write(MEM,b'\0'*0x20000);self.uc.mem_write(STACK,b'\0'*0x40000);self.uc.mem_write(GAME+0x106,bytes([p['selector']]))
  self.uc.mem_write(ROW,encode(row));self.uc.mem_write(SELECTION,struct.pack('<5Q',*[pointer(t) for t in p['targets']])+struct.pack('<4i',*p['words']))
  self.wi(CFG+0x38,p['melee']);self.wi(CFG+0x44,p['ranged']);self.wi(CFG+0x64,p['frame100'])
  rsp=STACK+0x20008;self.wq(rsp,STOP);self.wq(rsp+0x28,SELECTION);self.wq(rsp+0x30,ROW)
  for r,value in [(UC_X86_REG_RSP,rsp),(UC_X86_REG_RCX,GAME),(UC_X86_REG_RDX,PET),(UC_X86_REG_R8,OWNER),(UC_X86_REG_R9,CFG)]:self.uc.reg_write(r,value)
  self.uc.emu_start(ENTRY,STOP,count=10000);assert self.uc.reg_read(UC_X86_REG_RIP)==STOP
  return [decode(bytes(self.uc.mem_read(ROW,40))),self.trace]
def sample(**values):
 p=dict(selector=2,flags=None,retarget=0,targets=[1,2,3,4,5],words=[0,0,31,32],melee=10,ranged=10,frame100=49,ratings=[-2147483648,7,-4]);p.update(values);return p
def candidate(kind=0,base=0,target=7,rating=-2147483648):return [target,0x123456789abcdef,rating,41,3,16,2147483647,kind,base,91,92]
def build(check=False):
 em=Routing();cases=[]
 def case(p,row):cases.append([p,row,em.execute(p,row)])
 for kind,base,flags,target,rating in itertools.product(range(256),[0,1,255],[None,0,1,255],[0,7],[-2147483648,2147483647]):
  case(sample(flags=flags),candidate(kind,base,target,rating))
 for kind,targets,ratings,bonuses,words,frame in itertools.product([7,13],[[0,0,0,0,0],[1,0,3,0,5],[0,2,3,0,5],[1,2,0,0,5],[1,2,3,0,5],[1,1,1,0,1]],[[-2147483648]*3,[-2147483648,7,9],[7,9,11],[2147483647]*3],[(-1,0),(0,0),(1,0),(2147483647,-2147483648)],[[0,0,31,32],[3,3,-1,-2],[4,4,0,0],[-1,-1,-2147483648,2147483647]],[49,50]):
  case(sample(targets=targets,ratings=ratings,melee=bonuses[0],ranged=bonuses[1],words=words,frame100=frame),candidate(kind))
 for selector,kind,flags,targets,ratings in itertools.product([0,1,2,3,255],[0,1,6,7,13,255],[None,1],[[0]*5,[1,2,3,4,5]],[[0,1,2],[-2147483648]*3]):
  case(sample(selector=selector,flags=flags,targets=targets,ratings=ratings),candidate(kind))
 for mode,kind,ratings in itertools.product([0,1,2],[1,2,7,13],[[-2147483648,7,9],[7,9,11]]):
  case(sample(flags=1,retarget=mode,ratings=ratings),candidate(kind))
 out=json.dumps(cases,separators=(',',':'))+'\n'
 if check:assert OUT.read_text()==out
 else:OUT.write_text(out)
 libs=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libs)==1
 artifacts=[OUT,Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_decisions.py',ROOT/'tools/probe_warlock_runtime.py',*em.artifacts]
 result=dict(referenceVersion='3.3.93847',component='Current GenericPet target-category rating routing',processMemoryRead=False,targetProcessCodeExecuted=False,sourceCodeExecutedInOfflineEmulator=True,fullPerTargetScoringVerified=False,completeAiVerified=False,handlerEnabled=False,verified=dict(contexts=len(cases),capturedHelperBytes=sum(b-a for a,b in em.ranges),inlineTableBytes=52,scoreCalls=sum(sum(t[0]==1 for t in r[-1][1]) for r in cases)),emulatorLibrary=dict(path=str(libs[0].relative_to(ROOT)),sha256=digest(libs[0].read_bytes())),artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in sorted(set(artifacts))},services=['Skill flags and the mandatory per-target scoring callee are declared. Distinct payload mutations and supplied scores test exact prepass,copy/discard,retry and priority behavior;the fixture function is not a gameplay scoring formula.','The complete720-byte captured routing helper executes,including its52-byte inline13-entry jump table as data. Frame100 and selection word fields retain neutral raw names;no health/distance semantics are inferred.'],remaining=['The3092-byte current per-target scoring helper still requires implementation and independent comparison. Target search,world metadata,real actors/actions and lifecycle remain unfinished. No learned demon is enabled.'])
 receipt=ROOT/'tools/reference/generic-pet-rating-routing-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert receipt.read_text()==encoded
 else:receipt.write_text(encoded)
 print('Current GenericPet rating routing:',json.dumps(result['verified']))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
