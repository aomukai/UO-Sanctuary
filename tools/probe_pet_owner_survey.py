#!/usr/bin/env python3
"""Execute all725 captured callback bytes offline with declared leaf services."""
import argparse,json,struct
from pathlib import Path
import unicorn
from unicorn.x86_const import *
from probe_generic_pet_parameters import Initializer,BASE,STACK,OBJECTS,STOP,GAME,CFG
from probe_warlock_runtime import ROOT,digest

CAP=ROOT/'.local/class-skills/pet-owner-reference-20261109/owner-callback.json'
OUT=ROOT/'.local/class-skills/pet-owner-survey-source'
VECTOR=OBJECTS+0xa000;CONTEXT=OBJECTS+0xb000;MANAGER=OBJECTS+0xc000;VTABLE=MANAGER+0x100;NEWVECTOR=OBJECTS+0xd000
ALLOC=STOP+0x100;FREE=STOP+0x180
F={k:BASE+v for k,v in dict(monster=0x6bc90,target=0x4b06a0,valid=0x372ec0,refresh=0x371b10,allocator=0xbaa540,fatal=0x7b6af0).items()}
def pointer(unit):return OBJECTS+unit*0x1000 if unit else 0
def identity(address):return (address-OBJECTS)//0x1000 if address else 0
def signed(value):return (value+2**31)%2**32-2**31

class Survey(Initializer):
 def __init__(self):
  super().__init__();self.active=False;self.artifacts=[]
  capture=json.loads(CAP.read_text());row=capture['records'][0]
  assert row['rva']=='0x4b08b0' and row['allUnwindFragmentsCaptured'] and len(row['fragments'])==8
  for name,h in capture['artifacts'].items():assert digest((ROOT/name).read_bytes())==h,name
  self.artifacts.append(CAP)
  for f in row['fragments']:
   assert f['captured'] and f['invalidInstructionMarkers']==0
   path=next(ROOT/n for n,h in capture['artifacts'].items() if n.endswith('.bin') and h==f['sha256'])
   raw=path.read_bytes();a=BASE+int(f['rva'],16);self.map(a,len(raw));self.uc.mem_write(a,raw);self.ranges.append((a,a+len(raw)));self.artifacts.append(path)
  for a in [*F.values(),ALLOC,FREE]:self.map(a,1);self.uc.mem_write(a,b'\xc3')
  assert sum(int(f['endRva'],16)-int(f['rva'],16) for f in row['fragments'])==725
 def wi(self,a,v):self.uc.mem_write(a,struct.pack('<I',v&0xffffffff))
 def integer(self,a):return struct.unpack('<i',self.uc.mem_read(a,4))[0]
 def ret(self,value=0):
  rsp=self.uc.reg_read(UC_X86_REG_RSP);self.uc.reg_write(UC_X86_REG_RAX,value&0xffffffffffffffff);self.uc.reg_write(UC_X86_REG_RSP,rsp+8);self.uc.reg_write(UC_X86_REG_RIP,self.q(rsp))
 def hook(self,uc,a,size,user):
  if not self.active:return super().hook(uc,a,size,user)
  if any(x<=a<y for x,y in self.ranges):return
  c,d,t,z=[uc.reg_read(r) for r in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]]
  if a==F['monster']:
   self.trace.append([0,c&255,signed(d)]);self.ret(1 if self.p['row'] else 0)
  elif a==F['target']:
   assert c==GAME;unit=identity(d);self.trace.append([1,unit]);self.ret(pointer(self.p['current']))
  elif a==F['valid']:
   assert c==GAME and z&255==1
   result=self.p['valid'][min(self.validIndex,len(self.p['valid'])-1)];self.validIndex+=1
   self.trace.append([2,identity(d),identity(t),1,result]);self.ret(result)
  elif a==F['refresh']:
   assert c==GAME;unit=identity(d);self.trace.append([3,unit])
   if self.p['afterKind'] is not None:self.wi(d,self.p['afterKind'])
   if self.p['afterTarget'] is not None:self.putq(d+0x970,pointer(self.p['afterTarget']))
   self.ret()
  elif a==F['allocator']:self.ret(MANAGER)
  elif a==ALLOC:
   assert c==MANAGER and t==16 and 0<d<=4096;self.allocations+=1;self.ret(NEWVECTOR)
  elif a==FREE:
   assert c==MANAGER;self.frees+=1;self.ret()
  elif a==F['fatal']:
   self.unsupported=True;uc.emu_stop()
  else:raise AssertionError(('Undeclared code',hex(a)))
 def run_survey(self,p):
  self.active=True;self.p=p;self.trace=[];self.validIndex=0;self.allocations=0;self.frees=0;self.unsupported=False
  self.uc.mem_write(OBJECTS,b'\0'*0x10000);self.uc.mem_write(STACK,b'\0'*0x40000)
  for unit in range(1,7):
   a=pointer(unit);self.wi(a,p['kind']);self.wi(a+8,100+unit)
   self.putq(a+0x10,a+0x300 if p['monster'] else 0);self.putq(a+0x338,a+0x600 if p['ai'] else 0)
   self.putq(a+0x608,BASE+(0x4b60c0 if p['generic'] else 0x4b60c1))
   self.putq(a+0x38,a+0x900 if p['path'] else 0);self.putq(a+0x970,pointer(p['pathTarget']))
  self.uc.mem_write(GAME+0x106,bytes([p['selector']]));self.wi(CFG+0x10,p['swarm']);self.wi(CFG+0x70,p['count'])
  self.putq(CFG+0x78,VECTOR);self.putq(CFG+0x80,len(p['existing']));self.putq(CFG+0x88,p['capacity']|(0x8000000000000000 if p['inline'] else 0))
  for i,(target,count) in enumerate(p['existing']):self.putq(VECTOR+16*i,pointer(target));self.wi(VECTOR+16*i+8,count)
  self.putq(CONTEXT,pointer(p['pet']));self.putq(CONTEXT+8,CFG)
  self.putq(MANAGER,VTABLE);self.putq(VTABLE+8,ALLOC);self.putq(VTABLE+0x20,FREE)
  for visited in p['visits']:
   rsp=STACK+0x20008;self.putq(rsp,STOP)
   for reg,val in [(UC_X86_REG_RSP,rsp),(UC_X86_REG_RCX,GAME),(UC_X86_REG_RDX,pointer(6)),(UC_X86_REG_R8,pointer(visited)),(UC_X86_REG_R9,CONTEXT)]:self.uc.reg_write(reg,val)
   self.uc.emu_start(BASE+0x4b08b0,STOP,count=10000)
   if self.unsupported:break
   assert self.uc.reg_read(UC_X86_REG_RIP)==STOP
  size=self.q(CFG+0x80);assert size<=32;vector=self.q(CFG+0x78)
  return dict(visits=self.integer(CFG+0x70),swarm=[[identity(self.q(vector+16*i)),self.integer(vector+16*i+8)]for i in range(size)],trace=self.trace,unsupported=self.unsupported,allocations=self.allocations,frees=self.frees)

def sample(**values):
 p=dict(selector=1,pet=1,swarm=1,count=0,visits=[2],kind=1,monster=True,ai=True,generic=True,row=True,current=3,valid=[1],path=True,pathTarget=4,afterKind=None,afterTarget=None,existing=[],capacity=8,inline=False)
 p.update(values);return p

def build(check=False):
 em=Survey();cases=[]
 def case(p):cases.append(dict(input=p,output=em.run_survey(p)))
 for change in [dict(),dict(visits=[1]),dict(visits=[0]),dict(swarm=0),dict(swarm=-1),dict(kind=0),dict(kind=2),dict(kind=5),dict(monster=False),dict(ai=False),dict(generic=False),dict(row=False)]:case(sample(**change))
 for current in [0,3]:
  for valid in [[0],[1],[-1],[0,1],[0,-1]]:
   for target in [0,2,3,4]:case(sample(current=current,valid=valid,pathTarget=target))
 for selector in [0,2,3,255]:case(sample(selector=selector))
 for count in [-2147483648,-1,2147483647]:case(sample(count=count))
 for existing in [[],[[3,1]],[[3,2147483647]],[[4,7],[3,9]],[[3,1],[3,2]]]:case(sample(existing=existing))
 for existing in [[],[[4,2]],[[4,2],[5,3]]]:
  for inline in [False,True]:case(sample(existing=existing,capacity=len(existing),inline=inline))
 case(sample(path=False,current=0));case(sample(current=0,afterTarget=3));case(sample(current=0,afterKind=5))
 case(sample(visits=[1,2,2,0,3,4,2],existing=[[4,5]]))
 case(sample(visits=[2],current=3,valid=[0,1],afterTarget=2))
 for key in ['wargoatman','warbighead','warputriddefiler']:
  profile=next(x for x in json.loads((ROOT/'data/Sanctuary/Balance/generic-pet-parameters.json').read_text()) if x['Key']==key)
  case(sample(swarm=profile['Parameters'][4],visits=[1,2,0,3,2]))
 encoded=json.dumps(cases,indent=2)+'\n';OUT.mkdir(exist_ok=True)
 target=OUT/'cases.json'
 if check:assert target.read_text()==encoded
 else:target.write_text(encoded)
 libraries=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libraries)==1
 artifacts=[CAP,target,Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_parameters.py',ROOT/'tools/probe_warlock_runtime.py',ROOT/'data/Sanctuary/Balance/generic-pet-parameters.json',libraries[0],*em.artifacts]
 result=dict(referenceVersion='3.3.93847',callbackRva='0x4b08b0',sourceBytes=725,completeFragments=8,cases=len(cases),unsupported=sum(c['output']['unsupported']for c in cases),allocationContexts=sum(c['output']['allocations']>0 for c in cases),processMemoryRead=False,targetProcessCodeExecuted=False,sourceCodeExecutedInOfflineEmulator=True,unicornVersion=unicorn.__version__,artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in sorted(set(artifacts))},services=['Monster row existence,current target,target validation and actual path refresh remain declared source services. GenericPet callback identity is checked as pointer equality to root4b60c0.','Captured vector append/growth/copy/free executes against explicit managed-memory allocator services;raw capacity/allocator fidelity is not a gameplay claim.','Owner iterator order/membership and actual world registration remain supplied. Count includes self/null/nonmatching visits before all filters;no radius or pet-cap interpretation is invented.'])
 out=ROOT/'tools/reference/pet-owner-survey-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==text
 else:out.write_text(text)
 print(f"Owner survey:{len(cases)} full-current contexts,{result['unsupported']} assertion boundary,{result['allocationContexts']} allocation contexts;725bytes/all8fragments.")
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
