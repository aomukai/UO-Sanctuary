#!/usr/bin/env python3
"""Execute complete captured do155 offline over declared world/pet/state services."""
import argparse,json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import *
from probe_generic_pet_parameters import Initializer,BASE,STACK,OBJECTS,STOP
from probe_warlock_runtime import ROOT,STAGE,digest
GAME=OBJECTS;UNIT=OBJECTS+0x1000;ROW=OBJECTS+0x2000;PATH=OBJECTS+0x3000;TABLE=OBJECTS+0x4000;SKILLS=OBJECTS+0x6000
ROOMS=[OBJECTS+0x7000,OBJECTS+0x8000,OBJECTS+0x9000];TARGET=OBJECTS+0xa000;LEVEL=OBJECTS+0xb000;STATE=OBJECTS+0xc000
RVA=dict(get=0x6bf40,room=0x278450,formula=0x2bae90,coordinates=0x3ad8d0,level=0x2993a0,place=0x27c5e0,refresh=0x371b10,valid=0x372ec0,
 pets=0x35e6d0,typed=0x35e860,allocate=0x3a80c0,bind=0x45f8d0,aura=0x3afe80,write=0x24dcd0,log=0x7b6af0,log2=0x7b66e0,fatal=0x15675c8)
CAP=STAGE/'warlock-new-do-20261108/capture.json'
class Cast(Initializer):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.code=[]
  proof=json.loads(CAP.read_text());assert proof['dispatcherIndexLinked'];row=next(r for r in proof['records']if r['slot']==155);assert row['allUnwindFragmentsCaptured']
  for f in row['fragments']:
   p=next(ROOT/n for n,h in proof['artifacts'].items()if n.endswith('.bin')and h==f['sha256']);raw=p.read_bytes();assert digest(raw)==f['sha256'];a=BASE+int(f['rva'],16);self.map(a,len(raw));self.uc.mem_write(a,raw);self.ranges.append((a,a+len(raw)));self.code.append(p)
  for a,size in [(STACK,0x40000),(OBJECTS,0x10000),(STOP,0x1000),(BASE+0x1e03ac0,0x100)]:self.map(a,size)
  for a in RVA.values():self.map(BASE+a,1);self.uc.mem_write(BASE+a,b'\xc3')
  self.uc.hook_add(UC_HOOK_CODE,self.hook)
 def hook(self,u,address,size,user):
  if any(a<=address<b for a,b in self.ranges):return
  c=self.c;a,b,d,e=[u.reg_read(r)for r in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]];sp=u.reg_read(UC_X86_REG_RSP);value=0
  def i(p):return struct.unpack('<i',u.mem_read(p,4))[0]
  def puti(p,v):u.mem_write(p,struct.pack('<i',v))
  if address==BASE+RVA['get']:
   assert (a,b)==(c['selector'],375);self.trace.append([1]);value=ROW if c['row']else 0
  elif address==BASE+RVA['room']:
   if a==UNIT:
    self.roomCalls+=1;assert self.roomCalls<=2;idx=self.roomCalls-1;self.trace.append([3+idx]);value=ROOMS[idx]if c['room'if idx==0 else'secondRoom']else 0
   else:assert a==TARGET;self.trace.append([9]);value=ROOMS[2]if c['targetRoom']else 0
  elif address==BASE+RVA['formula']:
   assert(a,b,e,self.q(sp+0x28))==(c['selector'],UNIT,375,c['rank']);field={1001:0x194,1002:0x84,1003:0x190,1004:0x80}[d];self.trace.append([2,field])
   if field==0x84:value=c['range1'if self.rangeCalls==0 else'range2'];self.rangeCalls+=1
   else:value=c[{0x194:'maximum',0x190:'clear',0x80:'duration'}[field]]
  elif address==BASE+RVA['coordinates']:
   assert(a,b)==(GAME,UNIT);self.trace.append([5]);puti(d,c['x']);puti(e,c['y']);value=int(c['coordinates'])
  elif address==BASE+RVA['level']:
   assert(a,b)==(c['selector'],10);self.trace.append([6]);value=LEVEL if c['levelRow']else 0
  elif address==BASE+RVA['place']:
   assert(a,b,d,e)==(UNIT,c['x'],c['y'],0x1804);self.trace.append([7]);value=c['placement']
  elif address==BASE+RVA['refresh']:assert(a,b)==(GAME,UNIT);self.trace.append([8])
  elif address==BASE+RVA['valid']:assert(a,b,d,e&255)==(GAME,UNIT,TARGET,1);self.trace.append([10]);value=int(c['valid'])
  elif address in [BASE+RVA['pets'],BASE+RVA['typed']]:
   assert(a,b,d)==(GAME,UNIT,BASE+0x44aa70);typed=address==BASE+RVA['typed'];assert not typed or i(sp+0x28)==c['petType']
   v=list(struct.unpack('<6i',u.mem_read(e,24)));assert self.q(e+24)==ROW;self.trace.append([11,c['petType']if typed else -999,*v]);puti(e+16,c['remaining'])
  elif address==BASE+RVA['allocate']:
   assert self.q(a)==UNIT and self.q(a+8)==TARGET;v=list(struct.unpack('<6i',u.mem_read(a+16,24)));assert self.q(a+40)==0
   self.trace.append([12,*v]);value=STATE if c['allocate']else 0
  elif address==BASE+RVA['bind']:
   assert(a,b,d,e,self.q(sp+0x28))==(UNIT,STATE,ROW,375,c['rank']);self.trace.append([13])
  elif address==BASE+RVA['aura']:
   assert(a,b,d,e)==(GAME,UNIT,375,c['rank']);assert i(sp+0x28)==c['context']and i(sp+0x38)==c['state']and self.q(sp+0x40)==0
   assert self.q(sp+0x48)==ROW+0xa4 and self.q(sp+0x50)==ROW+0xac;self.trace.append([14])
  elif address==BASE+RVA['write']:
   assert(a,b)==(c['selector'],STATE)and struct.unpack('<H',u.mem_read(sp+0x28,2))[0]==0;self.trace.append([15,d,e if e<0x80000000 else e-0x100000000])
  elif address in [BASE+RVA['log'],BASE+RVA['log2']]:pass
  elif address==BASE+RVA['fatal']:self.boundary=True;u.emu_stop();return
  else:raise AssertionError(('Undeclared instruction',hex(address)))
  u.reg_write(UC_X86_REG_RAX,value&0xffffffffffffffff);u.reg_write(UC_X86_REG_RIP,self.q(sp));u.reg_write(UC_X86_REG_RSP,sp+8)
 def run(self,c):
  self.c=c;self.trace=[];self.roomCalls=0;self.rangeCalls=0;self.boundary=False;u=self.uc;u.mem_write(OBJECTS,b'\0'*0x10000);u.mem_write(STACK,b'\0'*0x40000)
  def pi(p,v):u.mem_write(p,struct.pack('<i',v))
  pi(UNIT,c['kind']);pi(UNIT+0x124,c['flags']);pi(UNIT+0x12c,c['context']);u.mem_write(GAME+0x106,bytes([c['selector']]));self.putq(UNIT+0x100,SKILLS if c['skillContext']else 0);self.putq(SKILLS+0x18,ROW)
  self.putq(UNIT+0x38,PATH if c['path']else 0);u.mem_write(PATH+2,struct.pack('<H',c['cx']&65535));u.mem_write(PATH+6,struct.pack('<H',c['cy']&65535));u.mem_write(PATH+0x10,struct.pack('<ii',c['cx'],c['cy']))
  self.putq(PATH+0x70,UNIT if c['selfTarget']else TARGET if c['target']else 0);pi(TARGET,c['targetKind']);pi(TARGET+8,c['targetId'])
  for room,level in zip(ROOMS,[10,c['currentLevel'],c['targetLevel']]):self.putq(room+0x18,room+0x100);self.putq(room+0x190,room+0x200);pi(room+0x3f8,level)
  for field,token in [(0x194,1001),(0x84,1002),(0x190,1003),(0x80,1004)]:pi(ROW+field,token)
  u.mem_write(ROW+0x112,struct.pack('<b',c['petType']));u.mem_write(ROW+0xa2,struct.pack('<h',c['state']));u.mem_write(LEVEL+0x14,bytes([c['mode']]))
  for selector in [1,2,3]:self.putq(BASE+0x1e03ac0+selector*16,TABLE)
  pi(TABLE+0x1300,c['petCount']);pi(TABLE+0x2d0,c['stateCount']);rsp=STACK+0x20008;self.putq(rsp,STOP);u.reg_write(UC_X86_REG_RSP,rsp)
  for reg,v in [(UC_X86_REG_RCX,GAME),(UC_X86_REG_RDX,UNIT if c['caster']else 0),(UC_X86_REG_R8,375),(UC_X86_REG_R9,c['rank'])]:u.reg_write(reg,v)
  u.emu_start(BASE+0x44aca0,STOP,count=10000);assert self.boundary or u.reg_read(UC_X86_REG_RIP)==STOP
  return dict(result=-1 if self.boundary else u.reg_read(UC_X86_REG_RAX)&0xffffffff,flags=struct.unpack('<I',u.mem_read(UNIT+0x124,4))[0],trace=self.trace.copy())
def build(check=False):
 em=Cast();cases=[];primary=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';states=ROOT/'tools/reference/d2r-named-equipment/json/states.json';pets=ROOT/'tools/reference/d2r-named-equipment/json/pettype.json'
 skill=json.loads(primary.read_text())['375'];assert skill['srvdofunc']==155 and skill['calc2']==3 and skill['Param9']==38 and skill['auralencalc']=='ln34'
 assert json.loads(states.read_text())['death_mark']['*ID']==212 and json.loads(pets.read_text())['demon']['lineNumber']==20
 base=dict(caster=True,row=True,skillContext=True,room=True,secondRoom=True,currentLevel=10,coordinates=True,maximum=3,x=110,y=120,cx=100,cy=100,kind=0,path=True,range1=38,range2=38,levelRow=True,mode=1,placement=0,target=True,selfTarget=False,targetKind=1,targetId=77,valid=True,targetRoom=True,targetLevel=10,petType=20,petCount=21,state=212,stateCount=256,clear=1,remaining=2,allocate=True,duration=125,rank=1,selector=1,context=-17,flags=1)
 def add(**kw):c=base|kw;cases.append(dict(input=c,output=em.run(c)))
 add()
 for key,values in dict(caster=[False],row=[False],skillContext=[False],room=[False],secondRoom=[False],maximum=[-1,0,1,3,2147483647],coordinates=[False],currentLevel=[1,40,75,103,109,0],mode=[0,2,3],placement=[1,-1],levelRow=[False],target=[False],selfTarget=[True],targetKind=[0,2,3,4,5],valid=[False],targetRoom=[False],targetLevel=[1,40,75,103,109],petType=[-1,0,1,19,21,127],state=[-1,0,255,256,32767],clear=[0,-1],remaining=[-1,0,3,4],allocate=[False],selector=[2,3],context=[-2147483648,2147483647]).items():
  for v in values:add(**{key:v})
 for mode in [0,1,2,3]:
  for placed in [0,1]:add(mode=mode,placement=placed)
 for maximum in [1,3]:
  for remaining in [-1,0,1,3,4]:
   for valid in [False,True]:add(maximum=maximum,remaining=remaining,valid=valid)
 for kind in range(6):
  for path in [False,True]:
   if kind in [2,4,5]and not path:continue
   add(kind=kind,path=path,cx=70000,cy=-7,x=4474,y=65529,range1=38,range2=38)
 for x,y,r1,r2 in [(138,100,38,38),(139,100,38,38),(110,100,3,38),(110,100,38,3),(110,100,-38,-38),(65535,65535,2147483647,2147483647),(2147483647,-2147483648,38,38),(100,100,0,0)]:add(x=x,y=y,range1=r1,range2=r2)
 for rank in [20,40,255]:
  for selector in [1,2,3]:add(rank=rank,selector=selector,duration=125+13*(rank-1))
 for flags in [-2147483648,0,0x21]:add(flags=flags)
 add(targetId=1);add(caster=False,row=False);add(selector=0);add(selector=0,petType=0);add(selector=0,petType=0,target=False)
 stage=ROOT/'.local/class-skills/death-mark-cast-source';stage.mkdir(exist_ok=True);p=stage/'cases.json';s=json.dumps(dict(cases=cases),separators=(',',':'))+'\n'
 if check:assert p.read_text()==s
 else:p.write_text(s)
 result=dict(referenceVersion='3.3.93847',entryRva='0x44aca0',skillId=375,dispatcherIndexLinked=True,currentMachineCodeExecutedOffline=True,processMemoryRead=False,targetProcessCodeExecuted=False,cases=len(cases),sourceBytes=sum(b-a for a,b in em.ranges),unicornVersion=unicorn.__version__,emulatorLibrarySha256=digest(next(Path(unicorn.__file__).parent.rglob('libunicorn.so*')).read_bytes()),
  externalServices=['Skill/room/level/coordinate/target helpers and formula outputs are explicit. Signed distance/range products and source coordinate width execute in the real captured body.','Pet traversal records callback44aa70,typed versus unfiltered path,and complete mutable directive;the supplied remaining count represents callback consumption,not a verified teleport implementation.','State factory captures caster,target,skill,rank,duration,stat/value/state/removal fields. Bind/aura/stat writes are recorded in source order without inventing their internals.','Source fatal assertion paths are captured as boundary result-1;no live code,input,memory writes or protection changes.'],
  limits=['No learned Death Mark or actual pet warp,CC removal,ordinary state replacement or native player payment is admitted by this composition.','Callback44aa70 and other target/pet/state services require their own current binding/world acceptance;source row alone does not establish them.'],artifacts={str(q.relative_to(ROOT)):digest(q.read_bytes())for q in [p,Path(__file__).resolve(),CAP,primary,states,pets,*em.code]})
 out=ROOT/'tools/reference/death-mark-cast-current-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print(f'Death Mark do155: {len(cases)} current-code cases,1431bytes;pet directive/state/service ordering with explicit world boundaries.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
