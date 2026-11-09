#!/usr/bin/env python3
"""Compose both current overrides,default initializer,copy,survey and distance."""
import argparse,json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import *
from probe_generic_pet_parameters import Initializer,BASE,STACK,OBJECTS,STOP
from probe_warlock_runtime import ROOT,STAGE,Image,digest
OUT=ROOT/'.local/class-skills/pet-ai-overrides-source'
ACTOR=OBJECTS+0x1000;DATA=OBJECTS+0x2000;CTRL=OBJECTS+0x3000;TICK=OBJECTS+0x4000;OWNER=OBJECTS+0x5000;APATH=OBJECTS+0x6000;OPATH=OBJECTS+0x7000;RESULT=OBJECTS+0x8000;MANAGER=OBJECTS+0x9000;VTABLE=OBJECTS+0xa000;HEAP=OBJECTS+0xb000;TRAMP=STOP+0x200
FREE=STOP+0x300
F={k:BASE+v for k,v in dict(resolve=0x370680,follow=0x4b2060,default=0x4b60c0,moving=0x27d040,search=0x4b3df0,remove=0x32e790,idle=0x425550,missing=0x392be0,iterator=0x35e6d0,allocator=0xbaa540,cookie=0x155eeb0).items()}
class Overrides(Initializer):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.artifacts=[];image=Image();self.artifacts.append(image.reference)
  for path,addresses in [(STAGE/'pet-ai-overrides-20261109.json',[0x4b63d0,0x4b66e0]),(STAGE/'callees-pet-override-initialize-20261109.json',[0x4b10b0]),(STAGE/'callees-live-actions-order-20261029.json',[0x4b0ed0]),(ROOT/'.local/class-skills/pet-owner-reference-20261109/owner-callback.json',[0x4b08b0])]:
   cap=json.loads(path.read_text());self.artifacts.append(path)
   for n,h in cap['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
   for address in addresses:
    row=next(r for r in cap['records']if int(r['rva'],16)==address);assert row['allUnwindFragmentsCaptured']
    for f in row['fragments']:
     p=next(ROOT/n for n,h in cap['artifacts'].items()if n.endswith('.bin')and h==f['sha256']);self.load(p,int(f['rva'],16))
  path=STAGE/'callees-live-search-services-idle-20261030-40bfd0-part0.bin';proof=json.loads((ROOT/'tools/reference/generic-pet-search-callback-source-evidence.json').read_text());assert digest(path.read_bytes())==proof['artifacts'][str(path.relative_to(ROOT))];self.artifacts.append(ROOT/'tools/reference/generic-pet-search-callback-source-evidence.json');self.load(path,0x40bfd0)
  for rva in [0x1813840,0x1813460,0x18141c0]:self.map(BASE+rva,16);self.uc.mem_write(BASE+rva,image.bytes(rva,16))
  self.map(OBJECTS,0x20000);self.map(STACK,0x40000);self.map(STOP,0x1000);self.map(BASE+0x197a178,8)
  for a in [*F.values(),FREE]:self.map(a,1);self.uc.mem_write(a,b'\xc3')
  self.uc.reg_write(UC_X86_REG_CR0,(self.uc.reg_read(UC_X86_REG_CR0)&~4)|2);self.uc.reg_write(UC_X86_REG_CR4,self.uc.reg_read(UC_X86_REG_CR4)|0x200);self.uc.hook_add(UC_HOOK_CODE,self.hook)
 def load(self,path,rva):
  raw=path.read_bytes();a=BASE+rva;self.map(a,len(raw));self.uc.mem_write(a,raw);self.ranges.append((a,a+len(raw)));self.artifacts.append(path)
 def wi(self,a,v):self.uc.mem_write(a,struct.pack('<I',v&0xffffffff))
 def wb(self,a,v):self.uc.mem_write(a,bytes([v]))
 def si(self,a):return struct.unpack('<i',self.uc.mem_read(a,4))[0]
 def ret(self,value=0):
  sp=self.uc.reg_read(UC_X86_REG_RSP);self.uc.reg_write(UC_X86_REG_RAX,value&0xffffffffffffffff);self.uc.reg_write(UC_X86_REG_RSP,sp+8);self.uc.reg_write(UC_X86_REG_RIP,self.q(sp))
 def step(self):
  job=self.job
  if job['index']==len(self.p['visits']):self.uc.reg_write(UC_X86_REG_RSP,job['rsp']);self.ret();return
  n=self.p['visits'][job['index']];job['index']+=1;sp=job['rsp']-0x1000;self.putq(sp,TRAMP)
  for r,v in [(UC_X86_REG_RSP,sp),(UC_X86_REG_RCX,OBJECTS),(UC_X86_REG_RDX,OWNER),(UC_X86_REG_R8,OBJECTS+n*0x100),(UC_X86_REG_R9,job['context'])]:self.uc.reg_write(r,v)
  self.uc.reg_write(UC_X86_REG_RIP,BASE+0x4b08b0)
 def frame(self):
  a=self.cfg;return dict(prefix=[self.si(a+i)for i in [0,4,8,12]],memory=[self.si(a+i)for i in [0x58,0x5c,0x60]],target=self.q(a+0x50),visits=self.si(a+0x70))
 def hook(self,u,a,size,user):
  if a==TRAMP:self.step();return
  if a==BASE+0x4b10b0:
   self.cfg=u.reg_read(UC_X86_REG_R9);self.trace.append('initialize:'+str(int(u.reg_read(UC_X86_REG_R8)==OWNER)))
  if a in [BASE+0x4b64f1,BASE+0x4b687f]:self.initial=self.frame()
  if any(lo<=a<hi for lo,hi in self.ranges):return
  c,d,v,z=[u.reg_read(r)for r in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]];sp=u.reg_read(UC_X86_REG_RSP);p=self.p;actor=ACTOR if p['actor']else 0
  name=next((n for n,x in F.items()if x==a),None)
  if name=='cookie':self.ret(u.reg_read(UC_X86_REG_RAX));return
  if name=='resolve':assert c==OBJECTS and d==0 and v==77;self.trace.append('resolve:0:77');self.ret(OWNER if p['owner']else 0);return
  if name=='iterator':
   assert c==OBJECTS and d==OWNER and v==BASE+0x4b08b0 and self.q(z)==actor and self.q(z+8)==self.cfg
   self.trace.append('iterator');self.job=dict(rsp=sp,index=0,context=z);self.step();return
  if name=='follow':
   assert c==OBJECTS and d==actor and v==OWNER and z==self.cfg;self.trace.append('follow');result=p['follow'][min(self.follows,len(p['follow'])-1)];self.follows+=1;self.ret(int(result));return
  if name=='default':
   assert c==OBJECTS and d==actor and v==TICK;self.trace.append('default')
   if p['afterMode']is not None:self.wi(ACTOR+12,p['afterMode'])
   self.ret();return
  if name=='moving':assert c==actor;self.trace.append('moving');self.ret(int(p['moving']));return
  if name=='search':
   assert d==OBJECTS and v==actor and z==OWNER and self.q(sp+0x28)==self.cfg and self.si(sp+0x38)==0 and self.q(sp+0x40)==0
   self.trace.append('search:0:0');vector=self.q(sp+0x30)
   if p['searchExternal']:self.putq(vector,HEAP);self.putq(vector+16,0)
   self.putq(c+0x30,(p['high']&0xffffffff)<<32);self.ret(c);return
  if name=='remove':
   assert c==OBJECTS;n=(d-(OBJECTS+0xd000))//64+1;self.trace.append(f'remove:{n}');self.removed.append(n);self.ret();return
  if name=='idle':assert c==OBJECTS and d==actor and v==15;self.trace.append('idle:15');self.ret();return
  if name=='missing':assert c==OBJECTS and d==actor and v==0 and z==1;self.trace.append('missing:0:1');self.ret();return
  if name=='allocator':self.ret(MANAGER);return
  if a==FREE:assert c==MANAGER and d==HEAP;self.trace.append('releaseSearch');self.ret();return
  raise AssertionError(('Undeclared instruction',hex(a)))
 def run(self,p):
  self.p=p;self.trace=[];self.removed=[];self.follows=0;self.cfg=0;self.initial=None;self.job=None;u=self.uc;u.mem_write(OBJECTS,b'\0'*0x20000);u.mem_write(STACK,b'\0'*0x40000)
  self.wb(OBJECTS+0x106,1);self.wi(ACTOR,p['kind']);self.wi(ACTOR+12,p['mode']);self.wi(ACTOR+0x124,p['flags124']);self.wi(ACTOR+0x128,p['flags128']);self.putq(ACTOR+0x10,DATA if p['data']else 0);self.putq(DATA+0x38,CTRL if p['actorControl']else 0);self.putq(CTRL+0x60,OBJECTS if p['ownerGame']else 0);self.wi(CTRL+0x68,77);self.wi(CTRL+0x6c,0)
  self.putq(TICK,CTRL if p['control']else 0);self.wi(CTRL,p['state']);self.wi(CTRL+0x10,p['mode10']);self.wi(OWNER,p['ownerKind']);self.wi(OWNER+12,p['ownerMode']);self.wi(OWNER+0x124,p['ownerFlags124']);self.wi(OWNER+0x128,p['ownerFlags128'])
  for a,path,kind,x,y,present in [(ACTOR,APATH,p['kind'],p['x'],p['y'],p['path']),(OWNER,OPATH,p['ownerKind'],p['ox'],p['oy'],p['ownerPath'])]:
   self.putq(a+0x38,path if present else 0);u.mem_write(path+2,struct.pack('<H',x&65535));u.mem_write(path+6,struct.pack('<H',y&65535));self.wi(path+0x10,x);self.wi(path+0x14,y)
  for i,typ in enumerate(p['events']):
   a=OBJECTS+0xd000+i*64;self.wb(a,typ);self.putq(a+0x38,a+64 if i+1<len(p['events'])else 0)
  self.putq(ACTOR+0x148,OBJECTS+0xd000 if p['events']else 0);self.putq(MANAGER,VTABLE);self.putq(VTABLE+32,FREE)
  sp=STACK+0x30008;self.putq(sp,STOP)
  for r,value in [(UC_X86_REG_RSP,sp),(UC_X86_REG_RCX,OBJECTS),(UC_X86_REG_RDX,ACTOR if p['actor']else 0),(UC_X86_REG_R8,TICK),(UC_X86_REG_R9,F['default']if p['fallback']else 0)]:u.reg_write(r,value)
  failed=False
  try:u.emu_start(BASE+(0x4b63d0 if p['index']==0 else 0x4b66e0),STOP,count=30000)
  except unicorn.UcError as error:
   if u.reg_read(UC_X86_REG_RIP)in [BASE+0x4b6408,BASE+0x4b6717,BASE+0x4b6525,BASE+0x4b68ae]:failed=True
   else:raise AssertionError((hex(u.reg_read(UC_X86_REG_RIP)),p))from error
  if not failed:assert u.reg_read(UC_X86_REG_RIP)==STOP
  return dict(unsupported=failed,trace=self.trace.copy(),initial=self.initial,removed=self.removed.copy(),mode=self.si(ACTOR+12))
def sample(**changes):
 p=dict(index=0,actor=True,control=True,state=0,mode10=1,kind=1,data=True,actorControl=True,ownerGame=True,owner=True,ownerKind=0,ownerMode=1,ownerFlags124=0,ownerFlags128=0,mode=1,flags124=0,flags128=0,path=True,ownerPath=True,x=10,y=10,ox=12,oy=10,fallback=True,afterMode=None,moving=False,follow=[False],high=1,searchExternal=False,events=[2,3,2],visits=[1,2,3]);p.update(changes);return p
def build(check=False):
 em=Overrides();cases=[]
 for index in [0,1]:
  for change in [dict(),dict(control=False),dict(state=1),dict(mode10=0),dict(actor=False),dict(kind=0),dict(data=False),dict(actorControl=False),dict(ownerGame=False),dict(owner=False),dict(ownerKind=1),dict(ownerMode=0),dict(ownerMode=17),dict(ownerKind=1,ownerMode=12),dict(ownerFlags124=0x10000),dict(ownerKind=3,ownerFlags128=0x4000000),dict(mode=0,owner=False),dict(mode=12,owner=False),dict(flags124=0x10000,owner=False),dict(kind=3,flags128=0x4000000),dict(ox=100,follow=[True]),dict(ox=100,follow=[False,True],high=0),dict(ox=38),dict(ox=39),dict(fallback=False),dict(afterMode=2),dict(afterMode=2,moving=True),dict(high=0),dict(high=0,follow=[True]),dict(high=0,searchExternal=True),dict(high=-1,searchExternal=True),dict(ownerPath=False),dict(ownerKind=2,ox=70000,oy=-1),dict(ownerKind=2,ownerPath=False),dict(path=False),dict(visits=[]),dict(ownerKind=1,visits=[1,2,3])]:
   p=sample(index=index,**change);cases.append(dict(input=p,output=em.run(p)))
 OUT.mkdir(exist_ok=True);path=OUT/'cases.json';text=json.dumps(cases,indent=2)+'\n'
 if check:assert path.read_text()==text
 else:path.write_text(text)
 libs=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libs)==1
 result=dict(referenceVersion='3.3.93847',overrideBytes=1715,initializerBytes=360,composedBytes=sum(b-a for a,b in em.ranges),sourceContexts=len(cases),processMemoryRead=False,targetProcessCodeExecuted=False,sourceCodeExecutedInOfflineEmulator=True,scope='Both complete overrides,default initializer,empty-default copy,owner survey(SwarmRaw0)and squared distance execute together. Owner membership,follow/default/search/moving/missing-owner actions and logical inline/search-heap cleanup are declared services. No birth defaults,raw allocator/exception ABI or unwritten frame tails inferred.',artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in sorted(set([path,Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_parameters.py',*em.artifacts,*libs]))})
 path=ROOT/'tools/reference/pet-ai-overrides-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
 if check:assert path.read_text()==text
 else:path.write_text(text)
 print(f"AI overrides:{len(cases)}current contexts/{result['composedBytes']}composed bytes;both callbacks and shared default copy/survey/distance.")
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
