#!/usr/bin/env python3
"""Execute saved285590;protected math calls remain supplied services."""
import json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import *
from probe_generic_pet_parameters import Initializer,BASE,STACK,OBJECTS,STOP
from probe_warlock_runtime import ROOT,STAGE,Image,digest
ENTRY=BASE+0x285590;ANGLE=BASE+0x157cc40;SPAN=BASE+0x263c20;ROUND=BASE+0x1570ecc
def bits(n):return struct.unpack('<I',struct.pack('<f',n))[0]
class Direction(Initializer):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.image=Image();self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set()
  self.parent=STAGE/'cleave-services-20261109.json';p=json.loads(self.parent.read_text());r=next(r for r in p['records']if int(r['rva'],16)==0x285590)
  assert p['referenceVersion']=='3.3.93847'and p['executableSha256']==self.image.ref['executable']['sha256']and p['sourceReferencesVerified']
  assert r['allUnwindFragmentsCaptured']and len(r['fragments'])==1
  self.code=STAGE/'cleave-services-20261109-285590-part0.bin';raw=self.code.read_bytes();assert len(raw)==346 and digest(raw)==r['fragments'][0]['sha256']
  self.map(ENTRY,len(raw));self.uc.mem_write(ENTRY,raw)
  self.constants={}
  for a,size in [(0x18132c4,4),(0x18129ec,4),(0x181299c,4),(0x1812d60,4),(0x1813188,4),(0x1815fc0,16),(0x1812ec0,4),(0x1812f08,4),(0x18132fc,4),(0x1812fec,4),(0x18130b4,4)]:
   b=self.image.bytes(a,size);self.map(BASE+a,size);self.uc.mem_write(BASE+a,b);self.constants[hex(a)]=b.hex()
  self.map(STACK,0x40000);self.map(OBJECTS,0x10000);self.map(STOP,0x1000)
  for a in [ANGLE,SPAN,ROUND]:self.map(a,1);self.uc.mem_write(a,b'\xc3')
  self.uc.reg_write(UC_X86_REG_CR0,(self.uc.reg_read(UC_X86_REG_CR0)&~4)|2);self.uc.reg_write(UC_X86_REG_CR4,self.uc.reg_read(UC_X86_REG_CR4)|0x200);self.uc.hook_add(UC_HOOK_CODE,self.hook)
 def hook(self,u,a,size,user):
  if ENTRY<=a<ENTRY+346:return
  if a==STOP:u.emu_stop();return
  if a not in [ANGLE,SPAN,ROUND]:raise AssertionError('Unqualified direction instruction '+hex(a))
  name,n,result={ANGLE:('angle',2,'angleBits'),SPAN:('span',3,'spanBits'),ROUND:('round',1,'roundBits')}[a]
  regs=[UC_X86_REG_XMM0,UC_X86_REG_XMM1,UC_X86_REG_XMM2];self.calls.append([name,[u.reg_read(regs[i])&0xffffffff for i in range(n)]])
  u.reg_write(UC_X86_REG_XMM0,self.case[result]);rsp=u.reg_read(UC_X86_REG_RSP);u.reg_write(UC_X86_REG_RIP,self.q(rsp));u.reg_write(UC_X86_REG_RSP,rsp+8)
 def run(self,c):
  self.case=c;self.calls=[];self.uc.mem_write(OBJECTS,b'\x00'*32);rsp=STACK+0x20000-8;self.uc.reg_write(UC_X86_REG_RSP,rsp);self.putq(rsp,STOP)
  self.uc.reg_write(UC_X86_REG_RCX,(c['fromX']&0xffffffff)|((c['fromY']&0xffffffff)<<32));self.uc.reg_write(UC_X86_REG_RDX,(c['toX']&0xffffffff)|((c['toY']&0xffffffff)<<32))
  self.uc.reg_write(UC_X86_REG_R8,OBJECTS);self.uc.reg_write(UC_X86_REG_R9,OBJECTS+8);self.uc.emu_start(ENTRY,STOP,count=1000)
  assert self.uc.reg_read(UC_X86_REG_RIP)==STOP
  out=struct.unpack('<iii',self.uc.mem_read(OBJECTS,12));return dict(x=out[0],y=out[1],heading=out[2],calls=self.calls)
def build():
 em=Direction();cases=[]
 def add(point,angle=0.,span=0.,rounded=32.,**replace):
  c=dict(zip(['fromX','fromY','toX','toY'],point),angleBits=bits(angle),spanBits=bits(span),roundBits=bits(rounded));c.update(replace);cases.append(c)
 for p in [(0,0,0,0),(1,2,1,2),(0,0,1,0),(1,0,0,0),(0,0,0,1),(0,1,0,0),(0,0,1,1),(1,1,0,0),(0,1,1,0),(1,0,0,1),(0,0,65536,65536),(12345,67890,12346,67892),(0,0,-1,-1),(-1,-1,0,0),(-2147483648,0,2147483647,0),(2147483647,0,-2147483648,0),(2147483647,-2147483648,-2147483648,2147483647)]:add(p)
 for span in [-10.,-3.1415927410125732,-3.141592502593994,0.,3.141592502593994,3.1415927410125732,10.,float('inf'),-float('inf')]:add((0,0,65536,131072),span=span)
 for rounded in [-65.,-64.,-1.,0.,1.,31.,32.,63.,64.,65.,2147483520.,2147483648.,-2147483648.,float('inf')]:add((131072,65536,0,0),rounded=rounded)
 for angle in [-3.1415927410125732,-.7853981852531433,2.356194496154785,float('inf'),-float('inf')]:add((0,0,65536,0),angle=angle)
 for field in ['angleBits','spanBits','roundBits']:add((0,0,0,0),**{field:0x7fc01234})
 rows=[dict(input=c,output=em.run(c))for c in cases];stage=ROOT/'.local/class-skills/cleave-direction-source';stage.mkdir(exist_ok=True);path=stage/'cases.json';path.write_text(json.dumps(rows,separators=(',',':'))+'\n')
 receipt=dict(referenceVersion='3.3.93847',executableSha256=em.image.ref['executable']['sha256'],entryRva='0x285590',sourceBytes=346,cases=len(rows),currentMachineCodeExecutedOffline=True,newProcessReads=0,protectedMathExecuted=False,mathServices=['157cc40:two supplied float arguments,result supplied','263c20:three supplied float arguments,result supplied','1570ecc:one supplied float argument,result supplied'],constants=em.constants,limits=['No actual angle/span/round contract is inferred.','Does not admit Cleave/start68/recipient450780 or claim live heading/arc membership.'],artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in [Path(__file__).resolve(),path,em.parent,em.code]})
 (ROOT/'tools/reference/cleave-direction-current-evidence.json').write_text(json.dumps(receipt,indent=2)+'\n');print('Current direction wrapper:'+str(len(rows))+' contexts;protected math supplied,zero live reads.')
if __name__=='__main__':build()
