#!/usr/bin/env python3
"""Execute retained current do164 with explicit direction/row/formula/area services."""
import json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import *
from probe_generic_pet_parameters import Initializer,BASE,STACK,OBJECTS,STOP
from probe_warlock_runtime import ROOT,STAGE,Image,digest
GAME=OBJECTS;UNIT=OBJECTS+0x1000;ROW=OBJECTS+0x2000;PATH=OBJECTS+0x3000
GET=BASE+0x6bf40;TARGET=BASE+0x3ad8d0;GATE=BASE+0x3a6090;DIR=BASE+0x285590;CALC=BASE+0x2bae90;REACH=BASE+0x27c360;CENTER=BASE+0x278450;AREA=BASE+0x3a6160
class Sweep(Initializer):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.image=Image();self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.code=[]
  self.parent=STAGE/'warlock-do-164-20261108/capture.json';p=json.loads(self.parent.read_text());r=p['records'][0];assert r['slot']==164 and r['allUnwindFragmentsCaptured']and p['dispatcherIndexLinked']
  for f in r['fragments']:
   q=next(ROOT/n for n,h in p['artifacts'].items()if n.endswith('.bin')and h==f['sha256']);raw=q.read_bytes();assert digest(raw)==f['sha256'];a=BASE+int(f['rva'],16);self.map(a,len(raw));self.uc.mem_write(a,raw);self.ranges.append((a,a+len(raw)));self.code.append(q)
  for rva in [0x18130b4,0x1812c80]:self.map(BASE+rva,4);self.uc.mem_write(BASE+rva,self.image.bytes(rva,4))
  self.map(STACK,0x40000);self.map(OBJECTS,0x10000);self.map(STOP,0x1000)
  for a in [GET,TARGET,GATE,DIR,CALC,REACH,CENTER,AREA]:self.map(a,1);self.uc.mem_write(a,b'\xc3')
  self.uc.reg_write(UC_X86_REG_CR0,(self.uc.reg_read(UC_X86_REG_CR0)&~4)|2);self.uc.reg_write(UC_X86_REG_CR4,self.uc.reg_read(UC_X86_REG_CR4)|0x200);self.uc.hook_add(UC_HOOK_CODE,self.hook)
 def i(self,a):return struct.unpack('<i',self.uc.mem_read(a,4))[0]
 def puti(self,a,v):self.uc.mem_write(a,struct.pack('<I',v&0xffffffff))
 def hook(self,u,a,size,user):
  if any(lo<=a<hi for lo,hi in self.ranges):return
  c=self.c;rcx=u.reg_read(UC_X86_REG_RCX);rdx=u.reg_read(UC_X86_REG_RDX);r8=u.reg_read(UC_X86_REG_R8);r9=u.reg_read(UC_X86_REG_R9);rsp=u.reg_read(UC_X86_REG_RSP);v=0
  if a==GET:assert(rcx,rdx)==(1,c['skill']);self.trace.append('row');v=ROW if c['row']else 0
  elif a==TARGET:
   assert(rcx,rdx)==(GAME,UNIT if c['actor']else 0);self.trace.append('target');self.puti(r8,c['targetX']);self.puti(r9,c['targetY']);v=c['target']
  elif a==GATE:assert(rcx,rdx,r8,r9,self.q(rsp+0x28))==(GAME,UNIT if c['actor']else 0,ROW,c['skill'],c['rank']);self.trace.append('gate');v=c['delayed']
  elif a==DIR:
   self.points.append([rcx&0xffffffff,rcx>>32,rdx&0xffffffff,rdx>>32]);self.puti(r8,17);self.puti(r8+4,19);self.puti(r9,c['heading']);self.trace.append('direction')
  elif a==CALC:
   assert(rcx,rdx,r9,self.q(rsp+0x28))==(1,UNIT,c['skill'],c['rank']);assert r8 in [1005,1006,1007];self.trace.append('calc'+str(r8-1000));v=c[{1005:'width',1006:'total',1007:'extra'}[r8]]
  elif a==REACH:assert rcx==UNIT;self.trace.append('reach');v=c['reach']
  elif a==CENTER:assert rcx==UNIT;self.trace.append('center');v=(c['centerX']&0xffffffff)|((c['centerY']&0xffffffff)<<32)
  elif a==AREA:
   assert(rcx,rdx)==(GAME,UNIT);assert self.q(rsp+0x30)==BASE+0x450780 and self.q(rsp+0x40)==0;ctx=self.q(rsp+0x38)
   assert self.q(ctx+8)==UNIT and self.q(ctx+16)==ROW
   self.requests.append([self.i(ctx),self.i(ctx+4),self.i(r8),self.i(r8+4),self.i(ctx+24),self.i(ctx+28),self.i(ctx+32),self.i(ctx+36),self.i(ctx+40),int(u.mem_read(ctx+44,1)[0]),r9&0xffffffff,self.q(rsp+0x28)&0xffffffff]);self.trace.append('area')
  else:raise AssertionError(('Undeclared instruction',hex(a)))
  u.reg_write(UC_X86_REG_RAX,v&0xffffffffffffffff);u.reg_write(UC_X86_REG_RIP,self.q(rsp));u.reg_write(UC_X86_REG_RSP,rsp+8)
 def run(self,c):
  self.c=c;self.trace=[];self.points=[];self.requests=[];u=self.uc;u.mem_write(OBJECTS,b'\0'*0x10000);u.mem_write(STACK,b'\0'*0x40000)
  u.mem_write(GAME+0x106,b'\x01');self.puti(UNIT,c['kind']);self.puti(UNIT+0x124,c['flags']);self.putq(UNIT+0x38,PATH if c['path']else 0)
  u.mem_write(PATH+2,struct.pack('<H',c['x']&65535));u.mem_write(PATH+6,struct.pack('<H',c['y']&65535));u.mem_write(PATH+0x10,struct.pack('<ii',c['x'],c['y']))
  for off,val in [(0x1a0,1005),(0x1a4,1006),(0x1a8,1007),(0x70,c['filter'])]:self.puti(ROW+off,val)
  u.mem_write(ROW+0x24d,bytes([c['sourceDamage']]))
  rsp=STACK+0x20008;self.putq(rsp,STOP);u.reg_write(UC_X86_REG_RSP,rsp);u.reg_write(UC_X86_REG_RCX,GAME);u.reg_write(UC_X86_REG_RDX,UNIT if c['actor']else 0);u.reg_write(UC_X86_REG_R8,c['skill']);u.reg_write(UC_X86_REG_R9,c['rank']);u.emu_start(BASE+0x4508e0,STOP,count=10000);assert u.reg_read(UC_X86_REG_RIP)==STOP
  return dict(result=u.reg_read(UC_X86_REG_RAX)&0xffffffff,flags=self.i(UNIT+0x124),trace=self.trace,points=self.points,requests=self.requests)
def build():
 em=Sweep();rows=[];base=dict(actor=True,row=True,target=True,delayed=False,kind=0,path=True,x=180,y=220,targetX=212,targetY=230,skill=391,rank=1,flags=0x21,filter=41347,sourceDamage=128,heading=0,width=11,total=30,extra=0,reach=2,centerX=180,centerY=220)
 def add(**kw):c=base|kw;rows.append(dict(input=c,output=em.run(c)))
 for rank in [1,10,19,20,40,255]:
  for heading in [0,63]:add(rank=rank,width=20 if rank>=20 else rank+10,heading=heading)
 for width,total in [(0,30),(-1,30),(1,0),(30,30),(31,30)]:add(width=width,total=total,sourceDamage=0)
 for kind in [2,4,5,3]:add(kind=kind,x=70000,y=-7,extra=3,reach=5,sourceDamage=77)
 add(row=False);add(target=False);add(delayed=True);add(actor=False,row=False,flags=0);add(path=False)
 stage=ROOT/'.local/class-skills/cleave-sweep-source';stage.mkdir(exist_ok=True);p=stage/'cases.json';p.write_text(json.dumps(rows,separators=(',',':'))+'\n')
 receipt=dict(referenceVersion='3.3.93847',entryRva='0x4508e0',dispatcherIndexLinked=True,currentMachineCodeExecutedOffline=True,processMemoryRead=False,cases=len(rows),sourceBytes=sum(b-a for a,b in em.ranges),externalServices=['Primary row lookup,target-coordinate resolution and immediate aura gate are supplied.','Direction service supplies a valid0..63heading;the outer function packed coordinates are checked independently.','calc5/6/7,weapon reach and area center are explicit inputs. All formula order,arc float32/wrap math,flags,source-damage fallback and full recipient request fields execute in the current body.','Area enumeration records the450780callback/context but never replaces its protected contact behavior with damage.'],limits=['Does not implement start68,recipient callback450780 or establish actual native target/arc membership,weapon contact/payment/lifecycle/input.','Heading helper and weapon reach were captured separately;this probe does not certify their math/inventory subcalls.'],artifacts={str(q.relative_to(ROOT)):digest(q.read_bytes())for q in [Path(__file__).resolve(),p,em.parent,*em.code]})
 (ROOT/'tools/reference/cleave-sweep-current-evidence.json').write_text(json.dumps(receipt,indent=2)+'\n');print('Current Cleave do164:'+str(len(rows))+' focused outer request contexts;no live process execution or old compiler matrix.')
if __name__=='__main__':build()
