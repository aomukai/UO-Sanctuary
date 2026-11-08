#!/usr/bin/env python3
"""Execute retained current do167;row/progressive/target/factory services are explicit."""
import json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import *
from probe_generic_pet_parameters import Initializer,BASE,STACK,OBJECTS,STOP
from probe_warlock_runtime import ROOT,STAGE,Image,digest
GAME=OBJECTS;UNIT=OBJECTS+0x1000;ROW=OBJECTS+0x2000;PATH=OBJECTS+0x3000
GET=BASE+0x6bf40;PROG=BASE+0x4421f0;TARGET=BASE+0x3ad8d0;CREATE=BASE+0x400760;COOKIE=BASE+0x155eeb0
class Launch(Initializer):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.image=Image();self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.code=[]
  self.parent=STAGE/'warlock-do-167-20261108/capture.json';p=json.loads(self.parent.read_text());r=p['records'][0];assert r['slot']==167 and r['allUnwindFragmentsCaptured']and p['dispatcherIndexLinked']
  for f in r['fragments']:
   q=next(ROOT/n for n,h in p['artifacts'].items()if n.endswith('.bin')and h==f['sha256']);raw=q.read_bytes();assert digest(raw)==f['sha256'];a=BASE+int(f['rva'],16);self.map(a,len(raw));self.uc.mem_write(a,raw);self.ranges.append((a,a+len(raw)));self.code.append(q)
  self.map(BASE+0x197a178,8);self.uc.mem_write(BASE+0x197a178,b'\0'*8);self.map(STACK,0x40000);self.map(OBJECTS,0x10000);self.map(STOP,0x1000)
  for a in [GET,PROG,TARGET,CREATE,COOKIE]:self.map(a,1);self.uc.mem_write(a,b'\xc3')
  self.uc.reg_write(UC_X86_REG_CR0,(self.uc.reg_read(UC_X86_REG_CR0)&~4)|2);self.uc.reg_write(UC_X86_REG_CR4,self.uc.reg_read(UC_X86_REG_CR4)|0x200);self.uc.hook_add(UC_HOOK_CODE,self.hook)
 def i(self,a):return struct.unpack('<i',self.uc.mem_read(a,4))[0]
 def puti(self,a,v):self.uc.mem_write(a,struct.pack('<I',v&0xffffffff))
 def hook(self,u,a,size,user):
  if any(lo<=a<hi for lo,hi in self.ranges):return
  c=self.c;rcx=u.reg_read(UC_X86_REG_RCX);rdx=u.reg_read(UC_X86_REG_RDX);r8=u.reg_read(UC_X86_REG_R8);r9=u.reg_read(UC_X86_REG_R9);rsp=u.reg_read(UC_X86_REG_RSP);v=0
  if a==GET:assert(rcx,rdx)==(1,c['skill']);self.trace.append('row');v=ROW if c['row']else 0
  elif a==PROG:assert(rcx,rdx)==(UNIT if c['actor']else 0,c['skill']);self.trace.append('progressive');v=c['missile']
  elif a==TARGET:
   assert(rcx,rdx)==(GAME,UNIT if c['actor']else 0);self.trace.append('target');self.puti(r8,c['aimX']);self.puti(r9,c['aimY']);v=c['target']
  elif a==CREATE:
   assert rcx==GAME;raw=bytes(u.mem_read(rdx,0x98));known=set()
   def i(o):known.update(range(o,o+4));return struct.unpack_from('<i',raw,o)[0]
   flags=i(0);owner=struct.unpack_from('<Q',raw,8)[0];known.update(range(8,16));assert owner==UNIT
   req=[flags,1]+[i(o)for o in [0x20,0x24,0x28,0x2c,0x30,0x3c,0x40]]
   assert all(v==0 for n,v in enumerate(raw)if n not in known),'Unaccounted request field'
   self.requests.append(req);self.trace.append('create');v=0x12345678 if c['allocation']else 0
  elif a==COOKIE:v=u.reg_read(UC_X86_REG_RAX)
  else:raise AssertionError(('Undeclared instruction',hex(a)))
  u.reg_write(UC_X86_REG_RAX,v&0xffffffffffffffff);u.reg_write(UC_X86_REG_RIP,self.q(rsp));u.reg_write(UC_X86_REG_RSP,rsp+8)
 def run(self,c):
  self.c=c;self.trace=[];self.requests=[];u=self.uc;u.mem_write(OBJECTS,b'\0'*0x10000);u.mem_write(STACK,b'\0'*0x40000)
  u.mem_write(GAME+0x106,b'\x01');self.puti(UNIT,c['kind']);self.puti(UNIT+0x124,c['flags']);self.putq(UNIT+0x38,PATH if c['path']else 0)
  u.mem_write(PATH+2,struct.pack('<H',c['x']&65535));u.mem_write(PATH+6,struct.pack('<H',c['y']&65535));u.mem_write(PATH+0x10,struct.pack('<ii',c['x'],c['y']))
  rsp=STACK+0x20008;self.putq(rsp,STOP);u.reg_write(UC_X86_REG_RSP,rsp);u.reg_write(UC_X86_REG_RCX,GAME);u.reg_write(UC_X86_REG_RDX,UNIT if c['actor']else 0);u.reg_write(UC_X86_REG_R8,c['skill']);u.reg_write(UC_X86_REG_R9,c['rank']);u.emu_start(BASE+0x452650,STOP,count=10000);assert u.reg_read(UC_X86_REG_RIP)==STOP
  return dict(result=u.reg_read(UC_X86_REG_RAX)&0xffffffff,flags=self.i(UNIT+0x124),trace=self.trace.copy(),requests=self.requests.copy())
def build():
 em=Launch();rows=[];base=dict(actor=True,row=True,target=True,allocation=True,missile=711,kind=0,path=True,x=180,y=220,aimX=212,aimY=230,skill=399,rank=1,flags=0x21)
 def add(**kw):c=base|kw;rows.append(dict(input=c,output=em.run(c)))
 add();add(allocation=False);add(row=False);add(missile=-1);add(missile=0);add(target=False)
 for kind in range(6):add(kind=kind,x=70000,y=-7,rank=21)
 for kind in [0,1,3]:add(kind=kind,path=False)
 add(aimX=0,aimY=0);add(aimX=180,aimY=220);add(rank=0);add(rank=255);add(actor=False,row=False,flags=0)
 stage=ROOT/'.local/class-skills/miasma-chain-launch-source';stage.mkdir(exist_ok=True);p=stage/'cases.json';p.write_text(json.dumps(rows,separators=(',',':'))+'\n')
 receipt=dict(referenceVersion='3.3.93847',entryRva='0x452650',dispatcherIndexLinked=True,currentMachineCodeExecutedOffline=True,processMemoryRead=False,cases=len(rows),sourceBytes=sum(b-a for a,b in em.ranges),externalServices=['Row and progressive missile helper are supplied;nonpositive ID fails before target/flag/factory.','Target coordinates,allocation result and cookie are explicit;the cookie preserves the pending RAX result.','Current full152byte factory request is checked,including all zero reserved/velocity/context fields. No factory/world execution is claimed.'],limits=['New maker43,moving-chain45,hit41/cloud41 dependencies,actual target/placement/factory/magic damage/casting/lifecycle remain separate.','A supplied progressive ID711 is not a complete current progressive-helper proof.'],artifacts={str(q.relative_to(ROOT)):digest(q.read_bytes())for q in [Path(__file__).resolve(),p,em.parent,*em.code]})
 (ROOT/'tools/reference/miasma-chain-launch-current-evidence.json').write_text(json.dumps(receipt,indent=2)+'\n');print('Miasma Chain do167:'+str(len(rows))+' current-instruction contexts;exact request/guards/factory failure,not a cloud or learned cast.')
if __name__=='__main__':build()
