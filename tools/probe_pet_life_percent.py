#!/usr/bin/env python3
"""Run captured124-byte life-percent and92-byte maximum helpers offline."""
import argparse,json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import *
from probe_generic_pet_parameters import Initializer,BASE,STACK,OBJECTS,STOP
from probe_warlock_runtime import ROOT,STAGE,digest

OUT=ROOT/'.local/class-skills/pet-life-percent-source'
UNIT=OBJECTS+0x1000;ROOTSTAT=OBJECTS+0x2000;META6=OBJECTS+0x3000;META7=OBJECTS+0x3100

class Life(Initializer):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.artifacts=[]
  for name,rva,size in [('callees-pet-initializer-property-20261109.json','0x27acb0',124),('callees-pet-initializer-maxlife-20261109.json','0x24fa20',92)]:
   path=STAGE/name;c=json.loads(path.read_text());row=c['records'][0]
   assert c['directCallTargetsVerified'] and row['allUnwindFragmentsCaptured'] and row['rva']==rva and row['bytes']==size
   for n,h in c['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
   self.artifacts.append(path)
   for f in row['fragments']:
    p=next(ROOT/n for n,h in c['artifacts'].items() if n.endswith('.bin') and h==f['sha256']);raw=p.read_bytes();a=BASE+int(f['rva'],16)
    self.map(a,len(raw));self.uc.mem_write(a,raw);self.ranges.append((a,a+len(raw)));self.artifacts.append(p)
  self.map(OBJECTS,0x10000);self.map(STACK,0x40000);self.map(STOP,0x1000)
  for a in [BASE+0xf6f10,BASE+0x24acd0]:self.map(a,1);self.uc.mem_write(a,b'\xc3')
  self.uc.hook_add(UC_HOOK_CODE,self.hook)
 def hook(self,u,a,size,user):
  if any(x<=a<y for x,y in self.ranges):return
  c,d,t=[u.reg_read(r) for r in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8]]
  if a==BASE+0xf6f10:
   assert c==1 and d in [6,7];self.trace.append([0,d]);value=META6 if d==6 else META7
  elif a==BASE+0x24acd0:
   assert c==ROOTSTAT and (d,t) in [(0x60000,META6),(0x70000,META7)]
   stat=d>>16;self.trace.append([1,stat]);value=self.p['current256'] if stat==6 else self.p['maximum256']
  else:raise AssertionError(('Undeclared code',hex(a)))
  sp=u.reg_read(UC_X86_REG_RSP);u.reg_write(UC_X86_REG_RAX,value&0xffffffff);u.reg_write(UC_X86_REG_RSP,sp+8);u.reg_write(UC_X86_REG_RIP,self.q(sp))
 def run(self,p):
  self.p=p;self.trace=[];u=self.uc;u.mem_write(OBJECTS,b'\0'*0x10000);u.mem_write(STACK,b'\0'*0x40000)
  self.putq(UNIT+0x88,ROOTSTAT);u.mem_write(UNIT+0x1bd,b'\x01');sp=STACK+0x20008;self.putq(sp,STOP)
  u.reg_write(UC_X86_REG_RSP,sp);u.reg_write(UC_X86_REG_RCX,UNIT if p['present'] else 0)
  u.emu_start(BASE+0x27acb0,STOP,count=2000);assert u.reg_read(UC_X86_REG_RIP)==STOP
  value=(u.reg_read(UC_X86_REG_RAX)+2**31)%2**32-2**31
  return dict(value=value,trace=self.trace)
def build(check=False):
 em=Life();cases=[]
 for current in [0,255,256,257,2688,-1,-2147483648,2147483647]:
  for maximum in [0,255,256,5504,-256,-2147483648,2147483647]:
   p=dict(present=True,current256=current,maximum256=maximum);cases.append(dict(input=p,output=em.run(p)))
 p=dict(present=False,current256=2688,maximum256=5504);cases.append(dict(input=p,output=em.run(p)));assert len(cases)==57
 OUT.mkdir(exist_ok=True);path=OUT/'cases.json';text=json.dumps(cases,indent=2)+'\n'
 if check:assert path.read_text()==text
 else:path.write_text(text)
 libs=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libs)==1
 artifacts=[path,Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_parameters.py',*em.artifacts,*libs]
 result=dict(referenceVersion='3.3.93847',sourceBytes=216,sourceContexts=57,processMemoryRead=False,targetProcessCodeExecuted=False,sourceCodeExecutedInOfflineEmulator=True,unicornVersion=unicorn.__version__,artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in sorted(set(artifacts))},scope='Both complete captured arithmetic/getter bodies execute with declared available stat6/7 metadata and FP8values. Null-unit behavior is included. Missing/raw-stat-root metadata and allocator fidelity are not native admission claims.')
 path=ROOT/'tools/reference/pet-life-percent-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
 if check:assert path.read_text()==text
 else:path.write_text(text)
 print('Life percent:57 complete-current contexts/216bytes;whole FP8operand shifts before signed division;no process read.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
