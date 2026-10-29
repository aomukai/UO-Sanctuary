#!/usr/bin/env python3
"""Execute complete current registry,refresh and all traversal dispatch paths."""
import argparse,itertools,json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE,UC_HOOK_MEM_INVALID,UcError
from unicorn.x86_const import *
from probe_generic_pet_decisions import Emulator
from probe_warlock_runtime import ROOT,STAGE,Image,digest
BASE=0x140000000;MEM=0x50000000;STACK=0x20000000;STOP=0x40000000
UNITS=MEM+0x10000;PROXIES=MEM+0x20000;ROOMS=MEM+0x30000;NODES=MEM+0x40000;BUCKETS=MEM+0x45000;HEAP=MEM+0x50000;ALLOC=MEM+0x60000;VTABLE=MEM+0x61000
REGS=[UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]
CALLER=STOP+0x1000;OVERRIDE=STOP+0x1100;ALLOCATE=STOP+0x1200;CONTEXT=MEM+0x70000
OUT=ROOT/'.local/class-skills/generic-pet-traversal-source-probe.json'
def signed(n):return (n+2**31)%2**32-2**31
def s64(n):return (n+2**63)%2**64-2**63
def game(n):return MEM+(n-1)*0x4000 if n else 0
def unit(n):return UNITS+n*1024 if n else 0
def proxy(n):return PROXIES+n*2048 if n else 0
def room(n):return ROOMS+n*2048 if n else 0
def node(n):return NODES+n*32 if n else 0
def identity(ptr):
 if not ptr:return 0
 assert UNITS<ptr<UNITS+0x10000 and (ptr-UNITS)%1024==0;return (ptr-UNITS)//1024
def gameid(ptr):
 if not ptr:return 0
 assert MEM<=ptr<MEM+0xc000 and (ptr-MEM)%0x4000==0;return 1+(ptr-MEM)//0x4000
class Traversal(Emulator):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.image=Image();self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.artifacts=[]
  for name in ['callees-live-search-traversal-20261030.json','traversal-services-20261030.json']:
   p=STAGE/name;proof=json.loads(p.read_text());self.artifacts.append(p)
   for n,h in proof['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
   for record in proof['records']:
    assert record['allUnwindFragmentsCaptured']
    for f in record['fragments']:
     assert f['captured'] and not f['invalidInstructionMarkers'];ps=[ROOT/n for n,h in proof['artifacts'].items() if n.endswith('.bin') and h==f['sha256']];assert len(ps)==1
     data=ps[0].read_bytes();a=BASE+int(f['rva'],16);assert len(data)==int(f['endRva'],16)-int(f['rva'],16);self.map(a,len(data));self.uc.mem_write(a,data);self.ranges.append((a,a+len(data)));self.artifacts.append(ps[0])
  self.map(MEM,0x80000);self.map(STACK,0x40000);self.map(STOP,0x3000)
  self.dispatch=self.image.bytes(0x17243c0,64);assert struct.unpack_from('<IIQ',self.dispatch,16)==(0,0,0)
  self.vtable=self.image.bytes(0x1724390,16);assert struct.unpack('<2Q',self.vtable)==(BASE+0x5f370,BASE+0x40ecb0)
  self.map(BASE+0x17243c0,128);self.map(BASE+0x1724390,16);self.uc.mem_write(BASE+0x1724390,self.vtable)
  for a in [BASE+n for n in [0x370610,0x278450,0xbaa540,0x5f370,0x40ecb0,0x5f1b0,0x7b6af0]]+[CALLER,OVERRIDE,ALLOCATE]:self.map(a,1);self.uc.mem_write(a,b'\xc3')
  self.uc.reg_write(UC_X86_REG_CR0,(self.uc.reg_read(UC_X86_REG_CR0)&~4)|2);self.uc.reg_write(UC_X86_REG_CR4,self.uc.reg_read(UC_X86_REG_CR4)|0x200)
  self.uc.hook_add(UC_HOOK_CODE,self.hook);self.uc.hook_add(UC_HOOK_MEM_INVALID,self.invalid)
 def ret(self,value=0):
  rsp=self.uc.reg_read(UC_X86_REG_RSP);to=self.q(rsp);self.uc.reg_write(UC_X86_REG_RAX,value&0xffffffffffffffff);self.uc.reg_write(UC_X86_REG_RSP,rsp+8);self.uc.reg_write(UC_X86_REG_RIP,to)
 def invalid(self,uc,access,address,size,value,user):
  if uc.reg_read(UC_X86_REG_RIP)==BASE+0x3335e3 and address==0x60:
   self.failed=True;self.trace.append([99]);return False
  raise AssertionError(('Unknown traversal memory boundary',hex(uc.reg_read(UC_X86_REG_RIP)),hex(address),size))
 def hook(self,uc,a,size,user):
  if any(lo<=a<hi for lo,hi in self.ranges):return
  c,d,v,z=[uc.reg_read(r) for r in REGS];p=self.p
  if a==BASE+0x370610:
   key=f'{gameid(d)}:{signed(c)}:{signed(v)}';first=p['buckets'].get(key,0);self.trace.append([0,gameid(d),signed(c),signed(v),first]);slot=BUCKETS+len(self.bucketSlots)*16
   if key not in self.bucketSlots:self.bucketSlots[key]=slot
   slot=self.bucketSlots[key];self.wq(slot,unit(first));self.ret(slot);return
  if a==BASE+0x278450:
   which=identity(c);value=p['roomMap'].get(str(which),0);self.trace.append([1,which,value]);self.ret(room(value));return
  if a in [CALLER,OVERRIDE]:
   assert c==game(p['game']) and identity(d)==p['subject'] and z==CONTEXT
   target=identity(v);kind=1 if a==CALLER else 2;values=p['results'] if kind==1 else p['overrideResults'];result=values.get(str(target),0);self.trace.append([2,kind,target,result])
   if str(target) in p['change160']:self.wq(unit(target)+0x160,unit(p['change160'][str(target)]))
   if str(target) in p['changeNodes']:
    n,nxt=p['changeNodes'][str(target)];self.wq(node(n)+8,node(nxt))
   self.ret(result);return
  if a==BASE+0xbaa540:self.ret(ALLOC);return
  if a==ALLOCATE:
   assert c==ALLOC and d==64 and v==16;self.allocations+=1;self.ret(HEAP);return
  if a==BASE+0x40ecb0:
   # Declared type-erasure ABI bridge;the actual57-byte forwarding callback
   # executes after unpacking its six arguments from the stored closure.
   obj=c;target=self.q(d);assert self.q(obj+8)==BASE+0x40d9b0;rsp=uc.reg_read(UC_X86_REG_RSP)
   args=[target,self.q(obj+0x30),self.q(obj+0x28),self.q(obj+0x20)]
   self.wq(rsp+0x28,self.q(obj+0x18));self.wq(rsp+0x30,self.q(obj+0x10))
   for reg,value in zip(REGS,args):uc.reg_write(reg,value)
   uc.reg_write(UC_X86_REG_RIP,BASE+0x40d9b0);return
  if a==BASE+0x5f370:self.disposals+=1;self.ret();return
  if a==BASE+0x5f1b0:self.ret();return
  if a==BASE+0x7b6af0:self.failed=True;self.trace.append([99]);uc.emu_stop();return
  raise AssertionError(('Unknown traversal helper',hex(a)))
 def execute(self,p):
  self.p=p;self.trace=[];self.failed=False;self.allocations=self.disposals=0;self.bucketSlots={};self.uc.mem_write(MEM,b'\0'*0x80000);self.uc.mem_write(STACK,b'\0'*0x40000)
  for key,u in p['units'].items():
   n=int(key);a=unit(n);kind,mode,ident,flags,flags2,path,target,typ,tid,data,ai,og,ot,oid,first=u
   for off,value in [(0,kind),(12,mode),(8,ident),(0x124,flags),(0x128,flags2)]:self.wi(a+off,signed(value))
   self.wq(a+0x38,a+0x200 if path else 0);self.wq(a+0x270,unit(target));self.wi(a+0x278,typ);self.wi(a+0x27c,tid)
   self.wq(a+0x10,a+0x280 if data else 0);self.wq(a+0x2b8,a+0x300 if ai else 0);self.wq(a+0x360,game(og));self.wi(a+0x368,oid);self.wi(a+0x36c,ot);self.wq(a+0x370,node(first))
  for n,nxt in p['next158'].items():self.wq(unit(int(n))+0x158,unit(nxt))
  for n,nxt in p['next160'].items():self.wq(unit(int(n))+0x160,unit(nxt))
  for n,first in p['tiles'].items():self.wq(game(int(n))+0x3630,unit(first))
  for key,r in p['rooms'].items():
   a=room(int(key));level,first,adj,proxies=r;self.wq(a,a+0x600);self.wi(a+0x40,len(adj));self.wq(a+0xa8,unit(first));self.wq(a+0x68,a+0x680);self.wq(a+0x70,len(proxies))
   self.wq(a+0x18,a+0x180);self.wq(a+0x210,a+0x400);self.wi(a+0x5f8,level)
   for i,n in enumerate(adj):self.wq(a+0x600+i*8,room(n))
   for i,n in enumerate(proxies):self.wq(a+0x680+i*8,proxy(n))
  for key,r in p['proxies'].items():
   a=proxy(int(key));flags,target,g,typ,ident=r;self.uc.mem_write(a+0x510,bytes([flags]));self.wq(a+0x2a8,unit(target));self.wq(a+0x2e0,game(g));self.wi(a+0x29c,typ);self.wi(a+0x2a0,ident)
  for key,(ident,nxt) in p['nodes'].items():self.wi(node(int(key)),ident);self.wq(node(int(key))+8,node(nxt))
  self.wq(ALLOC,VTABLE);self.wq(VTABLE+8,ALLOCATE)
  # Controlled descriptor inputs isolate all body dispatch paths. The actual
  # search caller's index1 is independently pinned to kind0/no override.
  desc=BASE+0x17243c0+p['index']*16;self.uc.mem_write(desc,struct.pack('<IIQ',p['kind']&0xffffffff,0,OVERRIDE if p['override'] else 0))
  rsp=STACK+0x20008;self.wq(rsp,STOP);self.wq(rsp+0x28,p['index']);entry={'refresh':0x371b10,'resolve':0x370680,'visit':0x40d9f0}[p['entry']]
  args=[game(p['game']),unit(p['subject']),CONTEXT,CALLER if p['supplied'] else 0]
  if p['entry']=='resolve':args=[game(p['game']),p['lookupType']&0xffffffff,p['lookupId']&0xffffffff,0]
  for reg,value in [(UC_X86_REG_RSP,rsp),*zip(REGS,args)]:self.uc.reg_write(reg,value)
  try:self.uc.emu_start(BASE+entry,STOP,count=200000)
  except UcError:
   if not self.failed:raise
  assert self.failed or self.uc.reg_read(UC_X86_REG_RIP)==STOP
  value=0 if self.failed or p['entry']=='refresh' else identity(self.uc.reg_read(UC_X86_REG_RAX)) if p['entry']=='resolve' else s64(self.uc.reg_read(UC_X86_REG_RAX))
  targets=[identity(self.q(unit(int(n))+0x270)) for n in p['units']];cached=[identity(self.q(proxy(int(n))+0x2a8)) for n in p['proxies']]
  return [int(self.failed),value,targets,cached,self.trace]
def u(kind=1,mode=1,ident=101,target=0,typ=1,tid=103,path=1,data=1,ai=1,og=0,ot=1,oid=0,first=0):return [kind,mode,ident,4,0,path,target,typ,tid,data,ai,og,ot,oid,first]
def sample(**values):
 p=dict(entry='visit',game=1,subject=1,index=1,kind=0,override=False,supplied=True,lookupType=1,lookupId=103,
 units={'1':u(),'2':u(ident=102),'3':u(ident=103),'4':u(ident=104),'5':u(ident=105),'6':u(ident=106)},
 next158={'3':4},next160={'3':4,'4':5},tiles={'1':3},buckets={'1:1:103':3,'1:1:104':3,'1:1:105':5,'1:1:102':2,'1:1:106':6},roomMap={'1':1},rooms={'1':[2,0,[2,3],[]],'2':[2,3,[],[1]],'3':[2,5,[],[2]]},
 proxies={'1':[0,3,1,1,103],'2':[1,5,1,1,105]},nodes={},results={},overrideResults={},change160={},changeNodes={});p.update(values);return p
def build(check=False):
 em=Traversal();cases=[]
 def case(p):cases.append([p,em.execute(p)])
 for gameid_,subject,kind,path,target,resolved,itemkind,itemmode in itertools.product([0,1],[0,1],[0,1,5],[0,1],[0,3,4],[0,3,4],[1,4],[0,1,2,3]):
  p=sample(entry='refresh',game=gameid_,subject=subject,buckets={'1:1:103':resolved});p['units']['1']=u(kind=kind,path=path,target=target);p['units']['3'][0:2]=[itemkind,itemmode];case(p)
 for typ,ident,head,nxt in itertools.product([0,1,4,5,-1],[0,103,104,-1],[0,3,4],[0,4]):case(sample(entry='resolve',lookupType=typ,lookupId=ident,buckets={f'1:{typ}:{ident}':head},tiles={'1':head},next158={'3':nxt}))
 for kind,override,supplied,result,adj in itertools.product([0,1,2,3,4],[False,True],[False,True],[0,1,-1,4294967296],[[],[2],[2,3],[3,2],[2,2],[0]]):
  p=sample(kind=kind,override=override,supplied=supplied,results={'4':result},overrideResults={'3':result});p['rooms']['1'][2]=adj;case(p)
 for kind,level,proxies,flags,cached,found in itertools.product([0,1,2],[1,2,40,75,103,109],[[],[1],[0]],[0,1],[0,3,4],[0,3,4]):
  p=sample(kind=kind,buckets={'1:1:103':found});p['rooms']['1'][2]=[2];p['rooms']['2']=[level,3,[],proxies];p['proxies']['1']=[flags,cached,1,1,103];case(p)
 # Two owner resolutions lead to the final owner's saved member nodes.
 for depth,firstResult,nextChange in itertools.product(range(7),[0,-1,4294967296],[False,True]):
  p=sample(kind=3,nodes={'1':[103,2],'2':[104,3],'3':[105,0]},results={'3':firstResult},changeNodes={'3':[1,0]} if nextChange else {})
  p['units']['1']=u(og=1,oid=102) if depth>=1 else u()
  p['units']['2']=u(ident=102,og=1,oid=106) if depth>=2 else u(ident=102)
  p['units']['6']=u(ident=106,og=1,first=1) if depth>=3 else u(ident=106)
  if depth==4:p['units']['6'][10]=0
  if depth==5:p['units']['6'][9]=0
  if depth==6:p['units']['6'][11]=0
  case(p)
 for kind,change in itertools.product([0,1,2],[0,5]):case(sample(kind=kind,change160={'3':change}))
 encoded=json.dumps(cases,separators=(',',':'))+'\n'
 if check:assert OUT.read_text()==encoded
 else:OUT.write_text(encoded)
 libs=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libs)==1
 artifacts=[OUT,Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_decisions.py',ROOT/'tools/probe_warlock_runtime.py',*em.artifacts]
 proof=dict(referenceVersion='3.3.93847',component='Current path refresh,registry and complete traversal bodies',processMemoryRead=False,sourceCodeExecutedInOfflineEmulator=True,handlerEnabled=False,fullAiVerified=False,
 verified=dict(contexts=len(cases),unsupportedBoundaries=sum(c[-1][0] for c in cases),capturedFunctionBytes=2309,completeFunctions=7,searchDescriptorIndex=1,searchDescriptorKind=0,searchDescriptorOverride=False),
 emulatorLibrary=dict(path=str(libs[0].relative_to(ROOT)),sha256=digest(libs[0].read_bytes())),artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in sorted(set(artifacts))},
 services=['Registry bucket lookup370610,room lookup278450,callbacks and type-erasure allocation/invocation/disposal are declared services. Actual104-byte registry,230-byte refresh,715-byte dispatcher,395/442-byte alternate room traversals,366-byte owner enumeration and57-byte closure execute.','Controlled descriptor rows exercise every dispatch body;actual search index1is pinned from immutable executable data to kind0/no override. This does not certify arbitrary descriptor index meanings.','Owner closure uses a declared virtual-call ABI forwarder to the actual57-byte callback. Managed GC/closure disposal is not certified native allocation parity. Linked room next pointers are read after callbacks;owner node next pointers before callbacks.'],remaining=['Native values/traces and full search-wrapper binding remain required. Real game registry/room graph/ownership and callback helper bindings,lifecycle and all class acceptance remain unfinished.'])
 p=ROOT/'tools/reference/generic-pet-traversal-source-evidence.json';text=json.dumps(proof,indent=2)+'\n'
 if check:assert p.read_text()==text
 else:p.write_text(text)
 print('Current traversal:',json.dumps(proof['verified']))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
