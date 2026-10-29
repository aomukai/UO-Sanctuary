#!/usr/bin/env python3
"""Run captured buff preparation,recipient callback and vector helpers offline."""
import argparse,itertools,json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE,UC_HOOK_MEM_READ
from unicorn.x86_const import *
from probe_generic_pet_decisions import Emulator,BASE,STACK,MEM,STOP,GAME,PET,OWNER,CFG,PATH
from probe_warlock_runtime import ROOT,STAGE,Image,digest
OUT=ROOT/'.local/class-skills/generic-pet-preparation-source-probe.json'
SLOTS=MEM+0x10000;VEC=MEM+0x18000;TABLE=MEM+0x19000;STATES=MEM+0x20000;SKILL=MEM+0x28000;UNITS=MEM+0x30000;HEAP=MEM+0x50000;ALLOC=MEM+0x58000;VTABLE=MEM+0x59000
GLOBAL=BASE+0x1e03ac0;PREP=BASE+0x4b2810;CALLBACK=BASE+0x4b25f0;THUNK=BASE+0x4b25e0;TRAMP=STOP+0x1000;FREE=STOP+0x2000
FNS={k:BASE+v for k,v in dict(skill=0x6bf40,state=0x1b0cf0,has=0x296bc0,iterate=0x377760,nested=0x35e6d0,distance=0x40bfd0,grow=0xab410,allocator=0xbaa540,cookie=0x155eeb0,fatal=0x7b6af0).items()};FNS['free']=FREE
REGS=[UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]
def signed(n):return (n+2**31)%2**32-2**31
def pointer(n):return UNITS+n*256 if n else 0
def identity(p):
 if not p:return 0
 assert UNITS<p<UNITS+0x10000 and (p-UNITS)%256==0;return (p-UNITS)//256
def encode(r):return struct.pack('<QQ5i4B',pointer(r[0]),r[1]&0xffffffffffffffff,*r[2:7],*r[7:])
def decode(b):
 r=list(struct.unpack('<QQ5i4B',b));r[0]=identity(r[0]);return r
class Preparation(Emulator):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.image=Image();self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.artifacts=[]
  for name,rvas in [('callees-live-actions-execute-20261029.json',[0x4b2810]),('callees-live-preparation-services-20261029.json',[0x4b6cd0,0x4b6c30]),('live-preparation-callback-20261029.json',[0x4b25f0]),('live-preparation-followup-20261029.json',[0x4b25e0])]:
   pp=STAGE/name;proof=json.loads(pp.read_text());self.artifacts.append(pp)
   for n,h in proof['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
   for rva in rvas:
    record=next(r for r in proof['records'] if int(r['rva'],16)==rva)
    assert record['allUnwindFragmentsCaptured'] or (rva==0x4b25e0 and record['boundedLeafReference'] and record['bytes']==16)
    for f in record['fragments']:
     assert f['captured'] and not f['invalidInstructionMarkers'];a=int(f['rva'],16);n=int(f['endRva'],16)-a
     paths=[ROOT/p for p,h in proof['artifacts'].items() if p.endswith('.bin') and h==f['sha256']];assert len(paths)==1
     p=paths[0];data=p.read_bytes();assert len(data)==n and digest(data)==f['sha256'];self.map(BASE+a,n);self.uc.mem_write(BASE+a,data);self.ranges.append((BASE+a,BASE+a+n));self.artifacts.append(p)
  assert bytes(self.uc.mem_read(THUNK,16))==bytes.fromhex('498bd04d8bc1e905000000cccccccccc')
  self.map(STACK,0x40000);self.map(MEM,0x60000);self.map(STOP,0x3000);self.map(GLOBAL,64);self.map(BASE+0x197a178,8);self.wq(BASE+0x197a178,0)
  for addr in FNS.values():self.map(addr,1);self.uc.mem_write(addr,b'\xc3')
  self.uc.hook_add(UC_HOOK_CODE,self.hook);self.uc.hook_add(UC_HOOK_MEM_READ,self.count_hook,begin=TABLE+0x2d0,end=TABLE+0x2d7);self.uc.hook_add(UC_HOOK_MEM_READ,self.state_hook,begin=STATES,end=STATES+68*32-1)
 def count_hook(self,uc,access,address,size,value,user):
  assert address==TABLE+0x2d0 and size in [4,8];self.trace.append([2,self.p['selector'],int(size==8),len(self.p['states'])])
 def state_hook(self,uc,access,address,size,value,user):
  if self.rawStateRead and size==2 and (address-STATES)%68==30:
   idx=(address-STATES)//68;self.trace.append([5,self.p['selector'],idx,self.p['states'][idx] or 0])
 def ret(self,value=0):
  rsp=self.uc.reg_read(UC_X86_REG_RSP);ret=self.q(rsp);self.uc.reg_write(UC_X86_REG_RAX,value&0xffffffffffffffff);self.uc.reg_write(UC_X86_REG_RSP,rsp+8);self.uc.reg_write(UC_X86_REG_RIP,ret)
 def step(self):
  job=self.jobs[-1]
  if job['index']==len(job['units']):self.jobs.pop();self.uc.reg_write(UC_X86_REG_RSP,job['rsp']);self.ret();return
  unit=job['units'][job['index']];job['index']+=1;job['unit']=unit;rsp=job['rsp']-0x100;self.wq(rsp,TRAMP)
  args=[GAME,pointer(unit),job['context'],0] if job['kind']==6 else [GAME,pointer(job['parent']),pointer(unit),job['context']]
  self.uc.reg_write(UC_X86_REG_RSP,rsp)
  for reg,value in zip(REGS,args):self.uc.reg_write(reg,value)
  self.uc.reg_write(UC_X86_REG_RIP,job['callback'])
 def hook(self,uc,a,size,user):
  if a==TRAMP:self.step();return
  # Direct table state-group loads occur in this captured scan,not in the
  # explicit lookup service. The memory hook preserves the distinction.
  if a==BASE+0x4b2aa6:self.rawStateRead=True
  if a==BASE+0x4b2ab7 or a==BASE+0x4b2b5d:self.rawStateRead=False
  if any(lo<=a<hi for lo,hi in self.ranges):return
  name=next((k for k,v in FNS.items() if v==a),None);assert name,('Undeclared helper',hex(a))
  c,d,v,z=[uc.reg_read(n) for n in REGS];p=self.p;rax=0
  if name=='skill':
   assert c&255==p['selector'];sid=signed(d);info=p['skills'].get(str(sid));self.trace.append([1,p['selector'],sid,info])
   if info is not None:uc.mem_write(SKILL+0xa0,struct.pack('<h',info[0]));uc.mem_write(SKILL+39,bytes([info[1]]));rax=SKILL
  elif name=='state':
   idx=signed(d);assert c&255==p['selector'] and 0<=idx<len(p['states']);value=p['states'][idx];self.trace.append([3,p['selector'],idx,value]);rax=STATES+idx*68 if value is not None else 0
  elif name=='has':
   unit=identity(c);state=signed(d);value=int(state in p['has'].get(str(unit),[]));self.trace.append([4,unit,state,value]);rax=value
  elif name in ['iterate','nested']:
   assert c==GAME and v==(CALLBACK if name=='iterate' else THUNK);kind=6 if name=='iterate' else 7;unit=identity(d)
   assert unit==p['owner'] if kind==6 else unit==self.jobs[-1]['unit'];units=p['visits'] if kind==6 else p['nested'].get(str(unit),[])
   self.trace.append([kind,unit,units]);self.jobs.append(dict(kind=kind,parent=unit,rsp=uc.reg_read(UC_X86_REG_RSP),callback=v,context=z,index=0,units=units));assert len(self.jobs)<8;self.step();return
  elif name=='distance':
   assert identity(c)==1;unit=self.jobs[-1]['unit'];info=p['units'][str(unit)];x,y=(info[2],info[3]) if info[1] else (0,0)
   if info[0] not in [2,4,5]:x&=65535;y&=65535
   assert (signed(d),signed(v))==(x,y);value=p['distance'].get(str(unit),25);self.trace.append([8,1,x,y,value]);rax=value
  elif name=='grow':
   count=self.q(c+8);assert count==10 or count>10;old=self.q(c);new=HEAP+self.allocations*1024;self.allocations+=1;assert self.allocations<32
   uc.mem_write(new,bytes(uc.mem_read(old,count*4)));self.wq(c,new);self.wq(c+16,d);self.grows+=1
  elif name=='allocator':rax=ALLOC
  elif name=='free':assert c==ALLOC and HEAP<=d<HEAP+0x8000;self.frees+=1
  elif name=='fatal':self.trace.append([99]);self.fatal=True;uc.emu_stop();return
  elif name!='cookie':raise AssertionError(name)
  self.ret(rax)
 def execute(self,p,rows):
  self.p=p;self.trace=[];self.jobs=[];self.rawStateRead=False;self.fatal=False;self.allocations=0;self.grows=0;self.frees=0
  self.uc.mem_write(MEM,b'\0'*0x60000);self.uc.mem_write(STACK,b'\0'*0x40000);self.uc.mem_write(GAME+0x106,bytes([p['selector']]))
  for n in [1,2,3]:self.wq(GLOBAL+n*16,TABLE)
  self.wq(TABLE+0x2d0,len(p['states']));self.wq(TABLE+0x2c8,STATES)
  for i,group in enumerate(p['states']):self.uc.mem_write(STATES+i*68+30,struct.pack('<h',group or 0))
  for n,info in p['units'].items():
   a=pointer(int(n));path=a+128 if info[1] else 0;self.coords(a,info[0],info[2] if info[0] in [2,4,5] else info[2]&65535,info[3] if info[0] in [2,4,5] else info[3]&65535,path)
  self.wq(ALLOC,VTABLE);self.wq(VTABLE+0x20,FREE)
  for i,row in enumerate(rows):self.uc.mem_write(SLOTS+i*40,encode(row))
  self.wq(VEC,SLOTS);self.wq(VEC+8,len(rows));self.wq(VEC+16,32)
  rsp=STACK+0x20008;self.wq(rsp,STOP)
  for reg,value in [(UC_X86_REG_RSP,rsp),*zip(REGS,[GAME,pointer(1),pointer(p['owner']),VEC])]:uc=self.uc;uc.reg_write(reg,value)
  self.uc.emu_start(PREP,STOP,count=200000);assert self.fatal or self.uc.reg_read(UC_X86_REG_RIP)==STOP
  return [int(self.fatal),[decode(bytes(self.uc.mem_read(SLOTS+i*40,40))) for i in range(len(rows))],self.trace]
def row(skill=41,kind=1,base=0,target=7):return [target,0,-2147483648,skill,3,16,2,kind,base,91,92]
def sample(**values):
 p=dict(selector=2,owner=2,states=[0,5,5,0],skills={'41':[1,6],'42':[3,4]},has={'1':[1],'2':[1]},visits=[2,3,4],nested={'2':[3,4]},units={'1':[1,1,20,30],'2':[0,1,25,35],'3':[1,1,45,55],'4':[0,1,50,60],'5':[1,1,55,65],'6':[2,1,-10,-20]},distance={});p.update(values);return p

def build(check=False):
 em=Preparation();cases=[];grows=0;frees=0
 def case(p,rows):
  nonlocal grows,frees
  result=em.execute(p,rows);grows+=em.grows;frees+=em.frees;cases.append([p,rows,result])
 for selector,state,flags,kind,base,petHas,owner in itertools.product([0,1,2,3,4],[-1,0,1,3,4,32767],[0,2,4,6,255],[0,1,2],[0,1],[0,1],[0,2]):
  case(sample(selector=selector,owner=owner,skills={'41':[state,flags]},has={'1':[0,1,2,3] if petHas else []}),[row(kind=kind,base=base)])
 for visits,petStates,ownerStates,thirdStates,fourthStates,flags,distance in itertools.product([[2,3,4],[3,2,4],[3,4,2],[0,6,5],[4,3,2]],[[1],[1,3],[]],[[1],[]],[[1],[]],[[1],[]],[0,2,4,6],[1024,1025]):
  case(sample(visits=visits,has={'1':petStates,'2':ownerStates,'3':thirdStates,'4':fourthStates},skills={'41':[1,flags],'42':[3,4]},distance={'2':distance,'3':distance,'4':distance}),[row(),row(42)])
 # Different groups,duplicate descriptors,missing rows,negative group IDs and
 # growing state-ID arrays exercise the actual captured container/cleanup code.
 for states,skills in itertools.product([[0]*16,[3]*16,[-1]*16,[0,1,2,1,2,0],[None,1,1,0]], [ {'41':[0,6],'42':[1,4]}, {'41':[1,6],'42':[1,6]}, {'41':[0,0]}, {} ]):
  for has in [{},{'1':list(range(16)),'2':list(range(16))}]:case(sample(states=states,skills=skills,has=has),[row(),row(42),row(41)])
 for kind,path,xy,distance in itertools.product([0,1,2,4,5,6],[0,1],[(0,0),(-10,-20),(70000,-70000)],[0,1024,1025]):
  if kind in [2,4,5] and not path:continue
  p=sample(visits=[3],has={'1':[1]},units={'1':[1,1,20,30],'2':[0,1,25,35],'3':[kind,path,*xy]},distance={'3':distance});case(p,[row()])
 case(sample(),[])
 out=json.dumps(cases,separators=(',',':'))+'\n'
 if check:assert OUT.read_text()==out
 else:OUT.write_text(out)
 libs=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libs)==1
 artifacts=[OUT,Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_decisions.py',ROOT/'tools/probe_warlock_runtime.py',*em.artifacts]
 result=dict(referenceVersion='3.3.93847',component='Current GenericPet buff preparation and recipient callbacks',processMemoryRead=False,targetProcessCodeExecuted=False,sourceCodeExecutedInOfflineEmulator=True,completeAiVerified=False,handlerEnabled=False,verified=dict(contexts=len(cases),fatalBoundaries=sum(r[-1][0] for r in cases),capturedBytes=sum(b-a for a,b in em.ranges),boundedLeafBytes=16,leafInstructionsBytes=11,stateVectorGrowths=grows,stateVectorFrees=frees),emulatorLibrary=dict(path=str(libs[0].relative_to(ROOT)),sha256=digest(libs[0].read_bytes())),artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in sorted(set(artifacts))},services=['Skill/state metadata,state presence,owner and nested enumeration order/membership,and distance are declared world services. The actual captured callback and its11-byte leaf thunk execute for each supplied visit;iterator bodies are not replaced with guessed world traversal.','The source vector append/destructor execute. Matrices use at most three buff groups within ten embedded group slots. State-ID reserve/allocation/free are declared memory services,including arrays beyond ten IDs;group-vector growth and allocator semantics are not certified.','The leaf has no unwind entry. Its16-byte bounded range ends at the preserved callback and contains exactly11bytes of register forwarding/tail jump plus five unreachable INT3 padding bytes;no unwind completeness is claimed for it.'],remaining=['Native policy verification,real world state/iterator/geometry bindings and full actor lifecycle remain required. Target search/rating/ordering and real skills/modes are separate. No learned demon is enabled.'])
 receipt=ROOT/'tools/reference/generic-pet-preparation-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert receipt.read_text()==encoded
 else:receipt.write_text(encoded)
 print('Current GenericPet preparation:',json.dumps(result['verified']))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
