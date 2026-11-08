#!/usr/bin/env python3
"""Replay current timed producer/remover/idle with declared null-callback ABI services."""
import argparse,json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import *
from probe_generic_pet_parameters import Initializer,BASE,STACK,OBJECTS,STOP
from probe_warlock_runtime import ROOT,STAGE,Image,digest
OUT=ROOT/'.local/class-skills/pet-timed-events-source';PET=OBJECTS+0x1000;EVENTS=OBJECTS+0x10000;POOL=OBJECTS+0x20000;NODES=POOL+0x100;TABLE=OBJECTS+0x40000;ROOTSTAT=OBJECTS+0x50000;SECURITY=OBJECTS+0x60000
GROUP=BASE+0x1633cc8
FNS={k:BASE+v for k,v in dict(neutralRequest=0x3de170,neutral=0x3dda90,deferred=0x373690,untimed=0x32fce0,validateA=0x765e0,validateB=0x765f0,fatal=0x7b6af0).items()}
def ptr(i):return NODES+(i-1)*0x50 if i else 0
def ident(p):return (p-NODES)//0x50+1 if p else 0
def signed(v):return (v+2**31)%2**32-2**31
class Events(Initializer):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.artifacts=[]
  sources=[('generic-helpers-live-root-20261029.json',{'0x425550','0x32e790'}),('callees-pet-ai-schedule-20261109.json',{'0x32fe20'}),('callees-pet-ai-event-20261109.json',{'0x32f9a0'}),('callees-pet-ai-event-node-20261109.json',{'0x32e900'})]
  for name,wanted in sources:
   path=STAGE/name;capture=json.loads(path.read_text());self.artifacts.append(path)
   for n,h in capture['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
   rows=[r for r in capture['records']if r['rva']in wanted];assert len(rows)==len(wanted)
   for r in rows:
    assert r['allUnwindFragmentsCaptured']
    for f in r['fragments']:
     p=next(ROOT/n for n,h in capture['artifacts'].items()if n.endswith('.bin')and h==f['sha256']);raw=p.read_bytes();a=BASE+int(f['rva'],16);self.map(a,len(raw));self.uc.mem_write(a,raw);self.ranges.append((a,a+len(raw)));self.artifacts.append(p)
  self.map(OBJECTS,0x80000);self.map(STACK,0x40000);self.map(STOP,0x1000)
  self.map(GROUP,28);raw=Image().bytes(0x1633cc8+4,4);assert struct.unpack('<i',raw)==(1,);self.uc.mem_write(GROUP+4,raw)
  for a,n in [(BASE+0x1e03ac0,64),(BASE+0x1d57088,48),(BASE+0x1708690,32)]:self.map(a,n)
  self.uc.mem_write(BASE+0x17086a0,bytes(range(16)))
  for a in FNS.values():self.map(a,1);self.uc.mem_write(a,b'\xc3')
  self.uc.hook_add(UC_HOOK_CODE,self.hook)
 def wi(self,a,v):self.uc.mem_write(a,struct.pack('<I',v&0xffffffff))
 def ret(self,value=0):
  sp=self.uc.reg_read(UC_X86_REG_RSP);self.uc.reg_write(UC_X86_REG_RAX,value);self.uc.reg_write(UC_X86_REG_RSP,sp+8);self.uc.reg_write(UC_X86_REG_RIP,self.q(sp))
 def hook(self,u,a,size,user):
  if a==BASE+0x32ea5e:self.allocated=max(self.allocated,ident(u.reg_read(UC_X86_REG_RAX)))
  if any(x<=a<y for x,y in self.ranges):return
  c,d,v,z=[u.reg_read(r)for r in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]]
  name=next((k for k,x in FNS.items()if x==a),None);assert name,('Undeclared instruction',hex(a))
  if name=='neutralRequest':assert c==PET and d==1
  elif name=='neutral':assert c==OBJECTS;self.trace.append('neutral');self.wi(PET+12,1)
  elif name=='deferred':
   assert c==PET and d==0;self.trace.append('deferred')
   if self.p['deferredMode']is not None:self.wi(PET+12,self.p['deferredMode'])
   if self.p['deferredFlags']is not None:self.wi(PET+0x124,self.p['deferredFlags'])
  elif name in ['untimed','fatal']:self.unsupported=True;u.emu_stop();return
  elif name=='validateB':u.mem_write(c,b'\xe9') # Declared validation of the actual caller's null callback only.
  self.ret()
 def call(self,rva,rcx,rdx,r8=0,r9=0,args=()):
  sp=STACK+0x20008;self.putq(sp,STOP)
  for i,arg in enumerate(args):self.putq(sp+0x28+8*i,arg&0xffffffffffffffff)
  for reg,value in [(UC_X86_REG_RSP,sp),(UC_X86_REG_RCX,rcx),(UC_X86_REG_RDX,rdx),(UC_X86_REG_R8,r8),(UC_X86_REG_R9,r9)]:self.uc.reg_write(reg,value&0xffffffffffffffff)
  self.uc.emu_start(BASE+rva,STOP,count=10000)
  if not self.unsupported:assert self.uc.reg_read(UC_X86_REG_RIP)==STOP
 def snapshot(self):
  count=self.allocated;nodes=[]
  for i in range(1,count+1):
   b=bytes(self.uc.mem_read(ptr(i),0x50));nodes.append([i,b[0],struct.unpack_from('<i',b,4)[0],struct.unpack_from('<H',b,2)[0],*struct.unpack_from('<3i',b,0x18)])
  def chain(start,offset):
   out=[]
   while start:out.append(ident(start));start=self.q(start+offset);assert len(out)<32
   return out
  return dict(mode=struct.unpack('<i',self.uc.mem_read(PET+12,4))[0],nodes=nodes,unit=chain(self.q(PET+0x148),0x38),buckets={str(i):chain(self.q(EVENTS+0xa08+(64+i)*8),0x30)for i in range(64)if self.q(EVENTS+0xa08+(64+i)*8)},trace=self.trace.copy(),unsupported=self.unsupported)
 def run(self,p):
  self.p=p;u=self.uc;u.mem_write(OBJECTS,b'\0'*0x80000);u.mem_write(STACK,b'\0'*0x40000);self.trace=[];self.unsupported=False;self.allocated=0
  self.wi(OBJECTS+0x170,p['now']);self.putq(OBJECTS+0x188,EVENTS);self.putq(EVENTS+0x1438,POOL);self.putq(POOL+0xbb80,ptr(1))
  for i in range(1,32):self.putq(ptr(i)+0x28,ptr(i+1))
  self.wi(PET,1);self.wi(PET+8,101);self.wi(PET+12,p['mode']);self.wi(PET+0x124,p['flags']);u.mem_write(PET+0x1bd,bytes([p['selector']]))
  self.putq(PET+0x88,ROOTSTAT if p['hasRoot']else 0);self.wi(ROOTSTAT+0x1c,p['root1c']);self.wi(ROOTSTAT+0xb34,p['rootb34'])
  self.putq(BASE+0x1d57088,SECURITY)
  for s in [1,2,3]:self.putq(BASE+0x1e03ac0+s*16,TABLE);self.wi(TABLE+0x2d0,p['stats'])
  outputs=[]
  for cmd in p['commands']:
   if cmd[0]=='add':self.call(0x32fe20,OBJECTS,PET,cmd[1],cmd[2],cmd[3:])
   elif cmd[0]=='idle':self.call(0x425550,OBJECTS,PET,cmd[1])
   elif cmd[0]=='remove':self.call(0x32e790,OBJECTS,ptr(cmd[1]))
   elif cmd[0]=='processing':u.mem_write(ptr(cmd[1])+2,struct.pack('<H',struct.unpack('<H',u.mem_read(ptr(cmd[1])+2,2))[0]|1))
   else:raise AssertionError(cmd)
   outputs.append(self.snapshot())
   if self.unsupported:break
  return outputs

def sample(**changes):
 p=dict(now=100,mode=1,selector=1,stats=55,hasRoot=False,root1c=-1,rootb34=0x400000,flags=0,deferredMode=None,deferredFlags=None,commands=[['add',2,115,0,0,0]])
 p.update(changes);return p
def build(check=False):
 em=Events();cases=[]
 def add(**changes):p=sample(**changes);cases.append(dict(input=p,output=em.run(p)))
 for frame in [0,99,100,101,115,128,164,2147483647]:add(commands=[['add',2,frame,17,-9,2147483647]])
 for ticks in [-10,0,1,15]:
  for mode in [1,5]:add(mode=mode,commands=[['add',1,120,0,0,0],['add',2,110,0,0,0],['add',2,111,0,0,0],['idle',ticks]])
 add(commands=[['add',2,115,0,0,0],['add',2,179,1,2,3],['remove',1]])
 add(commands=[['add',2,115,0,0,0],['add',2,179,1,2,3],['remove',2]])
 add(commands=[['add',2,115,0,0,0],['processing',1],['remove',1],['idle',15]])
 for changes in [dict(hasRoot=True),dict(hasRoot=True,mode=0),dict(hasRoot=True,mode=12),dict(hasRoot=True,flags=0x10000),dict(hasRoot=True,root1c=0),dict(hasRoot=True,rootb34=0),dict(hasRoot=True,stats=54),dict(selector=0),dict(selector=255)]:add(**changes)
 add(hasRoot=True,deferredMode=12)
 add(hasRoot=True,deferredFlags=0x10000)
 add(hasRoot=True,mode=0,commands=[['idle',15]])
 OUT.mkdir(exist_ok=True);path=OUT/'cases.json';encoded=json.dumps(cases,indent=2)+'\n'
 if check:assert path.read_text()==encoded
 else:path.write_text(encoded)
 libs=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libs)==1
 result=dict(referenceVersion='3.3.93847',sourceContexts=len(cases),machineCodeBytes=sum(b-a for a,b in em.ranges),processMemoryRead=False,targetProcessCodeExecuted=False,sourceCodeExecutedInOfflineEmulator=True,scope='Monster/null-callback ordinary timed requests and idle/removal over a supplied available event pool. Raw pool-growth branch,opaque callback validation,untimed sentinel,global dispatch and other unit kinds are not certified.',artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in sorted(set([path,Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_parameters.py',*em.artifacts,*libs]))})
 path=ROOT/'tools/reference/pet-timed-events-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert path.read_text()==encoded
 else:path.write_text(encoded)
 print(f"Timed events:{len(cases)}source sequences/{result['machineCodeBytes']}bytes;declared available pool and null-callback validation.")
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
