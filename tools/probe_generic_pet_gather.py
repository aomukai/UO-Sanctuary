#!/usr/bin/env python3
"""Execute three captured current descriptor/search-mask helpers offline."""
import argparse,itertools,json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE,UC_HOOK_MEM_READ
from unicorn.x86_const import *
from probe_generic_pet_decisions import Emulator,BASE,STACK,MEM,STOP,PET,CFG
from probe_warlock_runtime import ROOT,STAGE,Image,digest
OUT=ROOT/'.local/class-skills/generic-pet-gather-source-probe.json'
SLOTS=MEM+0x10000;VEC=MEM+0x18000;TABLE=MEM+0x19000;MON=MEM+0x1b000;ENTRIES=MEM+0x20000;HEAD=MEM+0x2f000;SKILL=MEM+0x30000;GLOBAL=BASE+0x1e03ac0
GATHER=BASE+0x4b42f0;MASK=BASE+0x4b30f0;FORMAT=BASE+0x4b4510
FNS={k:BASE+v for k,v in dict(append=0x4b6a90,monster=0x6bc90,rank=0x28bcf0,skill=0x6bf40,bonus=0x28b240,fatal=0x7b6af0).items()}
def signed(n):return (n+2**31)%2**32-2**31
def encode(r):return struct.pack('<QQ5i4B',r[0],r[1]&0xffffffffffffffff,*r[2:7],*r[7:])
def decode(b):return list(struct.unpack('<QQ5i4B',b))
class Gather(Emulator):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.image=Image();self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.artifacts=[]
  for name,rv in [('callees-live-actions-targets-20261029.json',[0x4b42f0,0x4b30f0]),('callees-live-skill-descriptor-20261029.json',[0x4b4510])]:
   pp=STAGE/name;proof=json.loads(pp.read_text());self.artifacts.append(pp)
   for n,h in proof['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
   for rva in rv:
    record=next(r for r in proof['records'] if int(r['rva'],16)==rva);assert record['allUnwindFragmentsCaptured']
    for f in record['fragments']:
     a=int(f['rva'],16);n=int(f['endRva'],16)-a
     paths=[ROOT/p for p in proof['artifacts'] if p.endswith('.bin') and digest((ROOT/p).read_bytes())==f['sha256']];assert len(paths)==1
     p=paths[0];data=p.read_bytes();assert len(data)==n;self.map(BASE+a,n);self.uc.mem_write(BASE+a,data);self.ranges.append((BASE+a,BASE+a+n));self.artifacts.append(p)
  self.constant=self.image.bytes(0x1816860,16);assert struct.unpack('<4i',self.constant)==(0,0,-2147483648,-1)
  self.map(BASE+0x1816860,16);self.uc.mem_write(BASE+0x1816860,self.constant)
  self.map(STACK,0x40000);self.map(MEM,0x40000);self.map(STOP,4096);self.map(GLOBAL,64)
  for addr in FNS.values():self.map(addr,1);self.uc.mem_write(addr,b'\xc3')
  self.uc.reg_write(UC_X86_REG_CR0,(self.uc.reg_read(UC_X86_REG_CR0)&~4)|2);self.uc.reg_write(UC_X86_REG_CR4,self.uc.reg_read(UC_X86_REG_CR4)|0x200)
  self.uc.hook_add(UC_HOOK_CODE,self.hook);self.uc.hook_add(UC_HOOK_MEM_READ,self.read_hook,begin=TABLE+0x11f0,end=TABLE+0x11f3)
  table=bytes(self.uc.mem_read(BASE+0x4b31d4,48));assert struct.unpack('<12I',table)==(0x4b3196,0x4b318c,0x4b317d,0x4b3182,0x4b31a9,0x4b3187,0x4b31a9,0x4b31a9,0x4b31a9,0x4b318c,0x4b317d,0x4b3191)
 def read_hook(self,uc,access,address,size,value,user):
  assert address==TABLE+0x11f0 and size==4;self.trace.append([5,self.selector,self.p['count']])
 def ret(self,value):
  rsp=self.uc.reg_read(UC_X86_REG_RSP);ret=self.q(rsp);self.uc.reg_write(UC_X86_REG_RAX,value&0xffffffffffffffff);self.uc.reg_write(UC_X86_REG_RSP,rsp+8);self.uc.reg_write(UC_X86_REG_RIP,ret)
 def hook(self,uc,a,size,user):
  if any(lo<=a<hi for lo,hi in self.ranges):return
  name=next((k for k,v in FNS.items() if v==a),None);assert name,('Undeclared helper',hex(a))
  c,d,v,z=[uc.reg_read(n) for n in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]];rax=0;p=self.p;pet=PET if p['pet'] else 0
  if name=='append':
   assert c==VEC;row=decode(bytes(uc.mem_read(d,40)));count=self.q(VEC+8);assert count<32
   uc.mem_write(SLOTS+count*40,encode(row));self.wq(VEC+8,count+1);self.trace.append([0,*row])
  elif name=='monster':
   assert c&255==self.selector and signed(d)==(742 if p['pet'] else -1);slot=self.monsterCalls;self.monsterCalls+=1;assert slot<8
   self.trace.append([1,self.selector,signed(d),slot]);rax=MON
  elif name=='rank':
   assert c==pet and z&0xffffffff==0 and v&255==p['allow'];idx=(d-ENTRIES)//256;assert d==ENTRIES+idx*256 and 0<=idx<len(self.entries)
   value=self.entries[idx][4];self.trace.append([2,idx,p['allow'],value]);rax=value
  elif name=='skill':
   assert c&255==self.selector;sid=signed(d);info=p['info'].get(str(sid));self.trace.append([3,self.selector,sid,info]);rax=SKILL if info is not None else 0
   if info is not None:
    uc.mem_write(SKILL+36,bytes([info[0]]));uc.mem_write(SKILL+39,bytes([info[1]]));uc.mem_write(SKILL+728,bytes([info[2]]))
  elif name=='bonus':
   assert c==pet;sid=signed(d);value=p['bonus'].get(str(sid),0);self.trace.append([4,sid,value]);rax=value
  elif name=='fatal':self.trace.append([99]);self.fatal=True;uc.emu_stop();return
  else:raise AssertionError(name)
  self.ret(rax)
 def rows(self):return [decode(bytes(self.uc.mem_read(SLOTS+40*i,40))) for i in range(self.q(VEC+8))]
 def execute(self,kind,p,entries,rows):
  self.p=p;self.entries=entries;self.selector=p['selector'] if p['pet'] else 0;self.fatal=False;self.trace=[];self.monsterCalls=0
  self.uc.mem_write(MEM,b'\0'*0x40000);self.uc.mem_write(STACK,b'\xa5'*0x40000)
  self.wi(PET+4,742);self.uc.mem_write(PET+445,bytes([self.selector]));self.wq(PET+256,HEAD if entries is not None else 0);self.wq(HEAD,ENTRIES if entries else 0)
  for i,e in enumerate(entries or []):
   sid,raw16,raw68,raw76,rank=e;a=ENTRIES+i*256;self.wq(a,a+128);self.uc.mem_write(a+128,struct.pack('<h',sid));self.wq(a+8,a+256 if i+1<len(entries) else 0)
   for off,value in [(16,raw16),(68,raw68),(76,raw76)]:self.wi(a+off,value)
  for n in [1,2,3]:self.wq(GLOBAL+n*16,TABLE)
  self.wi(TABLE+0x11f0,p['count']);self.uc.mem_write(MON+0x1c4,struct.pack('<8h',*p['monster']))
  for off,val in [(24,p['gather']),(36,p['mode']),(40,p['type']),(108,p['aux'])]:self.wi(CFG+off,val)
  self.uc.mem_write(CFG+28,bytes([p['allow']]));self.uc.mem_write(CFG+20,struct.pack('<H',p['flags']))
  self.wq(VEC,SLOTS);self.wq(VEC+8,len(rows));self.wq(VEC+16,32)
  for i,row in enumerate(rows):self.uc.mem_write(SLOTS+40*i,encode(row))
  rsp=STACK+0x20008;self.uc.mem_write(rsp-34,struct.pack('<H',p['tail'][0]));self.uc.mem_write(rsp-130,struct.pack('<H',p['tail'][1]))
  def run(entry):
   self.wq(rsp,STOP)
   for r,value in [(UC_X86_REG_RSP,rsp),(UC_X86_REG_RCX,PET if p['pet'] else 0),(UC_X86_REG_RDX,CFG),(UC_X86_REG_R8,VEC)]:self.uc.reg_write(r,value)
   self.uc.emu_start(entry,STOP,count=30000);assert self.fatal or self.uc.reg_read(UC_X86_REG_RIP)==STOP
  mask=-1
  if kind!='mask':run(GATHER)
  if not self.fatal and kind!='gather':run(MASK);mask=self.uc.reg_read(UC_X86_REG_RAX)&0xffffffff
  return [int(self.fatal),self.rows(),self.i(CFG+108),mask,self.trace]
def sample(**values):
 p=dict(pet=1,selector=2,gather=1,allow=1,mode=16,type=2,aux=17,flags=2,count=100,tail=[0x1234,0xbeef],monster=[41,42,-1,-1,-1,-1,-1,-1],info={'41':[4,1,4],'42':[0,0,3]},bonus={'41':31,'42':-8});p.update(values);return p
def build(check=False):
 em=Gather();cases=[];counts={}
 def case(kind,p,entries=None,rows=None):
  result=em.execute(kind,p,entries,rows or []);cases.append([kind,p,entries,rows or [],result]);counts[kind]=counts.get(kind,0)+1
 entries=[[41,16,-1,0,3],[42,-2,-1,0,1],[41,99,0,0,8],[43,14,-1,1,9],[44,15,-1,0,0],[45,16,-1,0,-1]]
 lists=[None,[],entries,list(reversed(entries)),[[41,1,-1,1,2],[41,2,-1,0,4]],[[41,3,0,0,5],[41,4,-1,0,6]]]
 for gather,pet,selector,typ,allow,skills in itertools.product([-1,0,1,2,3],[0,1],[0,1,2,3,4,255],[0,2,256,258,-1],[0,1],lists):
  case('composed',sample(gather=gather,pet=pet,selector=selector,type=typ,allow=allow),skills,[[0,0,999,99,88,77,66,55,1,44,33]])
 for rank,raw68,raw76,info,aux,bonus in itertools.product([-2147483648,-1,0,1,3,2147483647],[-1,0,2],[0,1],[None,[0,1,0],[4,1,13],[255,255,255]],[-2147483648,2147483647],[-1,2147483647]):
  case('composed',sample(info={'41':info},aux=aux,bonus={'41':bonus},tail=[0xff00,0x00ff]),[[41,-2147483648,raw68,raw76,rank]])
 for ids,count,selector,skills in itertools.product([[-1]*8,[-2,0,41,42,99,100,32767,-32768],[41]*8,[42,41,42,41,42,41,42,41]],[0,41,42,43,100,32768],[0,1,2,3,4],lists):
  case('composed',sample(gather=0,monster=ids,count=count,selector=selector),skills)
 for typ,base,info,flags,pet in itertools.product(range(256),[0,1,255],[None,[0,0,0],[0,1,0],[255,255,255]],[0,2,65535],[0,1]):
  case('mask',sample(pet=pet,flags=flags,info={'41':info}),rows=[[0,0,-2147483648,41,3,16,0,typ,base,99,100]])
 for flags,selector in itertools.product([0,2],[0,1,2,3,255]):
  case('mask',sample(flags=flags,selector=selector),rows=[[0,0,0,41,1,16,0,t,0,0,0] for t in range(32)])
  case('mask',sample(flags=flags,selector=selector),rows=[])
 out=json.dumps(cases,separators=(',',':'))+'\n'
 if check:assert OUT.read_text()==out
 else:OUT.write_text(out)
 libs=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libs)==1
 artifacts=[OUT,Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_decisions.py',ROOT/'tools/probe_warlock_runtime.py',*em.artifacts]
 result=dict(referenceVersion='3.3.93847',component='Current GenericPet gather,descriptor and search-mask helpers',processMemoryRead=False,targetProcessCodeExecuted=False,sourceCodeExecutedInOfflineEmulator=True,completeAiVerified=False,handlerEnabled=False,verified=dict(contexts=len(cases),byKind=counts,machineCodeBytes=sum(b-a for a,b in em.ranges),fatalBoundaries=sum(r[-1][0] for r in cases)),constant=dict(rva='0x1816860',bytes=16,sha256=digest(em.constant),values=[0,0,-2147483648,-1],origin='Pinned executable immutable data;not a live process read'),inlineTable=dict(rva='0x4b31d4',entries=12,bytes=48,executedAsData=True),emulatorLibrary=dict(path=str(libs[0].relative_to(ROOT)),sha256=digest(libs[0].read_bytes())),artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in sorted(set(artifacts))},services=['Monster and skill rows,skill-level and flag4 value are explicit declared services. Synthetic linked skills preserve signed IDs,entry16/68/76 and order. Append copies into a fixed preallocated32-entry vector;allocation is outside scope.','Source leaves descriptor bytes38/39 unwritten. Two explicit caller stack-tail inputs preserve them independently for base and skill descriptors. These bytes have no inferred gameplay meaning.','Three captured bodies execute together in composed cases. Every service argument,order,descriptor,state108 update and resulting search mask is retained. The search-mask helper is a category bitmask,not a distance radius. Byte445 is a raw unit field;no difficulty meaning is inferred.'],remaining=['Native verification,real metadata providers,downstream search/preparation/rating/ordering and actor integration remain required. No learned demon enabled or full AI/lifecycle accepted.'])
 receipt=ROOT/'tools/reference/generic-pet-gather-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert receipt.read_text()==encoded
 else:receipt.write_text(encoded)
 print('Current GenericPet gather:',json.dumps(result['verified']))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
