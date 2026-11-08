#!/usr/bin/env python3
"""Run the retained262-byte current command getter offline;no process access."""
import argparse,json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import *
from probe_generic_pet_parameters import Initializer,BASE,STACK,OBJECTS,STOP
from probe_warlock_runtime import ROOT,digest

CAP=ROOT/'.local/class-skills/warlock-runtime-reference/callees-live-actions-order-20261029.json'
OUT=ROOT/'.local/class-skills/pet-command-target-source'
UNIT=OBJECTS+0x1000;DATA=OBJECTS+0x2000;BUCKET=OBJECTS+0x3000;GAME=OBJECTS+0x4000
def pointer(unit):return OBJECTS+0x5000+unit*0x200 if unit else 0
def identity(address):return (address-OBJECTS-0x5000)//0x200 if address else 0
def signed(value):return (value+2**31)%2**32-2**31

class Target(Initializer):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.artifacts=[CAP]
  proof=json.loads(CAP.read_text());row=next(r for r in proof['records'] if r['rva']=='0x4b06a0')
  assert proof['directCallTargetsVerified'] and row['allUnwindFragmentsCaptured'] and row['bytes']==262
  for name,h in proof['artifacts'].items():assert digest((ROOT/name).read_bytes())==h,name
  for f in row['fragments']:
   assert f['captured'] and f['invalidInstructionMarkers']==0
   p=next(ROOT/n for n,h in proof['artifacts'].items() if n.endswith('.bin') and h==f['sha256'])
   data=p.read_bytes();a=BASE+int(f['rva'],16);self.map(a,len(data));self.uc.mem_write(a,data);self.ranges.append((a,a+len(data)));self.artifacts.append(p)
  self.map(OBJECTS,0x10000);self.map(STACK,0x40000);self.map(STOP,0x1000)
  for a in [BASE+0x370610,BASE+0x7b6af0]:self.map(a,1);self.uc.mem_write(a,b'\xc3')
  self.uc.hook_add(UC_HOOK_CODE,self.hook)
 def wi(self,a,v):self.uc.mem_write(a,struct.pack('<I',v&0xffffffff))
 def hook(self,u,a,size,user):
  if any(x<=a<y for x,y in self.ranges):return
  if a==BASE+0x7b6af0:self.unsupported=True;u.emu_stop();return
  assert a==BASE+0x370610,hex(a)
  kind=signed(u.reg_read(UC_X86_REG_RCX));game=u.reg_read(UC_X86_REG_RDX);ident=signed(u.reg_read(UC_X86_REG_R8))
  assert game==GAME;self.trace.append([kind,1,ident]);sp=u.reg_read(UC_X86_REG_RSP)
  u.reg_write(UC_X86_REG_RAX,BUCKET if self.p['bucket'] else 0);u.reg_write(UC_X86_REG_RSP,sp+8);u.reg_write(UC_X86_REG_RIP,self.q(sp))
 def run(self,p):
  self.p=p;self.trace=[];self.unsupported=False;u=self.uc;u.mem_write(OBJECTS,b'\0'*0x10000);u.mem_write(STACK,b'\0'*0x40000)
  self.wi(UNIT,p['kind']);self.putq(UNIT+0x10,DATA if p['data'] else 0)
  self.wi(DATA+0x48,p['target']);self.wi(DATA+0x4c,p['type'])
  self.putq(BUCKET,pointer(p['chain'][0]) if p['chain'] else 0)
  for i,unit in enumerate(p['chain']):
   self.wi(pointer(unit)+8,p['ids'][str(unit)]);self.putq(pointer(unit)+0x158,pointer(p['chain'][i+1]) if i+1<len(p['chain']) else 0)
  sp=STACK+0x20008;self.putq(sp,STOP)
  for reg,val in [(UC_X86_REG_RSP,sp),(UC_X86_REG_RCX,GAME if p['game'] else 0),(UC_X86_REG_RDX,UNIT if p['present'] else 0)]:u.reg_write(reg,val)
  u.emu_start(BASE+0x4b06a0,STOP,count=3000)
  if not self.unsupported:assert u.reg_read(UC_X86_REG_RIP)==STOP
  return dict(target=0 if self.unsupported else identity(u.reg_read(UC_X86_REG_RAX)),unsupported=self.unsupported,trace=self.trace)

def sample(**values):
 p=dict(present=True,kind=1,data=True,type=2,target=77,game=True,bucket=True,chain=[2,3,4],ids={'2':10,'3':77,'4':88});p.update(values);return p
def build(check=False):
 em=Target();cases=[]
 def add(**changes):p=sample(**changes);cases.append(dict(input=p,output=em.run(p)))
 add()
 for changes in [dict(present=False),dict(kind=0),dict(kind=2),dict(kind=5),dict(data=False)]:add(**changes)
 for game in [False,True]:
  for typ in [-2147483648,-1,0,1,2,3,4,2147483647]:add(game=game,type=typ)
 for target in [0,-1,-2147483648,2147483647]:add(target=target)
 for chain in [[],[2],[3,2],[2,4],[2,3],[4,3,2]]:add(chain=chain)
 add(bucket=False);add(ids={'2':77,'3':77,'4':88});add(type=1,target=10)
 assert len(cases)==35
 OUT.mkdir(exist_ok=True);text=json.dumps(cases,indent=2)+'\n';path=OUT/'cases.json'
 if check:assert path.read_text()==text
 else:path.write_text(text)
 libs=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libs)==1
 artifacts=[path,Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_parameters.py',*em.artifacts,libs[0]]
 result=dict(referenceVersion='3.3.93847',sourceBytes=262,sourceCases=35,assertionContexts=sum(c['output']['unsupported'] for c in cases),processMemoryRead=False,targetProcessCodeExecuted=False,sourceCodeExecutedInOfflineEmulator=True,unicornVersion=unicorn.__version__,artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in sorted(set(artifacts))},scope='Complete retained4b06a0 getter over an explicit370610 bucket-head service;actual source ID/next158 chain traversal executes. No validity/LOS/range/path filtering or command write is inferred.')
 path=ROOT/'tools/reference/pet-command-target-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
 if check:assert path.read_text()==text
 else:path.write_text(text)
 print('Command getter:35 complete-current contexts,2 explicit null-game assertion boundaries;no process read.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
