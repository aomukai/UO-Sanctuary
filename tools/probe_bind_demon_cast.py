#!/usr/bin/env python3
"""Execute complete current Bind Demon do158 offline; opaque helpers stay explicit."""
import argparse,json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import *
from probe_generic_pet_parameters import Initializer,BASE,STACK,OBJECTS,STOP
from probe_warlock_runtime import ROOT,STAGE,digest
OUT=ROOT/'.local/class-skills/bind-demon-cast-source';CAP=STAGE/'warlock-do-158-20261108/capture.json'
GAME=OBJECTS;CASTER=OBJECTS+0x2000;TARGET=OBJECTS+0x4000;OTHER=OBJECTS+0x5000;ROW=OBJECTS+0x6000;TABLE=OBJECTS+0x7000;COUNT=OBJECTS+0x8000;PATH=OBJECTS+0x9000;DATA=OBJECTS+0xa000;AI=OBJECTS+0xb000;NODES=OBJECTS+0xc000;VT=OBJECTS+0xd000
F={k:BASE+v for k,v in dict(skill=0x6bf40,refresh=0x371b10,eligible=0x44b240,stop1=0x333830,stop2=0x3333e0,value=0x24eb30,transfer=0x35f7d0,prepareFree=0x7c63e0,resolve=0x370680,stop3=0x333270,bind=0x44b3b0,room=0x2a3b30,log=0x7b6af0,log2=0x7b66e0,fatal=0x15675c8).items()};F['free']=STOP+0x1010
class Cast(Initializer):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.artifacts=[CAP]
  cap=json.loads(CAP.read_text());assert cap['dispatcherIndexLinked'];row=next(r for r in cap['records']if r['slot']==158);assert row['allUnwindFragmentsCaptured']
  for f in row['fragments']:
   path=next(ROOT/n for n,h in cap['artifacts'].items()if n.endswith('.bin')and h==f['sha256']);raw=path.read_bytes();assert digest(raw)==f['sha256'];a=BASE+int(f['rva'],16);self.map(a,len(raw));self.uc.mem_write(a,raw);self.ranges.append((a,a+len(raw)));self.artifacts.append(path)
  for a,size in [(OBJECTS,0x20000),(STACK,0x40000),(STOP,0x2000),(BASE+0x1e03ac0,64),(BASE+0x1cf9f70,8)]:self.map(a,size)
  for a in F.values():self.map(a,1);self.uc.mem_write(a,b'\xc3')
  self.uc.hook_add(UC_HOOK_CODE,self.hook)
 def wi(self,a,v):self.uc.mem_write(a,struct.pack('<I',v&0xffffffff))
 def si(self,a):return struct.unpack('<i',self.uc.mem_read(a,4))[0]
 def ui(self,a):return struct.unpack('<I',self.uc.mem_read(a,4))[0]
 def wh(self,a,v):self.uc.mem_write(a,struct.pack('<H',v&65535))
 def uh(self,a):return struct.unpack('<H',self.uc.mem_read(a,2))[0]
 def ret(self,value=0):
  sp=self.uc.reg_read(UC_X86_REG_RSP);self.uc.reg_write(UC_X86_REG_RAX,value&0xffffffffffffffff);self.uc.reg_write(UC_X86_REG_RIP,self.q(sp));self.uc.reg_write(UC_X86_REG_RSP,sp+8)
 def hook(self,u,a,size,user):
  if a==BASE+0x44bc6f and not self.p['game']or a==BASE+0x44bd49 and u.reg_read(UC_X86_REG_RDX)==0:self.unsupported=True;u.emu_stop();return
  if any(lo<=a<hi for lo,hi in self.ranges):return
  name=next((n for n,x in F.items()if x==a),None);assert name,hex(a)
  c,d,v,z=[u.reg_read(r)for r in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]];sp=u.reg_read(UC_X86_REG_RSP);p=self.p;result=0
  if name=='skill':assert(c,d)==(p['selector'],382);self.trace.append([1,c,d]);result=ROW if p['row']else 0
  elif name=='refresh':
   assert(c,d)==(GAME,CASTER if p['caster']else 0);self.trace.append([2,1 if d else 0]);self.putq(PATH+0x70,0 if p['refreshClears']else self.q(PATH+0x70))
  elif name=='eligible':assert(c,d,v,z)==(GAME,CASTER,TARGET,382);self.trace.append([3,1,2,382]);result=int(p['eligible'])
  elif name in ['stop1','stop2','stop3']:assert c==TARGET;self.trace.append([{'stop1':4,'stop2':5,'stop3':10}[name]])
  elif name=='value':assert c==TARGET;self.trace.append([6]);result=p['value']
  elif name=='transfer':
   assert(c,d,v)==(GAME,TARGET,CASTER);self.trace.append([7]);self.wi(CASTER+0x12c,p['lateContext'])
   if p['transferClearsData']:self.putq(TARGET+0x10,0)
  elif name=='prepareFree':pass
  elif name=='free':
   assert c==BASE+0x1cf9f70;node=(d-NODES)//32+1;assert 1<=node<=p['events'];self.trace.append([8,node]);self.freed.append(node)
   if p['freeMutatesNext']and node==1:self.putq(NODES+8,0)
  elif name=='resolve':
   assert(c,d,v)==(GAME,p['ownerKind']&0xffffffff,p['ownerId']&0xffffffff);self.trace.append([9,71,p['ownerKind'],p['ownerId']]);result=[0,TARGET,OTHER][p['resolved']]
  elif name=='bind':
   assert(c,d,v,z,self.si(sp+0x28),self.si(sp+0x30),u.mem_read(sp+0x38,1)[0])==(GAME,CASTER,TARGET,382,p['rank'],p['lateContext'],1)
   self.trace.append([11,382,p['rank'],p['lateContext'],1]);result=int(p['bind'])
  elif name=='room':assert c==TARGET;self.trace.append([12,self.ui(TARGET+0x124),self.uh(DATA+0x30)if self.q(TARGET+0x10)else -1])
  elif name in ['log','log2']:pass
  elif name=='fatal':self.unsupported=True;u.emu_stop();return
  else:raise AssertionError(name)
  self.ret(result)
 def run(self,p):
  self.p=p;self.trace=[];self.freed=[];self.unsupported=False;u=self.uc;u.mem_write(OBJECTS,bytes(0x20000));u.mem_write(STACK,bytes(0x40000));u.mem_write(GAME+0x106,bytes([p['selector']]))
  for s in [1,2,3]:self.putq(BASE+0x1e03ac0+s*16,TABLE)
  self.wi(TABLE+0x2d0,p['stateCount']);self.wh(ROW+0xa0,p['state']);self.wi(CASTER,p['kind']);self.wi(CASTER+0x124,p['casterFlags']);self.wi(CASTER+0x12c,p['context']);self.putq(CASTER+0x38,PATH if p['path']else 0)
  self.putq(PATH+0x70,CASTER if p['selfTarget']else TARGET if p['target']else 0);self.wi(TARGET,p['targetKind']);self.wi(TARGET+0x124,p['targetFlags']);self.wi(OTHER,p['resolvedKind']);self.putq(TARGET+0x10,DATA if p['data']else 0)
  self.wh(DATA+0x30,p['dataFlags']);self.wi(DATA+0x74,p['index']);self.putq(DATA+0x38,AI if p['control']else 0);self.wh(AI+0x38,p['aiFlags']);self.putq(AI+0x60,GAME if p['owner']else 0);self.wi(AI+0x68,p['ownerId']);self.wi(AI+0x6c,p['ownerKind'])
  self.putq(GAME+0x1d0+p['index']*8,COUNT if p['counter']else 0);self.wi(COUNT+0x2dc,p['count']);self.putq(TARGET+0xc0,NODES if p['events']else 0)
  for i in range(p['events']):self.putq(NODES+i*32+8,NODES+(i+1)*32 if i+1<p['events']else 0)
  self.putq(BASE+0x1cf9f70,VT);self.putq(VT+32,F['free']);sp=STACK+0x20008;self.putq(sp,STOP)
  for reg,value in zip([UC_X86_REG_RSP,UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9],[sp,GAME if p['game']else 0,CASTER if p['caster']else 0,382,p['rank']]):u.reg_write(reg,value&0xffffffffffffffff)
  u.emu_start(BASE+0x44bc50,STOP,count=12000);assert self.unsupported or u.reg_read(UC_X86_REG_RIP)==STOP
  return dict(result=-1 if self.unsupported else u.reg_read(UC_X86_REG_RAX)&0xffffffff,casterFlags=self.ui(CASTER+0x124),targetFlags=self.ui(TARGET+0x124),dataFlags=self.uh(DATA+0x30),count=self.si(COUNT+0x2dc),first=1 if self.q(TARGET+0xc0)else 0,freed=self.freed.copy(),trace=self.trace.copy())
def sample(**changes):
 p=dict(game=True,caster=True,row=True,selector=1,kind=0,path=True,target=True,selfTarget=False,refreshClears=False,state=214,stateCount=256,eligible=True,targetKind=1,data=True,control=True,aiFlags=0,dataFlags=0,index=3,counter=True,count=17,value=0,events=2,freeMutatesNext=False,owner=True,ownerKind=0,ownerId=17,resolved=0,resolvedKind=1,context=9,lateContext=-77,transferClearsData=False,bind=True,rank=1,casterFlags=1,targetFlags=1);p.update(changes);return p
def build(check=False):
 em=Cast();cases=[]
 def add(**changes):p=sample(**changes);cases.append(dict(input=p,output=em.run(p)))
 add()
 for key,values in dict(game=[False],caster=[False],row=[False],selector=[0,2,3,4],kind=[1,5],path=[False],target=[False],selfTarget=[True],refreshClears=[True],state=[-1,0,255,256,32767],stateCount=[0,214,215],eligible=[False],targetKind=[0,2],data=[False],control=[False],aiFlags=[1,2,3,0xffff],dataFlags=[2,8,0xffff],index=[0,9],counter=[False],count=[-1,2147483647],value=[-1,1],events=[0,1,3],freeMutatesNext=[True],owner=[False],ownerKind=[1,4],resolved=[1,2],resolvedKind=[0],transferClearsData=[True],bind=[False],rank=[20,40,255],casterFlags=[0,0xffffffff],targetFlags=[0,0xffffffff]).items():
  for value in values:add(**{key:value})
 for changes in [dict(row=False,caster=False),dict(eligible=False,data=False),dict(aiFlags=3,bind=False),dict(ownerKind=1,resolved=2),dict(ownerKind=1,resolved=2,resolvedKind=0),dict(resolved=1,bind=False),dict(owner=False,events=0,bind=False),dict(selector=0,state=-1),dict(selector=0,target=False)]:add(**changes)
 OUT.mkdir(exist_ok=True);path=OUT/'cases.json';text=json.dumps(cases,indent=2)+'\n'
 if check:assert path.read_text()==text
 else:path.write_text(text)
 libs=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libs)==1
 result=dict(referenceVersion='3.3.93847',sourceContexts=len(cases),machineCodeBytes=sum(b-a for a,b in em.ranges),skillId=382,doIndex=158,dispatcherIndexLinked=True,processMemoryRead=False,targetProcessCodeExecuted=False,sourceCodeExecutedInOfflineEmulator=True,
  scope='Complete current709byte do158 with declared eligibility/binding,transfer,AI stops,alignment,counter,registry,allocator and room effects. Return1after failed eligibility/binding,pre-bind mutations,event-next capture and current caster context are compared. This does not recover protected44b240/44b3b0 or admit learned binding/channel/payment.',
  artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in sorted(set([path,Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_parameters.py',*em.artifacts,*libs]))})
 path=ROOT/'tools/reference/bind-demon-cast-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
 if check:assert path.read_text()==text
 else:path.write_text(text)
 print(f"Bind Demon do158:{len(cases)}contexts/{result['machineCodeBytes']}current bytes;protected effects stay mandatory services.")
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
