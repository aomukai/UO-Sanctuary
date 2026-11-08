#!/usr/bin/env python3
"""Execute current tracking core and small-roster sort offline.

The declared domain is <=8 existing slots (normal Sigil limit is1).
Stat storage, state-list acquisition and missile lookup are explicit services.
"""
import argparse,json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import *
from probe_generic_pet_parameters import Initializer,BASE,STACK,OBJECTS,STOP
from probe_warlock_runtime import ROOT,STAGE,digest
OWNER=OBJECTS;NEW=OBJECTS+0x1000;LIST=OBJECTS+0x2000;FACTORY=OBJECTS+0x3000;RESOLVER=OBJECTS+0x4000;VTABLE=OBJECTS+0x5000;ACTORS=OBJECTS+0x6000;STATROW=OBJECTS+0xe000
F={k:BASE+v for k,v in dict(row=0xf6f10,get=0x24ac60,set=0x24dcd0,dispose=0x5f1b0,cookie=0x155eeb0).items()};F.update(factory=STOP+0x100,resolve=STOP+0x110,destroy=STOP+0x120)
class Tracking(Initializer):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.code=[];self.parents=[]
  for name,rva in [('services-sigil-limit-services-20261108.json',0x28fa10),('sigil-tracking-callback-20261108.json',0x290910)]:
   parent=STAGE/'warlock-do-157-20261108'/name;proof=json.loads(parent.read_text());row=next(r for r in proof['records']if int(r['rva'],16)==rva);assert row['allUnwindFragmentsCaptured'];self.parents.append(parent)
   for f in row['fragments']:
    p=next(ROOT/n for n,h in proof['artifacts'].items()if n.endswith('.bin')and h==f['sha256']);raw=p.read_bytes();assert digest(raw)==f['sha256'];a=BASE+int(f['rva'],16);self.map(a,len(raw));self.uc.mem_write(a,raw);self.ranges.append((a,a+len(raw)));self.code.append(p)
  self.map(BASE+0x197a178,8);self.map(STACK,0x40000);self.map(OBJECTS,0x10000);self.map(STOP,0x1000)
  for a in F.values():self.map(a,1);self.uc.mem_write(a,b'\xc3')
  self.uc.reg_write(UC_X86_REG_CR0,(self.uc.reg_read(UC_X86_REG_CR0)&~4)|2);self.uc.reg_write(UC_X86_REG_CR4,self.uc.reg_read(UC_X86_REG_CR4)|0x200);self.uc.hook_add(UC_HOOK_CODE,self.hook)
 def hook(self,u,a,size,user):
  if any(begin<=a<end for begin,end in self.ranges):return
  c=self.c;cx=u.reg_read(UC_X86_REG_RCX);dx=u.reg_read(UC_X86_REG_RDX);r8=u.reg_read(UC_X86_REG_R8);r9=u.reg_read(UC_X86_REG_R9);sp=u.reg_read(UC_X86_REG_RSP);value=0
  if a==F['factory']:
   assert cx==FACTORY and self.q(dx)==OWNER and self.q(r8)==(NEW if c['new']else 0);assert struct.unpack('<i',u.mem_read(r9,4))[0]==c['end'];u.mem_write(self.q(sp+0x28),bytes([int(c['created'])]));self.factoryCalls+=1;value=LIST if c['list']else 0
  elif a==F['resolve']:
   assert cx==RESOLVER;guid=struct.unpack('<i',u.mem_read(dx,4))[0];self.lookups.append(guid);value=self.units.get(guid,0)
  elif a==F['row']:assert cx==1 and dx in [354,355];value=STATROW
  elif a==F['get']:assert cx==LIST and r8==STATROW;value=self.stats.get((dx>>16,dx&65535),0)
  elif a==F['set']:
   assert (cx,dx)==(1,LIST)and r8 in [354,355];layer=struct.unpack('<H',u.mem_read(sp+0x28,2))[0];value=struct.unpack('<i',struct.pack('<I',r9&0xffffffff))[0];self.stats[r8,layer]=value;self.writes.append([r8,layer,value]);value=0
  elif a==F['destroy']:assert cx==RESOLVER and dx==0
  elif a==F['dispose']:assert cx==RESOLVER
  elif a==F['cookie']:value=u.reg_read(UC_X86_REG_RAX)
  else:raise AssertionError(('Undeclared instruction (no heap vector growth supported)',hex(a)))
  u.reg_write(UC_X86_REG_RAX,value&0xffffffffffffffff);u.reg_write(UC_X86_REG_RIP,self.q(sp));u.reg_write(UC_X86_REG_RSP,sp+8)
 def run(self,c):
  assert len(c['entries'])<=8;self.c=c;self.stats={(355,0):len(c['entries'])};self.writes=[];self.lookups=[];self.units={};self.factoryCalls=0;u=self.uc;u.mem_write(OBJECTS,b'\0'*0x10000);u.mem_write(STACK,b'\0'*0x40000)
  u.mem_write(OWNER+0x1bd,b'\1');u.mem_write(NEW+8,struct.pack('<i',c['id']));u.mem_write(LIST+0x24,struct.pack('<f',c['expiry']));u.mem_write(LIST+0x1c,struct.pack('<I',c['flags']));self.putq(FACTORY,VTABLE);self.putq(RESOLVER,VTABLE+0x20);self.putq(VTABLE+8,F['factory']);self.putq(VTABLE+0x20,F['destroy']);self.putq(VTABLE+0x28,F['resolve'])
  for slot,e in enumerate(c['entries'],1):
   guid,time,kind,data,present=e;self.stats[354,slot]=guid;self.stats[355,slot]=time;p=ACTORS+(slot-1)*0x200;u.mem_write(p,struct.pack('<i',kind));self.putq(p+0x10,p+0x100 if data else 0);u.mem_write(p+0x114,struct.pack('<f',5000.0))
   if present:self.units[guid]=p
  sp=STACK+0x20008;self.putq(sp,STOP);self.putq(sp+0x28,c['start']&0xffffffffffffffff);self.putq(sp+0x30,c['end']&0xffffffffffffffff);self.putq(sp+0x38,RESOLVER);u.reg_write(UC_X86_REG_RSP,sp);u.reg_write(UC_X86_REG_RCX,OWNER);u.reg_write(UC_X86_REG_RDX,NEW if c['new']else 0);u.reg_write(UC_X86_REG_R8,c['limit']);u.reg_write(UC_X86_REG_R9,FACTORY);u.emu_start(BASE+0x28fa10,STOP,count=100000);assert u.reg_read(UC_X86_REG_RIP)==STOP
  return dict(reschedule=u.reg_read(UC_X86_REG_RAX)&255,stats=[[stat,layer,value]for(stat,layer),value in sorted(self.stats.items())],writes=self.writes.copy(),lookups=self.lookups.copy(),factoryCalls=self.factoryCalls,expiry=struct.unpack('<f',u.mem_read(LIST+0x24,4))[0],flags=struct.unpack('<I',u.mem_read(LIST+0x1c,4))[0],lifetimes=[struct.unpack('<f',u.mem_read(ACTORS+i*0x200+0x114,4))[0]for i in range(len(c['entries']))])
def build(check=False):
 em=Tracking();cases=[];base=dict(new=True,id=99,start=100,end=225,expiry=125.0,flags=0,created=False,list=True,limit=1,entries=[])
 def add(c):cases.append(dict(input=c,output=em.run(c)))
 for limit in [0,1,2,3,8]:
  for count in range(7):
   for same in [False,True]:
    entries=[[10+i,40 if same else 40+i*7,3,True,True]for i in range(count)];add(base|dict(limit=limit,entries=entries))
 for e in [[10,0,3,True,True],[0,40,3,True,True],[-1,40,3,True,True],[10,40,3,True,False],[10,40,1,True,True],[10,40,3,False,True]]:
  for slot in range(3):
   entries=[[20+i,20+i,3,True,True]for i in range(3)];entries[slot]=e;add(base|dict(entries=entries))
 for expiry in [0.0,224.0,225.0,300.0]:
  for created in [False,True]:
   for new in [False,True]:add(base|dict(expiry=expiry,created=created,new=new))
 for start in [0,1,2147483647]:add(base|dict(start=start,flags=0x40))
 add(base|dict(list=False));add(base|dict(limit=-1));add(base|dict(end=0,expiry=-1.0));add(base|dict(end=-5,expiry=-10.0))
 stage=ROOT/'.local/class-skills/sigil-tracking-source';stage.mkdir(exist_ok=True);p=stage/'cases.json';s=json.dumps(dict(cases=cases),separators=(',',':'))+'\n'
 if check:assert p.read_text()==s
 else:p.write_text(s)
 result=dict(referenceVersion='3.3.93847',entryRva='0x28fa10',currentMachineCodeExecutedOffline=True,targetProcessCodeExecuted=False,processMemoryRead=False,cases=len(cases),sourceBytes=sum(b-a for a,b in em.ranges),unicornVersion=unicorn.__version__,emulatorLibrarySha256=digest(next(Path(unicorn.__file__).parent.rglob('libunicorn.so*')).read_bytes()),externalServices=['Existing or newly-created state list,stat354/355row/read/write and missile resolver are declared services;all tracking and <=32element sort decisions execute.','Cases have at most6existing slots so the original inline8element vector never grows. Larger corrupted/foreign lists and allocator failure are unsupported.','Owner selector1,known stat rows,integer stat values and ordinary state-list expiry are supplied. Source list allocation/posting and timer dispatch are outside this core.','Functor destruction/disposal and cookie ABI are declared no-gameplay services.'],limits=['Return is reschedule-needed,not a general tracking-success flag. Existing list entries can change while false is returned.','This does not implement recipient statuses,world missiles or owner-state removal callbacks.'],artifacts={str(q.relative_to(ROOT)):digest(q.read_bytes())for q in [p,Path(__file__).resolve(),*em.parents,*em.code]});out=ROOT/'tools/reference/sigil-tracking-current-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print(f'Current tracking core: {len(cases)} offline contexts;sort/ties/slot reuse/expiry/conditional timer result;no heap allocation or live input.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
