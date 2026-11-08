#!/usr/bin/env python3
"""Execute captured do154 offline; formula/getter/factory boundaries are explicit."""
import argparse,json,struct
import unicorn
from pathlib import Path
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import *
from probe_generic_pet_parameters import Initializer,BASE,STACK,OBJECTS,STOP
from probe_warlock_runtime import ROOT,STAGE,Image,digest
GAME=OBJECTS;UNIT=OBJECTS+0x1000;SKILL=OBJECTS+0x2000;PATH=OBJECTS+0x3000;TABLE=OBJECTS+0x4000
ENTRY=BASE+0x44a7a0;GET=BASE+0x6bf40;PROG=BASE+0x4421f0;VEL=BASE+0x2de700;CALC=BASE+0x2bae90;CREATE=BASE+0x400760;COOKIE=BASE+0x155eeb0
class Launch(Initializer):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.image=Image();self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.code=[]
  proof=json.loads((STAGE/'warlock-new-do-20261108/capture.json').read_text());assert proof['dispatcherIndexLinked']
  row=next(r for r in proof['records']if r['slot']==154);assert row['allUnwindFragmentsCaptured']
  for f in row['fragments']:
   p=next(ROOT/n for n,h in proof['artifacts'].items()if n.endswith('.bin')and h==f['sha256']);raw=p.read_bytes();assert digest(raw)==f['sha256'];a=BASE+int(f['rva'],16);self.map(a,len(raw));self.uc.mem_write(a,raw);self.ranges.append((a,a+len(raw)));self.code.append(p)
  self.vectors=list(zip(struct.unpack('<64i',self.image.bytes(0x170cbd0,256)),struct.unpack('<64i',self.image.bytes(0x170cf00,256))))
  for rva in [0x170cbd0,0x170cf00]:self.map(BASE+rva,256);self.uc.mem_write(BASE+rva,self.image.bytes(rva,256))
  self.map(BASE+0x197a178,8);self.uc.mem_write(BASE+0x197a178,b'\0'*8);self.map(BASE+0x1e03ac0,64)
  self.map(STACK,0x40000);self.map(OBJECTS,0x10000);self.map(STOP,0x1000)
  for a in [GET,PROG,VEL,CALC,CREATE,COOKIE]:self.map(a,1);self.uc.mem_write(a,b'\xc3')
  self.uc.reg_write(UC_X86_REG_CR0,(self.uc.reg_read(UC_X86_REG_CR0)&~4)|2);self.uc.reg_write(UC_X86_REG_CR4,self.uc.reg_read(UC_X86_REG_CR4)|0x200);self.uc.hook_add(UC_HOOK_CODE,self.hook)
 def hook(self,uc,address,size,user):
  if any(a<=address<b for a,b in self.ranges):return
  c=self.c;rcx=uc.reg_read(UC_X86_REG_RCX);rdx=uc.reg_read(UC_X86_REG_RDX);r8=uc.reg_read(UC_X86_REG_R8);r9=uc.reg_read(UC_X86_REG_R9);rsp=uc.reg_read(UC_X86_REG_RSP);value=0
  if address==GET:assert rcx==c[4]and rdx==c[9];value=SKILL if c[1]else 0;self.trace.append(1)
  elif address==PROG:assert rcx==(UNIT if c[0]else 0)and rdx==c[9];value=c[2];self.trace.append(2)
  elif address==VEL:assert(rcx,rdx,r8)==(c[4],c[2],c[10]);value=c[11];self.trace.append(3)
  elif address==CALC:
   assert(rcx,rdx,r9)==(c[4],UNIT,c[9])and self.q(rsp+0x28)==c[10]
   assert r8 in [1001,1002];value=c[12]if r8==1002 else c[13];self.trace.append(4 if r8==1002 else 5)
  elif address==CREATE:
   assert rcx==GAME;raw=bytes(uc.mem_read(rdx,0x98));known=set()
   def i(o):known.update(range(o,o+4));return struct.unpack_from('<i',raw,o)[0]
   flags=i(0);owner=struct.unpack_from('<Q',raw,8)[0];known.update(range(8,16));assert owner==UNIT
   req=[flags,1]+[i(o)for o in [0x20,0x24,0x28,0x2c,0x30,0x38,0x3c,0x40,0x68]];assert all(v==0 for j,v in enumerate(raw)if j not in known)
   self.requests.append(req);self.trace.append(6);value=0 if c[16]else 0x12345678
  elif address==COOKIE:value=uc.reg_read(UC_X86_REG_RAX) # MSVC cookie check preserves the pending function result.
  else:raise AssertionError(('Undeclared instruction',hex(address)))
  uc.reg_write(UC_X86_REG_RAX,value&0xffffffffffffffff);uc.reg_write(UC_X86_REG_RIP,self.q(rsp));uc.reg_write(UC_X86_REG_RSP,rsp+8)
 def run(self,c):
  self.c=c;self.requests=[];self.trace=[];u=self.uc;u.mem_write(OBJECTS,b'\0'*0x10000);u.mem_write(STACK,b'\0'*0x40000)
  u.mem_write(GAME+0x106,bytes([c[4]]));u.mem_write(UNIT,struct.pack('<I',c[5]));u.mem_write(UNIT+0x124,struct.pack('<I',c[15]));u.mem_write(UNIT+0x12c,struct.pack('<i',c[14]));self.putq(UNIT+0x38,PATH if c[6]else 0)
  u.mem_write(PATH+2,struct.pack('<H',c[7]&65535));u.mem_write(PATH+6,struct.pack('<H',c[8]&65535));u.mem_write(PATH+0x10,struct.pack('<ii',c[7],c[8]))
  self.putq(BASE+0x1e03ac0+c[4]*16,TABLE);u.mem_write(TABLE+0x1160,struct.pack('<i',c[3]));u.mem_write(SKILL+0x194,struct.pack('<II',1001,1002))
  rsp=STACK+0x20008;self.putq(rsp,STOP);u.reg_write(UC_X86_REG_RSP,rsp);u.reg_write(UC_X86_REG_RCX,GAME);u.reg_write(UC_X86_REG_RDX,UNIT if c[0]else 0);u.reg_write(UC_X86_REG_R8,c[9]);u.reg_write(UC_X86_REG_R9,c[10]);u.emu_start(ENTRY,STOP,count=10000);assert u.reg_read(UC_X86_REG_RIP)==STOP
  return dict(result=u.reg_read(UC_X86_REG_RAX)&0xffffffff,flags=struct.unpack('<I',u.mem_read(UNIT+0x124,4))[0],trace=self.trace.copy(),requests=self.requests.copy())
def build(check=False):
 em=Launch();cases=[];base=[1,1,689,742,1,0,1,180,220,394,1,25,0,16,-1,0,0]
 def add(c):cases.append(dict(input=c,output=em.run(c)))
 for count in [-2147483648,-3,-1,0,1,2,3,5,8,16,31,32,33,63,64,65,128,1073741825,2147483647]:
  for velocity in [-25,0,7]:
   for fail in [0,1]:
    c=base.copy();c[12]=velocity;c[13]=count;c[16]=fail;add(c)
 for selector in [1,2,3]:
  for kind in range(6):
   for path in [0,1]:
    if kind in [2,4,5]and not path:continue
    c=base.copy();c[4]=selector;c[5]=kind;c[6]=path;c[7]=70000;c[8]=-7;c[10]=10;c[13]=32;c[14]=-2147483648;add(c)
 for field,value in [(1,0),(2,-1),(2,742),(3,689),(15,0x21),(10,63)]:c=base.copy();c[field]=value;add(c)
 c=base.copy();c[0]=0;c[1]=0;add(c)
 stage=ROOT/'.local/class-skills/ring-of-fire-launch-source';stage.mkdir(exist_ok=True);p=stage/'cases.json';s=json.dumps(dict(cases=cases,vectors=em.vectors),separators=(',',':'))+'\n'
 if check:assert p.read_text()==s
 else:p.write_text(s)
 out=ROOT/'tools/reference/ring-of-fire-launch-current-evidence.json';result=dict(referenceVersion='3.3.93847',entryRva='0x44a7a0',currentMachineCodeExecutedOffline=True,targetProcessCodeExecuted=False,processMemoryRead=False,cases=len(cases),sourceBytes=sum(b-a for a,b in em.ranges),unicornVersion=unicorn.__version__,emulatorLibrarySha256=digest(next(Path(unicorn.__file__).parent.rglob('libunicorn.so*')).read_bytes()),externalServices=['Skill row lookup and progressive missile ID are supplied.','Missile count,base velocity,formula tokens194/198and source path/coords are explicit synthetic inputs;their helper internals are not emulated.','Factory records all152bytes with zero reserved fields and supplied success/failure;it does not allocate world actors or RNG.','Stack-cookie validation is a declared service preserving the pending return register.'],artifacts={str(q.relative_to(ROOT)):digest(q.read_bytes())for q in [p,Path(__file__).resolve(),STAGE/'warlock-new-do-20261108/capture.json',*em.code]},limits=['This establishes current launch assembly composition,not damage17,missile travel/collision or real factory/player resource/lifecycle behavior.','No missing/protected helper is replaced with a claimed current implementation. Native producer must compare ordered requests with these declared boundaries before learned admission.']);encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Ring do154 current x64: '+str(len(cases))+'offline contexts;guards,ordered requests,rounding/coordinates and failed allocations;no live input or old compiler matrix.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
