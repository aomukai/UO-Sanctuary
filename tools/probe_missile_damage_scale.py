#!/usr/bin/env python3
"""Execute the complete current candidate damage body offline, not in D2R.

Record/formula/owner services remain declared. No dispatcher identity claim.
"""
import argparse,json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import *
from probe_generic_pet_parameters import Initializer,BASE,STACK,OBJECTS,STOP
from probe_warlock_runtime import ROOT,STAGE,Image,digest
GAME=OBJECTS;MISSILE=OBJECTS+0x1000;DATA=OBJECTS+0x2000;ROW=OBJECTS+0x3000;PACKET=OBJECTS+0x4000;OWNER=OBJECTS+0x5000
GET=BASE+0xdad30;RESOLVE=BASE+0x370680;CALC=BASE+0x2bb3a0
CAPTURE=STAGE/'ring-damage-body-20261108/capture.json'
class Scale(Initializer):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.image=Image();self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.code=[]
  proof=json.loads(CAPTURE.read_text());row=proof['records'][0];assert row['rva']=='0x3fa2f0'and row['allUnwindFragmentsCaptured']and not proof['dispatcherIndexLinked']
  for f in row['fragments']:
   p=next(ROOT/n for n,h in proof['artifacts'].items()if n.endswith('.bin')and h==f['sha256']);raw=p.read_bytes();assert digest(raw)==f['sha256'];a=BASE+int(f['rva'],16);self.map(a,len(raw));self.uc.mem_write(a,raw);self.ranges.append((a,a+len(raw)));self.code.append(p)
  self.map(BASE+0x1812f28,8);constant=self.image.bytes(0x1812f28,8);assert struct.unpack('<d',constant)==(100.0,);self.uc.mem_write(BASE+0x1812f28,constant)
  self.map(STACK,0x40000);self.map(OBJECTS,0x10000);self.map(STOP,0x1000)
  for a in [GET,RESOLVE,CALC]:self.map(a,1);self.uc.mem_write(a,b'\xc3')
  self.uc.reg_write(UC_X86_REG_CR0,(self.uc.reg_read(UC_X86_REG_CR0)&~4)|2);self.uc.reg_write(UC_X86_REG_CR4,self.uc.reg_read(UC_X86_REG_CR4)|0x200);self.uc.hook_add(UC_HOOK_CODE,self.hook)
 def hook(self,u,a,size,user):
  if any(begin<=a<end for begin,end in self.ranges):return
  c=self.c;rcx=u.reg_read(UC_X86_REG_RCX);rdx=u.reg_read(UC_X86_REG_RDX);r8=u.reg_read(UC_X86_REG_R8);r9=u.reg_read(UC_X86_REG_R9);rsp=u.reg_read(UC_X86_REG_RSP)
  if a==GET:
   assert (rcx,rdx&0xffffffff)==(c['selector'],c['id']&0xffffffff if c['present']else 0xffffffff);self.trace.append(['row',rcx,rdx&0xffffffff]);value=ROW if c['row']else 0
  elif a==RESOLVE:
   assert (rcx,rdx,r8)==(GAME,2,77);self.trace.append(['owner',2,77]);value=OWNER if c['owner']else 0
  elif a==CALC:
   assert (rcx,rdx,r8,r9)==(c['selector'],MISSILE if c['present']else 0,OWNER if c['present']and c['ownerFlag']and c['owner']else 0,c['token']&0xffffffff)
   actual=struct.unpack('<ii',u.mem_read(rsp+0x28,16)[0:4]+u.mem_read(rsp+0x30,4));assert actual==(c['id']if c['present']else -1,c['word0e']if c['present']and c['kind']==3 and c['data']else 0)
   self.trace.append(['formula',rcx,int(r8!=0),r9,*actual]);value=c['percent']
  else:raise AssertionError(('Undeclared instruction',hex(a)))
  u.reg_write(UC_X86_REG_RAX,value&0xffffffffffffffff);u.reg_write(UC_X86_REG_RIP,self.q(rsp));u.reg_write(UC_X86_REG_RSP,rsp+8)
 def run(self,c):
  self.c=c;self.trace=[];u=self.uc;u.mem_write(OBJECTS,b'\0'*0x10000);u.mem_write(STACK,b'\0'*0x40000)
  u.mem_write(GAME+0x106,bytes([c['selector']]));u.mem_write(MISSILE,struct.pack('<ii',c['kind'],c['id']));self.putq(MISSILE+0x10,DATA if c['data']else 0);u.mem_write(DATA+0xe,struct.pack('<h',c['word0e']));u.mem_write(MISSILE+0x128,struct.pack('<I',0x400 if c['ownerFlag']else 0));u.mem_write(MISSILE+0xe8,struct.pack('<ii',2,77));u.mem_write(ROW+0xb4,struct.pack('<i',c['token']))
  initial=[0x12345678+i for i in range(24)]
  for i,value in zip([6,8,9,11,12,13,14],c['values']):initial[i]=value
  u.mem_write(PACKET,struct.pack('<24i',*initial));rsp=STACK+0x20008;self.putq(rsp,STOP);u.reg_write(UC_X86_REG_RSP,rsp);u.reg_write(UC_X86_REG_RCX,GAME);u.reg_write(UC_X86_REG_RDX,MISSILE if c['present']else 0);u.reg_write(UC_X86_REG_R8,OBJECTS+0x6000);u.reg_write(UC_X86_REG_R9,PACKET);u.emu_start(BASE+0x3fa2f0,STOP,count=10000);assert u.reg_read(UC_X86_REG_RIP)==STOP
  return dict(before=initial,after=list(struct.unpack('<24i',u.mem_read(PACKET,96))),trace=self.trace.copy())
def build(check=False):
 em=Scale();cases=[];base=dict(present=True,row=True,id=689,kind=3,data=True,word0e=11,ownerFlag=True,owner=True,selector=1,token=123,percent=100,values=[1,99,100,255,256,257,65535])
 def add(c):cases.append(dict(input=c,output=em.run(c)))
 for percent in [-2147483648,-1,0,1,5,33,99,100,105,150,155,200,10000,2147483647]:
  for values in [[1,99,100,255,256,257,65535],[-1,-99,-100,-255,-256,-257,-65535],[16777215,16777216,16777217,2147483520,2147483647,-2147483648,-2147483520]]:
   add(base|dict(percent=percent,values=values))
 for present in [False,True]:
  for row in [False,True]:
   for token in [-1,123]:
    for kind in [0,3]:
     for data in [False,True]:
      for ownerFlag in [False,True]:add(base|dict(present=present,row=row,token=token,kind=kind,data=data,ownerFlag=ownerFlag,percent=155,word0e=-32768))
 for selector in [1,2,3]:
  for owner in [False,True]:add(base|dict(selector=selector,owner=owner,word0e=32767,percent=105))
 stage=ROOT/'.local/class-skills/missile-damage-scale-source';stage.mkdir(exist_ok=True);p=stage/'cases.json';s=json.dumps(dict(cases=cases),separators=(',',':'))+'\n'
 if check:assert p.read_text()==s
 else:p.write_text(s)
 paths=[p,Path(__file__).resolve(),CAPTURE,*em.code];result=dict(referenceVersion='3.3.93847',candidateEntryRva='0x3fa2f0',candidateTableRva='0x163c0b0',candidateSlot=17,dispatcherIndexLinked=False,currentMachineCodeExecutedOffline=True,targetProcessCodeExecuted=False,processMemoryRead=False,cases=len(cases),sourceBytes=sum(b-a for a,b in em.ranges),unicornVersion=unicorn.__version__,emulatorLibrarySha256=digest(next(Path(unicorn.__file__).parent.rglob('libunicorn.so*')).read_bytes()),changedPacketOffsets=['0x18','0x20','0x24','0x2c','0x30','0x34','0x38'],externalServices=['Missile row lookup supplies a formula token from raw offset0xb4. Token-1 skips scaling.','Owner resolver and missile formula evaluator are supplied; exact formula/fnum binding and damage field gameplay names are not inferred.','The complete candidate instructions execute, including all guards, raw context forwarding and floating arithmetic.'],limits=['The immutable31entry table matches the owned image, but its dispatcher/index linkage is not yet established. No public Ring handler is enabled.','This scales only an existing raw packet. Damage roll stage, mitigation, collision and lifetime remain separate.'],artifacts={str(q.relative_to(ROOT)):digest(q.read_bytes())for q in paths});out=ROOT/'tools/reference/missile-damage-scale-current-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print(f'Current candidate damage scale: {len(cases)} offline contexts; no dispatcher identity or live execution claim.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
