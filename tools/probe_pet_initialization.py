#!/usr/bin/env python3
"""Compose retained initializer/copy/getter/survey/finish bodies offline."""
import argparse,json,struct
from pathlib import Path
from unicorn.x86_const import *
from probe_generic_pet_parameters import BASE,STACK,OBJECTS,STOP,CFG,COPY,TARGET_FN,UNIT_FN,ITERATE,COOKIE
from probe_pet_owner_survey import Survey,sample,pointer,identity,MANAGER,VTABLE,VECTOR
from probe_warlock_runtime import ROOT,STAGE,digest

OUT=ROOT/'.local/class-skills/pet-initialization-source'
TICK=OBJECTS+0x9000;MON=OBJECTS+0xe100;CTRL=OBJECTS+0xe300;BUCKET=OBJECTS+0xe800;TRAMP=STOP+0x2000

class Initialization(Survey):
 def __init__(self):
  super().__init__();self.map(TRAMP,1)
  # The old raw-prefix probe declared COPY and GET as services. Restore their
  # retained complete bodies here;owner callback is already loaded by Survey.
  for name,addresses in [('callees-live-actions-order-20261029.json',[0x4b0ed0,0x4b06a0]),('generic-helpers-live-root-20261029.json',[0x4b07b0])]:
   path=STAGE/name;proof=json.loads(path.read_text());self.artifacts.append(path)
   for n,h in proof['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
   for address in addresses:
    row=next(r for r in proof['records'] if int(r['rva'],16)==address);assert row['allUnwindFragmentsCaptured']
    for f in row['fragments']:
     p=next(ROOT/n for n,h in proof['artifacts'].items() if n.endswith('.bin') and h==f['sha256'])
     raw=p.read_bytes();a=BASE+int(f['rva'],16);self.map(a,len(raw));self.uc.mem_write(a,raw);self.ranges.append((a,a+len(raw)));self.artifacts.append(p)
  self.artifacts += [STAGE/'generic-helpers-actions.json',*self.code]
  self.map(BASE+0x370610,1);self.uc.mem_write(BASE+0x370610,b'\xc3')
 def command(self):return [self.integer(pointer(1)+0x348),self.integer(pointer(1)+0x34c)]
 def hook(self,u,a,size,user):
  if a==TRAMP:self.step();return
  if any(x<=a<y for x,y in self.ranges):return
  c,d,t,z=[u.reg_read(r) for r in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]]
  if a==BASE+0x370610:
   assert d==OBJECTS;kind=c&0xffffffff;ident=(t+2**31)%2**32-2**31
   self.trace.append([4,kind,ident]);self.ret(BUCKET+kind*8);return
  if a==UNIT_FN:
   unit=identity(c);value=self.scene['properties'][0 if unit==1 else 1]
   self.trace.append([5,unit,value,*self.command()])
   if unit==1 and self.scene['mutateControl']:
    for i,n in enumerate([99,98,97]):self.wi(CTRL+0x44+4*i,n)
   if unit==1 and self.scene['mutateCommand']:
    self.wi(pointer(1)+0x348,105);self.wi(pointer(1)+0x34c,2)
   if unit==2 and self.scene['afterOwnerKind'] is not None:self.wi(pointer(2),self.scene['afterOwnerKind'])
   self.ret(value);return
  if a==ITERATE:
   assert c==OBJECTS and d==pointer(2) and t==BASE+0x4b08b0
   assert self.q(z)==pointer(1) and self.q(z+8)==CFG
   self.trace.append([6,2,*self.command()]);self.job=dict(rsp=u.reg_read(UC_X86_REG_RSP),context=z,index=0);self.step();return
  if a==BASE+0x6bc90:
   finishing=self.finishing;self.trace.append([0,c&255,(d+2**31)%2**32-2**31])
   if finishing:
    if self.scene['finishAfterKind'] is not None:self.wi(pointer(1),self.scene['finishAfterKind'])
    if not self.scene['finishAfterData']:self.putq(pointer(1)+0x10,0)
    present=self.scene['finishRow'];self.uc.mem_write(MON+0x3e,bytes([self.scene['finishFlags']]))
   else:present=self.scene['row']
   self.ret(MON if present else 0);return
  if a==COOKIE:self.ret(u.reg_read(UC_X86_REG_RAX));return
  super().hook(u,a,size,user)
 def step(self):
  job=self.job
  if job['index']==len(self.scene['visits']):
   self.uc.reg_write(UC_X86_REG_RSP,job['rsp']);self.ret();return
  visited=self.scene['visits'][job['index']];job['index']+=1;sp=job['rsp']-0x1000;self.putq(sp,TRAMP)
  for reg,val in [(UC_X86_REG_RSP,sp),(UC_X86_REG_RCX,OBJECTS),(UC_X86_REG_RDX,pointer(2)),(UC_X86_REG_R8,pointer(visited)),(UC_X86_REG_R9,job['context'])]:self.uc.reg_write(reg,val)
  self.uc.reg_write(UC_X86_REG_RIP,BASE+0x4b08b0)
 def call(self,address,args):
  sp=STACK+0x20008;self.putq(sp,STOP);self.uc.reg_write(UC_X86_REG_RSP,sp)
  for reg,val in zip([UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9],args):self.uc.reg_write(reg,val)
  self.putq(sp+0x28,CFG)
  self.uc.emu_start(BASE+address,STOP,count=20000)
  assert not self.unsupported and self.uc.reg_read(UC_X86_REG_RIP)==STOP
 def run_initialization(self,p):
  self.active=True;self.finishing=False;self.scene=p;self.p=p;self.trace=[];self.validIndex=0;self.allocations=0;self.frees=0;self.unsupported=False
  self.uc.mem_write(OBJECTS,b'\0'*0x10000);self.uc.mem_write(STACK,b'\0'*0x40000)
  for unit in range(1,7):
   a=pointer(unit);kind=p['ownerKind'] if unit==2 else 1
   self.wi(a,kind);self.wi(a+4,500+unit);self.wi(a+8,100+unit);self.uc.mem_write(a+0x1bd,bytes([p['unitSelector']]))
   self.putq(a+0x10,a+0x300);self.putq(a+0x338,a+0x600);self.putq(a+0x608,BASE+0x4b60c0)
   self.putq(a+0x38,a+0x900);self.putq(a+0x970,pointer(4));self.wi(a+0x348,104);self.wi(a+0x34c,2)
  self.wi(pointer(1)+0x348,p['commandTarget']);self.wi(pointer(1)+0x34c,p['commandType'])
  for kind in [0,1]:
   nodes=[i for i in range(1,7) if (p['ownerKind'] if i==2 else 1)==kind]
   self.putq(BUCKET+8*kind,pointer(nodes[0]) if nodes else 0)
   for i,n in enumerate(nodes):self.putq(pointer(n)+0x158,pointer(nodes[i+1]) if i+1<len(nodes) else 0)
  self.uc.mem_write(OBJECTS+0x106,bytes([p['selector']]))
  self.putq(TICK,CTRL);self.putq(TICK+0x28,MON if p['parameters'] is not None else 0)
  if p['parameters'] is not None:self.uc.mem_write(MON+0x90,struct.pack('<24h',*p['parameters']))
  for i,n in enumerate(p['memory']):self.wi(CTRL+0x44+4*i,n)
  # The root's destination scratch is an initialized inline vector. The
  # initializer copies an empty temporary over it using the real472byte body.
  self.putq(CFG+0x78,VECTOR);self.putq(CFG+0x80,2);self.putq(CFG+0x88,0x8000000000000019);self.wi(CFG+0x70,999)
  self.putq(MANAGER,VTABLE)
  self.call(0x4b0b90,[OBJECTS,pointer(1),pointer(2) if p['owner'] else 0,TICK])
  fields=[0,4,8,12,16,0x14,0x18,0x1c,0x20,0x24,0x28,0x2c,0x30,0x34,0x38,0x3c,0x40,0x44,0x48,0x4c]
  params=[self.integer(CFG+i) if i not in [0x14,0x1c,0x28] else int.from_bytes(self.uc.mem_read(CFG+i,2 if i==0x14 else 1),'little') for i in fields]
  vector=self.q(CFG+0x78);count=self.q(CFG+0x80)
  initialized=dict(parameters=params,target=identity(self.q(CFG+0x50)),memory=[self.integer(CFG+i)for i in [0x58,0x5c,0x60]],properties=[self.integer(CFG+i)for i in [0x64,0x68]],gather=self.integer(CFG+0x6c),visits=self.integer(CFG+0x70),swarm=[[identity(self.q(vector+16*i)),self.integer(vector+16*i+8)]for i in range(count)],command=self.command(),trace=self.trace.copy())
  self.trace=[];self.finishing=True
  if p['finishTarget']:self.wi(pointer(p['finishTarget']),p['finishKind'])
  self.call(0x4b07b0,[pointer(1),pointer(p['finishTarget'])])
  return dict(initialized=initialized,finished=dict(result=self.uc.reg_read(UC_X86_REG_RAX)&255,command=self.command(),kind=self.integer(pointer(1)),hasData=bool(self.q(pointer(1)+0x10)),trace=self.trace.copy()))

def scene(**values):
 p=sample();p.update(dict(owner=True,ownerKind=0,afterOwnerKind=None,selector=1,unitSelector=1,
  commandType=2,commandTarget=104,memory=[7,8,9],properties=[11,12],mutateControl=False,mutateCommand=False,
  parameters=[30,30,60,10,1,0,0,0,1,4,4,0,0,15,1,0,25,0,0,0,0,0,0,0],visits=[1,3,5],
  finishTarget=5,finishKind=1,finishRow=True,finishFlags=1,finishAfterKind=None,finishAfterData=True))
 p.update(values);return p
def build(check=False):
 em=Initialization();cases=[]
 def add(**changes):p=scene(**changes);cases.append(dict(input=p,output=em.run_initialization(p)))
 add()
 for changes in [dict(owner=False),dict(ownerKind=1),dict(ownerKind=2),dict(afterOwnerKind=1),dict(ownerKind=1,afterOwnerKind=0),dict(parameters=None),dict(mutateControl=True),dict(mutateCommand=True),dict(selector=3,unitSelector=2),dict(visits=[]),dict(visits=[1,3,3,0,5]),dict(row=False),dict(valid=[0]),dict(commandTarget=999),dict(memory=[-2147483648,2147483647,-1],properties=[-9,2147483647])]:add(**changes)
 for typ in [0,1,3]:add(commandType=typ,commandTarget=102)
 for changes in [dict(finishTarget=0),dict(finishTarget=2,finishKind=0),dict(finishKind=2),dict(finishFlags=0),dict(finishRow=False),dict(finishAfterKind=0),dict(finishAfterData=False)]:add(**changes)
 for row in json.loads((ROOT/'data/Sanctuary/Balance/generic-pet-parameters.json').read_text()):add(parameters=row['Parameters'])
 OUT.mkdir(exist_ok=True);text=json.dumps(cases,indent=2)+'\n';path=OUT/'cases.json'
 if check:assert path.read_text()==text
 else:path.write_text(text)
 artifacts=[path,Path(__file__).resolve(),ROOT/'tools/probe_pet_owner_survey.py',ROOT/'tools/probe_generic_pet_parameters.py',*em.artifacts]
 import unicorn
 libs=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libs)==1;artifacts+=libs
 result=dict(referenceVersion='3.3.93847',composedBytes=2522,sourceContexts=len(cases),processMemoryRead=False,targetProcessCodeExecuted=False,sourceCodeExecutedInOfflineEmulator=True,unicornVersion=unicorn.__version__,components={'initializer4b0b90':818,'emptyDefaultCopy4b0ed0':472,'commandGetter4b06a0':262,'ownerSurvey4b08b0':725,'finish4b07b0':245},artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in sorted(set(artifacts))},limits=['Property27acb0,registry bucket head,row lookup/flags,target validity/path refresh and owner-iterator membership/order remain declared services. Actual owner callback and getter execute;neither is a no-op or supplied preselected target.', 'Captured copy body executes only the initializer-reachable empty-default temporary with initialized inline destination;generic raw container/allocator equivalence is not claimed. Managed frame scratch is transient.','Complete root AI scheduling/movement/combat/world admission remains separate.'])
 path=ROOT/'tools/reference/pet-initialization-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
 if check:assert path.read_text()==text
 else:path.write_text(text)
 print(f"Initialization:{len(cases)}composed contexts/2522retained current bytes;real command consumption,owner callback and finishing;no process read.")
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
