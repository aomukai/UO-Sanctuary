#!/usr/bin/env python3
"""Execute the retained current resistance-scaling leaf,including its inline table."""
import argparse,hashlib,json,struct
from pathlib import Path
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9,UC_X86_REG_RSP,UC_X86_REG_RIP,UC_X86_REG_RAX
ROOT=Path(__file__).resolve().parents[1];CAP=ROOT/'.local/class-skills/warlock-runtime-reference/callees-apocalypse-resistance-20261109.json'
BASE=0x140000000;DATA=0x2000000;STACK=0x4000000;STOP=0x6000000
TARGET=DATA;HIRE=DATA+0x1000;STAT=DATA+0x2000;ROOTSTAT=DATA+0x3000
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
class Probe:
 def __init__(self):
  d=json.loads(CAP.read_text());r=next(r for r in d['records']if r['rva']=='0x453660');assert r['allUnwindFragmentsCaptured']and r['bytes']==256
  self.binary=next(ROOT/p for p,h in d['artifacts'].items()if p.endswith('.bin')and h==r['fragments'][0]['sha256']);self.raw=self.binary.read_bytes();assert sha(self.binary)==r['fragments'][0]['sha256']
  # Disassembler (bad) markers occur only in the ten-entry inline jump table.
  self.table=struct.unpack_from('<10I',self.raw,0xd8);assert set(self.table)=={0x45369a,0x453725}
  assert [i+36 for i,a in enumerate(self.table)if a==0x45369a]==[36,37,39,41,43,45]
  self.u=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set()
  for a,n in [(BASE+0x453660,256),(DATA,0x5000),(STACK,0x20000),(STOP,0x1000)]:self.map(a,n)
  self.u.mem_write(BASE+0x453660,self.raw)
  for a in [0x2af480,0xf6f10,0x24ac60]:self.map(BASE+a,1);self.u.mem_write(BASE+a,b'\xc3')
  self.u.hook_add(UC_HOOK_CODE,self.hook)
 def map(self,a,n):
  for p in range(a&~4095,(a+n+4095)&~4095,4096):
   if p not in self.pages:self.u.mem_map(p,4096);self.pages.add(p)
 def puti(self,a,n):self.u.mem_write(a,struct.pack('<I',n&0xffffffff))
 def putq(self,a,n):self.u.mem_write(a,struct.pack('<Q',n))
 def hook(self,u,a,n,_):
  if BASE+0x453660<=a<BASE+0x453738:return
  c=self.c;sp=u.reg_read(UC_X86_REG_RSP);x,y,z=[u.reg_read(r)for r in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8]]
  if a==BASE+0x2af480:assert(x,y,z)==(c['selector'],int(c['selector']!=1),78);value=HIRE if c['record']else 0
  elif a==BASE+0xf6f10:assert(x,y)==(c['selector'],c['stat']&0xffffffff);value=STAT if c['definition']else 0
  elif a==BASE+0x24ac60:assert(x,y,z)==(ROOTSTAT,(c['stat']<<16)&0xffffffff,STAT);value=c['resistance']&0xffffffff
  else:raise AssertionError(('Unbound current instruction',hex(a)))
  self.trace.append(hex(a-BASE));u.reg_write(UC_X86_REG_RAX,value);ret=struct.unpack('<Q',u.mem_read(sp,8))[0];u.reg_write(UC_X86_REG_RIP,ret);u.reg_write(UC_X86_REG_RSP,sp+8)
 def run(self,c):
  self.c=c;self.trace=[];u=self.u;u.mem_write(DATA,b'\0'*0x5000);u.mem_write(STACK,b'\0'*0x20000)
  self.puti(TARGET,c['kind']);self.puti(TARGET+4,78);u.mem_write(TARGET+0x1bd,bytes([c['selector']]));self.putq(TARGET+0x88,ROOTSTAT if c['root']else 0);self.puti(HIRE+0xc,c['flag'])
  sp=STACK+0x10008;self.putq(sp,STOP);u.reg_write(UC_X86_REG_RSP,sp)
  for r,v in [(UC_X86_REG_RCX,0),(UC_X86_REG_RDX,TARGET if c['present']else 0),(UC_X86_REG_R8,c['stat']&0xffffffff),(UC_X86_REG_R9,c['value']&0xffffffff)]:u.reg_write(r,v)
  u.emu_start(BASE+0x453660,STOP,count=500);assert u.reg_read(UC_X86_REG_RIP)==STOP
  raw=u.reg_read(UC_X86_REG_RAX)&0xffffffff;return dict(value=raw if raw<0x80000000 else raw-0x100000000,trace=self.trace.copy())
def build(check=False):
 p=Probe();rows=[];base=dict(stat=39,value=-5,present=True,kind=1,selector=1,record=False,flag=0,definition=True,root=True,resistance=100)
 def add(**kw):c=base|kw;rows.append(dict(input=c,output=p.run(c)))
 for stat in [-2147483648,-1,0,35,*range(36,47),2147483647]:
  for value in [-2147483648,-40,-5,-1,0,1,2147483647]:add(stat=stat,value=value)
 for k,vs in dict(present=[False],kind=[0,2,3,4,5],selector=[0,2,3,255],definition=[False],root=[False],resistance=[-2147483648,-100,0,99,101,105,200,2147483647]).items():
  for v in vs:add(**{k:v})
 for record in [False,True]:
  for flag in [-2147483648,-1,0,1,2147483647]:add(record=record,flag=flag)
 stage=ROOT/'.local/class-skills/skill-resistance-source';stage.mkdir(parents=True,exist_ok=True);fixture=stage/'cases.json';text=json.dumps(rows,indent=2)+'\n'
 if check:assert fixture.read_text()==text
 else:fixture.write_text(text)
 proof=dict(referenceVersion='3.3.93847',entryRva='0x453660',currentMachineCodeExecutedOffline=True,processMemoryRead=False,cases=len(rows),sourceBytes=256,codePrefixBytes=216,inlineTableBytes=40,stats=[36,37,39,41,43,45],nativeComparisonScope='Returned numeric value for explicit source lookup/stat-root contexts;external ABI calls asserted by emulator,not reimplemented world metadata.',artifacts={str(x.relative_to(ROOT)):sha(x)for x in [CAP,p.binary,Path(__file__).resolve(),fixture]})
 out=ROOT/'tools/reference/skill-resistance-current-evidence.json';text=json.dumps(proof,indent=2)+'\n'
 if check:assert out.read_text()==text
 else:out.write_text(text)
 print('Current resistance leaf:',len(rows),'cases;inline jump data identified,not misclassified as instructions. No live code executed.')
if __name__=='__main__':
 a=argparse.ArgumentParser();a.add_argument('--check',action='store_true');build(a.parse_args().check)
