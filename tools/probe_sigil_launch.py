#!/usr/bin/env python3
"""Run complete current Sigil do157 offline over explicit external services."""
import argparse,json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import *
from probe_generic_pet_parameters import Initializer,BASE,STACK,OBJECTS,STOP
from probe_warlock_runtime import ROOT,STAGE,Image,digest
GAME=OBJECTS;UNIT=OBJECTS+0x1000;SKILL=OBJECTS+0x2000;TABLE=OBJECTS+0x3000;MISSILE=OBJECTS+0x4000;DATA=OBJECTS+0x5000
F={k:BASE+v for k,v in dict(get=0x6bf40,select=0x28f950,place=0x3af770,point=0x3ad8d0,distance=0x2c4800,create=0x400760,calc=0x2bae90,ceil=0x15982f8,track=0x290000,event=0x32f9a0,cookie=0x155eeb0).items()}
CAPTURE=STAGE/'warlock-do-157-20261108/capture.json'
class Sigil(Initializer):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.image=Image();self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.code=[]
  proof=json.loads(CAPTURE.read_text());row=proof['records'][0];assert row['slot']==157 and row['allUnwindFragmentsCaptured']and proof['dispatcherIndexLinked']
  for f in row['fragments']:
   p=next(ROOT/n for n,h in proof['artifacts'].items()if n.endswith('.bin')and h==f['sha256']);raw=p.read_bytes();assert digest(raw)==f['sha256'];a=BASE+int(f['rva'],16);self.map(a,len(raw));self.uc.mem_write(a,raw);self.ranges.append((a,a+len(raw)));self.code.append(p)
  ceiling=STAGE/'warlock-do-157-20261108/services-sigil-rounding-20261108.json';record=json.loads(ceiling.read_text());fragment=record['records'][0]['fragments'][0];p=next(ROOT/n for n,h in record['artifacts'].items()if n.endswith('.bin')and h==fragment['sha256']);raw=p.read_bytes();assert digest(raw)==fragment['sha256']and raw[0x86:0x88]==b'\xc3\xcc'
  # All finite branches stay within this135byte prefix and return at+0x86.
  # NaN has an external tail edge and is explicitly outside this fixture.
  self.map(F['ceil'],135);self.uc.mem_write(F['ceil'],raw[:135]);self.ranges.append((F['ceil'],F['ceil']+135));self.code += [ceiling,p]
  for rva,size in [(0x197a178,8),(0x1e03ac0,64),(0x1813084,4),(0x1812d60,4)]:self.map(BASE+rva,size)
  constant=self.image.bytes(0x1813084,4);assert struct.unpack('<f',constant)==(40.0,);self.uc.mem_write(BASE+0x1813084,constant)
  self.map(STACK,0x40000);self.map(OBJECTS,0x10000);self.map(STOP,0x1000)
  one=self.image.bytes(0x1812d60,4);assert struct.unpack('<f',one)==(1.0,);self.uc.mem_write(BASE+0x1812d60,one)
  for name,a in F.items():
   if name!='ceil':self.map(a,1);self.uc.mem_write(a,b'\xc3')
  self.uc.reg_write(UC_X86_REG_CR0,(self.uc.reg_read(UC_X86_REG_CR0)&~4)|2);self.uc.reg_write(UC_X86_REG_CR4,self.uc.reg_read(UC_X86_REG_CR4)|0x200);self.uc.hook_add(UC_HOOK_CODE,self.hook)
 def hook(self,u,a,size,user):
  if any(begin<=a<end for begin,end in self.ranges):
   if a==F['ceil']:self.trace.append([8])
   return
  c=self.c;cx=u.reg_read(UC_X86_REG_RCX);dx=u.reg_read(UC_X86_REG_RDX);r8=u.reg_read(UC_X86_REG_R8);r9=u.reg_read(UC_X86_REG_R9);sp=u.reg_read(UC_X86_REG_RSP);value=0;unit=UNIT if c['caster']else 0
  def i(p):return struct.unpack('<i',u.mem_read(p,4))[0]
  if a==F['get']:assert(cx,dx)==(c['selector'],c['skill']);self.trace.append([1]);value=SKILL if c['row']else 0
  elif a==F['select']:assert(cx,dx,r8,r9)==(c['selector'],unit,c['skill'],c['rank'])and self.q(sp+0x28)==SKILL+0x68 and self.q(sp+0x30)==3;self.trace.append([2]);value=c['missile']
  elif a==F['place']:assert(cx,dx,r8)==(GAME,unit,c['missile']);self.trace.append([3]);value=int(c['place'])
  elif a==F['point']:
   assert(cx,dx)==(GAME,unit);u.mem_write(r8,struct.pack('<i',c['x']));u.mem_write(r9,struct.pack('<i',c['y']));self.trace.append([4])
  elif a==F['distance']:assert(cx,dx&0xffffffff,r8&0xffffffff)==(UNIT,c['x']&0xffffffff,c['y']&0xffffffff);self.trace.append([5]);value=c['distance']
  elif a==F['create']:
   assert cx==GAME;raw=bytes(u.mem_read(dx,152));known=set()
   def read(off):known.update(range(off,off+4));return struct.unpack_from('<i',raw,off)[0]
   known.update(range(8,16));assert struct.unpack_from('<Q',raw,8)[0]==UNIT
   self.requests.append([read(off)for off in [0,0x20,0x24,0x28,0x3c,0x40,0x68]]);assert all(v==0 for j,v in enumerate(raw)if j not in known);self.trace.append([6]);value=MISSILE if c['allocate']else 0
  elif a==F['calc']:assert(cx,dx,r8,r9)==(c['selector'],UNIT,123,c['skill'])and i(sp+0x28)==c['rank'];self.trace.append([7]);value=c['limit']
  elif a==F['track']:
   assert(cx,dx,r8&0xffffffff,r9&0xffffffff)==(UNIT,MISSILE,c['limit']&0xffffffff,c['state']&0xffffffff)
   assert self.q(sp+0x38)==BASE+0x3a7f30
   self.trace.append([9,i(sp+0x28),i(sp+0x30)]);value=int(c['track'])
  elif a==F['event']:
   assert(cx,dx,r8)==(GAME,UNIT,12);assert self.q(sp+0x28)==0 and all(i(sp+off)==0 for off in [0x30,0x38,0x40]);self.trace.append([10,r9&0xffffffff])
  elif a==F['cookie']:value=u.reg_read(UC_X86_REG_RAX)
  else:raise AssertionError(('Undeclared instruction',hex(a)))
  u.reg_write(UC_X86_REG_RAX,value&0xffffffffffffffff);u.reg_write(UC_X86_REG_RIP,self.q(sp));u.reg_write(UC_X86_REG_RSP,sp+8)
 def run(self,c):
  self.c=c;self.trace=[];self.requests=[];u=self.uc;u.mem_write(OBJECTS,b'\0'*0x10000);u.mem_write(STACK,b'\0'*0x40000)
  u.mem_write(GAME+0x106,bytes([c['selector']]));u.mem_write(GAME+0x170,struct.pack('<i',c['frame']));u.mem_write(UNIT+0x124,struct.pack('<I',c['flags']));u.mem_write(UNIT+0x12c,struct.pack('<i',c['context']));u.mem_write(SKILL+0x198,struct.pack('<I',123));u.mem_write(SKILL+0xa0,struct.pack('<h',c['state']));u.mem_write(MISSILE,struct.pack('<i',c['kind']));self.putq(MISSILE+0x10,DATA if c['data']else 0);u.mem_write(DATA+0x14,struct.pack('<f',c['milliseconds']));self.putq(BASE+0x1e03ac0+c['selector']*16,TABLE);u.mem_write(TABLE+0x1160,struct.pack('<i',c['count']))
  sp=STACK+0x20008;self.putq(sp,STOP);u.reg_write(UC_X86_REG_RSP,sp);u.reg_write(UC_X86_REG_RCX,GAME);u.reg_write(UC_X86_REG_RDX,UNIT if c['caster']else 0);u.reg_write(UC_X86_REG_R8,c['skill']);u.reg_write(UC_X86_REG_R9,c['rank']);u.emu_start(BASE+0x44a100,STOP,count=10000);assert u.reg_read(UC_X86_REG_RIP)==STOP
  return dict(result=u.reg_read(UC_X86_REG_RAX)&0xffffffff,flags=struct.unpack('<I',u.mem_read(UNIT+0x124,4))[0],trace=self.trace.copy(),requests=self.requests.copy())
def build(check=False):
 em=Sigil();cases=[];skillsPath=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';missilesPath=ROOT/'tools/reference/d2r-named-equipment/json/missiles.json';statesPath=ROOT/'tools/reference/d2r-named-equipment/json/states.json';skills=json.loads(skillsPath.read_text());missiles=json.loads(missilesPath.read_text());states=json.loads(statesPath.read_text());byName={r['Missile']:r for r in missiles.values()};bindings=[]
 for skill,ids,dispatch in [(393,[699,686,702],30),(396,[700,687,703],61),(400,[701,688,704],30)]:
  row=skills[str(skill)];assert row['srvdofunc']==157 and row['aurastate']=='sigil'and row['calc3']==1
  selected=[byName[row['srvmissile'+letter]]for letter in 'abc'];assert [r['*ID']for r in selected]==ids and all(r['pSrvDoFunc']==14 and r['Param1']==6 and r['Param2']==dispatch and r['Range']=='100+(lvl*25)'for r in selected)
  bindings.append(dict(skill=skill,missiles=ids,serverDo=157,missileDo=14,period=6,dispatch=dispatch,range='100+(lvl*25)'))
 assert next(r for r in states.values()if r['state']=='sigil')['*ID']==205
 base=dict(caster=True,row=True,selector=1,skill=393,rank=1,missile=699,count=742,place=True,x=120,y=140,distance=10,allocate=True,limit=1,track=True,kind=3,data=True,milliseconds=5000.0,state=205,frame=100,flags=0,context=-1)
 def add(c):cases.append(dict(input=c,output=em.run(c)))
 for skill,ids in [(393,[699,686,702]),(396,[700,687,703]),(400,[701,688,704])]:
  for rank,mid in zip([1,10,20],ids):
   for selector in [1,2,3]:
    for track in [False,True]:add(base|dict(skill=skill,rank=rank,missile=mid,selector=selector,track=track))
 for milliseconds in [-41.0,-40.0,-39.0,-1.0,0.0,1.0,39.0,40.0,41.0,4999.0,5000.0,5001.0,16777216.0]:
  for data in [False,True]:
   for kind in [0,3]:add(base|dict(milliseconds=milliseconds,data=data,kind=kind))
 for field,values in [('caster',[False]),('row',[False]),('missile',[-1,742]),('place',[False]),('distance',[-1,100,101]),('allocate',[False]),('limit',[-1,0,3]),('x',[0]),('y',[0]),('flags',[0x21]),('context',[-2147483648]),('state',[-1]),('frame',[2147483600])]:
  for value in values:add(base|{field:value})
 add(base|dict(x=0,y=0));add(base|dict(caster=False,row=False))
 stage=ROOT/'.local/class-skills/sigil-launch-source';stage.mkdir(exist_ok=True);p=stage/'cases.json';encoded=json.dumps(dict(cases=cases),separators=(',',':'))+'\n'
 if check:assert p.read_text()==encoded
 else:p.write_text(encoded)
 result=dict(referenceVersion='3.3.93847',entryRva='0x44a100',dispatcherIndexLinked=True,currentMachineCodeExecutedOffline=True,targetProcessCodeExecuted=False,processMemoryRead=False,cases=len(cases),pinnedBindings=bindings,sourceBytes=sum(b-a for a,b in em.ranges),unicornVersion=unicorn.__version__,emulatorLibrarySha256=digest(next(Path(unicorn.__file__).parent.rglob('libunicorn.so*')).read_bytes()),externalServices=['Record/progressive selector/placement/coordinates/distance supplied; complete do157 executes all guards and call ordering.','Factory captures all152bytes including zero fields;success and created actor raw kind/data/float14 are supplied,not real allocation.','Calc field198,tracking state/limit callback and event scheduling are declared services. Tracking service includes unqualified list replacement/removal internals.','The captured135byte finite-input paths of rounding helper15982f8 execute with owned constants40and1;NaN tail handling is outside this fixture.','Cookie service preserves the pending return per previously captured current cookie helper.'],limits=['No learned cast,world status recipient,tracking callback cleanup or payment is enabled by this component.','Source field12c and created data float14 retain raw labels;gameplay field roles are not inferred.'],artifacts={str(q.relative_to(ROOT)):digest(q.read_bytes())for q in [p,Path(__file__).resolve(),CAPTURE,skillsPath,missilesPath,statesPath,*em.code]});out=ROOT/'tools/reference/sigil-launch-current-evidence.json';s=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==s
 else:out.write_text(s)
 print(f'Current Sigil do157: {len(cases)} offline contexts; explicit selector/placement/factory/formula/tracking/rounding services.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
