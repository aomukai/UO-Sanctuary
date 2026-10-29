#!/usr/bin/env python3
"""Execute complete captured current scorer and routing/scorer compositions."""
import argparse,itertools,json,struct
from pathlib import Path
import unicorn
from unicorn import UC_HOOK_MEM_READ,UC_HOOK_MEM_INVALID,UcError
from unicorn.x86_const import *
from probe_generic_pet_rating_routing import Routing,BASE,STACK,MEM,STOP,GAME,CFG,ROW,SELECTION,SKILL
from probe_warlock_runtime import ROOT,STAGE,digest
LANDMARKS={'flags4':0x4b4691,'flags8':0x4b46a2,'state-type2':0x4b490e,'owner-type3':0x4b4a19,'ranged':0x4b4b05,'movement-type7':0x4b4b7a,'midpoint':0x4b4bf7,'random-destination':0x4b4d94,'owner-or-clear-target':0x4b4e6d,'owner-frame104':0x4b4f0c,'monster-gate':0x4b5000,'melee':0x4b5024,'collision':0x4b5081,'reject':0x4b50f0,'leave':0x4b50f8,'missing-pet-fatal':0x4b512d,'first-selector-fatal':0x4b516b,'loop-selector-fatal':0x4b514c}
OUT=ROOT/'.local/class-skills/generic-pet-target-scoring-source-probe.json'
SCORE=BASE+0x4b45e0;ROUTE=BASE+0x4b5200;GLOBAL=BASE+0x1e03ac0;TABLE=MEM+0x19000;STATES=MEM+0x20000;INFO=MEM+0x23000;UNITS=MEM+0x30000
FNS={k:BASE+v for k,v in dict(skill=0x6bf40,flag4=0x28b240,cost=0xf6f10,stat=0x24acd0,distance=0x40bfd0,state=0x1b0cf0,has=0x296bc0,owner0=0x35d0e0,owner1=0x35eb80,calc=0x2bae90,collision=0x27cee0,fatal=0x7b6af0).items()}
def signed(n):return (n+2**31)%2**32-2**31
def pointer(n):return UNITS+n*4096 if n else 0
def target(p):
 if not p:return 0
 assert UNITS<p<UNITS+8*4096 and (p-UNITS)%4096==0;return (p-UNITS)//4096
def encode(r):return struct.pack('<QQ5i4B',pointer(r[0]),r[1]&0xffffffffffffffff,*r[2:7],*r[7:])
def decode(b):
 r=list(struct.unpack('<QQ5i4B',b));r[0]=target(r[0]);r[1]=(r[1]+2**63)%2**64-2**63;return r
class Scoring(Routing):
 def __init__(self):
  super().__init__();self.landmarks=set();pp=STAGE/'callees-live-after-combat-rating-20261029.json';proof=json.loads(pp.read_text());self.artifacts.append(pp)
  for n,h in proof['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
  record=next(r for r in proof['records'] if r['rva']=='0x4b45e0');assert record['allUnwindFragmentsCaptured']
  for f in record['fragments']:
   a=int(f['rva'],16);n=int(f['endRva'],16)-a;paths=[ROOT/p for p,h in proof['artifacts'].items() if p.endswith('.bin') and h==f['sha256']];assert len(paths)==1
   p=paths[0];data=p.read_bytes();assert len(data)==n and digest(data)==f['sha256'];self.map(BASE+a,n);self.uc.mem_write(BASE+a,data);self.ranges.append((BASE+a,BASE+a+n));self.artifacts.append(p)
  assert struct.unpack('<13I',self.uc.mem_read(BASE+0x4b518c,52))==(0x4b46e8,0x4b4721,0x4b475a,0x4b47f5,0x4b4793,0x4b47c6,0x4b4836,0x4b4836,0x4b4836,0x4b4836,0x4b4836,0x4b4836,0x4b4793)
  assert struct.unpack('<13I',self.uc.mem_read(BASE+0x4b51c0,52))==(0x4b50f0,0x4b490e,0x4b49fd,0x4b5024,0x4b4b05,0x4b50f0,0x4b4b7a,0x4b4edf,0x4b50f0,0x4b50f0,0x4b4b05,0x4b4f73,0x4b5081)
  self.map(MEM,0x40000);self.map(GLOBAL,64)
  for addr in FNS.values():self.map(addr,1);self.uc.mem_write(addr,b'\xc3')
  self.uc.hook_add(UC_HOOK_MEM_READ,self.count_hook,begin=TABLE+0x2d0,end=TABLE+0x2d3)
  self.uc.hook_add(UC_HOOK_MEM_READ,self.monster_hook,begin=UNITS,end=UNITS+8*4096-1)
  self.uc.hook_add(UC_HOOK_MEM_INVALID,self.invalid)
 def count_hook(self,uc,access,address,size,value,user):
  assert address==TABLE+0x2d0 and size==4;self.trace.append([5,self.p['selector'],len(self.p['states'])])
 def monster_hook(self,uc,access,address,size,value,user):
  n=(address-UNITS)//4096;off=(address-UNITS)%4096
  if size==1 and 0x6f2<=off<=0x7f1:self.trace.append([11,n,off-0x6f2,self.p['units'][str(n)][12]])
 def invalid(self,uc,access,address,size,value,user):
  if uc.reg_read(UC_X86_REG_RIP)==BASE+0x4b490e and address==0xa2 and size==2:
   self.status=2;self.trace.append([99,2]);return False
  raise AssertionError(('Unexpected invalid source context',hex(uc.reg_read(UC_X86_REG_RIP)),hex(address),size))
 def hook(self,uc,a,size,user):
  if a-BASE in LANDMARKS.values():self.landmarks.add(a-BASE)
  reg=lambda r:uc.reg_read(r);rsp=reg(UC_X86_REG_RSP)
  if a==SCORE:
   context=self.q(rsp+0x28);candidate=self.q(rsp+0x30);assert context==SELECTION+40
   self.trace.append([20,decode(bytes(uc.mem_read(candidate,40))),*[self.i(context+off) for off in [0,4,8,12]]]);self.calls+=1
  if any(lo<=a<hi for lo,hi in self.ranges):return
  name=next((k for k,v in FNS.items() if v==a),None);assert name,('Undeclared scorer helper',hex(a))
  c,d,v,z=[reg(n) for n in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]];p=self.p;rax=0
  if name=='skill':
   assert c&255==p['selector'];self.trace.append([0,p['selector'],signed(d),p['skill']]);rax=SKILL if p['skill'] is not None else 0
  elif name=='flag4':self.trace.append([1,target(c),signed(d),p['flag4']]);rax=p['flag4']
  elif name=='cost':self.trace.append([2,int(c)&255,signed(d),p['cost']]);rax=INFO if p['cost'] else 0
  elif name=='stat':
   n=(c-UNITS-0x300)//4096;assert c==pointer(n)+0x300 and v==INFO and d&65535==0
   self.trace.append([3,n,signed(d),p['stat']]);rax=p['stat']
  elif name=='distance':self.trace.append([4,target(c),signed(d),signed(v),p['distance']]);rax=p['distance']
  elif name=='state':
   assert c&255==p['selector'];idx=signed(d);value=p['states'][idx];self.trace.append([6,p['selector'],idx,value]);rax=STATES+idx*68 if value is not None else 0
  elif name=='has':
   n=target(c);idx=signed(d);value=int(idx in p['has'].get(str(n),[]));self.trace.append([7,n,idx,value]);rax=value
  elif name=='owner0':self.trace.append([8,target(c),signed(d),p['owner0']]);rax=p['owner0']
  elif name=='owner1':self.trace.append([9,target(c),p['owner1']]);rax=p['owner1']
  elif name=='calc':
   self.trace.append([10,int(c)&255,target(d),signed(v),signed(z),signed(self.q(rsp+0x28)),p['calc']]);rax=p['calc']
  elif name=='collision':
   assert v==z==0;self.trace.append([12,target(c),target(d),0,0,p['collision']]);rax=p['collision']
  elif name=='fatal':self.status=1;self.trace.append([99,1]);uc.emu_stop();return
  self.ret(rax)
 def execute(self,kind,p,row):
  self.p=p;self.trace=[];self.status=0;self.calls=0;self.uc.mem_write(MEM,b'\0'*0x40000);self.uc.mem_write(STACK,b'\0'*0x40000)
  self.uc.mem_write(GAME+0x106,bytes([p['selector']]));self.uc.mem_write(GAME+0x104,bytes([p['game260']]))
  for i in [1,2,3]:self.wq(GLOBAL+i*16,TABLE)
  self.wi(TABLE+0x2d0,len(p['states']))
  for i,g in enumerate(p['states']):self.uc.mem_write(STATES+i*68+30,struct.pack('<H',g or 0))
  if p['skill'] is not None:
   f36,f39,quarter,bonus,element,state,cls,compare,calc=p['skill']
   for off,value in [(36,f36),(39,f39),(644,element)]:self.uc.mem_write(SKILL+off,bytes([value]))
   for off,value in [(532,quarter),(730,bonus),(162,state),(272,cls)]:self.uc.mem_write(SKILL+off,struct.pack('<h',value))
   self.uc.mem_write(SKILL+274,struct.pack('<b',compare));self.wi(SKILL+276,calc)
  for key,info in p['units'].items():
   n=int(key);a=pointer(n);k,path,x,y,cls,uid,selector,stats,md,mr,snapshot,index,mb=info
   self.coords(a,k,x if k in [2,4,5] else x&65535,y if k in [2,4,5] else y&65535,a+0x200 if path else 0)
   self.wi(a+4,cls);self.wi(a+8,uid);self.uc.mem_write(a+445,bytes([selector]));self.wq(a+0x88,a+0x300 if stats else 0);self.wq(a+0x10,a+0x400 if md else 0)
   self.wq(a+0x400,a+0x600 if mr else 0);self.uc.mem_write(a+0x430,bytes([4 if snapshot else 0]));self.uc.mem_write(a+0x490,bytes([index]));self.uc.mem_write(a+0x6f2,bytes([mb])*256)
  self.wq(pointer(1)+0x28,p['seed']);self.uc.mem_write(ROW,encode(row));self.uc.mem_write(SELECTION,struct.pack('<5Q',*[pointer(t) for t in p['targets']])+struct.pack('<4i',*p['words']))
  for off,key in [(44,'mode'),(52,'meleeMax'),(56,'melee'),(64,'rangedMax'),(68,'ranged'),(72,'random'),(76,'progressive'),(100,'frame100'),(104,'frame104'),(108,'frame108')]:self.wi(CFG+off,p[key])
  rsp=STACK+0x20008;self.wq(rsp,STOP);self.wq(rsp+0x28,SELECTION+40 if kind=='score' else SELECTION);self.wq(rsp+0x30,ROW)
  for r,value in [(UC_X86_REG_RSP,rsp),(UC_X86_REG_RCX,GAME),(UC_X86_REG_RDX,pointer(p['pet'])),(UC_X86_REG_R8,pointer(p['owner'])),(UC_X86_REG_R9,CFG)]:self.uc.reg_write(r,value)
  try:self.uc.emu_start(SCORE if kind=='score' else ROUTE,STOP,count=30000)
  except UcError:assert self.status==2
  assert self.status or self.uc.reg_read(UC_X86_REG_RIP)==STOP
  return [self.status,decode(bytes(self.uc.mem_read(ROW,40))),self.q(pointer(1)+0x28),self.trace]
def unit(kind=1,path=1,x=70,y=90,cls=8,uid=3,selector=3,stats=1,md=1,mr=1,snapshot=0,index=2,mb=25):return [kind,path,x,y,cls,uid,selector,stats,md,mr,snapshot,index,mb]
def sample(**values):
 p=dict(selector=2,game260=1,pet=1,owner=2,frame100=67,frame104=50,frame108=3,skill=[0,0,9,7,0,1,-1,-1,0x12345],flag4=0,cost=1,stat=0,distance=25,states=[0,5,5,0],has={},owner0=1,owner1=1,calc=6,collision=0,mode=12,meleeMax=225,melee=10,rangedMax=625,ranged=5,random=3,progressive=4,seed=0x1234567812345678,targets=[3,4,5,6,7],words=[0,0,0,3],units={'1':unit(x=20,y=30,cls=742,uid=73,selector=2,md=0,mr=0),'2':unit(kind=0,x=45,y=55,cls=0,uid=2,selector=2,md=0,mr=0),'3':unit(),'4':unit(kind=0,x=75,y=95,uid=4),'5':unit(kind=2,x=-10,y=-20,uid=5),'6':unit(kind=6,path=0,uid=6),'7':unit(x=80,y=100,uid=7)})
 p.update(values);return p
def candidate(kind=4,base=0,target=3,rank=3,rating=-2147483648):return [target,0x123456789abcdef,rating,41,rank,16,2147483647,kind,base,91,92]
def build(check=False):
 em=Scoring();cases=[];counts={};calls=0
 def case(kind,p,row):
  nonlocal calls
  result=em.execute(kind,p,row);calls+=em.calls;cases.append([kind,p,row,result]);counts[kind]=counts.get(kind,0)+1
 for typ,base,target_id,skill in itertools.product(range(256),[0,1],[0,3],[None,[0,0,9,7,0,1,-1,-1,0x12345]]):case('score',sample(skill=skill),candidate(typ,base,target_id))
 for element,value,stats,cost in itertools.product([*range(14),255],[-2147483648,-101,-100,-99,-66,-65,-33,-32,0,32,33,65,66,99,100,2147483647],[0,1],[0,1]):
  p=sample(stat=value,cost=cost);p['skill'][4]=element;p['units']['3'][7]=stats;case('score',p,candidate())
 for flags,stacks,field108,fields,rank in itertools.product([0,4,8,12,255],[-1,3,4],[-1,0,2,2147483647],[(-32768,-32768),(-7,9),(7,-9),(32767,32767)],[-1,1,2147483647]):
  p=sample(flag4=stacks,frame108=field108);p['skill'][0]=flags;p['skill'][2:4]=fields;case('score',p,candidate(rank=rank))
 for selector,state,states,has,rating in itertools.product([0,1,2,3,4],[-1,0,1,4,32767],[[0,5,5,0],[None,5,5,0],[65535,65535,0],[0],[]],[[],[0],[1],[2]],[-2147483648,71]):
  p=sample(selector=selector,states=states,has={'3':has});p['skill'][5]=state;case('score',p,candidate(2,rating=rating))
 for ownerKind,clsMatch,owner0,compare,current,limit,words in itertools.product([-1,0,1,2],[0,1],[-1,1,3],[-1,1],[-1,0,2,3,7],[-1,0,3,6],[[0,0,0,0],[0,0,-1,2],[0,0,0,3],[0,0,-1,5],[0,0,0,-1]]):
  p=sample(owner=0 if ownerKind<0 else 2,owner0=owner0,owner1=current,calc=limit,words=words);p['units']['2'][0]=max(0,ownerKind);p['skill'][6]=742 if clsMatch else -1;p['skill'][7]=compare;case('score',p,candidate(3))
 for typ,distance,frame,owner,bonuses,random in itertools.product([4,5,7,8,11,12,13],[0,111,112,113,625,626],[32,33,49,50,66,67,74,75],[0,2],[(3,2),(2,3)],[-1,0,1,2,3,32,2147483647]):
  case('score',sample(distance=distance,frame100=frame,owner=owner,melee=bonuses[0],ranged=bonuses[1],random=random),candidate(typ))
 for md,mr,snapshot,index,override,mb,typ in itertools.product([0,1],[0,1],[0,1],[0,1,2,255],[0,2,255],[0,24,25,255],[0,1,2]):
  p=sample(game260=index);p['units']['3']=unit(kind=typ,md=md,mr=mr,snapshot=snapshot,index=override,mb=mb);case('score',p,candidate(12))
 shapes=[unit(kind=k,path=path,x=x,y=y) for k,path in [(0,0),(0,1),(1,0),(1,1),(2,1),(4,1),(5,1),(6,0)] for x,y in [(-10,-20),(70000,-70000),(11,31)]]
 for petshape,targetshape,seed,frame,owner in itertools.product(shapes[::4],shapes[1::4],[0,1,0xffffffffffffffff,0x123456789abcdef0],[32,99],[0,2]):
  p=sample(seed=seed,frame100=frame,owner=owner,distance=626,melee=2,ranged=3);p['units']['1']=petshape;p['units']['3']=targetshape;case('score',p,candidate(7))
 for typ,flags,bonuses,targets,seed,frame in itertools.product(range(1,14),[0,1,4,5],[(1,2),(2,1)],[[0]*5,[3,4,5,6,7],[3,0,5,0,7],[0,4,0,0,0],[3,3,3,3,3]],[0,1,0xffffffffffffffff,0x123456789abcdef0],[32,67]):
  p=sample(seed=seed,frame100=frame,melee=bonuses[0],ranged=bonuses[1],targets=targets,owner=0);p['skill'][1]=flags;case('composed',p,candidate(typ))
 for flags,seed,targets in itertools.product([0,1],[0,1,0xffffffffffffffff,0x123456789abcdef0],[[3,4,5,6,7],[3,4,5,0,7]]):
  p=sample(seed=seed,frame100=32,owner=0,random=1,targets=targets);p['skill'][1]=flags;case('composed',p,candidate(7,rank=2147483624))
 for flags,frame,ownerFrame,owner,base,present in itertools.product([0,4],[32,33,66,67],[32,33,66,67],[0,2],[0,1],[0,1]):
  p=sample(frame100=frame,frame104=ownerFrame,owner=owner);p['skill'][1]=flags
  if not present:p['skill']=None
  case('score',p,candidate(8,base=base))
 for distance,collision,target_id in itertools.product([224,225,226],[0,1,256],[0,3]):case('score',sample(distance=distance,collision=collision),candidate(13,target=target_id))
 case('score',sample(pet=0),candidate())
 out=json.dumps(cases,separators=(',',':'))+'\n'
 if check:assert OUT.read_text()==out
 else:OUT.write_text(out)
 libs=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libs)==1
 artifacts=[OUT,Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_rating_routing.py',ROOT/'tools/probe_generic_pet_decisions.py',ROOT/'tools/probe_warlock_runtime.py',*em.artifacts]
 result=dict(referenceVersion='3.3.93847',component='Current GenericPet full per-target scoring and routing compositions',processMemoryRead=False,targetProcessCodeExecuted=False,sourceCodeExecutedInOfflineEmulator=True,completeAiVerified=False,handlerEnabled=False,verified=dict(contexts=len(cases),byKind=counts,scorerEntries=calls,capturedHelperBytes=sum(b-a for a,b in em.ranges),inlineTableBytes=156,fatalBoundaries=sum(r[-1][0]==1 for r in cases),unsupportedType2Metadata=sum(r[-1][0]==2 for r in cases),branchLandmarksVisited=[k for k,v in LANDMARKS.items() if v in em.landmarks],branchLandmarksUnvisited=[k for k,v in LANDMARKS.items() if v not in em.landmarks]),emulatorLibrary=dict(path=str(libs[0].relative_to(ROOT)),sha256=digest(libs[0].read_bytes())),artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in sorted(set(artifacts))},services=['Complete3092-byte scoring and720-byte routing bodies execute as captured. Skill/stat/state/monster metadata,distance/collision,owner counters and formula output are declared services;exact arguments and order are retained. Actual current pet RNG executes inline and its final64-bit seed is retained.','Three13-entry inline tables,total156bytes,are validated and executed as data. Required source type2metadata-null reads are recorded as explicit unsupported contexts;missing pet/invalid state selectors stop at the source fatal boundary. No fake scoring function replaces the scorer in compositions.','Current type7coordinate paths deliberately retain repeated X-field reads. Static paths require a valid path;dynamic null paths yield zero coordinates. The loop-only selector fatal requires a selector mutation during metadata calls and is not exercised;arbitrary reentrant metadata mutations are outside this matrix. RNG advancement for discarded temporary candidates is covered explicitly.'],remaining=['Managed/native verification,real metadata/formula/geometry/state/owner adapters,target search,ordering,mode/skill effects and complete actor lifecycle remain required. No learned demon enabled or class completed.'])
 receipt=ROOT/'tools/reference/generic-pet-target-scoring-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert receipt.read_text()==encoded
 else:receipt.write_text(encoded)
 print('Current GenericPet target scoring:',json.dumps(result['verified']))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
