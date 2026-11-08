#!/usr/bin/env python3
"""Execute current physical+fire fill and empty-vector/function packet copies."""
import argparse,hashlib,json,struct
from pathlib import Path
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9,UC_X86_REG_RSP,UC_X86_REG_RIP,UC_X86_REG_RAX
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/warlock-runtime-reference'
BASE=0x140000000;DATA=0x2000000;STACK=0x4000000;STOP=0x6000000;CASTER=DATA;ROW=DATA+0x1000;PACKET=DATA+0x2000
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
class Probe:
 def __init__(self):
  self.u=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.artifacts=[]
  for a,n in [(DATA,0x10000),(STACK,0x20000),(STOP,0x1000)]:self.map(a,n)
  for name,rvas in [('callees-apocalypse-packet-area-20261109.json',[0x3aef10,0x3aeda0,0x392350]),('callees-apocalypse-packet-leaves-20261109.json',[0x3a97a0])]:
   path=STAGE/name;d=json.loads(path.read_text());self.artifacts.append(path)
   for rva in rvas:
    r=next(r for r in d['records']if int(r['rva'],16)==rva);assert r['allUnwindFragmentsCaptured']
    for f in r['fragments']:
     p=next(ROOT/p for p,h in d['artifacts'].items()if p.endswith('.bin')and h==f['sha256']);raw=p.read_bytes();assert sha(p)==f['sha256']
     a=BASE+int(f['rva'],16);self.map(a,len(raw));self.u.mem_write(a,raw);self.ranges.append((a,a+len(raw)));self.artifacts.append(p)
     if rva==0x3a97a0:assert struct.unpack_from('<I',raw,0x184)[0]==0x3a9835 # Fire type1 inline table edge.
  for a in [0x6bf40,0x28c790,0x28c9d0,0x28cdc0,0x28d050,0x28d2c0,0x76430]:self.map(BASE+a,1);self.u.mem_write(BASE+a,b'\xc3')
  self.u.hook_add(UC_HOOK_CODE,self.hook)
 def map(self,a,n):
  for p in range(a&~4095,(a+n+4095)&~4095,4096):
   if p not in self.pages:self.u.mem_map(p,4096);self.pages.add(p)
 def pi(self,a,n):self.u.mem_write(a,struct.pack('<I',n&0xffffffff))
 def pq(self,a,n):self.u.mem_write(a,struct.pack('<Q',n))
 def q(self,a):return struct.unpack('<Q',self.u.mem_read(a,8))[0]
 def i(self,a):return struct.unpack('<i',self.u.mem_read(a,4))[0]
 def hook(self,u,a,n,_):
  if any(lo<=a<hi for lo,hi in self.ranges):return
  sp=u.reg_read(UC_X86_REG_RSP);x,y,z,w=[u.reg_read(r)for r in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]];c=self.c;value=0
  if a==BASE+0x6bf40:assert(x,y)==(c['selector'],401);value=ROW
  elif a==BASE+0x76430:
   assert y==PACKET+0x158 and self.q(y)==1;self.pq(x,1);self.copies+=1
  else:
   assert(x,y,z,w)==(c['selector'],CASTER,401,c['rank'])
   lookup={0x28c790:('pmin',0),0x28c9d0:('pmax',0),0x28cdc0:('fmin',1),0x28d050:('fmax',1),0x28d2c0:('length',None)}
   key,flag=lookup[a-BASE]
   if flag is not None:assert self.i(sp+0x28)==flag
   value=c[key]&0xffffffff
  self.trace.append(hex(a-BASE));u.reg_write(UC_X86_REG_RAX,value);ret=self.q(sp);u.reg_write(UC_X86_REG_RIP,ret);u.reg_write(UC_X86_REG_RSP,sp+8)
 def call(self,rva,args):
  sp=STACK+0x10008;self.pq(sp,STOP);self.u.reg_write(UC_X86_REG_RSP,sp)
  for r,v in zip([UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9],args):self.u.reg_write(r,v)
  self.u.emu_start(BASE+rva,STOP,count=5000);assert self.u.reg_read(UC_X86_REG_RIP)==STOP
 def run(self,c):
  self.c=c;self.trace=[];self.copies=0;self.u.mem_write(DATA,b'\0'*0x10000);self.u.mem_write(STACK,b'\0'*0x20000)
  self.u.mem_write(CASTER+0x1bd,bytes([c['selector']]));self.pq(CASTER+0x28,c['seed']);self.u.mem_write(ROW+0x284,b'\x01')
  self.pi(PACKET+0x18,c['physical']);self.pi(PACKET+0x20,c['fire']);self.pq(PACKET+0x40,PACKET+0x58);self.pq(PACKET+0x48,0);self.pq(PACKET+0x50,0x8000000000000010);self.pq(PACKET+0x158,1)
  self.call(0x3aef10,[CASTER,PACKET,401,c['rank']]);self.call(0x3aeda0,[CASTER,PACKET,401,c['rank']]);seed=self.q(CASTER+0x28);copies=[]
  for index in range(3):
   dest=DATA+0x4000+index*0x1000;self.call(0x392350,[dest,PACKET]);copies.append([self.i(dest+0x18),self.i(dest+0x20),self.i(dest+8),self.u.mem_read(dest+0xc,1)[0],self.i(dest+0x144)])
  assert seed==self.q(CASTER+0x28)and self.copies==3
  return dict(physical=self.i(PACKET+0x18),fire=self.i(PACKET+0x20),seed=seed,copies=copies,trace=self.trace.copy())
def build(check=False):
 p=Probe();cases=[];ranges=[(0,0),(1,1),(1,2),(1,3),(0,256),(20480,25600),(-1,0),(-5,-1),(9,4),(-2147483648,2147483647),(2147483647,-2147483648)]
 for seed in [(666<<32), (666<<32)|7,(666<<32)|173,0xffffffffffffffff,0xfffffff000000000,(666<<32)|2147483647]:
  for lo,hi in ranges:
   for pmin,pmax in [(0,0),(5,9)]:
    c=dict(selector=1,rank=1,seed=seed,pmin=pmin,pmax=pmax,fmin=lo,fmax=hi,length=0,physical=17,fire=23);cases.append(dict(input=c,output=p.run(c)))
 stage=ROOT/'.local/class-skills/spell-packet-roll-source';stage.mkdir(parents=True,exist_ok=True);fixture=stage/'cases.json';text=json.dumps(cases,indent=2)+'\n'
 if check:assert fixture.read_text()==text
 else:fixture.write_text(text)
 paths=[Path(__file__).resolve(),fixture,*p.artifacts];result=dict(referenceVersion='3.3.93847',currentMachineCodeExecutedOffline=True,processMemoryRead=False,cases=len(cases),functions=dict(physical='0x3aef10',elemental='0x3aeda0',fireWriter='0x3a97a0',copy='0x392350'),
  scope='Qualified present caster/skill and firetype1. Current physical and elemental wrappers plus fire writer execute with explicitly supplied endpoint/length getters. Actual zero-vector/empty-function packet copy executes three times,retaining scalar fields and seed. No full endpoint-formula,nonempty custom-function or other-element comparison.',artifacts={str(x.relative_to(ROOT)):sha(x)for x in paths})
 out=ROOT/'tools/reference/spell-packet-roll-current-evidence.json';text=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==text
 else:out.write_text(text)
 print('Current physical/fire roll + three copies:',len(cases),'contexts;exclusive maximum,unchanged seed on copy. No live code execution.')
if __name__=='__main__':
 a=argparse.ArgumentParser();a.add_argument('--check',action='store_true');build(a.parse_args().check)
